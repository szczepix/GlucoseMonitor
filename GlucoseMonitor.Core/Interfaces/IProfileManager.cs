using GlucoseMonitor.Core.Models;

namespace GlucoseMonitor.Core.Interfaces;

/// <summary>
/// Manages Nightscout server profiles with secure storage.
/// </summary>
public interface IProfileManager
{
    /// <summary>
    /// Gets all configured server profiles.
    /// </summary>
    /// <returns>List of server profiles with decrypted tokens.</returns>
    List<ServerProfile> GetProfiles();

    /// <summary>
    /// Gets the currently active profile.
    /// </summary>
    /// <returns>The active profile, or null if no profiles exist.</returns>
    ServerProfile? GetActiveProfile();

    /// <summary>
    /// Sets the active profile by ID.
    /// </summary>
    /// <param name="profileId">The profile ID to set as active.</param>
    void SetActiveProfile(string profileId);

    /// <summary>
    /// Saves a profile. If the profile ID exists, it will be updated; otherwise, a new profile is created.
    /// </summary>
    /// <param name="profile">The profile to save.</param>
    void SaveProfile(ServerProfile profile);

    /// <summary>
    /// Deletes a profile by ID.
    /// </summary>
    /// <param name="profileId">The profile ID to delete.</param>
    void DeleteProfile(string profileId);

    /// <summary>
    /// Checks if profiles have been migrated from legacy config.
    /// </summary>
    bool HasProfiles { get; }

    /// <summary>
    /// Forces a reload of profiles from disk.
    /// Call this to refresh the profile list after external changes.
    /// </summary>
    void ReloadProfiles();

    /// <summary>
    /// Migrates legacy configuration to profile system.
    /// </summary>
    /// <param name="url">Legacy Nightscout URL.</param>
    /// <param name="token">Legacy API token.</param>
    /// <param name="units">Legacy units setting.</param>
    /// <param name="interval">Legacy polling interval.</param>
    void MigrateFromLegacyConfig(string url, string token, string units, int interval);
}
