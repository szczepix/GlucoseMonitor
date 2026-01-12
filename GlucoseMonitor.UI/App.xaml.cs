using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GlucoseMonitor.Core.Interfaces;
using GlucoseMonitor.Core.Models;
using GlucoseMonitor.Infrastructure.Services;
using GlucoseMonitor.Infrastructure.DependencyInjection;
using GlucoseMonitor.UI.Services;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Serilog;
using Serilog.Events;

namespace GlucoseMonitor.UI;

public partial class App : Application
{
    public static ServiceContainer Services { get; private set; } = null!;
    public static IGlucoseDataService GlucoseService { get; private set; } = null!;
    public static IConfigurationService ConfigService { get; private set; } = null!;
    public static IStateManager StateManager { get; private set; } = null!;
    public static IGlucoseHistoryService HistoryService { get; private set; } = null!;
    public static IProfileManager ProfileManager { get; private set; } = null!;
    public static ISecureStorageService SecureStorage { get; private set; } = null!;
    public static GlucoseMonitor.Core.Interfaces.ILogger? Logger { get; set; }

    public static MainWindow? MainWindowInstance { get; set; }
    public static OverlayWindow? OverlayWindowInstance { get; set; }

    private TaskbarIcon? _trayIcon;

    public App()
    {
        InitializeComponent();
        InitializeSerilog();
        InitializeServices();
    }

    private void InitializeSerilog()
    {
        // Create logs folder in AppData
        var logFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GlucoseMonitor",
            "Logs");
        Directory.CreateDirectory(logFolder);

        // Configure Serilog with rolling file logs
        var logPath = Path.Combine(logFolder, "GlucoseMonitor-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .Enrich.FromLogContext()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                shared: true)
            .CreateLogger();

        Log.Information("=== Glucose Monitor Starting ===");
        Log.Information("Application version: {Version}", GetType().Assembly.GetName().Version);
        Log.Information("Log folder: {LogFolder}", logFolder);

        // Handle unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
            Log.CloseAndFlush();
        };
    }

    private void InitializeServices()
    {
        Log.Debug("Initializing services...");
        Services = new ServiceContainer();

        // Create early logger for services initialized before MainWindow
        var earlyLogger = new SerilogAdapter();

        ConfigService = new ConfigurationService();
        Services.RegisterSingleton<IConfigurationService>(ConfigService);

        StateManager = new StateManagerService();
        Services.RegisterSingleton<IStateManager>(StateManager);

        HistoryService = new GlucoseHistoryService();
        Services.RegisterSingleton<IGlucoseHistoryService>(HistoryService);

        // Initialize secure storage and profile manager
        SecureStorage = new SecureStorageService(earlyLogger);
        Services.RegisterSingleton<ISecureStorageService>(SecureStorage);

        ProfileManager = new ProfileManagerService(SecureStorage, earlyLogger);
        Services.RegisterSingleton<IProfileManager>(ProfileManager);

        GlucoseService = new NightscoutService();
        Services.RegisterSingleton<IGlucoseDataService>(GlucoseService);

        // Migrate legacy config or load active profile
        InitializeProfile();
    }

    private void InitializeProfile()
    {
        // Check if we need to migrate from legacy config
        if (!ProfileManager.HasProfiles)
        {
            Log.Information("No profiles found, checking for legacy configuration...");
            var (url, token, units, interval) = ConfigService.LoadConfiguration();

            if (!string.IsNullOrWhiteSpace(url))
            {
                Log.Information("Migrating legacy configuration to profile system");
                ProfileManager.MigrateFromLegacyConfig(url, token, units, interval);
            }
        }

        // Load active profile into GlucoseService
        var activeProfile = ProfileManager.GetActiveProfile();
        if (activeProfile != null)
        {
            Log.Information("Loading profile: {ProfileName}", activeProfile.Name);
            ApplyProfile(activeProfile);
        }
        else
        {
            Log.Warning("No active profile found");
        }
    }

    /// <summary>
    /// Applies a server profile to the glucose service.
    /// </summary>
    public static void ApplyProfile(ServerProfile profile)
    {
        GlucoseService.NightscoutUrl = profile.Url;
        GlucoseService.AccessToken = profile.Token;
        GlucoseService.Units = profile.Units;
        Log.Information("Applied profile: {ProfileName} ({Url})", profile.Name, profile.Url);
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
        try
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
                _trayIcon.Icon = new System.Drawing.Icon(iconPath);
            }

            _trayIcon.ContextFlyout = contextMenu;

            // Handle left click to show overlay
            _trayIcon.LeftClickCommand = new RelayCommand(() => ShowOverlay());

            // Handle double click to show settings
            _trayIcon.DoubleClickCommand = new RelayCommand(() => ShowMainWindow());

            _trayIcon.ForceCreate();
        }
        catch (Exception ex)
        {
            Logger?.LogError($"Failed to create tray icon: {ex.Message}");
        }
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
        Log.Information("=== Glucose Monitor Shutting Down ===");

        // Dispose tray icon
        if (Current is App app && app._trayIcon != null)
        {
            app._trayIcon.Dispose();
        }

        OverlayWindowInstance?.Close();
        MainWindowInstance?.Close();

        // Flush and close Serilog
        Log.CloseAndFlush();
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
