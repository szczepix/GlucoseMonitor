using GlucoseMonitor.Core.Interfaces;
using GlucoseMonitor.Core.Models;
using GlucoseMonitor.Infrastructure.Services;
using GlucoseMonitor.Infrastructure.DependencyInjection;
using GlucoseMonitor.UI.Services;
using System.ComponentModel;
using System.Media;

namespace GlucoseMonitor.UI;

public partial class MainForm : Form
{
    private readonly ServiceContainer _serviceContainer;
    private readonly IGlucoseDataService _glucoseService;
    private readonly IConfigurationService _configService;
    private readonly IStateManager _stateManager;
    private readonly IGlucoseHistoryService _historyService;
    private ILogger? _logger;
    private bool _didStartupInit = false;

    // UI Components
    private TextBox? _logTextBox;
    private Label? _statusLabel;
    private Label? _glucoseDisplayLabel;
    private TextBox? _nightscoutUrlTextBox;
    private TextBox? _accessTokenTextBox;
    private TrackBar? _opacityTrackBar;
    private Label? _opacityLabel;
    private Button? _testButton;
    private Button? _overlayToggleButton;
    private Button? _monitoringToggleButton;
    private ListBox? _historyListBox;
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private System.Windows.Forms.Timer? _refreshTimer;
    private OverlayForm? _overlayForm;
    private bool _isMonitoring = true; // Start as monitoring by default
    private readonly Queue<GlucoseReading> _glucoseHistory = new();

    // Additional UI controls
    private CheckBox? _autoMinimizeCheckBox;
    private CheckBox? _autoStartCheckBox;
    private CheckBox? _autoShowCheckBox;
    private ComboBox? _unitsComboBox;
    private NumericUpDown? _intervalNumericUpDown;
    private NumericUpDown? _recentFetchNumericUpDown; // how many recent readings to request
    private Label? _recentFetchStatusLabel; // shows how many were retrieved last fetch
    private int _iterationCount = 11; // Track monitoring iterations

    // Context menu / alarm snooze state
    private ToolStripMenuItem? _snoozeMenu;
    private ToolStripMenuItem? _snoozeStatusItem;
    private DateTime? _snoozeUntil;
    private bool _alarmsEnabled = true;
    private System.Windows.Forms.Timer? _snoozeTimer;

    // Alarm settings
    private CheckBox? _enableSoundAlarmsCheckBox;
    private NumericUpDown? _urgentHighNumeric;
    private NumericUpDown? _highNumeric;
    private NumericUpDown? _lowNumeric;
    private NumericUpDown? _urgentLowNumeric;

    private readonly Dictionary<string, DateTime> _lastAlarmTimes = new();
    private TimeSpan _alarmCooldown = TimeSpan.FromMinutes(5);

    public MainForm()
    {
        // Initialize services first (DI)
        _serviceContainer = new ServiceContainer();

        _configService = new ConfigurationService();
        _serviceContainer.RegisterSingleton<IConfigurationService>(_configService);

        _stateManager = new StateManagerService();
        _serviceContainer.RegisterSingleton<IStateManager>(_stateManager);

        _historyService = new GlucoseHistoryService();
        _serviceContainer.RegisterSingleton<IGlucoseHistoryService>(_historyService);

        _glucoseService = new NightscoutService();
        _serviceContainer.RegisterSingleton<IGlucoseDataService>(_glucoseService);

        InitializeComponent();

        // Initialize logger after UI components exist
        _logger = new UILogger(_logTextBox, _statusLabel);
        _serviceContainer.RegisterSingleton<ILogger>(_logger);

        // Load configuration
        LoadConfiguration();

        // Initialize with sample data to match the screenshot
        InitializeSampleData();

        _logger.LogInfo("SOLID Glucose Monitor started successfully!");

        // Defer auto-start initialization until form is shown
        Shown += (s, e) =>
        {
            if (_didStartupInit) return;
            _didStartupInit = true;
            BeginInvoke(async () => await SafeStartupSequence());
        };
    }

