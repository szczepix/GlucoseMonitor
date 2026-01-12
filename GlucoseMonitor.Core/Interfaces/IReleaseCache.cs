using GlucoseMonitor.Core.Models;

namespace GlucoseMonitor.Core.Interfaces;

/// <summary>
/// Caches GitHub releases to minimize API calls and avoid rate limits.
/// </summary>
public interface IReleaseCache
{
    /// <summary>
    /// Gets cached releases if still valid (within TTL).
    /// </summary>
    /// <returns>Cached releases or null if cache is expired/missing.</returns>
    List<ReleaseInfo>? GetCachedReleases();

    /// <summary>
    /// Stores releases in cache with current timestamp.
    /// </summary>
    /// <param name="releases">Releases to cache.</param>
    void CacheReleases(List<ReleaseInfo> releases);

    /// <summary>
    /// Cache time-to-live in minutes. Default is 15 minutes.
    /// </summary>
    int CacheTtlMinutes { get; set; }

    /// <summary>
    /// Clears both memory and disk cache.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets the timestamp of the last cache update.
    /// </summary>
    DateTime? LastCacheTime { get; }
}
