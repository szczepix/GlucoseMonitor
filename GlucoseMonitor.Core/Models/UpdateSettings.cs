namespace GlucoseMonitor.Core.Models;

/// <summary>
/// User preferences for the auto-update system.
/// </summary>
public class UpdateSettings
{
    /// <summary>
    /// Whether to automatically check for updates on startup.
    /// </summary>
    public bool AutoCheckForUpdates { get; set; } = true;

    /// <summary>
    /// Whether to include pre-release versions in the version list.
    /// </summary>
    public bool IncludePreReleases { get; set; } = false;

    /// <summary>
    /// Whether to automatically download updates (not just notify).
    /// </summary>
    public bool AutoDownload { get; set; } = false;

    /// <summary>
    /// Last time we checked for updates.
    /// </summary>
    public DateTime? LastCheckTime { get; set; }

    /// <summary>
    /// Version tag that the user chose to skip (won't show notification for this version).
    /// </summary>
    public string? SkippedVersion { get; set; }

    /// <summary>
    /// Whether enough time has passed since last check (1 hour minimum).
    /// </summary>
    public bool ShouldCheckForUpdates
    {
        get
        {
            if (!AutoCheckForUpdates) return false;
            if (!LastCheckTime.HasValue) return true;
            return DateTime.UtcNow - LastCheckTime.Value > TimeSpan.FromHours(1);
        }
    }
}