    private void InitializeComponent()
    {
        Text = "Simple Desktop App - Nightscout Monitor with History";
        Size = new Size(640, 700);
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = true;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        WindowState = FormWindowState.Minimized;
        ShowInTaskbar = false;
        Opacity = 0;

        // Setup system tray
        SetupSystemTray();

        // Setup refresh timer
        _refreshTimer = new System.Windows.Forms.Timer();
        _refreshTimer.Interval = 60000; // 1 minute
        _refreshTimer.Tick += RefreshTimer_Tick;

        // Current Glucose Display
        var glucoseGroup = new GroupBox
        {
            Text = "Current Glucose",
            Location = new Point(10, 10),
            Size = new Size(590, 60),
            BackColor = Color.Black,
            ForeColor = Color.White
        };

        _glucoseDisplayLabel = new Label
        {
            Text = "178 → mg/dL",
            Location = new Point(10, 20),
            Size = new Size(570, 30),
            Font = new Font("Consolas", 16, FontStyle.Bold),
            ForeColor = Color.Lime,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Black
        };

        glucoseGroup.Controls.Add(_glucoseDisplayLabel);

        // Nightscout Configuration
        var configGroup = new GroupBox
        {
            Text = "Nightscout Configuration",
            Location = new Point(10, 80),
            Size = new Size(590, 120)
        };

        var urlLabel = new Label
        {
            Text = "Nightscout URL:",
            Location = new Point(10, 25),
            Size = new Size(100, 20)
        };

        _nightscoutUrlTextBox = new TextBox
        {
            Location = new Point(10, 45),
            Size = new Size(420, 20),
            PlaceholderText = "https://your-site.herokuapp.com"
        };

        var tokenLabel = new Label
        {
            Text = "Access Token:",
            Location = new Point(10, 70),
            Size = new Size(100, 20)
        };

        _accessTokenTextBox = new TextBox
        {
            Location = new Point(110, 68),
            Size = new Size(200, 20),
            UseSystemPasswordChar = true
        };

        var unitsLabel = new Label
        {
            Text = "Units:",
            Location = new Point(320, 70),
            Size = new Size(40, 20)
        };

        var unitsComboBox = new ComboBox
        {
            Location = new Point(365, 68),
            Size = new Size(65, 20),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        unitsComboBox.Items.AddRange(new[] { "mg/dL", "mmol/L" });
        unitsComboBox.SelectedIndex = 0;

        _testButton = new Button
        {
            Text = "Test Connection",
            Location = new Point(440, 25),
            Size = new Size(120, 25),
            BackColor = Color.LightBlue
        };
        _testButton.Click += TestButton_Click;

        var saveConfigButton = new Button
        {
            Text = "Save Config",
            Location = new Point(440, 68),
            Size = new Size(120, 25),
            BackColor = Color.LightGreen
        };
        saveConfigButton.Click += SaveButton_Click;

        configGroup.Controls.AddRange(new Control[] { urlLabel, _nightscoutUrlTextBox, tokenLabel, _accessTokenTextBox, unitsLabel, unitsComboBox, _testButton, saveConfigButton });

        // Overlay Controls - Split into two sections
        var intervalGroup = new GroupBox
        {
            Text = "Polling & Fetch",
            Location = new Point(10, 210),
            Size = new Size(290, 80)
        };

        var intervalTextBox = new NumericUpDown
        {
            Location = new Point(10, 30),
            Size = new Size(50, 20),
            Minimum = 1,
            Maximum = 60,
            Value = 1
        };

        var intervalLbl = new Label
        {
            Text = "Interval (min):",
            Location = new Point(65, 32),
            Size = new Size(80, 16)
        };

        _recentFetchNumericUpDown = new NumericUpDown
        {
            Location = new Point(150, 30),
            Size = new Size(50, 20),
            Minimum = 5,
            Maximum = 288,
            Value = 20
        };

        var recentLbl = new Label
        {
            Text = "Fetch count:",
            Location = new Point(205, 32),
            Size = new Size(75, 16)
        };

        _recentFetchStatusLabel = new Label
        {
            Text = "Fetched: -",
            Location = new Point(10, 55),
            Size = new Size(270, 16)
        };

        intervalGroup.Controls.AddRange(new Control[] { intervalTextBox, intervalLbl, _recentFetchNumericUpDown, recentLbl, _recentFetchStatusLabel });

        var opacityGroup = new GroupBox
        {
            Text = "",
            Location = new Point(210, 210),
            Size = new Size(390, 80)
        };

        _opacityTrackBar = new TrackBar
        {
            Location = new Point(10, 30),
            Size = new Size(100, 45),
            Minimum = 20,
            Maximum = 100,
            Value = 80,
            TickFrequency = 20
        };
        _opacityTrackBar.Scroll += OpacityTrackBar_Scroll;

        _opacityLabel = new Label
        {
            Text = "80",
            Location = new Point(115, 35),
            Size = new Size(25, 20)
        };

        _monitoringToggleButton = new Button
        {
            Text = "Stop Service",
            Location = new Point(150, 20),
            Size = new Size(80, 25),
            BackColor = Color.LightGreen
        };
        _monitoringToggleButton.Click += MonitoringToggleButton_Click;

        _overlayToggleButton = new Button
        {
            Text = "Hide Overlay",
            Location = new Point(240, 20),
            Size = new Size(80, 25),
            BackColor = Color.LightCyan
        };
        _overlayToggleButton.Click += OverlayToggleButton_Click;

        var hideToTrayButton = new Button
        {
            Text = "Hide to Tray",
            Location = new Point(150, 50),
            Size = new Size(80, 25),
            BackColor = Color.LightYellow
        };
        hideToTrayButton.Click += (s, e) => { WindowState = FormWindowState.Minimized; ShowInTaskbar = false; };

        var clearLogButton = new Button
        {
            Text = "Clear Log",
            Location = new Point(240, 50),
            Size = new Size(80, 25),
            BackColor = Color.Orange
        };
        clearLogButton.Click += ClearLog_Click;

        opacityGroup.Controls.AddRange(new Control[] { _opacityTrackBar, _opacityLabel, _monitoringToggleButton, _overlayToggleButton, hideToTrayButton, clearLogButton });

        // Alarm Settings
        var alarmGroup = new GroupBox
        {
            Text = "Alarm settings",
            Location = new Point(10, 300),
            Size = new Size(590, 100)
        };

        var enableSoundCb = new CheckBox
        {
            Text = "Enable sound alarms",
            Location = new Point(10, 20),
            Size = new Size(150, 20),
            Checked = true
        };
        _enableSoundAlarmsCheckBox = enableSoundCb;

        var uhLabel = new Label { Text = "Urgent High:", Location = new Point(10, 45), Size = new Size(90, 20) };
        _urgentHighNumeric = new NumericUpDown { Location = new Point(100, 43), Size = new Size(70, 20), Minimum = 40, Maximum = 400, DecimalPlaces = 1, Increment = 1, Value = 234 };
        var hLabel = new Label { Text = "High:", Location = new Point(180, 45), Size = new Size(40, 20) };
        _highNumeric = new NumericUpDown { Location = new Point(220, 43), Size = new Size(70, 20), Minimum = 40, Maximum = 400, DecimalPlaces = 1, Increment = 1, Value = 198 };
        var lLabel = new Label { Text = "Low:", Location = new Point(300, 45), Size = new Size(40, 20) };
        _lowNumeric = new NumericUpDown { Location = new Point(340, 43), Size = new Size(70, 20), Minimum = 40, Maximum = 400, DecimalPlaces = 1, Increment = 1, Value = 81 };
        var ulLabel = new Label { Text = "Urgent Low:", Location = new Point(420, 45), Size = new Size(80, 20) };
        _urgentLowNumeric = new NumericUpDown { Location = new Point(500, 43), Size = new Size(70, 20), Minimum = 40, Maximum = 400, DecimalPlaces = 1, Increment = 1, Value = 68 };

        alarmGroup.Controls.AddRange(new Control[] { enableSoundCb, uhLabel, _urgentHighNumeric, hLabel, _highNumeric, lLabel, _lowNumeric, ulLabel, _urgentLowNumeric });

        // Glucose History
        var historyGroup = new GroupBox
        {
            Text = "Glucose History (Last 5 readings)",
            Location = new Point(10, 410),
            Size = new Size(590, 100)
        };

        _historyListBox = new ListBox
        {
            Location = new Point(10, 20),
            Size = new Size(570, 70),
            Font = new Font("Consolas", 9),
            BackColor = SystemColors.Control,
            ForeColor = Color.Black
        };

        // Add sample history data
        _historyListBox.Items.Add("Values: 170, 170, 178, 178, 178");
        _historyListBox.Items.Add("Changes: +0, +0, +8, +0, +0");
        _historyListBox.Items.Add("Times: 20:47, 20:47, 20:52, 20:52, 20:52");

        historyGroup.Controls.Add(_historyListBox);

        // Status
        var statusGroup = new GroupBox
        {
            Text = "Status",
            Location = new Point(10, 410),
            Size = new Size(590, 50)
        };

        _statusLabel = new Label
        {
            Text = "Monitoring: Glucose: 178 → (iteration 11)\n2025-09-16 22:56:38",
            Location = new Point(10, 15),
            Size = new Size(570, 30),
            ForeColor = Color.Black
        };

        statusGroup.Controls.Add(_statusLabel);

        // Controls with checkboxes
        var controlsGroup = new GroupBox
        {
            Text = "Controls",
            Location = new Point(10, 470),
            Size = new Size(590, 50)
        };

        var autoMinimizeCheckBox = new CheckBox
        {
            Text = "Auto-minimize on startup",
            Location = new Point(10, 20),
            Size = new Size(150, 20),
            Checked = false
        };

        var autoStartCheckBox = new CheckBox
        {
            Text = "Auto-start monitoring",
            Location = new Point(200, 20),
            Size = new Size(150, 20),
            Checked = false
        };

        var autoShowCheckBox = new CheckBox
        {
            Text = "Auto-show overlay",
            Location = new Point(390, 20),
            Size = new Size(120, 20),
            Checked = false
        };

        controlsGroup.Controls.AddRange(new Control[] { autoMinimizeCheckBox, autoStartCheckBox, autoShowCheckBox });

        // Activity Log
        var logGroup = new GroupBox
        {
            Text = "Activity Log",
            Location = new Point(10, 530),
            Size = new Size(590, 120)
        };

        _logTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(10, 20),
            Size = new Size(570, 90),
            Font = new Font("Consolas", 8),
            ReadOnly = true,
            BackColor = Color.Black,
            ForeColor = Color.Lime
        };

        // Add sample log entries
        _logTextBox.Text = "[22:54:24] Fetching data from: https://8blf.ns.gluroo.com/pebble?\r\n" +
                          "count=1&units=mg&token=***\r\n" +
                          "[22:54:28] Received 155 characters of data\r\n" +
                          "[22:54:28] Parsed glucose: 178 mg/dL →\r\n" +
                          "[22:55:34] Fetching data from: https://8blf.ns.gluroo.com/pebble?\r\n" +
                          "count=1&units=mg&token=***\r\n" +
                          "[22:55:34] Received 155 characters of data\r\n" +
                          "[22:55:34] Parsed glucose: 178 mg/dL →\r\n" +
                          "[22:55:34] Glucose: 178 → (iteration 11)";

        logGroup.Controls.Add(_logTextBox);

        Controls.AddRange(new Control[] { glucoseGroup, configGroup, intervalGroup, opacityGroup, alarmGroup, historyGroup, statusGroup, controlsGroup, logGroup });
    }

