namespace GlucoseMonitor.Core.Models;

/// <summary>
/// Represents the current stage of an update operation.
/// </summary>
public enum UpdateStage
{
    Idle,
    CheckingForUpdates,
    Downloading,
    Verifying,
    Extracting,
    CreatingBackup,
    Installing,
    Complete,
    Failed
}

/// <summary>
/// Reports progress during update download and installation.
/// </summary>
public class UpdateProgress
{
    public UpdateStage Stage { get; set; } = UpdateStage.Idle;
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the completion percentage (0-100).
    /// </summary>
    public int PercentComplete => TotalBytes > 0 ? (int)(BytesDownloaded * 100 / TotalBytes) : 0;

    /// <summary>
    /// Gets the downloaded size formatted as human-readable string.
    /// </summary>
    public string FormattedProgress
    {
        get
        {
            var downloaded = FormatBytes(BytesDownloaded);
            var total = FormatBytes(TotalBytes);
            return $"{downloaded} / {total}";
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    /// <summary>
    /// Creates a progress report for a specific stage.
    /// </summary>
    public static UpdateProgress ForStage(UpdateStage stage, string message = "") => new()
    {
        Stage = stage,
        StatusMessage = string.IsNullOrEmpty(message) ? GetDefaultMessage(stage) : message
    };

    /// <summary>
    /// Creates a failed progress report.
    /// </summary>
    public static UpdateProgress Failed(string error) => new()
    {
        Stage = UpdateStage.Failed,
        StatusMessage = "Update failed",
        ErrorMessage = error
    };

    private static string GetDefaultMessage(UpdateStage stage) => stage switch
    {
        UpdateStage.Idle => "Ready",
        UpdateStage.CheckingForUpdates => "Checking for updates...",
        UpdateStage.Downloading => "Downloading update...",
        UpdateStage.Verifying => "Verifying download...",
        UpdateStage.Extracting => "Extracting files...",
        UpdateStage.CreatingBackup => "Creating backup...",
        UpdateStage.Installing => "Installing update...",
        UpdateStage.Complete => "Update complete!",
        UpdateStage.Failed => "Update failed",
        _ => string.Empty
    };
}
