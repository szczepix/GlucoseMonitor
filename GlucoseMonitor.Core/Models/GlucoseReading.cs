using System.Drawing;

namespace GlucoseMonitor.Core.Models;

public class GlucoseReading
{
    public double Value { get; set; }
    public double Delta { get; set; }
    public string Direction { get; set; } = "Unknown";
    public DateTime Timestamp { get; set; }
    public string Units { get; set; } = "mg/dL";
    public bool IsStale { get; set; }
    public string? Battery { get; set; }

    public Color GetGlucoseColor()
    {
        return Value switch
        {
            < 70 => Color.Red,      // Low
            < 80 => Color.Orange,   // Low-normal
            <= 180 => Color.Lime,   // Normal
            <= 250 => Color.Yellow, // High-normal
            _ => Color.Red          // High
        };
    }

    public string GetDirectionArrow()
    {
        return Direction?.ToLower() switch
        {
            "doubleup" => "⇈",
            "singleup" => "↑",
            "fortyfiveup" => "↗",
            "flat" => "→",
            "fortyfivedown" => "↘",
            "singledown" => "↓",
            "doubledown" => "⇊",
            _ => "?"
        };
    }
}