namespace GlucoseMonitor.Core.Models;

/// <summary>
/// Represents a Nightscout server profile with connection settings.
/// Tokens are stored encrypted at rest using Windows DPAPI.
/// </summary>
public class ServerProfile
{
    /// <summary>
    /// Unique identifier for the profile.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name for the profile (e.g., "My Nightscout", "Mock Server").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Nightscout server URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// API access token. Stored encrypted at rest, decrypted only in memory.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Units for glucose display (mg/dL or mmol/L).
    /// </summary>
    public string Units { get; set; } = "mg";

    /// <summary>
    /// Polling interval in minutes.
    /// </summary>
    public int Interval { get; set; } = 1;

    /// <summary>
    /// Whether this profile should be selected by default on startup.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Creates a display-friendly string for the profile.
    /// </summary>
    public override string ToString() => Name;
}

/// <summary>
/// Container for serializing profiles to JSON.
/// </summary>
public class ProfileData
{
    /// <summary>
    /// List of server profiles.
    /// </summary>
    public List<ServerProfile> Profiles { get; set; } = new();

    /// <summary>
    /// ID of the currently active profile.
    /// </summary>
    public string? ActiveProfileId { get; set; }
}
