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

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int Left, Right, Top, Bottom; }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const uint LWA_ALPHA = 0x2;

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_DONOTROUND = 1;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    private DispatcherTimer _refreshTimer = null!;
    private readonly TextBlock[] _valCells;
    private readonly TextBlock[] _chgCells;
    private readonly TextBlock[] _timeCells;
    private AppWindow? _appWindow;
    private IntPtr _hwnd;
    private bool _isDragging;
    private POINT _dragStartCursor;
    private PointInt32 _windowStartPosition;
    private double _opacity = 0.8;
    private DispatcherTimer? _topmostTimer;
    private DispatcherTimer? _flashTimer;
    private bool _flashState = false;
    private string _currentAlertLevel = "Normal";
    private double _lastGlucoseValue = 0;

    // Default glucose ranges (mg/dL) - Type 1 Diabetes recommended
    public static double UrgentLowThreshold = 54;    // Level 2 hypoglycemia
    public static double LowThreshold = 70;          // Level 1 hypoglycemia
    public static double HighThreshold = 180;        // Above target range
    public static double UrgentHighThreshold = 250;  // Significantly high

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
        SetupFlashTimer();
        LoadPosition();
        LoadThresholds();

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

        // Remove rounded corners on Windows 11
        int cornerPref = DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

        // Extend frame into client area to remove border
        var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(_hwnd, ref margins);

        // Set opacity from state
        var state = App.StateManager.LoadCustomState();
        if (state.TryGetValue("Opacity", out var opacityStr) && double.TryParse(opacityStr, out var opacity))
        {
            _opacity = opacity;
        }
        SetWindowOpacity(_opacity);

        // Force always-on-top using Win32 API
        EnforceTopmost();

        // Start timer to keep window on top (every 500ms)
        _topmostTimer = new DispatcherTimer();
        _topmostTimer.Interval = TimeSpan.FromMilliseconds(500);
        _topmostTimer.Tick += (s, e) => EnforceTopmost();
        _topmostTimer.Start();

        // Auto-size window to content after a short delay
        DispatcherQueue.TryEnqueue(() =>
        {
            ResizeToContent();
        });
    }

    private void EnforceTopmost()
    {
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private void ResizeToContent()
    {
        if (_appWindow == null) return;

        // Get the root grid and measure its desired size
        var rootGrid = Content as Microsoft.UI.Xaml.Controls.Grid;
        if (rootGrid != null)
        {
            rootGrid.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredSize = rootGrid.DesiredSize;

            // Add small padding and resize
            int width = (int)desiredSize.Width + 4;
            int height = (int)desiredSize.Height + 4;

            // Minimum size
            width = Math.Max(width, 200);
            height = Math.Max(height, 80);

            _appWindow.Resize(new SizeInt32(width, height));
        }
    }

    private void SetWindowOpacity(double opacity)
    {
        _opacity = Math.Clamp(opacity, 0.1, 1.0);

        // Set extended window styles: layered + toolwindow (removes from taskbar)
        int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TOOLWINDOW);

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

        // Use screen coordinates for smooth dragging
        GetCursorPos(out _dragStartCursor);
        _windowStartPosition = _appWindow.Position;
        _isDragging = true;
        ((UIElement)sender).CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || _appWindow == null) return;

        // Get current screen cursor position
        GetCursorPos(out POINT currentCursor);
        var deltaX = currentCursor.X - _dragStartCursor.X;
        var deltaY = currentCursor.Y - _dragStartCursor.Y;

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

    private void SetupFlashTimer()
    {
        _flashTimer = new DispatcherTimer();
        _flashTimer.Interval = TimeSpan.FromMilliseconds(500);
        _flashTimer.Tick += (s, e) =>
        {
            if (_currentAlertLevel == "Normal")
            {
                // No flashing needed
                SetBackgroundColor(Microsoft.UI.Colors.Black);
                _flashTimer?.Stop();
                return;
            }

            _flashState = !_flashState;

            if (_flashState)
            {
                // Flash color based on alert level
                var color = _currentAlertLevel switch
                {
                    "UrgentLow" or "UrgentHigh" => Microsoft.UI.Colors.DarkRed,
                    "Low" or "High" => Windows.UI.Color.FromArgb(255, 200, 100, 0), // Orange
                    _ => Microsoft.UI.Colors.Black
                };
                SetBackgroundColor(color);
            }
            else
            {
                SetBackgroundColor(Microsoft.UI.Colors.Black);
            }
        };
    }

    private void SetBackgroundColor(Windows.UI.Color color)
    {
        var rootGrid = Content as Microsoft.UI.Xaml.Controls.Grid;
        if (rootGrid != null)
        {
            rootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
        }
    }

    private void UpdateAlertLevel(double glucoseValue)
    {
        _lastGlucoseValue = glucoseValue;
        string newLevel;

        if (glucoseValue <= UrgentLowThreshold)
            newLevel = "UrgentLow";
        else if (glucoseValue <= LowThreshold)
            newLevel = "Low";
        else if (glucoseValue >= UrgentHighThreshold)
            newLevel = "UrgentHigh";
        else if (glucoseValue >= HighThreshold)
            newLevel = "High";
        else
            newLevel = "Normal";

        if (newLevel != _currentAlertLevel)
        {
            _currentAlertLevel = newLevel;

            if (newLevel == "Normal")
            {
                _flashTimer?.Stop();
                SetBackgroundColor(Microsoft.UI.Colors.Black);
            }
            else
            {
                _flashTimer?.Start();
            }
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

            // Update alert level for flashing
            UpdateAlertLevel(reading.Value);
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

    // Context menu handlers
    private void MenuShowSettings_Click(object sender, RoutedEventArgs e)
    {
        App.ShowMainWindow();
    }

    private async void MenuRefresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshGlucoseAsync();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        App.ExitApplication();
    }

    private void LoadThresholds()
    {
        try
        {
            var state = App.StateManager.LoadCustomState();
            if (state.TryGetValue("UrgentLowThreshold", out var urgentLow) && double.TryParse(urgentLow, out var ul))
                UrgentLowThreshold = ul;
            if (state.TryGetValue("LowThreshold", out var low) && double.TryParse(low, out var l))
                LowThreshold = l;
            if (state.TryGetValue("HighThreshold", out var high) && double.TryParse(high, out var h))
                HighThreshold = h;
            if (state.TryGetValue("UrgentHighThreshold", out var urgentHigh) && double.TryParse(urgentHigh, out var uh))
                UrgentHighThreshold = uh;
        }
        catch { }
    }

    public static void SaveThresholds()
    {
        try
        {
            var state = App.StateManager.LoadCustomState();
            state["UrgentLowThreshold"] = UrgentLowThreshold.ToString();
            state["LowThreshold"] = LowThreshold.ToString();
            state["HighThreshold"] = HighThreshold.ToString();
            state["UrgentHighThreshold"] = UrgentHighThreshold.ToString();
            App.StateManager.SaveState(state);
        }
        catch { }
    }

    public static void ResetThresholdsToDefaults()
    {
        UrgentLowThreshold = 54;    // Level 2 hypoglycemia
        LowThreshold = 70;          // Level 1 hypoglycemia
        HighThreshold = 180;        // Above target range
        UrgentHighThreshold = 250;  // Significantly high
        SaveThresholds();
    }
}
