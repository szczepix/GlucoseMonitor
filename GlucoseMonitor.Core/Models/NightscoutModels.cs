using Newtonsoft.Json;

namespace GlucoseMonitor.Core.Models;

public class NightscoutData
{
    [JsonProperty("status")]
    public Status? Status { get; set; }

    [JsonProperty("bgs")]
    public List<BloodGlucose>? Bgs { get; set; }

    [JsonProperty("cals")]
    public List<Calibration>? Cals { get; set; }
}

public class Status
{
    [JsonProperty("apiEnabled")]
    public bool ApiEnabled { get; set; }

    [JsonProperty("careportalEnabled")]
    public bool CareportalEnabled { get; set; }

    [JsonProperty("head")]
    public string? Head { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("version")]
    public string? Version { get; set; }

    [JsonProperty("settings")]
    public Settings? Settings { get; set; }
}

public class Settings
{
    [JsonProperty("units")]
    public string? Units { get; set; }

    [JsonProperty("timeFormat")]
    public int TimeFormat { get; set; }

    [JsonProperty("customTitle")]
    public string? CustomTitle { get; set; }

    [JsonProperty("nightMode")]
    public bool NightMode { get; set; }

    [JsonProperty("showRawbg")]
    public string? ShowRawbg { get; set; }

    [JsonProperty("alarmUrgentHigh")]
    public bool AlarmUrgentHigh { get; set; }

    [JsonProperty("alarmHigh")]
    public bool AlarmHigh { get; set; }

    [JsonProperty("alarmLow")]
    public bool AlarmLow { get; set; }

    [JsonProperty("alarmUrgentLow")]
    public bool AlarmUrgentLow { get; set; }

    [JsonProperty("alarmTimeagoWarn")]
    public bool AlarmTimeagoWarn { get; set; }

    [JsonProperty("alarmTimeagoUrgent")]
    public bool AlarmTimeagoUrgent { get; set; }

    [JsonProperty("language")]
    public string? Language { get; set; }

    [JsonProperty("scaleY")]
    public string? ScaleY { get; set; }

    [JsonProperty("showPlugins")]
    public string? ShowPlugins { get; set; }

    [JsonProperty("showForecast")]
    public string? ShowForecast { get; set; }

    [JsonProperty("focusHours")]
    public int FocusHours { get; set; }

    [JsonProperty("heartbeat")]
    public int Heartbeat { get; set; }

    [JsonProperty("baseURL")]
    public string? BaseURL { get; set; }

    [JsonProperty("authDefaultRoles")]
    public string? AuthDefaultRoles { get; set; }

    [JsonProperty("thresholds")]
    public Thresholds? Thresholds { get; set; }

    [JsonProperty("enable")]
    public List<string>? Enable { get; set; }
}

public class Thresholds
{
    [JsonProperty("bgHigh")]
    public int BgHigh { get; set; }

    [JsonProperty("bgTargetTop")]
    public int BgTargetTop { get; set; }

    [JsonProperty("bgTargetBottom")]
    public int BgTargetBottom { get; set; }

    [JsonProperty("bgLow")]
    public int BgLow { get; set; }
}

public class BloodGlucose
{
    [JsonProperty("_id")]
    public string? Id { get; set; }

    [JsonProperty("sgv")]
    public string? Sgv { get; set; }

    [JsonProperty("date")]
    public long DateTime { get; set; }

    [JsonProperty("dateString")]
    public string? DateString { get; set; }

    [JsonProperty("trend")]
    public int Trend { get; set; }

    [JsonProperty("direction")]
    public string? Direction { get; set; }

    [JsonProperty("device")]
    public string? Device { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("utcOffset")]
    public int UtcOffset { get; set; }

    [JsonProperty("sysTime")]
    public string? SysTime { get; set; }

    [JsonProperty("mills")]
    public long Mills { get; set; }

    [JsonProperty("bgdelta")]
    public string? BgDelta { get; set; }

    [JsonProperty("battery")]
    public string? Battery { get; set; }

    [JsonProperty("iob")]
    public string? Iob { get; set; }

    [JsonProperty("bwp")]
    public string? Bwp { get; set; }

    [JsonProperty("bwpo")]
    public string? Bwpo { get; set; }

    [JsonProperty("cob")]
    public string? Cob { get; set; }
}

public class Calibration
{
    [JsonProperty("_id")]
    public string? Id { get; set; }

    [JsonProperty("device")]
    public string? Device { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("dateString")]
    public string? DateString { get; set; }

    [JsonProperty("date")]
    public long Date { get; set; }

    [JsonProperty("scale")]
    public double Scale { get; set; }

    [JsonProperty("intercept")]
    public double Intercept { get; set; }

    [JsonProperty("slope")]
    public double Slope { get; set; }
}