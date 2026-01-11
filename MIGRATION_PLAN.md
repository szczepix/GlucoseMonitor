# FloatingGlucose Migration Plan: WinForms to WinUI 3

## Current Status

- **Framework**: .NET 9
- **Target UI**: WinUI 3 (Windows App SDK 1.8)
- **Target Platform**: Windows 11 (22H2+)
- **Build Status**: PASSING

---

## Package Versions (Latest Stable)

| Package | Version |
|---------|---------|
| Microsoft.WindowsAppSDK | 1.8.251106002 |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.7175 |
| Target Framework | net9.0-windows10.0.26100.0 |
| Min Platform Version | 10.0.22621.0 (Windows 11 22H2) |

---

## Completed Tasks

1. **Updated all projects to .NET 9**
2. **Created WinUI 3 project structure**:
   - `GlucoseMonitor.UI.csproj` - Updated for WinUI 3 with Windows App SDK 1.8
   - `app.manifest` - DPI awareness settings
   - `App.xaml` / `App.xaml.cs` - WinUI 3 application entry point with DI setup
   - `MainWindow.xaml` / `MainWindow.xaml.cs` - Settings window with full configuration UI
   - `OverlayWindow.xaml` / `OverlayWindow.xaml.cs` - Floating glucose display (always-on-top, draggable)
   - `Services/WinUILogger.cs` - ILogger implementation for WinUI 3

3. **Removed legacy WinForms files**:
   - `MainForm.cs` (deleted)
   - `OverlayForm.cs` (deleted)
   - `Program.cs` (deleted)
   - `Services/UILogger.cs` (deleted)

4. **Updated to latest stable packages** (Windows App SDK 1.8, Build Tools 26100.7175)

5. **Build verified** - Project compiles successfully

6. **Application runtime verified** - App launches successfully with both windows

7. **GitHub Actions workflows created**:
   - `.github/workflows/build.yml` - CI for PRs and pushes
   - `.github/workflows/release.yml` - Automated releases on tags

---

## Pending Tasks

### Task 1: Test Nightscout Integration

- [ ] Configure Nightscout URL and token
- [ ] Test connection button works
- [ ] Glucose data fetches successfully
- [ ] History table displays readings
- [ ] Delta and time ago display correctly

### Task 2: Test Alarms

- [ ] Alarm thresholds can be configured
- [ ] Sound alarms trigger correctly (note: currently logs only, no sound implementation)

### Task 3: Known Issues to Address

1. **Window Opacity**: WinUI 3 doesn't have direct window opacity - needs composition API
2. **Sound Alarms**: MediaPlayer implementation needs to be added
3. **System Tray**: Consider adding system tray icon for minimizing

### Task 4: Update Installer (WiX)

After WinUI 3 migration is validated:
- Update `GlucoseMonitor.Installer` project for new output path
- Test MSI installer creation
- Verify installation and shortcuts work

---

## File Structure After Migration

```
GlucoseMonitor.UI/
├── App.xaml                 # Application resources (XamlControlsResources)
├── App.xaml.cs              # App entry point, DI setup, static service references
├── MainWindow.xaml          # Settings UI (XAML)
├── MainWindow.xaml.cs       # Settings logic, monitoring control, alarms
├── OverlayWindow.xaml       # Floating overlay UI (XAML)
├── OverlayWindow.xaml.cs    # Overlay logic, dragging, position save/load
├── app.manifest             # DPI/compatibility settings
├── GlucoseMonitor.UI.csproj # WinUI 3 project file
└── Services/
    └── WinUILogger.cs       # ILogger implementation for WinUI
```

---

## Build Commands

```bash
# Restore packages
dotnet restore

# Build Debug
dotnet build GlucoseMonitor.UI -c Debug

# Build Release
dotnet build GlucoseMonitor.UI -c Release

# Run application
dotnet run --project GlucoseMonitor.UI

# Publish self-contained
dotnet publish GlucoseMonitor.UI -c Release -r win-x64 --self-contained true -o ./publish
```

---

## Rollback Plan

If WinUI 3 migration fails, revert to WinForms:
```bash
git checkout HEAD -- GlucoseMonitor.UI/
```

Or switch to WPF (easier migration path):
- WPF builds with just `dotnet build`
- XAML syntax is nearly identical to WinUI 3
- Excellent transparent window support
- No Visual Studio components required

---

## Notes

- WinUI 3 1.8 includes new AI APIs for Copilot+ PCs (optional to use)
- Windows App SDK is now a metapackage - can use individual component packages if needed
- For self-contained installs, the publish folder contains all required runtime files
