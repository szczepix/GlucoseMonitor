using GlucoseMonitor.Core.Interfaces;

namespace GlucoseMonitor.UI.Services;

public class WinUILogger : ILogger
{
    private readonly MainWindow _mainWindow;

    public WinUILogger(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public void LogMessage(string message)
    {
        _mainWindow.AppendLog(message);
    }

    public void LogError(string error)
    {
        LogMessage($"ERROR: {error}");
        _mainWindow.UpdateStatus(error, isError: true);
    }

    public void LogInfo(string info)
    {
        LogMessage($"INFO: {info}");
        _mainWindow.UpdateStatus(info, isError: false);
    }
}
