using GlucoseMonitor.Core.Models;

namespace GlucoseMonitor.Core.Interfaces;

/// <summary>
/// Main service for checking and applying application updates.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Current application version (from assembly).
    /// </summary>
    Version CurrentVersion { get; }

    /// <summary>
    /// Whether an update operation is currently in progress.
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// User preferences for updates.
    /// </summary>
    UpdateSettings Settings { get; }

    /// <summary>
    /// Fetches available releases from GitHub.
    /// Uses cache if available and valid.
    /// </summary>
    /// <param name="includePreReleases">Whether to include pre-release versions.</param>
    /// <returns>List of available releases, newest first.</returns>
    Task<List<ReleaseInfo>> GetAvailableReleasesAsync(bool includePreReleases = false);

    /// <summary>
    /// Checks if a newer version is available.
    /// </summary>
    /// <param name="includePreReleases">Whether to include pre-release versions.</param>
    /// <returns>The latest release if newer than current, null otherwise.</returns>
    Task<ReleaseInfo?> CheckForUpdateAsync(bool includePreReleases = false);

    /// <summary>
    /// Downloads a specific release.
    /// </summary>
    /// <param name="release">The release to download.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the downloaded file.</returns>
    Task<string> DownloadReleaseAsync(
        ReleaseInfo release,
        IProgress<UpdateProgress> progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a downloaded update. This will:
    /// 1. Create a backup of the current installation
    /// 2. Schedule the update to be applied after app exit
    /// 3. Exit the application
    /// </summary>
    /// <param name="downloadedFilePath">Path to the downloaded update ZIP.</param>
    /// <param name="progress">Progress reporter.</param>
    Task InstallUpdateAsync(string downloadedFilePath, IProgress<UpdateProgress> progress);

    /// <summary>
    /// Saves user update settings.
    /// </summary>
    void SaveSettings();

    /// <summary>
    /// Raised when a new version is available (for notifications).
    /// </summary>
    event EventHandler<ReleaseInfo>? UpdateAvailable;
}
