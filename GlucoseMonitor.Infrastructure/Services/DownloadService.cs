using System.Net.Http.Headers;
using System.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using GlucoseMonitor.Core.Interfaces;
using GlucoseMonitor.Core.Models;
using Serilog;

namespace GlucoseMonitor.Infrastructure.Services;

/// <summary>
/// Secure file download service with SHA256 verification.
/// </summary>
public class DownloadService : IDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly Serilog.ILogger _logger;
    private const int BufferSize = 81920; // 80 KiB buffer for efficient streaming

    public DownloadService(Serilog.ILogger logger)
    {
        _logger = logger;

        var handler = new HttpClientHandler
        {
            // Enforce TLS 1.2+
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(30) // Long timeout for large downloads
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("GlucoseMonitor", "1.0"));
    }

    public async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Security: Only allow HTTPS
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException("Downloads must use HTTPS for security");
        }

        _logger.Information("Starting download from {Url}", url);
        progress?.Report(UpdateProgress.ForStage(UpdateStage.Downloading, "Connecting..."));

        // Ensure directory exists
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        _logger.Information("Download size: {Size} bytes", totalBytes);

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

        var buffer = new byte[BufferSize];
        long bytesDownloaded = 0;
        int bytesRead;
        var lastProgressReport = DateTime.UtcNow;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            bytesDownloaded += bytesRead;

            // Report progress every 100ms to avoid flooding
            if (DateTime.UtcNow - lastProgressReport > TimeSpan.FromMilliseconds(100))
            {
                progress?.Report(new UpdateProgress
                {
                    Stage = UpdateStage.Downloading,
                    BytesDownloaded = bytesDownloaded,
                    TotalBytes = totalBytes,
                    StatusMessage = "Downloading..."
                });
                lastProgressReport = DateTime.UtcNow;
            }
        }

        // Final progress report
        progress?.Report(new UpdateProgress
        {
            Stage = UpdateStage.Downloading,
            BytesDownloaded = bytesDownloaded,
            TotalBytes = bytesDownloaded,
            StatusMessage = "Download complete"
        });

        _logger.Information("Download complete: {Bytes} bytes written to {Path}", bytesDownloaded, destinationPath);
    }

    public async Task<string> ComputeSha256Async(string filePath)
    {
        _logger.Debug("Computing SHA256 for {Path}", filePath);

        using var sha256 = SHA256.Create();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
        var hashBytes = await sha256.ComputeHashAsync(stream);
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        _logger.Debug("SHA256: {Hash}", hash);
        return hash;
    }

    public async Task<bool> VerifyFileAsync(string filePath, string expectedSha256)
    {
        _logger.Information("Verifying file integrity: {Path}", filePath);

        var actualHash = await ComputeSha256Async(filePath);
        var isValid = string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase);

        if (isValid)
        {
            _logger.Information("File verification passed");
        }
        else
        {
            _logger.Error("File verification FAILED! Expected: {Expected}, Actual: {Actual}",
                expectedSha256, actualHash);

            // Security: Delete potentially tampered file
            try
            {
                File.Delete(filePath);
                _logger.Warning("Deleted potentially tampered file: {Path}", filePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete tampered file");
            }
        }

        return isValid;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
