using GlucoseMonitor.Core.Models;

namespace GlucoseMonitor.Core.Interfaces;

public interface IGlucoseHistoryService
{
    void AddReading(GlucoseReading reading);
    List<GlucoseReading> GetHistory();
    List<double> GetLast5Changes();
    List<double> GetLast5Values();
    string GetChangesDisplayText();
    string GetValuesDisplayText();
    string GetTimesDisplayText();
    string GetTrendDescription();
    int Count { get; }
    GlucoseReading? Latest { get; }
    void Clear();
}