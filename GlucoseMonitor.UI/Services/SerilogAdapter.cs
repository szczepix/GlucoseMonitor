using Serilog;

namespace GlucoseMonitor.UI.Services;

/// <summary>
/// Simple Serilog adapter for services that need logging before MainWindow is created.
/// </summary>
public class SerilogAdapter : GlucoseMonitor.Core.Interfaces.ILogger
{
    public void LogMessage(string message) => Log.Information(message);
    public void LogError(string error) => Log.Error(error);
    public void LogInfo(string info) => Log.Information(info);
    public void LogWarning(string warning) => Log.Warning(warning);
    public void LogDebug(string debug) => Log.Debug(debug);
}
