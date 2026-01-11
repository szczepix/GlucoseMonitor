using Microsoft.UI.Xaml;
using GlucoseMonitor.Core.Interfaces;
using GlucoseMonitor.Infrastructure.Services;
using GlucoseMonitor.Infrastructure.DependencyInjection;
using GlucoseMonitor.UI.Services;

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
        OverlayWindowInstance?.Close();
        MainWindowInstance?.Close();
        Environment.Exit(0);
    }
}
