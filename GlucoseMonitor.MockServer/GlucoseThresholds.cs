namespace GlucoseMonitor.MockServer;

/// <summary>
/// Glucose threshold constants based on ADA 2024 guidelines.
/// These values define the boundaries for glucose alarm categories.
/// </summary>
public static class GlucoseThresholds
{
    /// <summary>Urgent high threshold (>= 250 mg/dL triggers urgent alarm)</summary>
    public const double UrgentHigh = 250;

    /// <summary>High threshold (>= 180 mg/dL triggers high alarm)</summary>
    public const double High = 180;

    /// <summary>Target top (upper bound of normal range)</summary>
    public const double TargetTop = 180;

    /// <summary>Target bottom (lower bound of normal range)</summary>
    public const double TargetBottom = 70;

    /// <summary>Low threshold (<= 70 mg/dL triggers low alarm)</summary>
    public const double Low = 70;

    /// <summary>Urgent low threshold (<= 54 mg/dL triggers urgent alarm)</summary>
    public const double UrgentLow = 54;

    /// <summary>
    /// Gets the alarm category for a given glucose value.
    /// </summary>
    /// <param name="value">Glucose value in mg/dL</param>
    /// <returns>Alarm category name or null if in normal range</returns>
    public static string? GetAlarmCategory(double value)
    {
        if (value >= UrgentHigh) return "UrgentHigh";
        if (value >= High) return "High";
        if (value <= UrgentLow) return "UrgentLow";
        if (value <= Low) return "Low";
        return null;
    }
}
