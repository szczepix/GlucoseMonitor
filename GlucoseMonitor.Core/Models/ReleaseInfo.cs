namespace GlucoseMonitor.Core.Models;

/// <summary>
/// Represents a GitHub release with asset information.
/// </summary>
public class ReleaseInfo
{
    public string TagName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsPreRelease { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string HtmlUrl { get; set; } = string.Empty;

    // Primary asset (GlucoseMonitor-win-x64.zip)
    public string DownloadUrl { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public long AssetSize { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;

    /// <summary>
    /// Parses version from tag. Supports both semantic (v1.0.0) and date-based (v2026.01.12-hash) formats.
    /// </summary>
    public Version? ParseVersion()
    {
        var tag = TagName.TrimStart('v');

        // Try semantic version first (v1.0.0)
        if (Version.TryParse(tag, out var semver))
            return semver;

        // Try date-based format (v2026.01.12-hash)
        var dashIdx = tag.IndexOf('-');
        if (dashIdx > 0)
        {
            var datePart = tag[..dashIdx];
            var parts = datePart.Split('.');
            if (parts.Length == 3 &&
                int.TryParse(parts[0], out var year) &&
                int.TryParse(parts[1], out var month) &&
                int.TryParse(parts[2], out var day))
            {
                // Convert to comparable version: year.monthday.0.0
                return new Version(year, month * 100 + day, 0, 0);
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if this release is newer than the given version.
    /// </summary>
    public bool IsNewerThan(Version current)
    {
        var releaseVersion = ParseVersion();
        return releaseVersion != null && releaseVersion > current;
    }

    /// <summary>
    /// Gets a display-friendly version string.
    /// </summary>
    public string DisplayVersion => TagName.TrimStart('v');

    /// <summary>
    /// Gets the asset size formatted as human-readable string.
    /// </summary>
    public string FormattedSize
    {
        get
        {
            if (AssetSize < 1024) return $"{AssetSize} B";
            if (AssetSize < 1024 * 1024) return $"{AssetSize / 1024.0:F1} KB";
            return $"{AssetSize / (1024.0 * 1024.0):F1} MB";
        }
    }
}