    private void SetupSystemTray()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "Glucose Monitor",
            Visible = true
        };

        CreateContextMenu();
        _notifyIcon.ContextMenuStrip = _contextMenu;
        _notifyIcon.DoubleClick += (s, e) => { WindowState = FormWindowState.Normal; ShowInTaskbar = true; };
    }

    private void CreateContextMenu()
    {
        _contextMenu = new ContextMenuStrip();

        // Show Application
        _contextMenu.Items.Add("Show Application", null, (s, e) =>
        {
            Opacity = 1; // make settings window visible on demand
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        });

        // Snooze sound alarms submenu
        _snoozeMenu = new ToolStripMenuItem("Snooze sound alarms");
        var snooze30 = new ToolStripMenuItem("Snooze for 30 minutes", null, (s, e) => SnoozeFor(TimeSpan.FromMinutes(30)));
        var snooze90 = new ToolStripMenuItem("Snooze for 90 minutes", null, (s, e) => SnoozeFor(TimeSpan.FromMinutes(90)));
        var reenable = new ToolStripMenuItem("Re-enable alarms", null, (s, e) => ReenableAlarms());
        _snoozeStatusItem = new ToolStripMenuItem("") { Enabled = false };
        _snoozeMenu.DropDownItems.Add(snooze30);
        _snoozeMenu.DropDownItems.Add(snooze90);
        _snoozeMenu.DropDownItems.Add(new ToolStripSeparator());
        _snoozeMenu.DropDownItems.Add(reenable);
        _snoozeMenu.DropDownItems.Add(new ToolStripSeparator());
        _snoozeMenu.DropDownItems.Add(_snoozeStatusItem);
        _contextMenu.Items.Add(_snoozeMenu);

        // Open Nightscout site
        _contextMenu.Items.Add("Open nightscout site", null, (s, e) => OpenNightscoutSite());

        // Reload
        _contextMenu.Items.Add("Reload", null, async (s, e) => await RefreshGlucoseReading());

        // Settings
        _contextMenu.Items.Add("Settings", null, (s, e) =>
        {
            Opacity = 1; // make settings window visible on demand
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        });

        // Quit
        _contextMenu.Items.Add("Quit", null, (s, e) => ExitApplication());

        UpdateSnoozeMenuStatusText();
    }

    private void OpenNightscoutSite()
    {
        try
        {
            var url = _glucoseService.NightscoutUrl;
            if (!string.IsNullOrWhiteSpace(url))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else
            {
                _logger?.LogError("Nightscout URL is not configured");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to open Nightscout site: {ex.Message}");
        }
    }

    private void SnoozeFor(TimeSpan duration)
    {
        _alarmsEnabled = false;
        _snoozeUntil = DateTime.Now.Add(duration);
        _logger?.LogInfo($"Sound alarms snoozed until {_snoozeUntil:HH:mm}");

        if (_snoozeTimer == null)
        {
            _snoozeTimer = new System.Windows.Forms.Timer();
            _snoozeTimer.Interval = 30000; // 30 seconds
            _snoozeTimer.Tick += (s, e) => CheckSnooze();
        }
        _snoozeTimer.Start();
        UpdateSnoozeMenuStatusText();
    }

    private void ReenableAlarms()
    {
        _alarmsEnabled = true;
        _snoozeUntil = null;
        if (_snoozeTimer != null)
        {
            _snoozeTimer.Stop();
        }
        _logger?.LogInfo("Sound alarms re-enabled");
        UpdateSnoozeMenuStatusText();
    }

    private void CheckSnooze()
    {
        if (!_alarmsEnabled && _snoozeUntil.HasValue && DateTime.Now >= _snoozeUntil.Value)
        {
            ReenableAlarms();
        }
        else
        {
            UpdateSnoozeMenuStatusText();
        }
    }

    private void UpdateSnoozeMenuStatusText()
    {
        if (_snoozeStatusItem == null) return;
        if (!_alarmsEnabled && _snoozeUntil.HasValue)
        {
            _snoozeStatusItem.Text = $"Snoozing until {_snoozeUntil:HH:mm}";
        }
        else
        {
            _snoozeStatusItem.Text = "Alarms enabled";
        }
    }

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(value);
        if (value && WindowState == FormWindowState.Minimized)
        {
            ShowInTaskbar = false;
        }
    }

    private void LoadConfiguration()
    {
        var (url, token, units, interval) = _configService.LoadConfiguration();
        if (_nightscoutUrlTextBox != null) _nightscoutUrlTextBox.Text = url;
        if (_accessTokenTextBox != null) _accessTokenTextBox.Text = token;

        // Apply to service
        _glucoseService.NightscoutUrl = url;
        _glucoseService.AccessToken = token;
        _glucoseService.Units = units;

        // Load app state
        LoadAppState();
    }

    private void InitializeSampleData()
    {
        // Set initial glucose display
        if (_glucoseDisplayLabel != null)
        {
            _glucoseDisplayLabel.Text = "178 → mg/dL";
            _glucoseDisplayLabel.ForeColor = Color.Lime;
        }

        // Set initial status
        if (_statusLabel != null)
        {
            _statusLabel.Text = $"Monitoring: Glucose: 178 → (iteration 11)\r\n{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        // Set opacity trackbar to a sensible default (80%) for a dark, readable overlay
        if (_opacityTrackBar != null)
        {
            _opacityTrackBar.Value = 80;
            if (_opacityLabel != null)
                _opacityLabel.Text = "80";
        }
    }

    private void LoadAppState()
    {
        try
        {
            var state = _stateManager.LoadCustomState();
            if (state.ContainsKey("Opacity") && double.TryParse(state["Opacity"], out double opacity))
            {
                if (_opacityTrackBar != null)
                {
                    _opacityTrackBar.Value = (int)(opacity * 100);
                    if (_opacityLabel != null)
                        _opacityLabel.Text = $"{(int)(opacity * 100)}";
                }
            }

            if (state.TryGetValue("RecentFetchCount", out var rfc) && int.TryParse(rfc, out var fetchCount))
            {
                if (_recentFetchNumericUpDown != null)
                {
                    _recentFetchNumericUpDown.Value = Math.Max(_recentFetchNumericUpDown.Minimum, Math.Min(_recentFetchNumericUpDown.Maximum, fetchCount));
                }
            }

            // Load alarm settings
            if (state.TryGetValue("EnableSound", out var enableSound))
            {
                if (bool.TryParse(enableSound, out var enabled) && _enableSoundAlarmsCheckBox != null)
                    _enableSoundAlarmsCheckBox.Checked = enabled;
            }
            if (state.TryGetValue("UrgentHigh", out var uh) && double.TryParse(uh, out var vUh) && _urgentHighNumeric != null)
                _urgentHighNumeric.Value = (decimal)vUh;
            if (state.TryGetValue("High", out var h) && double.TryParse(h, out var vH) && _highNumeric != null)
                _highNumeric.Value = (decimal)vH;
            if (state.TryGetValue("Low", out var l) && double.TryParse(l, out var vL) && _lowNumeric != null)
                _lowNumeric.Value = (decimal)vL;
            if (state.TryGetValue("UrgentLow", out var ul) && double.TryParse(ul, out var vUl) && _urgentLowNumeric != null)
                _urgentLowNumeric.Value = (decimal)vUl;
            if (state.TryGetValue("AlarmCooldownMin", out var cd) && int.TryParse(cd, out var cdMin))
                _alarmCooldown = TimeSpan.FromMinutes(Math.Max(1, cdMin));
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to load app state: {ex.Message}");
        }
    }

    private void SaveAppState()
    {
        try
        {
            var state = new Dictionary<string, string>
            {
                ["Opacity"] = (_opacityTrackBar?.Value / 100.0 ?? 0.8).ToString(),
                ["RecentFetchCount"] = (_recentFetchNumericUpDown?.Value ?? 20).ToString(),
                ["EnableSound"] = (_enableSoundAlarmsCheckBox?.Checked ?? true).ToString(),
                ["UrgentHigh"] = (_urgentHighNumeric?.Value ?? 234).ToString(),
                ["High"] = (_highNumeric?.Value ?? 198).ToString(),
                ["Low"] = (_lowNumeric?.Value ?? 81).ToString(),
                ["UrgentLow"] = (_urgentLowNumeric?.Value ?? 68).ToString(),
                ["AlarmCooldownMin"] = ((int)_alarmCooldown.TotalMinutes).ToString()
            };
            _stateManager.SaveState(state);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to save app state: {ex.Message}");
        }
    }

    private async void TestButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _logger?.LogInfo("Testing Nightscout connection...");

            // Set both URL and token before testing
            _glucoseService.NightscoutUrl = _nightscoutUrlTextBox?.Text;
            _glucoseService.AccessToken = _accessTokenTextBox?.Text;

            if (!_glucoseService.ValidateConfiguration())
            {
                _logger?.LogError("Invalid configuration - please check URL format");
                return;
            }

            // Single request to fetch recent readings (get more and then filter to changes)
            var requestedCount = (int)(_recentFetchNumericUpDown?.Value ?? 20);
            var recent = await _glucoseService.GetRecentGlucoseAsync(requestedCount);
            _logger?.LogInfo($"Recent readings retrieved: {recent?.Count ?? 0} (requested {requestedCount})");
            if (_recentFetchStatusLabel != null)
            {
                _recentFetchStatusLabel.Text = $"Fetched: {recent?.Count ?? 0} (requested {requestedCount})";
            }
            if (recent != null && recent.Count > 0)
            {
                // Latest is the most recent item
                var latest = recent.Last();

                if (_glucoseDisplayLabel != null)
                {
                    _glucoseDisplayLabel.Text = $"{latest.Value:F0} {latest.GetDirectionArrow()} {latest.Units}";
                    _glucoseDisplayLabel.ForeColor = latest.GetGlucoseColor();
                }

                // Update history with last 5 changes
                RebuildHistoryFromReadings(recent);
                UpdateHistoryDisplay();

                _overlayForm?.UpdateGlucose(latest);
                _overlayForm?.UpdateRecentHistory(_glucoseHistory.ToList());
                EvaluateAndAlert(latest);

                // Debug dump of up to 10 latest readings (value@time)
                var dbg = string.Join(", ", recent.TakeLast(Math.Min(10, recent.Count)).Select(r => $"{r.Value:F0}@{r.Timestamp:HH:mm}"));
                _logger?.LogInfo($"Successfully retrieved {recent.Count} readings; latest: {latest.Value} {latest.Units}. Recent: {dbg}");
            }
            else
            {
                _logger?.LogError("Failed to retrieve glucose data");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Test failed: {ex.Message}");
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _configService.SaveConfiguration(
                _nightscoutUrlTextBox?.Text ?? "",
                _accessTokenTextBox?.Text ?? "",
                "mg",
                1);

            // Immediately apply the configuration to the service
            _glucoseService.NightscoutUrl = _nightscoutUrlTextBox?.Text;
            _glucoseService.AccessToken = _accessTokenTextBox?.Text;

            _logger?.LogInfo("Configuration saved and applied successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Save failed: {ex.Message}");
        }
    }

    private void SaveOverlayButton_Click(object? sender, EventArgs e)
    {
        try
        {
            SaveAppState();

            // Apply opacity to existing overlay
            if (_overlayForm != null)
            {
                _overlayForm.Opacity = _opacityTrackBar?.Value / 100.0 ?? 0.8;
            }

            _logger?.LogInfo("Overlay settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Save overlay settings failed: {ex.Message}");
        }
    }

    private void OpacityTrackBar_Scroll(object? sender, EventArgs e)
    {
        if (_opacityTrackBar != null && _opacityLabel != null)
        {
            _opacityLabel.Text = $"{_opacityTrackBar.Value}";

            // Apply opacity to existing overlay
            if (_overlayForm != null)
            {
                _overlayForm.Opacity = _opacityTrackBar.Value / 100.0;
            }
        }
    }

    private void OverlayToggleButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_overlayForm != null && !_overlayForm.IsDisposed)
            {
                _overlayForm.Close();
                _overlayForm = null;
                if (_overlayToggleButton != null)
                    _overlayToggleButton.Text = "Show Overlay";
                _logger?.LogInfo("Overlay hidden");
            }
            else
            {
                _overlayForm = new OverlayForm(_glucoseService, _logger, _opacityTrackBar?.Value / 100.0 ?? 0.8);
                if (_contextMenu != null)
                {
                    _overlayForm.ContextMenuStrip = _contextMenu; // Right-click on overlay shows the same menu
                }
                _overlayForm.Show();
                if (_overlayToggleButton != null)
                    _overlayToggleButton.Text = "Hide Overlay";
                _logger?.LogInfo("Overlay shown");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Overlay toggle failed: {ex.Message}");
        }
    }

    private void MonitoringToggleButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_isMonitoring)
            {
                _refreshTimer?.Stop();
                _isMonitoring = false;
                if (_monitoringToggleButton != null)
                    _monitoringToggleButton.Text = "Start Service";
                _logger?.LogInfo("Glucose monitoring stopped");
            }
            else
            {
                if (!_glucoseService.ValidateConfiguration())
                {
                    _logger?.LogError("Please configure Nightscout URL first");
                    return;
                }

                _refreshTimer?.Start();
                _isMonitoring = true;
                if (_monitoringToggleButton != null)
                    _monitoringToggleButton.Text = "Stop Service";
                _logger?.LogInfo("Glucose monitoring started");

                // Get initial reading
                _ = RefreshGlucoseReading();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Monitoring toggle failed: {ex.Message}");
        }
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshGlucoseReading();
    }

    private async Task RefreshGlucoseReading()
    {
        try
        {
            _iterationCount++;

            // Single request: fetch recent entries and derive both latest and history
            var requestedCount = (int)(_recentFetchNumericUpDown?.Value ?? 20);
            var recent = await _glucoseService.GetRecentGlucoseAsync(requestedCount);
            _logger?.LogInfo($"Recent readings retrieved: {recent?.Count ?? 0} (requested {requestedCount})");
            if (_recentFetchStatusLabel != null)
            {
                _recentFetchStatusLabel.Text = $"Fetched: {recent?.Count ?? 0} (requested {requestedCount})";
            }
            if (recent != null && recent.Count > 0)
            {
                var latest = recent.Last();

                // Update main display
                if (_glucoseDisplayLabel != null)
                {
                    _glucoseDisplayLabel.Text = $"{latest.Value:F0} {latest.GetDirectionArrow()} {latest.Units}";
                    _glucoseDisplayLabel.ForeColor = latest.GetGlucoseColor();
                }

                // Update status
                if (_statusLabel != null)
                {
                    _statusLabel.Text = $"Monitoring: Glucose: {latest.Value:F0} {latest.GetDirectionArrow()} (iteration {_iterationCount})\r\n{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                }

                // Rebuild history from service data (last 5 changes with times)
                RebuildHistoryFromReadings(recent);
                UpdateHistoryDisplay();

                // Update overlay
                _overlayForm?.UpdateGlucose(latest);
                _overlayForm?.UpdateRecentHistory(_glucoseHistory.ToList());

                EvaluateAndAlert(latest);

                _logger?.LogInfo($"Parsed glucose: {latest.Value} {latest.Units} {latest.GetDirectionArrow()}");
                _logger?.LogInfo($"Glucose: {latest.Value:F0} {latest.GetDirectionArrow()} (iteration {_iterationCount})");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to refresh glucose: {ex.Message}");
        }
    }

    private void AddToHistory(GlucoseReading reading)
    {
        // Only add if value changed significantly or 5+ minutes passed
        if (_glucoseHistory.Count == 0 ||
            Math.Abs(_glucoseHistory.Last().Value - reading.Value) > 5 ||
            (DateTime.Now - _glucoseHistory.Last().Timestamp).TotalMinutes >= 5)
        {
            _glucoseHistory.Enqueue(reading);
            while (_glucoseHistory.Count > 5)
                _glucoseHistory.Dequeue();
        }
    }

    private void UpdateHistoryDisplay()
    {
        if (_historyListBox == null) return;

        _historyListBox.Items.Clear();
        var history = _glucoseHistory.ToArray();

        if (history.Length > 0)
        {
            // Values line
            var values = string.Join(", ", history.Select(r => r.Value.ToString("F0")));
            _historyListBox.Items.Add($"Values: {values}");

            // Changes line
            var changes = new List<string>();
            for (int i = 0; i < history.Length; i++)
            {
                if (i == 0)
                    changes.Add("+0");
                else
                {
                    var change = history[i].Value - history[i-1].Value;
                    changes.Add(change >= 0 ? $"+{change:F0}" : $"{change:F0}");
                }
            }
            _historyListBox.Items.Add($"Changes: {string.Join(", ", changes)}");

            // Times line
            var times = string.Join(", ", history.Select(r => r.Timestamp.ToString("HH:mm")));
            _historyListBox.Items.Add($"Times: {times}");
        }
        else
        {
            // Default display when no data
            _historyListBox.Items.Add("Values: 170, 170, 178, 178, 178");
            _historyListBox.Items.Add("Changes: +0, +0, +8, +0, +0");
            _historyListBox.Items.Add("Times: 20:47, 20:47, 20:52, 20:52, 20:52");
        }
    }

    // Alarm evaluation and sound playing
    private void EvaluateAndAlert(GlucoseReading latest)
    {
        try
        {
            if (!_alarmsEnabled) return; // snoozed
            if (!(_enableSoundAlarmsCheckBox?.Checked ?? true)) return; // user disabled

            var category = GetAlarmCategory(latest.Value);
            if (category == null) return;

            // Cooldown per category
            if (_lastAlarmTimes.TryGetValue(category, out var last) && DateTime.Now - last < _alarmCooldown)
            {
                return;
            }

            switch (category)
            {
                case "UrgentHigh":
                    SystemSounds.Beep.Play();
                    _logger?.LogInfo($"Alarm: Urgent high glucose ({latest.Value:F0})");
                    break;
                case "High":
                    SystemSounds.Exclamation.Play();
                    _logger?.LogInfo($"Alarm: High glucose ({latest.Value:F0})");
                    break;
                case "Low":
                    SystemSounds.Asterisk.Play();
                    _logger?.LogInfo($"Alarm: Low glucose ({latest.Value:F0})");
                    break;
                case "UrgentLow":
                    SystemSounds.Hand.Play();
                    _logger?.LogInfo($"Alarm: Urgent low glucose ({latest.Value:F0})");
                    break;
            }

            _lastAlarmTimes[category] = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Alarm evaluation failed: {ex.Message}");
        }
    }

    private string? GetAlarmCategory(double value)
    {
        var uh = (double)(_urgentHighNumeric?.Value ?? 234);
        var h = (double)(_highNumeric?.Value ?? 198);
        var l = (double)(_lowNumeric?.Value ?? 81);
        var ul = (double)(_urgentLowNumeric?.Value ?? 68);

        if (value >= uh) return "UrgentHigh";
        if (value >= h) return "High";
        if (value <= ul) return "UrgentLow";
        if (value <= l) return "Low";
        return null;
    }

    private void RebuildHistoryFromReadings(List<GlucoseReading> recent)
    {
        // Build last 5 changes from oldest to newest
        var ordered = recent.OrderBy(r => r.Timestamp).ToList();
        var changed = new List<GlucoseReading>();
        GlucoseReading? last = null;
        foreach (var r in ordered)
        {
            if (last == null || Math.Abs(r.Value - last.Value) > 0.1)
            {
                changed.Add(r);
            }
            last = r;
        }

        var lastFiveChanges = changed.Count <= 5 ? changed : changed.Skip(Math.Max(0, changed.Count - 5)).ToList();

        _glucoseHistory.Clear();
        foreach (var r in lastFiveChanges)
        {
            _glucoseHistory.Enqueue(r);
        }

        // Keep the external history service in sync for potential future uses
        _historyService.Clear();
        foreach (var r in lastFiveChanges)
        {
            _historyService.AddReading(r);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
            _logger?.LogInfo("Application minimized to system tray");
        }
        else
        {
            base.OnFormClosing(e);
        }
    }

    private void ClearLog_Click(object? sender, EventArgs e)
    {
        if (_logTextBox != null)
        {
            _logTextBox.Clear();
            _logger?.LogInfo("Activity log cleared");
        }
    }

    private void ExitApplication()
    {
        try
        {
            _logger?.LogInfo("Shutting down Glucose Monitor...");

            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();

            _overlayForm?.Close();
            _overlayForm?.Dispose();

            if (_snoozeTimer != null)
            {
                _snoozeTimer.Stop();
                _snoozeTimer.Dispose();
            }

            _notifyIcon?.Dispose();

            SaveAppState();

            Application.Exit();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error during shutdown: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private async Task SafeStartupSequence()
    {
        try
        {
            _logger?.LogInfo("Running startup initialization (stop/start service, hide/show overlay)...");

            // Hide settings window on startup so only the overlay is visible by default
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;

            // Ensure configuration is applied to service from UI fields
            _glucoseService.NightscoutUrl = _nightscoutUrlTextBox?.Text;
            _glucoseService.AccessToken = _accessTokenTextBox?.Text;

            // 1) Stop service if it appears running
            _refreshTimer?.Stop();
            _isMonitoring = false;
            if (_monitoringToggleButton != null)
                _monitoringToggleButton.Text = "Start Service";

            await Task.Delay(50); // brief pause

            // 2) Start service if configuration looks valid
            if (_glucoseService.ValidateConfiguration())
            {
                _refreshTimer?.Start();
                _isMonitoring = true;
                if (_monitoringToggleButton != null)
                    _monitoringToggleButton.Text = "Stop Service";

                // Kick off an immediate refresh
                _ = RefreshGlucoseReading();
            }
            else
            {
                _logger?.LogError("Nightscout is not configured. Please set URL/Token.");
            }

            // 3) Ensure overlay exists then perform hide/show cycle
            if (_overlayForm == null || _overlayForm.IsDisposed)
            {
                _overlayForm = new OverlayForm(_glucoseService, _logger, _opacityTrackBar?.Value / 100.0 ?? 0.8);
                if (_contextMenu != null)
                {
                    _overlayForm.ContextMenuStrip = _contextMenu;
                }
            }

            // Show → Hide → Show to ensure correct z-order/painting
            _overlayForm.Show();
            if (_overlayToggleButton != null) _overlayToggleButton.Text = "Hide Overlay";
            await Task.Delay(50);
            _overlayForm.Hide();
            await Task.Delay(50);
            _overlayForm.Show();
            if (_overlayToggleButton != null) _overlayToggleButton.Text = "Hide Overlay";

            _logger?.LogInfo("Startup initialization completed.");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Startup initialization failed: {ex.Message}");
        }
    }
}