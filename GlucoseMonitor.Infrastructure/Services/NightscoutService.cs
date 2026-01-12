using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using GlucoseMonitor.Core.Interfaces;
using GlucoseMonitor.Core.Models;
using Serilog;

namespace GlucoseMonitor.Infrastructure.Services;

public class NightscoutService : IGlucoseDataService
{
    private readonly HttpClient _httpClient;
    public string? NightscoutUrl { get; set; }
    public string? AccessToken { get; set; }
    public string Units { get; set; } = "mg";

    public event EventHandler<string>? LogMessage;
    public event EventHandler<GlucoseReading>? GlucoseDataReceived;
    public event EventHandler<string>? ErrorOccurred;

    public NightscoutService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<GlucoseReading?> GetLatestGlucoseAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NightscoutUrl))
            {
                throw new InvalidOperationException("Nightscout URL is not configured");
            }

            // Try legacy pebble endpoint first (some Nightscout setups still expose it)
            var pebbleUrl = BuildApiUrl();
            var displayPebbleUrl = string.IsNullOrEmpty(AccessToken) ? pebbleUrl : pebbleUrl.Replace(AccessToken, "***");
            LogMessage?.Invoke(this, $"Fetching data from: {displayPebbleUrl} (pebble)");

            GlucoseReading? reading = null;
            try
            {
                var pebbleResponse = await _httpClient.GetStringAsync(pebbleUrl);
                LogMessage?.Invoke(this, $"Received {pebbleResponse.Length} characters of data from pebble endpoint");

                var nightscoutData = JsonConvert.DeserializeObject<NightscoutData>(pebbleResponse);
                if (nightscoutData?.Bgs != null && nightscoutData.Bgs.Any())
                {
                    var latestBg = nightscoutData.Bgs.First();
                    reading = ParseGlucoseReading(latestBg);
                    LogMessage?.Invoke(this, $"Parsed glucose (pebble): {reading.Value} {reading.Units} {reading.GetDirectionArrow()}");
                }
                else
                {
                    LogMessage?.Invoke(this, "Pebble endpoint returned no bgs data; will try entries API.");
                }
            }
            catch (Exception ex)
            {
                // Swallow and try entries API next, but log for context
                LogMessage?.Invoke(this, $"Pebble endpoint failed: {ex.Message}. Trying entries API...");
            }

            if (reading == null)
            {
                // Fallback to the standard entries API used by most Nightscout instances
                var entriesUrl = BuildEntriesApiUrl();
                var displayEntriesUrl = string.IsNullOrEmpty(AccessToken) ? entriesUrl : entriesUrl.Replace(AccessToken, "***");
                LogMessage?.Invoke(this, $"Fetching data from: {displayEntriesUrl} (entries)");
                var entriesResponse = await _httpClient.GetStringAsync(entriesUrl);
                LogMessage?.Invoke(this, $"Received {entriesResponse.Length} characters of data from entries endpoint");

                var entries = JsonConvert.DeserializeObject<List<BloodGlucose>>(entriesResponse);
                if (entries == null || !entries.Any())
                {
                    throw new InvalidOperationException("No glucose data found in entries response");
                }

                var latestBg = entries.First();
                reading = ParseGlucoseReading(latestBg);
                LogMessage?.Invoke(this, $"Parsed glucose (entries): {reading.Value} {reading.Units} {reading.GetDirectionArrow()}");
            }

            if (reading != null)
            {
                GlucoseDataReceived?.Invoke(this, reading);
            }
            return reading;
        }
        catch (HttpRequestException ex)
        {
            var error = $"Network error: {ex.Message}";
            LogMessage?.Invoke(this, error);
            ErrorOccurred?.Invoke(this, error);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            var error = $"Request timeout: {ex.Message}";
            LogMessage?.Invoke(this, error);
            ErrorOccurred?.Invoke(this, error);
            return null;
        }
        catch (JsonException ex)
        {
            var error = $"JSON parsing error: {ex.Message}";
            LogMessage?.Invoke(this, error);
            ErrorOccurred?.Invoke(this, error);
            return null;
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error: {ex.Message}";
            LogMessage?.Invoke(this, error);
            ErrorOccurred?.Invoke(this, error);
            return null;
        }
    }

    public async Task<List<GlucoseReading>> GetRecentGlucoseAsync(int count)
    {
        var result = new List<GlucoseReading>();
        try
        {
            if (string.IsNullOrWhiteSpace(NightscoutUrl))
            {
                Log.Warning("NightscoutUrl is empty or null");
                throw new InvalidOperationException("Nightscout URL is not configured");
            }

            // Use entries API for bulk fetch (most reliable and supports count)
            var baseUrl = NightscoutUrl?.TrimEnd('/');
            var url = new StringBuilder($"{baseUrl}/api/v1/entries/sgv.json?count={count}");
            if (!string.IsNullOrWhiteSpace(AccessToken))
            {
                url.Append($"&token={AccessToken}");
            }

            var displayUrl = string.IsNullOrEmpty(AccessToken)
                ? url.ToString()
                : url.ToString().Replace(AccessToken, "***");
            Log.Debug("Fetching from URL: {Url}", displayUrl);
            LogMessage?.Invoke(this, $"Fetching recent data from: {displayUrl}");

            var response = await _httpClient.GetStringAsync(url.ToString());
            Log.Debug("Response length: {Length} chars", response.Length);
            Log.Debug("Response preview: {Preview}", response.AsSpan(0, Math.Min(200, response.Length)).ToString());
            LogMessage?.Invoke(this, $"Received {response.Length} characters for recent entries");

            var entries = JsonConvert.DeserializeObject<List<BloodGlucose>>(response) ?? new List<BloodGlucose>();
            Log.Debug("Deserialized {Count} entries", entries.Count);

            if (entries.Count == 0)
            {
                Log.Warning("No entries after deserialization");
                return result;
            }

            // Nightscout returns newest first; reverse to oldest→newest for UI/time deltas
            foreach (var bg in entries.OrderBy(e => e.DateTime))
            {
                result.Add(ParseGlucoseReading(bg));
            }
            Log.Debug("Parsed {Count} glucose readings", result.Count);
        }
        catch (Exception ex)
        {
            var error = $"Failed to fetch recent glucose: {ex.Message}";
            Log.Error(ex, "Failed to fetch glucose data");
            LogMessage?.Invoke(this, error);
            ErrorOccurred?.Invoke(this, error);
        }

        return result;
    }

    private string BuildApiUrl()
    {
        var baseUrl = NightscoutUrl?.TrimEnd('/');
        var count = 1;
        var units = Units;

        var url = new StringBuilder($"{baseUrl}/pebble?count={count}&units={units}");

        if (!string.IsNullOrWhiteSpace(AccessToken))
        {
            url.Append($"&token={AccessToken}");
        }

        return url.ToString();
    }

    private string BuildEntriesApiUrl()
    {
        var baseUrl = NightscoutUrl?.TrimEnd('/');
        var count = 1;
        var url = new StringBuilder($"{baseUrl}/api/v1/entries/sgv.json?count={count}");
        if (!string.IsNullOrWhiteSpace(AccessToken))
        {
            url.Append($"&token={AccessToken}");
        }
        return url.ToString();
    }

    private GlucoseReading ParseGlucoseReading(BloodGlucose bg)
    {
        if (!double.TryParse(bg.Sgv, NumberStyles.Any, CultureInfo.InvariantCulture, out var glucose))
        {
            throw new InvalidOperationException($"Invalid glucose value: {bg.Sgv}");
        }

        if (!double.TryParse(bg.BgDelta, NumberStyles.Any, CultureInfo.InvariantCulture, out var delta))
        {
            delta = 0;
        }

        // Nightscout timestamps are in UTC milliseconds since epoch. Convert to local time.
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(bg.DateTime).LocalDateTime;
        var age = DateTime.Now - timestamp;
        var isStale = age.TotalMinutes > 15;

        return new GlucoseReading
        {
            Value = glucose,
            Delta = delta,
            Direction = bg.Direction ?? "Unknown",
            Timestamp = timestamp,
            Units = Units == "mmol" ? "mmol/L" : "mg/dL",
            IsStale = isStale,
            Battery = bg.Battery
        };
    }

    public bool ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(NightscoutUrl))
        {
            ErrorOccurred?.Invoke(this, "Nightscout URL is required");
            return false;
        }

        if (!Uri.TryCreate(NightscoutUrl, UriKind.Absolute, out var uri))
        {
            ErrorOccurred?.Invoke(this, "Invalid Nightscout URL format");
            return false;
        }

        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            ErrorOccurred?.Invoke(this, "Nightscout URL must start with http:// or https://");
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}