# Glucose Monitor Solution

A modern .NET 9 WinForms application for monitoring blood glucose levels using Nightscout API, built with SOLID principles.

## Solution Structure

```
GlucoseMonitorSolution/
├── GlucoseMonitor.Core/                    # Domain models and interfaces
│   ├── Models/
│   │   ├── GlucoseReading.cs              # Core glucose data model
│   │   └── NightscoutModels.cs            # Nightscout API models
│   └── Interfaces/
│       ├── IGlucoseDataService.cs         # Glucose data abstraction
│       ├── IConfigurationService.cs       # Configuration management
│       ├── IStateManager.cs               # Application state
│       ├── ILogger.cs                     # Logging abstraction
│       └── IGlucoseHistoryService.cs      # History management
├── GlucoseMonitor.Infrastructure/          # Concrete implementations
│   ├── Services/
│   │   ├── NightscoutService.cs           # Nightscout API service
│   │   ├── ConfigurationService.cs       # File-based configuration
│   │   ├── StateManagerService.cs        # State persistence
│   │   └── GlucoseHistoryService.cs       # History tracking
│   └── DependencyInjection/
│       └── ServiceContainer.cs            # Simple DI container
├── GlucoseMonitor.UI/                      # User interface layer
│   ├── MainForm.cs                        # Main application window
│   ├── OverlayForm.cs                     # Floating overlay
│   └── Services/
│       └── UILogger.cs                    # UI-specific logging
└── GlucoseMonitor.Tests/                   # Unit tests
```

## SOLID Principles Implementation

### ✅ Single Responsibility Principle (SRP)
- Each service has one clear responsibility
- UI components separated from business logic
- Configuration, state, and logging are isolated

### ✅ Open/Closed Principle (OCP)
- Easy to extend with new glucose data sources
- Interface-based design allows new implementations
- Plugin architecture for different logging strategies

### ✅ Liskov Substitution Principle (LSP)
- All implementations can be substituted via interfaces
- Consistent behavior across implementations

### ✅ Interface Segregation Principle (ISP)
- Small, focused interfaces
- Clients depend only on what they use
- No fat interfaces

### ✅ Dependency Inversion Principle (DIP)
- High-level modules depend on abstractions
- Dependency injection throughout
- Testable architecture

## Key Features

- **Real-time Glucose Monitoring**: Connects to Nightscout API
- **Transparent Overlay**: Configurable opacity floating window
- **History Tracking**: Last 5 readings with trend analysis
- **KISS Architecture**: Keep It Simple, Stupid design
- **Auto-start Options**: Service, overlay, and window management
- **Multi-monitor Support**: Position saving across screen configurations
- **State Persistence**: Remembers settings between sessions

## Building and Running

```bash
# Build the solution
dotnet build

# Run the application
dotnet run --project GlucoseMonitor.UI

# Run tests
dotnet test
```

## Dependencies

- **.NET 9**: Bundled with self-contained deployment
- **Windows App SDK**: Bundled with application
- **WinUI 3**: Modern UI framework
- **H.NotifyIcon.WinUI**: System tray support
- **Serilog**: Logging framework

## Configuration

The application stores configuration in:
- **Config**: `%APPDATA%\GlucoseMonitor\config.txt`
- **State**: `%APPDATA%\GlucoseMonitor\app_state.txt`
- **Overlay Position**: `%APPDATA%\GlucoseMonitor\overlay_position.txt`

## Architecture Benefits

1. **Maintainability**: Clear separation of concerns
2. **Testability**: Interface-based design
3. **Extensibility**: Easy to add new features
4. **Scalability**: Modular architecture
5. **Robustness**: Proper error handling and fallbacks

## Installer (MSI)

A WiX v6 installer project is included to create a Windows MSI that installs Glucose Monitor into Program Files and adds Start Menu and Desktop shortcuts.

Location: `GlucoseMonitor.Installer`

Prerequisites:
- .NET SDK 9.x (for building the app)
- EITHER use the WiX v6 SDK-style project (no separate WiX install), OR use the WiX command-line .NET tool (see Option B below)
- On target machines: No runtime installation required (self-contained deployment)

Option A — SDK-style project (WixToolset.Sdk v6):
1. Close any running instance of GlucoseMonitor.UI to avoid file locks.
2. Publish the UI app in Release as self-contained:
   - `dotnet publish GlucoseMonitor.UI -c Release -r win-x64 --self-contained true -o GlucoseMonitor.UI/bin/Release/net9.0-windows10.0.26100.0/win-x64/publish`
3. Build the installer (MSI):
   - `dotnet build -c Release GlucoseMonitor.Installer/GlucoseMonitor.Installer.wixproj`
4. The MSI will be created under `GlucoseMonitor.Installer/bin/Release/`.

Option B — WiX command-line .NET tool (per https://docs.firegiant.com/wix/using-wix/#command-line-net-tool):
1. Install the tool (once): `dotnet tool install --global wix`
2. Ensure your PATH includes `%USERPROFILE%\.dotnet\tools`.
3. Publish the UI as in Option A step 2 (so the EXE exists to package).
4. Build the MSI directly from Product.wxs, passing the harvest directory variable:
   - `wix build .\GlucoseMonitor.Installer\Product.wxs -o .\GlucoseMonitor.Installer\bin\Release\GlucoseMonitor.Installer.msi -dHarvestDirectory=".\\GlucoseMonitor.UI\\bin\\Release\\net9.0-windows10.0.26100.0\\win-x64\\publish"`

Install:
- Run the generated MSI on the target machine.
- It installs to `C:\\Program Files\\GlucoseMonitor` and creates Start Menu and Desktop shortcuts named "Glucose Monitor".
- **No .NET runtime installation required** - all dependencies are bundled in the app.

Notes:
- The WiX authoring uses the v4 schema (`xmlns="http://wixtoolset.org/schemas/v4/wxs"`), which is correct for WiX v6.
- The installer harvests files from the publish directory containing the self-contained application.
- The published app includes .NET 9 runtime and Windows App SDK, making it a true standalone executable.

## Future Enhancements

- Add more glucose data sources (Dexcom, FreeStyle)
- Implement alerts and notifications
- Add data export capabilities
- Create mobile companion app
- Add cloud synchronization

## License

This project follows KISS (Keep It Simple, Stupid) principles while maintaining enterprise-grade SOLID architecture.