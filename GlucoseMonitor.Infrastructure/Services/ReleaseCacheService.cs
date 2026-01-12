using System.Text.Json;
using GlucoseMonitor.Core.Interfaces;
using GlucoseMonitor.Core.Models;
using Serilog;

namespace GlucoseMonitor.Infrastructure.Services;

/// <summary>
/// Caches GitHub releases to memory and disk to minimize API calls and avoid rate limits.
/// </summary>
public class ReleaseCacheService : IReleaseCache
{
    private readonly string _cachePath;
    private readonly Serilog.ILogger _logger;
    private CacheData? _memoryCache;

    public int CacheTtlMinutes { get; set; } = 15;
    public DateTime? LastCacheTime => _memoryCache?.CachedAt;

    public ReleaseCacheService(Serilog.ILogger logger)
    {
        _logger = logger;

        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GlucoseMonitor", "Cache");
        Directory.CreateDirectory(cacheDir);
        _cachePath = Path.Combine(cacheDir, "releases.json");

        _logger.Debug("Release cache path: {Path}", _cachePath);
    }

    public List<ReleaseInfo>? GetCachedReleases()
    {
        // Check memory cache first (fastest)
        if (_memoryCache != null && IsCacheValid(_memoryCache.CachedAt))
        {
            _logger.Debug("Using memory cache ({Count} releases)", _memoryCache.Releases.Count);
            return _memoryCache.Releases;
        }

        // Check disk cache (persists across app restarts)
        if (File.Exists(_cachePath))
        {
            try
            {
                var json = File.ReadAllText(_cachePath);
                var cached = JsonSerializer.Deserialize<CacheData>(json, GetJsonOptions());

                if (cached != null && IsCacheValid(cached.CachedAt))
                {
                    _memoryCache = cached;
                    _logger.Debug("Using disk cache ({Count} releases)", cached.Releases.Count);
                    return cached.Releases;
                }

                _logger.Debug("Disk cache expired");
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to read release cache");
            }
        }

        return null;
    }

    public void CacheReleases(List<ReleaseInfo> releases)
    {
        var data = new CacheData
        {
            Releases = releases,
            CachedAt = DateTime.UtcNow
        };
        _memoryCache = data;

        try
        {
            var json = JsonSerializer.Serialize(data, GetJsonOptions());
            File.WriteAllText(_cachePath, json);
            _logger.Debug("Cached {Count} releases to disk", releases.Count);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to write release cache");
        }
    }

    public void ClearCache()
    {
        _memoryCache = null;

        if (File.Exists(_cachePath))
        {
            try
            {
                File.Delete(_cachePath);
                _logger.Debug("Cache cleared");
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to delete cache file");
            }
        }
    }

    private bool IsCacheValid(DateTime cachedAt)
    {
        return DateTime.UtcNow - cachedAt < TimeSpan.FromMinutes(CacheTtlMinutes);
    }

    private static JsonSerializerOptions GetJsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private class CacheData
    {
        public List<ReleaseInfo> Releases { get; set; } = new();
        public DateTime CachedAt { get; set; }
    }
}
