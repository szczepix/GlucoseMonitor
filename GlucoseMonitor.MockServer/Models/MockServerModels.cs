using System.Text.Json.Serialization;

namespace GlucoseMonitor.MockServer.Models;

/// <summary>
/// SGV (Sensor Glucose Value) entry from Nightscout API.
/// Used by both MockServer and IntegrationTests.
/// </summary>
public class SgvEntry
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("sgv")]
    public int Sgv { get; set; }

    [JsonPropertyName("date")]
    public long Date { get; set; }

    [JsonPropertyName("dateString")]
    public string? DateString { get; set; }

    [JsonPropertyName("trend")]
    public int Trend { get; set; }

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("device")]
    public string? Device { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("utcOffset")]
    public int UtcOffset { get; set; }

    [JsonPropertyName("sysTime")]
    public string? SysTime { get; set; }

    [JsonPropertyName("mills")]
    public long Mills { get; set; }

    [JsonPropertyName("bgdelta")]
    public double? BgDelta { get; set; }
}

/// <summary>
/// Pebble API response format (legacy endpoint).
/// </summary>
public class PebbleResponse
{
    [JsonPropertyName("status")]
    public List<StatusInfo>? Status { get; set; }

    [JsonPropertyName("bgs")]
    public List<PebbleBg>? Bgs { get; set; }
}

/// <summary>
/// Status info for Pebble response.
/// </summary>
public class StatusInfo
{
    [JsonPropertyName("now")]
    public long Now { get; set; }
}

/// <summary>
/// Blood glucose entry for Pebble response.
/// </summary>
public class PebbleBg
{
    [JsonPropertyName("sgv")]
    public string? Sgv { get; set; }

    [JsonPropertyName("trend")]
    public int Trend { get; set; }

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("datetime")]
    public long Datetime { get; set; }

    [JsonPropertyName("bgdelta")]
    public string? BgDelta { get; set; }

    [JsonPropertyName("battery")]
    public string? Battery { get; set; }

    [JsonPropertyName("iob")]
    public string? Iob { get; set; }

    [JsonPropertyName("cob")]
    public string? Cob { get; set; }
}

/// <summary>
/// Mock server status response.
/// </summary>
public class MockStatusResponse
{
    [JsonPropertyName("scenario")]
    public string? Scenario { get; set; }

    [JsonPropertyName("currentGlucose")]
    public double CurrentGlucose { get; set; }

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("delta")]
    public double Delta { get; set; }

    [JsonPropertyName("serverTime")]
    public string? ServerTime { get; set; }
}
