using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security;
using System.Text.Json;
using GlucoseMonitor.Core.Interfaces;
using GlucoseMonitor.Core.Models;
using Serilog;

namespace GlucoseMonitor.Infrastructure.Services;

/// <summary>
/// Update service that fetches releases from GitHub API.
/// </summary>
public class GitHubUpdateService : IUpdateService
{
    private readonly IReleaseCache _cache;
    private readonly IDownloadService _downloadService;
    private readonly Serilog.ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly string _repositoryOwner;
    private readonly string _repositoryName;
    private readonly string _settingsPath;

    public Version CurrentVersion { get; }
    public bool IsBusy { get; private set; }
    public UpdateSettings Settings { get; private set; }

    public event EventHandler<ReleaseInfo>? UpdateAvailable;

    public GitHubUpdateService(
        IReleaseCache cache,
        IDownloadService downloadService,
        Serilog.ILogger logger,
        string repositoryOwner,
        string repositoryName)
    {
        _cache = cache;
        _downloadService = downloadService;
        _logger = logger;
        _repositoryOwner = repositoryOwner;
        _repositoryName = repositoryName;

        // Get version from assembly
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        CurrentVersion = assembly.GetName().Version ?? new Version(1, 0, 0);

        // Settings path
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDir = Path.Combine(appData, "GlucoseMonitor");
        Directory.CreateDirectory(settingsDir);
        _settingsPath = Path.Combine(settingsDir, "update_settings.json");

        // Load settings
        Settings = LoadSettings();

        // Configure HttpClient for GitHub API
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("GlucoseMonitor", CurrentVersion.ToString()));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

