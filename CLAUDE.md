# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET 9 WinForms application for monitoring blood glucose levels using the Nightscout API. The solution implements SOLID principles with a clean architecture pattern consisting of four main projects:

- **GlucoseMonitor.Core**: Domain models and interfaces (IGlucoseDataService, IConfigurationService, IStateManager, ILogger, IGlucoseHistoryService)
- **GlucoseMonitor.Infrastructure**: Concrete implementations of services (NightscoutService, ConfigurationService, StateManagerService, GlucoseHistoryService) and DI container
- **GlucoseMonitor.UI**: WinForms UI layer with MainForm and OverlayForm, plus UILogger service
- **GlucoseMonitor.Tests**: Unit tests
- **GlucoseMonitor.Installer**: WiX v6 MSI installer project

## Development Commands

### Building and Running
```bash
# Build entire solution
dotnet build

# Build in Release mode
dotnet build -c Release

# Run the main application
dotnet run --project GlucoseMonitor.UI

# Run tests
dotnet test

# Clean solution
dotnet clean

# Restore packages
dotnet restore
```

### Installer Commands
```bash
# Publish self-contained single-file (for installer)
dotnet publish GlucoseMonitor.UI -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true

# Build MSI installer (WiX v6 SDK-style)
dotnet build GlucoseMonitor.Installer\GlucoseMonitor.Installer.wixproj -c Release -p:HarvestDirectory="GlucoseMonitor.UI\\bin\\Release\\net9.0-windows\\win-x64\\publish"

# Alternative: Use wix CLI tool
dotnet tool install --global wix
wix build .\GlucoseMonitor.Installer\Product.wxs -o .\GlucoseMonitor.Installer\bin\Release\GlucoseMonitor.Installer.msi -dHarvestDirectory=".\\GlucoseMonitor.UI\\bin\\Release\\net9.0-windows\\win-x64\\publish"
```

## Architecture Patterns

### Dependency Injection
The solution uses a custom ServiceContainer in `GlucoseMonitor.Infrastructure/DependencyInjection/ServiceContainer.cs` for dependency management. Services are registered and resolved through interfaces.

### Data Flow
1. **NightscoutService** fetches glucose data from Nightscout API
2. **GlucoseHistoryService** maintains last 5 readings with trend analysis
3. **ConfigurationService** persists settings to `%APPDATA%\GlucoseMonitor\config.txt`
4. **StateManagerService** saves application state to `%APPDATA%\GlucoseMonitor\app_state.txt`
5. **UILogger** handles logging for the UI layer

### Key Components
- **MainForm.cs**: Primary application window with configuration UI
- **OverlayForm.cs**: Transparent floating overlay displaying glucose readings
- **GlucoseReading.cs**: Core domain model for glucose data
- **NightscoutModels.cs**: API response models for Nightscout integration

## Configuration Storage
The application stores configuration in the following locations:
- **Config**: `%APPDATA%\GlucoseMonitor\config.txt`
- **State**: `%APPDATA%\GlucoseMonitor\app_state.txt`
- **Overlay Position**: `%APPDATA%\GlucoseMonitor\overlay_position.txt`

## Quality Tools

### Code Analysis
- **Qodana**: JetBrains code analysis configured via `qodana.yaml`
- Profile: `qodana.starter`
- IDE: QDNET (for .NET analysis)

## Dependencies
- **.NET 9**: Target framework
- **Windows Forms**: UI framework
- **Newtonsoft.Json**: JSON processing
- **System.Drawing.Common**: Graphics support
- **WiX v6**: MSI installer creation

## Testing
Unit tests are located in `GlucoseMonitor.Tests/`. The test project references the Core and Infrastructure projects to test service implementations and interfaces.

## Platform Notes
- **Target OS**: Windows (WinForms application)
- **Architecture**: Can be built for Any CPU or specific (win-x64 for self-contained)
- **Framework**: Framework-dependent by default, self-contained option available for installer