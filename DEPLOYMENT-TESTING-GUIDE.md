# Deployment Testing Guide

This guide explains how to test the self-contained single-file deployment changes.

## Overview of Changes

The application has been configured for **self-contained single-file deployment**:

- **Before**: Framework-dependent deployment requiring .NET 9 runtime installation
- **After**: Self-contained deployment with all dependencies bundled in a single executable

## Key Configuration Changes

### GlucoseMonitor.UI.csproj
```xml
<SelfContained>true</SelfContained>
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
<PublishSingleFile>true</PublishSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

## Testing Steps

### 1. Build the Application (Windows required)

```powershell
# Clean previous builds
dotnet clean

# Restore dependencies
dotnet restore

# Publish as self-contained
dotnet publish GlucoseMonitor.UI -c Release -r win-x64 --self-contained true
```

Expected output location:
```
GlucoseMonitor.UI\bin\Release\net9.0-windows10.0.26100.0\win-x64\publish\
```

### 2. Verify Single-File Output

Check the publish directory:

```powershell
Get-ChildItem .\GlucoseMonitor.UI\bin\Release\net9.0-windows10.0.26100.0\win-x64\publish\
```

You should see:
- `GlucoseMonitor.UI.exe` (the main executable, ~150-200 MB)
- `Assets\blood_drop.ico` (icon file)
- Minimal additional files (WinUI 3 native libraries may be separate)

### 3. Test on Clean Windows Machine

**Critical Test**: Run on a Windows 11 machine that does **NOT** have .NET 9 runtime installed.

```powershell
# Copy the entire publish folder to test machine
# Navigate to the publish folder
cd path\to\publish

# Run the application
.\GlucoseMonitor.UI.exe
```

**Expected behavior**:
- Application launches successfully
- No error about missing .NET runtime
- All features work normally

**If it fails**:
- Check Windows Event Viewer for detailed error messages
- Verify Windows 11 22H2 or later (build 22621+)
- Check for any missing VC++ redistributables

### 4. Test the MSI Installer

Build the installer:

```powershell
# Using the PowerShell script
.\GlucoseMonitor.Installer\buildInstaller.ps1

# OR manually
dotnet build GlucoseMonitor.Installer\GlucoseMonitor.Installer.wixproj -c Release
```

Find and run the MSI:
```powershell
$msi = Get-ChildItem .\GlucoseMonitor.Installer\bin\Release\*.msi | Select-Object -First 1
Start-Process $msi.FullName
```

**Verify**:
- Installer runs successfully
- Application installs to `C:\Program Files\GlucoseMonitor`
- Start Menu shortcut works
- Desktop shortcut works (if created)
- Application runs from installed location

### 5. Verify File Size and Performance

**File Size**:
- Self-contained executable: ~150-200 MB (includes .NET runtime + Windows App SDK)
- Framework-dependent: ~2-5 MB (without runtime)

**Performance**:
- Startup time should be similar to framework-dependent
- Memory usage may be slightly higher
- No runtime installation means easier deployment

## Troubleshooting

### Issue: Application crashes on startup

**Possible causes**:
1. WinUI 3 self-contained issues (known in earlier .NET versions)
2. Missing native dependencies
3. Incompatible Windows version

**Solutions**:
- Verify Windows 11 22H2+ (build 22621 minimum)
- Check Event Viewer for detailed error info
- Try without `PublishSingleFile` first:
  ```powershell
  dotnet publish GlucoseMonitor.UI -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false
  ```

### Issue: File size too large

**Current settings enable compression**:
- `EnableCompressionInSingleFile=true` is already set
- Further reduction would require:
  - Trimming (risky for WinUI 3)
  - Native AOT (not supported for WinUI 3)

### Issue: Missing Assets/Icons

**Solution**:
Ensure `blood_drop.ico` is in the publish output. The `.csproj` includes:
```xml
<Content Include="Assets\blood_drop.ico">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```

## Comparison: Before vs After

| Aspect | Framework-Dependent (Before) | Self-Contained (After) |
|--------|------------------------------|------------------------|
| **Runtime Required** | Yes (.NET 9) | No (bundled) |
| **File Size** | ~2-5 MB | ~150-200 MB |
| **Deployment** | Requires .NET install | Standalone |
| **Updates** | .NET updates separate | Must rebuild/redeploy |
| **Distribution** | Smaller download | Larger download |
| **User Experience** | May need .NET install | Just run |

## Rollback Plan

If self-contained deployment causes issues, revert by changing `GlucoseMonitor.UI.csproj`:

```xml
<SelfContained>false</SelfContained>
<WindowsAppSDKSelfContained>false</WindowsAppSDKSelfContained>
<!-- Remove or comment out PublishSingleFile options -->
```

And in `.github/workflows/release.yml`:
```yaml
- name: Publish GlucoseMonitor.UI (framework-dependent)
  run: dotnet publish GlucoseMonitor.UI -c Release -r win-x64 --self-contained false -o ./publish/app
```

## References

- [.NET Single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/)
- [Windows App SDK deployment](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-overview)
- [WinUI 3 self-contained deployment](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/self-contained-deploy/)

## Success Criteria

✅ Application builds successfully  
✅ Single executable created (~150-200 MB)  
✅ Runs on Windows 11 without .NET runtime installed  
✅ All features work (glucose monitoring, overlay, alerts, etc.)  
✅ Installer creates and installs successfully  
✅ Installed app runs from Program Files  
✅ No crashes or runtime errors  
