using GlucoseMonitor.Core.Models;

namespace GlucoseMonitor.Core.Interfaces;

public interface IGlucoseDataService
{
    string? NightscoutUrl { get; set; }
    string? AccessToken { get; set; }
    string Units { get; set; }

    event EventHandler<string>? LogMessage;
    event EventHandler<GlucoseReading>? GlucoseDataReceived;
    event EventHandler<string>? ErrorOccurred;

    Task<GlucoseReading?> GetLatestGlucoseAsync();
    Task<List<GlucoseReading>> GetRecentGlucoseAsync(int count);
    bool ValidateConfiguration();
    void Dispose();
}