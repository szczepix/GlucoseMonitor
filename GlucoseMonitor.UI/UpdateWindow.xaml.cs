using GlucoseMonitor.Core.Interfaces;
using GlucoseMonitor.Core.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Serilog;
using Windows.Graphics;

namespace GlucoseMonitor.UI;

/// <summary>
/// Minecraft-style version manager window.
/// </summary>
public sealed partial class UpdateWindow : Window
{
    private readonly IUpdateService _updateService;
    private readonly DispatcherQueue _dispatcherQueue;
    private List<ReleaseInfo> _releases = new();
    private ReleaseInfo? _selectedRelease;
    private CancellationTokenSource? _downloadCts;
    private bool _isDownloading;

    public UpdateWindow(IUpdateService updateService)
    {
        InitializeComponent();
        _updateService = updateService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Set window size
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(600, 700));

        // Initialize UI
        CurrentVersionText.Text = $"Current: v{_updateService.CurrentVersion}";
        ShowPreReleasesCheck.IsChecked = _updateService.Settings.IncludePreReleases;

        // Load releases
        _ = LoadReleasesAsync();
    }

    private async Task LoadReleasesAsync()
    {
        LoadingRing.IsActive = true;
        RefreshBtn.IsEnabled = false;
        VersionList.Items.Clear();

        try
        {
            var includePreReleases = ShowPreReleasesCheck.IsChecked ?? false;
            _releases = await _updateService.GetAvailableReleasesAsync(includePreReleases);

            foreach (var release in _releases)
            {
                VersionList.Items.Add(CreateVersionItem(release));
            }

            if (_releases.Count == 0)
            {
                ReleaseNotesText.Text = "No releases found. Check your internet connection.";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load releases");
            ReleaseNotesText.Text = $"Failed to load releases: {ex.Message}";
        }
        finally
        {
            LoadingRing.IsActive = false;
            RefreshBtn.IsEnabled = true;
        }
    }

    private Grid CreateVersionItem(ReleaseInfo release)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.Tag = release;

        // Version icon
        var icon = new FontIcon
        {
            Glyph = release.IsPreRelease ? "\uE7BA" : "\uE73E", // Beaker vs Checkmark
            Foreground = new SolidColorBrush(release.IsPreRelease
                ? Windows.UI.Color.FromArgb(255, 255, 165, 0)  // Orange
                : Windows.UI.Color.FromArgb(255, 50, 205, 50)), // LimeGreen
            FontSize = 16,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        // Version details
        var detailsPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        detailsPanel.Children.Add(new TextBlock
        {
            Text = release.TagName,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
        });
        detailsPanel.Children.Add(new TextBlock
        {
            Text = release.PublishedAt.LocalDateTime.ToString("MMM d, yyyy"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128))
        });
        Grid.SetColumn(detailsPanel, 1);
        grid.Children.Add(detailsPanel);

        // Status badge
        var badge = new Border
        {
            Background = new SolidColorBrush(release.IsPreRelease
                ? Windows.UI.Color.FromArgb(255, 80, 60, 20)   // Dark orange
                : Windows.UI.Color.FromArgb(255, 20, 60, 20)), // Dark green
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.Child = new TextBlock
        {
            Text = release.IsPreRelease ? "Pre-release" : "Stable",
            FontSize = 11,
            Foreground = new SolidColorBrush(release.IsPreRelease
                ? Windows.UI.Color.FromArgb(255, 255, 200, 100)
                : Windows.UI.Color.FromArgb(255, 100, 255, 100))
        };

        // Add "Current" badge if this is the current version
        if (!release.IsNewerThan(_updateService.CurrentVersion))
        {
            var currentVersion = release.ParseVersion();
            if (currentVersion != null && currentVersion == _updateService.CurrentVersion)
            {
                badge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 20, 40, 80));
                ((TextBlock)badge.Child).Text = "Current";
                ((TextBlock)badge.Child).Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 150, 255));
            }
        }

        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);

        return grid;
    }

    private void VersionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VersionList.SelectedItem is Grid grid && grid.Tag is ReleaseInfo release)
        {
            _selectedRelease = release;
            UpdateReleaseDetails(release);
            UpdateButtonStates();
        }
    }

    private void UpdateReleaseDetails(ReleaseInfo release)
    {
        ReleaseNotesText.Text = string.IsNullOrEmpty(release.Body)
            ? "No release notes available."
            : release.Body;
        FileSizeText.Text = $"Size: {release.FormattedSize}";
        PublishedDateText.Text = $"Published: {release.PublishedAt.LocalDateTime:MMM d, yyyy h:mm tt}";
    }

    private void UpdateButtonStates()
    {
        if (_isDownloading)
        {
            DownloadBtn.IsEnabled = false;
            SkipBtn.IsEnabled = false;
            return;
        }

        var canDownload = _selectedRelease != null &&
                          _selectedRelease.IsNewerThan(_updateService.CurrentVersion);

        DownloadBtn.IsEnabled = canDownload;
        SkipBtn.IsEnabled = canDownload;

        if (_selectedRelease != null && !canDownload)
        {
            DownloadBtn.Content = _selectedRelease.ParseVersion() == _updateService.CurrentVersion
                ? "Current Version"
                : "Older Version";
        }
        else
        {
            DownloadBtn.Content = "Download & Install";
        }
    }

    private void ShowPreReleases_Changed(object sender, RoutedEventArgs e)
    {
        _updateService.Settings.IncludePreReleases = ShowPreReleasesCheck.IsChecked ?? false;
        _updateService.SaveSettings();
        _ = LoadReleasesAsync();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _ = LoadReleasesAsync();
    }

    private async void DownloadInstall_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRelease == null) return;

        _isDownloading = true;
        _downloadCts = new CancellationTokenSource();
        UpdateButtonStates();

        ProgressPanel.Visibility = Visibility.Visible;
        CancelBtn.Content = "Cancel Download";

        var progress = new Progress<UpdateProgress>(p =>
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                DownloadProgress.Value = p.PercentComplete;
                ProgressText.Text = p.StatusMessage;
                ProgressPercent.Text = $"{p.PercentComplete}%";

                if (p.Stage == UpdateStage.Failed)
                {
                    ProgressText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 100, 100));
                    ProgressText.Text = p.ErrorMessage ?? "Update failed";
                }
            });
        });

        try
        {
            var downloadPath = await _updateService.DownloadReleaseAsync(
                _selectedRelease, progress, _downloadCts.Token);

            // Install and restart
            await _updateService.InstallUpdateAsync(downloadPath, progress);

            // Exit application (update script will restart it)
            App.ExitApplication();
        }
        catch (OperationCanceledException)
        {
            Log.Information("Download cancelled by user");
            ProgressText.Text = "Download cancelled";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update failed");
            ProgressText.Text = $"Error: {ex.Message}";
            ProgressText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 100, 100));
        }
        finally
        {
            _isDownloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
            UpdateButtonStates();
            CancelBtn.Content = "Close";
        }
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRelease == null) return;

        _updateService.Settings.SkippedVersion = _selectedRelease.TagName;
        _updateService.SaveSettings();

        Log.Information("User skipped version {Version}", _selectedRelease.TagName);

        // Show confirmation
        ReleaseNotesText.Text = $"You will not be notified about {_selectedRelease.TagName}.\n\n" +
                                "You can still install it manually from this window.";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading)
        {
            _downloadCts?.Cancel();
        }
        else
        {
            Close();
        }
    }
}
