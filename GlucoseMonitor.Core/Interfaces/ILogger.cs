namespace GlucoseMonitor.Core.Interfaces;

public interface ILogger
{
    void LogMessage(string message);
    void LogError(string error);
    void LogInfo(string info);
}