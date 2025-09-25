using GlucoseMonitor.Core.Interfaces;

namespace GlucoseMonitor.Infrastructure.Services;

public class StateManagerService : IStateManager
{
    private readonly string _statePath;

    public bool IsServiceRunning { get; set; } = false;
    public bool IsOverlayVisible { get; set; } = false;
    public bool IsMainWindowMinimized { get; set; } = true;
    public bool AutoStartService { get; set; } = true;
    public bool AutoShowOverlay { get; set; } = true;
    public bool AutoMinimizeWindow { get; set; } = true;
    public int OverlayOpacity { get; set; } = 50;

    public StateManagerService()
    {
        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlucoseMonitor");
        Directory.CreateDirectory(configDir);
        _statePath = Path.Combine(configDir, "app_state.txt");
    }

    public void SaveState()
    {
        try
        {
            var lines = new[]
            {
                IsServiceRunning.ToString(),
                IsOverlayVisible.ToString(),
                IsMainWindowMinimized.ToString(),
                AutoStartService.ToString(),
                AutoShowOverlay.ToString(),
                AutoMinimizeWindow.ToString(),
                OverlayOpacity.ToString()
            };

            File.WriteAllLines(_statePath, lines);
        }
        catch
        {
            // Ignore save errors for KISS approach
        }
    }

    public void SaveState(Dictionary<string, string> customState)
    {
        try
        {
            var lines = new List<string>();
            foreach (var kvp in customState)
            {
                lines.Add($"{kvp.Key}={kvp.Value}");
            }
            File.WriteAllLines(_statePath, lines);
        }
        catch
        {
            // Ignore save errors for KISS approach
        }
    }

    public void LoadState()
    {
        try
        {
            if (File.Exists(_statePath))
            {
                var lines = File.ReadAllLines(_statePath);
                if (lines.Length >= 6)
                {
                    if (bool.TryParse(lines[0], out var isServiceRunning))
                        IsServiceRunning = isServiceRunning;
                    if (bool.TryParse(lines[1], out var isOverlayVisible))
                        IsOverlayVisible = isOverlayVisible;
                    if (bool.TryParse(lines[2], out var isMainWindowMinimized))
                        IsMainWindowMinimized = isMainWindowMinimized;
                    if (bool.TryParse(lines[3], out var autoStartService))
                        AutoStartService = autoStartService;
                    if (bool.TryParse(lines[4], out var autoShowOverlay))
                        AutoShowOverlay = autoShowOverlay;
                    if (bool.TryParse(lines[5], out var autoMinimizeWindow))
                        AutoMinimizeWindow = autoMinimizeWindow;

                    // Load opacity if available
                    if (lines.Length >= 7 && int.TryParse(lines[6], out var overlayOpacity))
                        OverlayOpacity = overlayOpacity;
                }
            }
        }
        catch
        {
            // Use defaults on error
        }
    }

    public Dictionary<string, string> LoadCustomState()
    {
        var state = new Dictionary<string, string>();
        try
        {
            if (File.Exists(_statePath))
            {
                var lines = File.ReadAllLines(_statePath);
                foreach (var line in lines)
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        state[parts[0]] = parts[1];
                    }
                }
            }
        }
        catch
        {
            // Return empty dictionary on error
        }
        return state;
    }
}