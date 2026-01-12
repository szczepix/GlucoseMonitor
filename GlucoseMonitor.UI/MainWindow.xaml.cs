using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Microsoft.UI.Dispatching;
using GlucoseMonitor.Core.Models;
using GlucoseMonitor.Core.Interfaces;
using GlucoseMonitor.UI.Services;
using Windows.Graphics;
using WinRT.Interop;
using Windows.Media.Core;
using Windows.Media.Playback;
using System.Runtime.InteropServices;
using Serilog;

namespace GlucoseMonitor.UI;

public sealed partial class MainWindow : Window
{
    // P/Invoke for system sounds
    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONHAND = 0x00000010;      // Critical/Error
    private const uint MB_ICONQUESTION = 0x00000020;
    private const uint MB_ICONEXCLAMATION = 0x00000030; // Warning
    private const uint MB_ICONASTERISK = 0x00000040;   // Information

    private readonly DispatcherTimer _refreshTimer;
    private bool _isMonitoring = true;
    private bool _isInitialized = false;
    private bool _isLoadingProfiles = false;
    private int _iterationCount = 0;
    private readonly Queue<GlucoseReading> _glucoseHistory = new();
    private readonly Dictionary<string, DateTime> _lastAlarmTimes = new();
    private readonly TimeSpan _alarmCooldown = TimeSpan.FromMinutes(5);
    private AppWindow? _appWindow;
    private List<ServerProfile> _profiles = new();
    private ReleaseInfo? _pendingUpdate;

    public MainWindow()
    {
        InitializeComponent();
        SetupWindow();
        LoadConfiguration();

        // Initialize logger
        App.Logger = new WinUILogger(this);

        _refreshTimer = new DispatcherTimer();
        _refreshTimer.Interval = TimeSpan.FromMinutes(1);
        _refreshTimer.Tick += async (s, e) => await RefreshGlucoseAsync();

        // Start monitoring automatically
        if (App.GlucoseService.ValidateConfiguration())
        {
            _refreshTimer.Start();
            _ = RefreshGlucoseAsync();
        }

        // Subscribe to update notifications
        App.UpdateService.UpdateAvailable += OnUpdateAvailable;

        // Check for updates on startup (non-blocking)
        if (App.UpdateService.Settings.ShouldCheckForUpdates)
        {
            _ = CheckForUpdatesAsync();
        }

        App.Logger?.LogInfo("Glucose Monitor started");
    }

