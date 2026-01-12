using Serilog;

namespace GlucoseMonitor.UI.Services;

public class WinUILogger : GlucoseMonitor.Core.Interfaces.ILogger
{
    private readonly MainWindow _mainWindow;

    public WinUILogger(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        Log.Debug("WinUILogger initialized");
    }

    public void LogMessage(string message)
    {
        Log.Information(message);
        _mainWindow.AppendLog(message);
    }

    public void LogError(string error)
    {
        Log.Error(error);
        _mainWindow.AppendLog($"ERROR: {error}");
        _mainWindow.UpdateStatus(error, isError: true);
    }

    public void LogInfo(string info)
    {
        Log.Information(info);
        _mainWindow.AppendLog($"INFO: {info}");
        _mainWindow.UpdateStatus(info, isError: false);
    }

    public void LogWarning(string warning)
    {
        Log.Warning(warning);
        _mainWindow.AppendLog($"WARN: {warning}");
    }

    public void LogDebug(string debug)
    {
        Log.Debug(debug);
        // Debug messages only go to file, not UI
    }
}
