using GlucoseMonitor.Core.Interfaces;
using GlucoseMonitor.Core.Models;

namespace GlucoseMonitor.Infrastructure.Services;

public class GlucoseHistoryService : IGlucoseHistoryService
{
    private readonly Queue<GlucoseReading> _history = new();
    private const int MaxHistoryCount = 10;

    public void AddReading(GlucoseReading reading)
    {
        _history.Enqueue(reading);

        while (_history.Count > MaxHistoryCount)
        {
            _history.Dequeue();
        }
    }

    public List<GlucoseReading> GetHistory()
    {
        return _history.ToList();
    }

    public List<double> GetLast5Changes()
    {
        var history = _history.ToList();
        var changes = new List<double>();

        if (history.Count < 2)
        {
            return changes;
        }

        for (int i = 1; i < history.Count; i++)
        {
            var change = history[i].Value - history[i - 1].Value;
            changes.Add(change);
        }

        return changes.TakeLast(5).ToList();
    }

    public string GetChangesDisplayText()
    {
        var changes = GetLast5Changes();
        if (!changes.Any())
        {
            return "No changes yet";
        }

        var changeTexts = changes.Select(change =>
        {
            var sign = change >= 0 ? "+" : "";
            return $"{sign}{change:F0}";
        });

        return string.Join(", ", changeTexts);
    }

    public List<double> GetLast5Values()
    {
        return _history.TakeLast(5).Select(r => r.Value).ToList();
    }

    public string GetValuesDisplayText()
    {
        var readings = _history.TakeLast(5).ToList();
        if (!readings.Any())
        {
            return "No data yet";
        }

        return string.Join(", ", readings.Select(r => r.Value.ToString("F0")));
    }

    public string GetTimesDisplayText()
    {
        var readings = _history.TakeLast(5).ToList();
        if (!readings.Any())
        {
            return "No times yet";
        }

        return string.Join(", ", readings.Select(r => r.Timestamp.ToString("HH:mm")));
    }

    public string GetTrendDescription()
    {
        var changes = GetLast5Changes();
        if (changes.Count < 3)
        {
            return "Insufficient data";
        }

        var lastThree = changes.TakeLast(3).ToList();
        var avgChange = lastThree.Average();

        return avgChange switch
        {
            > 10 => "Rising rapidly",
            > 5 => "Rising",
            > 1 => "Rising slowly",
            >= -1 => "Stable",
            >= -5 => "Falling slowly",
            >= -10 => "Falling",
            _ => "Falling rapidly"
        };
    }

    public int Count => _history.Count;
    public GlucoseReading? Latest => _history.LastOrDefault();

    public void Clear()
    {
        _history.Clear();
    }
}