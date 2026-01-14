# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A .NET 9 application for monitoring blood glucose levels using the Nightscout API. The solution uses SOLID principles with clean architecture:

- **GlucoseMonitor.Core**: Domain models and interfaces (IGlucoseDataService, IConfigurationService, IStateManager, ILogger, IGlucoseHistoryService)
- **GlucoseMonitor.Infrastructure**: Service implementations (NightscoutService, ConfigurationService, StateManagerService, GlucoseHistoryService) and custom DI container
- **GlucoseMonitor.UI**: WinUI 3 application with MainWindow (settings) and OverlayWindow (floating display)
- **GlucoseMonitor.Tests**: xUnit tests
- **GlucoseMonitor.Installer**: WiX v6 MSI installer

## Features

- **Floating Overlay**: Always-on-top, draggable glucose display with history table
- **Glucose Alerts**: Flashing visual alerts for out-of-range values (ADA 2024 guidelines)
  - Red flash: Urgent low (<54) or urgent high (>250 mg/dL)
  - Orange flash: Low (<70) or high (>180 mg/dL)
  - User-adjustable thresholds with reset to defaults
- **Sound Alarms**: System beeps for glucose alerts with cooldown
- **System Tray**: Background operation with tray icon and context menu
- **Window Opacity**: Adjustable overlay transparency (20-100%)

## Technology

- **UI Framework**: WinUI 3 (Windows App SDK 1.8)
- **Target Framework**: `net9.0-windows10.0.26100.0` (Windows 11 24H2)
- **Minimum Platform**: Windows 11 22H2 (10.0.22621.0)

## Development Commands

```bash
# Build entire solution
dotnet build

# Build UI project specifically (requires VS components for WinUI 3)
dotnet build GlucoseMonitor.UI -c Debug

# Run the application
dotnet run --project GlucoseMonitor.UI

# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Build Release
dotnet build -c Release
```

### Installer Commands

```bash
# Publish self-contained (bundles all dependencies)
dotnet publish GlucoseMonitor.UI -c Release -r win-x64 --self-contained true -o GlucoseMonitor.UI/bin/Release/net9.0-windows10.0.26100.0/win-x64/publish

# Build MSI installer (after publishing)
dotnet build GlucoseMonitor.Installer\GlucoseMonitor.Installer.wixproj -c Release -p:HarvestDirectory="GlucoseMonitor.UI\\bin\\Release\\net9.0-windows10.0.26100.0\\win-x64\\publish"
```

## Architecture

### Dependency Injection
Custom `ServiceContainer` in `GlucoseMonitor.Infrastructure/DependencyInjection/ServiceContainer.cs` provides singleton registration and resolution via interfaces.

### Data Flow
1. **NightscoutService** fetches glucose data from Nightscout API
2. **GlucoseHistoryService** maintains last 5 readings with trend analysis
3. **ConfigurationService** persists settings to `%APPDATA%\GlucoseMonitor\config.txt`
4. **StateManagerService** saves app state to `%APPDATA%\GlucoseMonitor\app_state.txt`

### Key UI Components
- **MainWindow**: Settings window with configuration UI
- **OverlayWindow**: Transparent floating overlay displaying glucose readings (always-on-top, draggable)

## Configuration Storage

All user data in `%APPDATA%\GlucoseMonitor\`:
- `config.txt` - User settings
- `app_state.txt` - Application state
- `overlay_position.txt` - Overlay window position

## Platform Requirements

- **OS**: Windows 11 22H2+ (build 22621)
- **Framework**: Self-contained deployment (no .NET installation required)
- **Build**: Requires Visual Studio components for WinUI 3 (Windows App SDK 1.8)
