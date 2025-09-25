using GlucoseMonitor.Core.Interfaces;

namespace GlucoseMonitor.UI.Services;

public class UILogger : ILogger
{
    private readonly TextBox? _logTextBox;
    private readonly Label? _statusLabel;

    public UILogger(TextBox? logTextBox, Label? statusLabel)
    {
        _logTextBox = logTextBox;
        _statusLabel = statusLabel;
    }

    public void LogMessage(string message)
    {
        if (_logTextBox != null)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            if (_logTextBox.InvokeRequired)
            {
                _logTextBox.Invoke(() =>
                {
                    _logTextBox.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
                    _logTextBox.ScrollToCaret();
                });
            }
            else
            {
                _logTextBox.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
                _logTextBox.ScrollToCaret();
            }
        }
    }

    public void LogError(string error)
    {
        LogMessage($"ERROR: {error}");
        UpdateStatus(error, Color.Red);
    }

    public void LogInfo(string info)
    {
        LogMessage($"INFO: {info}");
        UpdateStatus(info, Color.Green);
    }

    private void UpdateStatus(string status, Color color)
    {
        if (_statusLabel != null)
        {
            if (_statusLabel.InvokeRequired)
            {
                _statusLabel.Invoke(() =>
                {
                    _statusLabel.Text = status;
                    _statusLabel.ForeColor = color;
                });
            }
            else
            {
                _statusLabel.Text = status;
                _statusLabel.ForeColor = color;
            }
        }
    }
}