        _logger.Information("UpdateService initialized. Current version: {Version}", CurrentVersion);
    }

    public async Task<List<ReleaseInfo>> GetAvailableReleasesAsync(bool includePreReleases = false)
    {
        // Try cache first
        var cached = _cache.GetCachedReleases();
        if (cached != null)
        {
            _logger.Debug("Using cached releases");
            return FilterReleases(cached, includePreReleases);
        }

        // Fetch from GitHub
        _logger.Information("Fetching releases from GitHub API");
        IsBusy = true;

        try
        {
            var url = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/releases";
            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.Warning("GitHub API rate limit exceeded");
                // Return empty list if rate limited and no cache
                return new List<ReleaseInfo>();
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var releases = ParseReleases(json);

            // Fetch checksums for each release
            await FetchChecksumsAsync(releases);

            // Cache the results
            _cache.CacheReleases(releases);
            Settings.LastCheckTime = DateTime.UtcNow;
            SaveSettings();

            _logger.Information("Fetched {Count} releases from GitHub", releases.Count);
            return FilterReleases(releases, includePreReleases);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to fetch releases from GitHub");
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<ReleaseInfo?> CheckForUpdateAsync(bool includePreReleases = false)
    {
        try
        {
            var releases = await GetAvailableReleasesAsync(includePreReleases);
            var latest = releases.FirstOrDefault();

            if (latest != null && latest.IsNewerThan(CurrentVersion))
            {
                // Check if user skipped this version
                if (Settings.SkippedVersion == latest.TagName)
                {
                    _logger.Debug("Skipping notification for {Version} (user skipped)", latest.TagName);
                    return null;
                }

                _logger.Information("Update available: {Version}", latest.TagName);
                UpdateAvailable?.Invoke(this, latest);
                return latest;
            }

            _logger.Debug("No updates available. Current: {Current}, Latest: {Latest}",
                CurrentVersion, latest?.TagName ?? "none");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to check for updates");
            return null;
        }
    }

    public async Task<string> DownloadReleaseAsync(
        ReleaseInfo release,
        IProgress<UpdateProgress> progress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(release.DownloadUrl))
        {
            throw new InvalidOperationException("Release has no download URL");
        }

        IsBusy = true;
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "GlucoseMonitor_Update");
            Directory.CreateDirectory(tempDir);

            var downloadPath = Path.Combine(tempDir, release.AssetName);

            // Delete existing file if present
            if (File.Exists(downloadPath))
            {
                File.Delete(downloadPath);
            }

            await _downloadService.DownloadFileAsync(
                release.DownloadUrl,
                downloadPath,
                progress,
                cancellationToken);

            // Verify hash if available
            if (!string.IsNullOrEmpty(release.Sha256Hash))
            {
                progress.Report(UpdateProgress.ForStage(UpdateStage.Verifying));
                var isValid = await _downloadService.VerifyFileAsync(downloadPath, release.Sha256Hash);
                if (!isValid)
                {
                    throw new SecurityException("Downloaded file failed integrity verification");
                }
            }
            else
            {
                _logger.Warning("No SHA256 hash available for verification");
            }

            return downloadPath;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task InstallUpdateAsync(string downloadedFilePath, IProgress<UpdateProgress> progress)
    {
        _logger.Information("Installing update from {Path}", downloadedFilePath);

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var backupDir = Path.Combine(appData, "GlucoseMonitor", "backup");
        var tempDir = Path.GetDirectoryName(downloadedFilePath)!;
        var extractDir = Path.Combine(tempDir, "extracted");
        var appDir = AppContext.BaseDirectory;

        try
        {
            // Step 1: Create backup
            progress.Report(UpdateProgress.ForStage(UpdateStage.CreatingBackup));
            await CreateBackupAsync(appDir, backupDir);

            // Step 2: Extract update
            progress.Report(UpdateProgress.ForStage(UpdateStage.Extracting));
            if (Directory.Exists(extractDir))
            {
                Directory.Delete(extractDir, true);
            }
            ZipFile.ExtractToDirectory(downloadedFilePath, extractDir);
            _logger.Information("Extracted update to {Path}", extractDir);

            // Step 3: Create update script
            progress.Report(UpdateProgress.ForStage(UpdateStage.Installing, "Preparing restart..."));
            var scriptPath = Path.Combine(tempDir, "update.ps1");
            await CreateUpdateScriptAsync(scriptPath, appDir, extractDir, tempDir);

            // Step 4: Launch script and exit
            _logger.Information("Launching update script and exiting");
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true
            };
            var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to launch update script");
            }

            // The application will exit after this
            progress.Report(UpdateProgress.ForStage(UpdateStage.Installing, "Restarting..."));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to install update");
            progress.Report(UpdateProgress.Failed(ex.Message));
            throw;
        }
    }

    public void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to save update settings");
        }
    }

    private UpdateSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<UpdateSettings>(json) ?? new UpdateSettings();
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load update settings, using defaults");
        }
        return new UpdateSettings();
    }

    private List<ReleaseInfo> ParseReleases(string json)
    {
        var releases = new List<ReleaseInfo>();

        using var doc = JsonDocument.Parse(json);
        foreach (var release in doc.RootElement.EnumerateArray())
        {
            var tagName = release.GetProperty("tag_name").GetString() ?? "";
            var assets = release.GetProperty("assets");

            // Find the main app asset
            string downloadUrl = "", assetName = "";
            long assetSize = 0;

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.StartsWith("GlucoseMonitor-win-x64", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                    assetName = name;
                    assetSize = asset.GetProperty("size").GetInt64();
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl)) continue;

            releases.Add(new ReleaseInfo
            {
                TagName = tagName,
                Name = release.GetProperty("name").GetString() ?? tagName,
                Body = release.GetProperty("body").GetString() ?? "",
                IsPreRelease = release.GetProperty("prerelease").GetBoolean(),
                PublishedAt = release.GetProperty("published_at").GetDateTimeOffset(),
                HtmlUrl = release.GetProperty("html_url").GetString() ?? "",
                DownloadUrl = downloadUrl,
                AssetName = assetName,
                AssetSize = assetSize
            });
        }

        // Sort by version (newest first)
        return releases.OrderByDescending(r => r.ParseVersion()).ToList();
    }

    private async Task FetchChecksumsAsync(List<ReleaseInfo> releases)
    {
        foreach (var release in releases)
        {
            try
            {
                // Try to find checksums.txt in the release
                var checksumsUrl = release.DownloadUrl.Replace(release.AssetName, "checksums.txt");
                var response = await _httpClient.GetAsync(checksumsUrl);

                if (response.IsSuccessStatusCode)
                {
                    var checksums = await response.Content.ReadAsStringAsync();
                    foreach (var line in checksums.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && parts[1] == release.AssetName)
                        {
                            release.Sha256Hash = parts[0];
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Could not fetch checksums for {Release}", release.TagName);
            }
        }
    }

    private List<ReleaseInfo> FilterReleases(List<ReleaseInfo> releases, bool includePreReleases)
    {
        if (includePreReleases)
            return releases;
        return releases.Where(r => !r.IsPreRelease).ToList();
    }

    private async Task CreateBackupAsync(string appDir, string backupDir)
    {
        _logger.Information("Creating backup at {Path}", backupDir);

        if (Directory.Exists(backupDir))
        {
            Directory.Delete(backupDir, true);
        }
        Directory.CreateDirectory(backupDir);

        // Copy essential files
        foreach (var file in Directory.GetFiles(appDir, "*.*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(appDir, file);
            var destPath = Path.Combine(backupDir, relativePath);

            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(file, destPath, overwrite: true);
        }

        // Write backup metadata
        var metadata = new
        {
            BackupTime = DateTime.UtcNow,
            Version = CurrentVersion.ToString()
        };
        var json = JsonSerializer.Serialize(metadata);
        await File.WriteAllTextAsync(Path.Combine(backupDir, "backup.json"), json);

        _logger.Information("Backup created successfully");
    }

    private async Task CreateUpdateScriptAsync(string scriptPath, string appDir, string extractDir, string tempDir)
    {
        var script = $@"
# GlucoseMonitor Update Script
$ErrorActionPreference = 'Stop'

$appDir = '{appDir.Replace("'", "''")}'
$extractDir = '{extractDir.Replace("'", "''")}'
$tempDir = '{tempDir.Replace("'", "''")}'
$processName = 'GlucoseMonitor.UI'

# Wait for app to exit
$timeout = 30
$elapsed = 0
while ((Get-Process -Name $processName -ErrorAction SilentlyContinue) -and $elapsed -lt $timeout) {{
    Start-Sleep -Seconds 1
    $elapsed++
}}

if ($elapsed -ge $timeout) {{
    Write-Error 'Timeout waiting for application to exit'
    exit 1
}}

# Small delay for file handles to be released
Start-Sleep -Seconds 2

try {{
    # Copy new files
    Copy-Item -Path ""$extractDir\*"" -Destination $appDir -Recurse -Force

    # Start updated application
    Start-Process -FilePath ""$appDir\GlucoseMonitor.UI.exe""

    # Clean up temp files
    Start-Sleep -Seconds 3
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}}
catch {{
    Write-Error ""Update failed: $_""
    exit 1
}}
";

        await File.WriteAllTextAsync(scriptPath, script);
        _logger.Debug("Created update script at {Path}", scriptPath);
    }
}
