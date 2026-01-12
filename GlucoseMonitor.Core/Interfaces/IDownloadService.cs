using GlucoseMonitor.Core.Models;

namespace GlucoseMonitor.Core.Interfaces;

/// <summary>
/// Service for downloading files with progress reporting and integrity verification.
/// </summary>
public interface IDownloadService
{
    /// <summary>
    /// Downloads a file from a URL to a local path with progress reporting.
    /// Only HTTPS URLs are allowed for security.
    /// </summary>
    /// <param name="url">The HTTPS URL to download from.</param>
    /// <param name="destinationPath">Local path to save the file.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the SHA256 hash of a local file.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>Lowercase hex-encoded SHA256 hash.</returns>
    Task<string> ComputeSha256Async(string filePath);

    /// <summary>
    /// Verifies a downloaded file against an expected SHA256 hash.
    /// Deletes the file if verification fails.
    /// </summary>
    /// <param name="filePath">Path to the file to verify.</param>
    /// <param name="expectedSha256">Expected lowercase hex-encoded SHA256 hash.</param>
    /// <returns>True if hash matches, false otherwise.</returns>
    Task<bool> VerifyFileAsync(string filePath, string expectedSha256);
}
