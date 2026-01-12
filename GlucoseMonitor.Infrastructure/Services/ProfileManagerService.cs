using System.Text.Json;
using GlucoseMonitor.Core.Interfaces;
using GlucoseMonitor.Core.Models;

namespace GlucoseMonitor.Infrastructure.Services;

/// <summary>
/// Manages server profiles with encrypted token storage.
/// Profiles are stored in JSON format with tokens encrypted using DPAPI.
/// </summary>
public class ProfileManagerService : IProfileManager
{
    private readonly ISecureStorageService _secureStorage;
    private readonly ILogger _logger;
    private readonly string _profilesPath;

    private ProfileData _data = new();
    private bool _isLoaded;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProfileManagerService(ISecureStorageService secureStorage, ILogger logger)
    {
        _secureStorage = secureStorage;
        _logger = logger;

        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GlucoseMonitor");
        Directory.CreateDirectory(configDir);

        _profilesPath = Path.Combine(configDir, "profiles.json");
    }

    /// <inheritdoc/>
    public bool HasProfiles
    {
        get
        {
            EnsureLoaded();
            return _data.Profiles.Count > 0;
        }
    }

    /// <summary>
    /// Forces a reload of profiles from disk.
    /// </summary>
    public void ReloadProfiles()
    {
        _isLoaded = false;
        EnsureLoaded();
        _logger.LogInfo($"Reloaded {_data.Profiles.Count} profile(s) from disk");
    }

    /// <inheritdoc/>
    public List<ServerProfile> GetProfiles()
    {
        EnsureLoaded();

        // Return profiles with decrypted tokens (for in-memory use only)
        return _data.Profiles.Select(p => new ServerProfile
        {
            Id = p.Id,
            Name = p.Name,
            Url = p.Url,
            Token = _secureStorage.Decrypt(p.Token), // Decrypt for use
            Units = p.Units,
            Interval = p.Interval,
            IsDefault = p.IsDefault
        }).ToList();
    }

    /// <inheritdoc/>
    public ServerProfile? GetActiveProfile()
    {
        EnsureLoaded();

        var profiles = GetProfiles();
        if (profiles.Count == 0)
        {
            return null;
        }

        // Find by active ID, or default, or first
        var active = profiles.FirstOrDefault(p => p.Id == _data.ActiveProfileId)
                  ?? profiles.FirstOrDefault(p => p.IsDefault)
                  ?? profiles.First();

        return active;
    }

    /// <inheritdoc/>
    public void SetActiveProfile(string profileId)
    {
        EnsureLoaded();

        if (_data.Profiles.Any(p => p.Id == profileId))
        {
            _data.ActiveProfileId = profileId;
            Save();
            _logger.LogInfo($"Active profile set to: {profileId}");
        }
    }

    /// <inheritdoc/>
    public void SaveProfile(ServerProfile profile)
    {
        EnsureLoaded();

        // Create profile with encrypted token for storage
        var storageProfile = new ServerProfile
        {
            Id = profile.Id,
            Name = profile.Name,
            Url = profile.Url,
            Token = _secureStorage.Encrypt(profile.Token), // Encrypt for storage
            Units = profile.Units,
            Interval = profile.Interval,
            IsDefault = profile.IsDefault
        };

        // If this is the new default, clear other defaults
        if (profile.IsDefault)
        {
            foreach (var p in _data.Profiles)
            {
                p.IsDefault = false;
            }
        }

        // Update existing or add new
        var existing = _data.Profiles.FindIndex(p => p.Id == profile.Id);
        if (existing >= 0)
        {
            _data.Profiles[existing] = storageProfile;
            _logger.LogInfo($"Updated profile: {profile.Name}");
        }
        else
        {
            _data.Profiles.Add(storageProfile);
            _logger.LogInfo($"Added new profile: {profile.Name}");

            // If first profile, set as active
            if (_data.Profiles.Count == 1)
            {
                _data.ActiveProfileId = profile.Id;
                storageProfile.IsDefault = true;
            }
        }

        Save();
    }

    /// <inheritdoc/>
    public void DeleteProfile(string profileId)
    {
        EnsureLoaded();

        var profile = _data.Profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile != null)
        {
            _data.Profiles.Remove(profile);
            _logger.LogInfo($"Deleted profile: {profile.Name}");

            // If we deleted the active profile, select another
            if (_data.ActiveProfileId == profileId)
            {
                _data.ActiveProfileId = _data.Profiles.FirstOrDefault()?.Id;
            }

            Save();
        }
    }

    /// <inheritdoc/>
    public void MigrateFromLegacyConfig(string url, string token, string units, int interval)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogInfo("No legacy config to migrate (empty URL)");
            return;
        }

        _logger.LogInfo("Migrating legacy configuration to profile system");

        var profile = new ServerProfile
        {
            Id = Guid.NewGuid().ToString(),
            Name = "My Nightscout",
            Url = url,
            Token = token, // Will be encrypted by SaveProfile
            Units = units,
            Interval = interval,
            IsDefault = true
        };

        SaveProfile(profile);
        _logger.LogInfo("Legacy configuration migrated successfully");
    }

    private void EnsureLoaded()
    {
        if (_isLoaded) return;

        try
        {
            if (File.Exists(_profilesPath))
            {
                var json = File.ReadAllText(_profilesPath);
                _data = JsonSerializer.Deserialize<ProfileData>(json, JsonOptions) ?? new ProfileData();
                _logger.LogInfo($"Loaded {_data.Profiles.Count} profile(s)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load profiles: {ex.Message}");
            _data = new ProfileData();
        }

        _isLoaded = true;
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, JsonOptions);
            File.WriteAllText(_profilesPath, json);
            _logger.LogDebug("Profiles saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to save profiles: {ex.Message}");
        }
    }
}
