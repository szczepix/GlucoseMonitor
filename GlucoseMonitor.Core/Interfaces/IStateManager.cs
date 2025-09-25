namespace GlucoseMonitor.Core.Interfaces;

public interface IStateManager
{
    bool IsServiceRunning { get; set; }
    bool IsOverlayVisible { get; set; }
    bool IsMainWindowMinimized { get; set; }
    bool AutoStartService { get; set; }
    bool AutoShowOverlay { get; set; }
    bool AutoMinimizeWindow { get; set; }
    int OverlayOpacity { get; set; }

    void SaveState();
    void SaveState(Dictionary<string, string> customState);
    void LoadState();
    Dictionary<string, string> LoadCustomState();
}