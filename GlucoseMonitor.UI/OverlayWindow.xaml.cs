using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using GlucoseMonitor.Core.Models;
using Windows.Graphics;
using WinRT.Interop;
using System.Runtime.InteropServices;

namespace GlucoseMonitor.UI;

public sealed partial class OverlayWindow : Window
{
    // P/Invoke for window transparency
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const uint LWA_ALPHA = 0x2;

    private DispatcherTimer _refreshTimer = null!;
    private readonly TextBlock[] _valCells;
    private readonly TextBlock[] _chgCells;
    private readonly TextBlock[] _timeCells;
    private AppWindow? _appWindow;
    private IntPtr _hwnd;
    private bool _isDragging;
    private PointInt32 _dragStartPosition;
    private PointInt32 _windowStartPosition;
    private double _opacity = 0.8;

    public OverlayWindow()
    {
        InitializeComponent();

        // Store cell references for easy updating
        _valCells = new[] { Val0, Val1, Val2, Val3, Val4 };
        _chgCells = new[] { Chg0, Chg1, Chg2, Chg3, Chg4 };
        _timeCells = new[] { Time0, Time1, Time2, Time3, Time4 };

        SetupWindow();
        SetupDragging();
        SetupTimer();
        LoadPosition();

        // Initial fetch
        _ = RefreshGlucoseAsync();
    }

    private void SetupWindow()
    {
        _hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow != null)
        {
            // Set window size
            _appWindow.Resize(new SizeInt32(320, 180));

            // Remove title bar for overlay look
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsAlwaysOnTop = true;
            }

            Title = "Glucose Overlay";
        }

        // Set opacity from state
        var state = App.StateManager.LoadCustomState();
        if (state.TryGetValue("Opacity", out var opacityStr) && double.TryParse(opacityStr, out var opacity))
        {
            _opacity = opacity;
        }
        SetWindowOpacity(_opacity);
    }

    private void SetWindowOpacity(double opacity)
    {
        _opacity = Math.Clamp(opacity, 0.1, 1.0);

        // Set layered window style
        int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);

        // Set opacity (0-255)
        byte alpha = (byte)(_opacity * 255);
        SetLayeredWindowAttributes(_hwnd, 0, alpha, LWA_ALPHA);
    }

    public void UpdateOpacity(double opacity)
    {
        SetWindowOpacity(opacity);
    }

    private void SetupDragging()
    {
        var rootGrid = Content as Microsoft.UI.Xaml.Controls.Grid;
        if (rootGrid != null)
        {
            rootGrid.PointerPressed += OnPointerPressed;
            rootGrid.PointerMoved += OnPointerMoved;
            rootGrid.PointerReleased += OnPointerReleased;
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_appWindow == null) return;

        _isDragging = true;
        var point = e.GetCurrentPoint(Content as UIElement);
        _dragStartPosition = new PointInt32((int)point.Position.X, (int)point.Position.Y);
        _windowStartPosition = _appWindow.Position;
        ((UIElement)sender).CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || _appWindow == null) return;

        var point = e.GetCurrentPoint(Content as UIElement);
        var deltaX = (int)point.Position.X - _dragStartPosition.X;
        var deltaY = (int)point.Position.Y - _dragStartPosition.Y;

        _appWindow.Move(new PointInt32(
            _windowStartPosition.X + deltaX,
            _windowStartPosition.Y + deltaY));
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ((UIElement)sender).ReleasePointerCapture(e.Pointer);
            SavePosition();
        }
    }

    private void SetupTimer()
    {
        _refreshTimer = new DispatcherTimer();
        _refreshTimer.Interval = TimeSpan.FromMinutes(1);
        _refreshTimer.Tick += async (s, e) => await RefreshGlucoseAsync();
        _refreshTimer.Start();
    }

    private async Task RefreshGlucoseAsync()
    {
        try
        {
            if (!App.GlucoseService.ValidateConfiguration())
            {
                GlucoseValueText.Text = "Config?";
                return;
            }

            var readings = await App.GlucoseService.GetRecentGlucoseAsync(20);
            if (readings != null && readings.Count > 0)
            {
                var latest = readings.Last();
                UpdateGlucoseDisplay(latest);
                UpdateHistoryDisplay(readings);
            }
        }
        catch (Exception ex)
        {
            App.Logger?.LogError($"Overlay refresh failed: {ex.Message}");
        }
    }

    public void UpdateGlucoseDisplay(GlucoseReading reading)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            GlucoseValueText.Text = $"{reading.Value:F0} {reading.GetDirectionArrow()}";
            GlucoseValueText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                ToWindowsColor(reading.GetGlucoseColor()));

            var sign = reading.Delta >= 0 ? "+" : "";
            DeltaText.Text = $"{sign}{reading.Delta:F1} {reading.Units}";

            var age = DateTime.Now - reading.Timestamp;
            TimeAgoText.Text = age.TotalMinutes < 1
                ? "just now"
                : age.TotalMinutes < 60
                    ? $"{(int)age.TotalMinutes} min ago"
                    : $"{(int)age.TotalHours}h ago";
        });
    }

    public void UpdateHistoryDisplay(List<GlucoseReading> readings)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var ordered = readings.OrderByDescending(r => r.Timestamp).Take(5).ToList();

            // Pad to 5 if needed
            while (ordered.Count < 5)
            {
                ordered.Add(new GlucoseReading { Value = double.NaN, Timestamp = DateTime.MinValue });
            }

            for (int i = 0; i < 5; i++)
            {
                var r = ordered[i];
                _valCells[i].Text = double.IsNaN(r.Value) ? "" : r.Value.ToString("F0");

                // Calculate change vs next older reading
                if (double.IsNaN(r.Value))
                {
                    _chgCells[i].Text = "";
                }
                else if (i == 4 || double.IsNaN(ordered[i + 1].Value))
                {
                    _chgCells[i].Text = "+0";
                }
                else
                {
                    var delta = r.Value - ordered[i + 1].Value;
                    _chgCells[i].Text = delta >= 0 ? $"+{delta:F0}" : $"{delta:F0}";
                }

                _timeCells[i].Text = double.IsNaN(r.Value) || r.Timestamp == DateTime.MinValue
                    ? ""
                    : r.Timestamp.ToString("HH:mm");
            }
        });
    }

    private static Windows.UI.Color ToWindowsColor(System.Drawing.Color c)
    {
        return Windows.UI.Color.FromArgb(c.A, c.R, c.G, c.B);
    }

    private void LoadPosition()
    {
        try
        {
            var positionFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GlucoseMonitor", "overlay_position.txt");

            if (File.Exists(positionFile) && _appWindow != null)
            {
                var lines = File.ReadAllLines(positionFile);
                if (lines.Length >= 2 &&
                    int.TryParse(lines[0], out int x) &&
                    int.TryParse(lines[1], out int y))
                {
                    _appWindow.Move(new PointInt32(x, y));
                    return;
                }
            }
        }
        catch { }

        // Default: top-right corner
        if (_appWindow != null)
        {
            var area = DisplayArea.Primary;
            if (area != null)
            {
                _appWindow.Move(new PointInt32(
                    area.WorkArea.Width - 340,
                    20));
            }
        }
    }

    private void SavePosition()
    {
        try
        {
            if (_appWindow == null) return;

            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GlucoseMonitor");
            Directory.CreateDirectory(configDir);

            var positionFile = Path.Combine(configDir, "overlay_position.txt");
            File.WriteAllLines(positionFile, new[]
            {
                _appWindow.Position.X.ToString(),
                _appWindow.Position.Y.ToString()
            });
        }
        catch { }
    }
}
