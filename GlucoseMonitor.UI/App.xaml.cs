using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GlucoseMonitor.Core.Interfaces;
using GlucoseMonitor.Infrastructure.Services;
using GlucoseMonitor.Infrastructure.DependencyInjection;
using GlucoseMonitor.UI.Services;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace GlucoseMonitor.UI;

public partial class App : Application
{
    public static ServiceContainer Services { get; private set; } = null!;
    public static IGlucoseDataService GlucoseService { get; private set; } = null!;
    public static IConfigurationService ConfigService { get; private set; } = null!;
    public static IStateManager StateManager { get; private set; } = null!;
    public static IGlucoseHistoryService HistoryService { get; private set; } = null!;
    public static ILogger? Logger { get; set; }

    public static MainWindow? MainWindowInstance { get; set; }
    public static OverlayWindow? OverlayWindowInstance { get; set; }

    private TaskbarIcon? _trayIcon;

    public App()
    {
        InitializeComponent();
        InitializeServices();
    }

    private void InitializeServices()
    {
        Services = new ServiceContainer();

        ConfigService = new ConfigurationService();
        Services.RegisterSingleton<IConfigurationService>(ConfigService);

        StateManager = new StateManagerService();
        Services.RegisterSingleton<IStateManager>(StateManager);

        HistoryService = new GlucoseHistoryService();
        Services.RegisterSingleton<IGlucoseHistoryService>(HistoryService);

        GlucoseService = new NightscoutService();
        Services.RegisterSingleton<IGlucoseDataService>(GlucoseService);

        // Load configuration
        var (url, token, units, interval) = ConfigService.LoadConfiguration();
        GlucoseService.NightscoutUrl = url;
        GlucoseService.AccessToken = token;
        GlucoseService.Units = units;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Create and show overlay window (main display)
        OverlayWindowInstance = new OverlayWindow();
        OverlayWindowInstance.Activate();

        // Create main settings window (hidden by default)
        MainWindowInstance = new MainWindow();
        // MainWindow starts hidden - user can open via tray or overlay context menu

        // Initialize system tray icon
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        // Create context menu
        var contextMenu = new MenuFlyout();

        var showOverlayItem = new MenuFlyoutItem { Text = "Show Overlay" };
        showOverlayItem.Click += (s, e) => ShowOverlay();

        var settingsItem = new MenuFlyoutItem { Text = "Settings" };
        settingsItem.Click += (s, e) => ShowMainWindow();

        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (s, e) => ExitApplication();

        contextMenu.Items.Add(showOverlayItem);
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new MenuFlyoutSeparator());
        contextMenu.Items.Add(exitItem);

        // Create tray icon with blood drop icon
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Glucose Monitor",
            ContextMenuMode = ContextMenuMode.SecondWindow
        };

        // Load blood drop icon from ICO file
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "blood_drop.ico");
        if (File.Exists(iconPath))
        {
            var iconUri = new Uri(iconPath);
            _trayIcon.IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(iconUri);
        }

        _trayIcon.ContextFlyout = contextMenu;

        // Handle left click to show overlay
        _trayIcon.LeftClickCommand = new RelayCommand(() => ShowOverlay());

        // Handle double click to show settings
        _trayIcon.DoubleClickCommand = new RelayCommand(() => ShowMainWindow());

        _trayIcon.ForceCreate();
    }

    public static void ShowMainWindow()
    {
        if (MainWindowInstance != null)
        {
            MainWindowInstance.Activate();
        }
    }

    public static void ShowOverlay()
    {
        if (OverlayWindowInstance != null)
        {
            OverlayWindowInstance.Activate();
        }
    }

    public static void ExitApplication()
    {
        // Dispose tray icon
        if (Current is App app && app._trayIcon != null)
        {
            app._trayIcon.Dispose();
        }

        OverlayWindowInstance?.Close();
        MainWindowInstance?.Close();
        Environment.Exit(0);
    }

    // Simple relay command for tray icon
    private class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute) => _execute = execute;

#pragma warning disable CS0067 // Event is required by ICommand interface
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}