    private void SetupWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow != null)
        {
            _appWindow.Resize(new SizeInt32(500, 750));
            Title = "Glucose Monitor - Settings";
        }
    }

    private void LoadConfiguration()
    {
        // Load profiles into selector
        LoadProfiles();

        // Load state
        var state = App.StateManager.LoadCustomState();
        if (state.TryGetValue("Opacity", out var opStr) && double.TryParse(opStr, out var op))
            OpacitySlider.Value = op * 100;
        if (state.TryGetValue("RecentFetchCount", out var rfcStr) && double.TryParse(rfcStr, out var rfc))
            FetchCountBox.Value = rfc;
        if (state.TryGetValue("EnableSound", out var esStr) && bool.TryParse(esStr, out var es))
            EnableAlarmsCheck.IsChecked = es;

        // Load overlay thresholds (using OverlayWindow's static values which are already loaded)
        UrgentLowBox.Value = OverlayWindow.UrgentLowThreshold;
        LowBox.Value = OverlayWindow.LowThreshold;
        HighBox.Value = OverlayWindow.HighThreshold;
        UrgentHighBox.Value = OverlayWindow.UrgentHighThreshold;

        _isInitialized = true;
    }

    private void LoadProfiles()
    {
        _isLoadingProfiles = true;
        try
        {
            // Force reload from disk to pick up any changes
            App.ProfileManager.ReloadProfiles();
            _profiles = App.ProfileManager.GetProfiles();
            ProfileSelector.Items.Clear();

            Log.Debug("Loading {Count} profiles into UI", _profiles.Count);
            foreach (var profile in _profiles)
            {
                Log.Debug("  Profile: {Name} -> {Url}", profile.Name, profile.Url);
                ProfileSelector.Items.Add(profile);
            }

            // Select active profile
            var activeProfile = App.ProfileManager.GetActiveProfile();
            if (activeProfile != null)
            {
                Log.Information("Active profile: {Name} ({Url})", activeProfile.Name, activeProfile.Url);
                var profileToSelect = _profiles.FirstOrDefault(p => p.Id == activeProfile.Id);
                if (profileToSelect != null)
                {
                    ProfileSelector.SelectedItem = profileToSelect;
                    LoadProfileIntoUI(profileToSelect);
                }
            }
        }
        finally
        {
            _isLoadingProfiles = false;
        }
    }

    private void LoadProfileIntoUI(ServerProfile profile)
    {
        ProfileNameBox.Text = profile.Name;
        NightscoutUrlBox.Text = profile.Url;
        AccessTokenBox.Password = profile.Token;
        IntervalBox.Value = profile.Interval;
    }

    private void ProfileSelector_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingProfiles) return;

        if (ProfileSelector.SelectedItem is ServerProfile profile)
        {
            Log.Information("=== PROFILE SWITCH ===");
            Log.Information("Switching to profile: {Name}", profile.Name);
            Log.Information("  URL: {Url}", profile.Url);
            Log.Information("  Token: {HasToken}", !string.IsNullOrEmpty(profile.Token) ? "[set]" : "[empty]");

            LoadProfileIntoUI(profile);
            App.ProfileManager.SetActiveProfile(profile.Id);
            App.ApplyProfile(profile);

            // Verify the service was updated
            Log.Information("Service updated:");
            Log.Information("  GlucoseService.NightscoutUrl = {Url}", App.GlucoseService.NightscoutUrl);

            App.Logger?.LogInfo($"Switched to profile: {profile.Name} ({profile.Url})");

            // Refresh data with new profile
            if (_isMonitoring && App.GlucoseService.ValidateConfiguration())
            {
                Log.Information("Triggering data refresh with new profile...");
                _ = RefreshGlucoseAsync();
            }
            else
            {
                Log.Warning("Not refreshing: monitoring={Monitoring}, valid={Valid}",
                    _isMonitoring, App.GlucoseService.ValidateConfiguration());
            }
        }
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        // Create a new profile with default values
        var newProfile = new ServerProfile
        {
            Id = Guid.NewGuid().ToString(),
            Name = "New Profile",
            Url = "",
            Token = "",
            Units = "mg",
            Interval = 1,
            IsDefault = false
        };

        App.ProfileManager.SaveProfile(newProfile);
        LoadProfiles();

        // Select the new profile
        var added = _profiles.FirstOrDefault(p => p.Id == newProfile.Id);
        if (added != null)
        {
            ProfileSelector.SelectedItem = added;
        }

        App.Logger?.LogInfo("Created new profile");
    }

    private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileSelector.SelectedItem is not ServerProfile profile)
        {
            return;
        }

        if (_profiles.Count <= 1)
        {
            App.Logger?.LogWarning("Cannot delete the last profile");
            return;
        }

        // Confirm deletion
        var dialog = new ContentDialog
        {
            Title = "Delete Profile",
            Content = $"Are you sure you want to delete '{profile.Name}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            App.ProfileManager.DeleteProfile(profile.Id);
            LoadProfiles();
            App.Logger?.LogInfo($"Deleted profile: {profile.Name}");
        }
    }

    private void SaveState()
    {
        var state = App.StateManager.LoadCustomState();
        state["Opacity"] = (OpacitySlider.Value / 100.0).ToString();
        state["RecentFetchCount"] = FetchCountBox.Value.ToString();
        state["EnableSound"] = (EnableAlarmsCheck.IsChecked ?? true).ToString();
        App.StateManager.SaveState(state);
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Logger?.LogInfo("Testing connection...");
            App.GlucoseService.NightscoutUrl = NightscoutUrlBox.Text;
            App.GlucoseService.AccessToken = AccessTokenBox.Password;

            if (!App.GlucoseService.ValidateConfiguration())
            {
                App.Logger?.LogError("Invalid configuration");
                return;
            }

            var count = (int)FetchCountBox.Value;
            var readings = await App.GlucoseService.GetRecentGlucoseAsync(count);
            FetchStatusText.Text = $"Fetched: {readings?.Count ?? 0} (requested {count})";

            if (readings != null && readings.Count > 0)
            {
                var latest = readings.Last();
                UpdateGlucoseDisplay(latest);
                App.OverlayWindowInstance?.UpdateGlucoseDisplay(latest);
                App.OverlayWindowInstance?.UpdateHistoryDisplay(readings);
                App.Logger?.LogInfo($"Connection OK: {latest.Value} {latest.Units}");
            }
        }
        catch (Exception ex)
        {
            App.Logger?.LogError($"Test failed: {ex.Message}");
        }
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get current profile or create new one
            ServerProfile profile;
            if (ProfileSelector.SelectedItem is ServerProfile existingProfile)
            {
                profile = existingProfile;
            }
            else
            {
                profile = new ServerProfile { Id = Guid.NewGuid().ToString() };
            }

            // Update profile with UI values
            profile.Name = string.IsNullOrWhiteSpace(ProfileNameBox.Text) ? "My Nightscout" : ProfileNameBox.Text;
            profile.Url = NightscoutUrlBox.Text;
            profile.Token = AccessTokenBox.Password;
            profile.Units = "mg";
            profile.Interval = (int)IntervalBox.Value;

            // Save profile (token will be encrypted)
            App.ProfileManager.SaveProfile(profile);

            // Apply to service
            App.ApplyProfile(profile);

            // Reload profiles to update UI
            LoadProfiles();

            SaveState();
            App.Logger?.LogInfo($"Profile saved: {profile.Name}");
        }
        catch (Exception ex)
        {
            App.Logger?.LogError($"Save failed: {ex.Message}");
        }
    }

    private void ToggleMonitoring_Click(object sender, RoutedEventArgs e)
    {
        if (_isMonitoring)
        {
            _refreshTimer.Stop();
            _isMonitoring = false;
            ToggleMonitoringBtn.Content = "Start Service";
            App.Logger?.LogInfo("Monitoring stopped");
        }
        else
        {
            if (!App.GlucoseService.ValidateConfiguration())
            {
                App.Logger?.LogError("Please configure Nightscout URL first");
                return;
            }

            _refreshTimer.Start();
            _isMonitoring = true;
            ToggleMonitoringBtn.Content = "Stop Service";
            _ = RefreshGlucoseAsync();
            App.Logger?.LogInfo("Monitoring started");
        }
    }

    private void ToggleOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (App.OverlayWindowInstance != null)
        {
            try
            {
                App.OverlayWindowInstance.Close();
                App.OverlayWindowInstance = null;
                ToggleOverlayBtn.Content = "Show Overlay";
                App.Logger?.LogInfo("Overlay hidden");
            }
            catch
            {
                App.OverlayWindowInstance = new OverlayWindow();
                App.OverlayWindowInstance.Activate();
                ToggleOverlayBtn.Content = "Hide Overlay";
            }
        }
        else
        {
            App.OverlayWindowInstance = new OverlayWindow();
            App.OverlayWindowInstance.Activate();
            ToggleOverlayBtn.Content = "Hide Overlay";
            App.Logger?.LogInfo("Overlay shown");
        }
    }

    private void OpacitySlider_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (OpacityValueText != null)
        {
            OpacityValueText.Text = $"{(int)OpacitySlider.Value}%";

            // Update overlay window opacity
            double opacity = OpacitySlider.Value / 100.0;
            App.OverlayWindowInstance?.UpdateOpacity(opacity);
        }
    }

    private void ThresholdChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        // Skip during initialization
        if (!_isInitialized) return;

        // Update OverlayWindow static thresholds
        if (!double.IsNaN(UrgentLowBox.Value))
            OverlayWindow.UrgentLowThreshold = UrgentLowBox.Value;
        if (!double.IsNaN(LowBox.Value))
            OverlayWindow.LowThreshold = LowBox.Value;
        if (!double.IsNaN(HighBox.Value))
            OverlayWindow.HighThreshold = HighBox.Value;
        if (!double.IsNaN(UrgentHighBox.Value))
            OverlayWindow.UrgentHighThreshold = UrgentHighBox.Value;

        // Save to persistent state
        OverlayWindow.SaveThresholds();
    }

    private void ResetThresholds_Click(object sender, RoutedEventArgs e)
    {
        // Reset to ADA 2024 recommended defaults
        OverlayWindow.ResetThresholdsToDefaults();

        // Update UI
        UrgentLowBox.Value = OverlayWindow.UrgentLowThreshold;
        LowBox.Value = OverlayWindow.LowThreshold;
        HighBox.Value = OverlayWindow.HighThreshold;
        UrgentHighBox.Value = OverlayWindow.UrgentHighThreshold;

        App.Logger?.LogInfo("Thresholds reset to defaults (ADA 2024)");
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        LogText.Text = "";
        App.Logger?.LogInfo("Log cleared");
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        SaveState();
        App.ExitApplication();
    }

    private async Task RefreshGlucoseAsync()
    {
        try
        {
            _iterationCount++;
            var count = (int)FetchCountBox.Value;
            Log.Debug("Fetching glucose data: iteration={Iteration}, count={Count}", _iterationCount, count);

            var readings = await App.GlucoseService.GetRecentGlucoseAsync(count);

            DispatcherQueue.TryEnqueue(() =>
            {
                FetchStatusText.Text = $"Fetched: {readings?.Count ?? 0} (requested {count})";
            });

            if (readings != null && readings.Count > 0)
            {
                var latest = readings.Last();
                Log.Information("Glucose reading: Value={Value}, Direction={Direction}, Time={Time}, Delta={Delta}",
                    latest.Value, latest.Direction, latest.Timestamp, latest.Delta);

                UpdateGlucoseDisplay(latest);
                App.OverlayWindowInstance?.UpdateGlucoseDisplay(latest);
                App.OverlayWindowInstance?.UpdateHistoryDisplay(readings);
                EvaluateAlarm(latest);

                App.Logger?.LogInfo($"Glucose: {latest.Value:F0} {latest.GetDirectionArrow()} (iter {_iterationCount})");
            }
            else
            {
                Log.Warning("No glucose readings returned from API");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh glucose data");
            App.Logger?.LogError($"Refresh failed: {ex.Message}");
        }
    }

    private void UpdateGlucoseDisplay(GlucoseReading reading)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            GlucoseDisplayText.Text = $"{reading.Value:F0} {reading.GetDirectionArrow()} {reading.Units}";
            GlucoseDisplayText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                ToWindowsColor(reading.GetGlucoseColor()));

            StatusText.Text = $"Monitoring: {reading.Value:F0} {reading.GetDirectionArrow()} (iter {_iterationCount})\n{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        });
    }

    private void EvaluateAlarm(GlucoseReading reading)
    {
        if (!(EnableAlarmsCheck.IsChecked ?? false)) return;

        var category = GetAlarmCategory(reading.Value);
        if (category == null)
        {
            Log.Debug("Glucose {Value} is in normal range", reading.Value);
            return;
        }

        if (_lastAlarmTimes.TryGetValue(category, out var last) && DateTime.Now - last < _alarmCooldown)
        {
            Log.Debug("Alarm {Category} suppressed due to cooldown (last: {LastTime})", category, last);
            return;
        }

        // Play alarm sound based on category
        Log.Warning("ALARM: {Category} - Glucose value {Value} mg/dL", category, reading.Value);
        PlayAlarmSound(category);
        App.Logger?.LogInfo($"Alarm: {category} ({reading.Value:F0})");

        _lastAlarmTimes[category] = DateTime.Now;
    }

    private void PlayAlarmSound(string category)
    {
        try
        {
            // Use system beep sounds - urgent uses critical sound, normal uses warning
            uint soundType = category switch
            {
                "UrgentHigh" or "UrgentLow" => MB_ICONHAND,      // Critical beep
                "High" or "Low" => MB_ICONEXCLAMATION,           // Warning beep
                _ => MB_ICONASTERISK                              // Info beep
            };

            // Play the beep multiple times for urgent alarms
            int repeatCount = category.StartsWith("Urgent") ? 3 : 1;
            for (int i = 0; i < repeatCount; i++)
            {
                MessageBeep(soundType);
                if (i < repeatCount - 1)
                    Thread.Sleep(300);
            }
        }
        catch (Exception ex)
        {
            App.Logger?.LogError($"Failed to play alarm: {ex.Message}");
        }
    }

    private string? GetAlarmCategory(double value)
    {
        var uh = UrgentHighBox.Value;
        var h = HighBox.Value;
        var l = LowBox.Value;
        var ul = UrgentLowBox.Value;

        if (value >= uh) return "UrgentHigh";
        if (value >= h) return "High";
        if (value <= ul) return "UrgentLow";
        if (value <= l) return "Low";
        return null;
    }

    private static Windows.UI.Color ToWindowsColor(System.Drawing.Color c)
    {
        return Windows.UI.Color.FromArgb(c.A, c.R, c.G, c.B);
    }

    public void AppendLog(string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText.Text += $"[{timestamp}] {message}\n";
        });
    }

    public void UpdateStatus(string status, bool isError)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            StatusText.Text = status;
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                isError ? Microsoft.UI.Colors.Red : Microsoft.UI.Colors.Green);
        });
    }

    #region Update Methods

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var includePreReleases = App.UpdateService.Settings.IncludePreReleases;
            var update = await App.UpdateService.CheckForUpdateAsync(includePreReleases);

            if (update != null)
            {
                ShowUpdateBanner(update);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check for updates");
        }
    }

    private void OnUpdateAvailable(object? sender, ReleaseInfo release)
    {
        DispatcherQueue.TryEnqueue(() => ShowUpdateBanner(release));
    }

    private void ShowUpdateBanner(ReleaseInfo release)
    {
        _pendingUpdate = release;
        UpdateBannerText.Text = "A new version is available!";
        UpdateVersionText.Text = $"{release.TagName} - {release.FormattedSize}";
        UpdateBanner.Visibility = Visibility.Visible;
        App.Logger?.LogInfo($"Update available: {release.TagName}");
    }

    private void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        App.ShowUpdateWindow();
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesBtn.IsEnabled = false;
        CheckUpdatesBtn.Content = "Checking...";

        try
        {
            var includePreReleases = App.UpdateService.Settings.IncludePreReleases;
            var update = await App.UpdateService.CheckForUpdateAsync(includePreReleases);

            if (update != null)
            {
                ShowUpdateBanner(update);
            }
            else
            {
                App.Logger?.LogInfo($"No updates available. Current: v{App.UpdateService.CurrentVersion}");
            }
        }
        catch (Exception ex)
        {
            App.Logger?.LogError($"Update check failed: {ex.Message}");
        }
        finally
        {
            CheckUpdatesBtn.IsEnabled = true;
            CheckUpdatesBtn.Content = "Check for Updates";
        }
    }

    private void VersionManager_Click(object sender, RoutedEventArgs e)
    {
        App.ShowUpdateWindow();
    }

    #endregion
}
