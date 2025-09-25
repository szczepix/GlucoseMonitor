PowerShell commands to build a self-contained WiX v6 MSI for Glucose Monitor

# 1) Navigate to solution root
Set-Location C:\projects\FloatingGlucose\GlucoseMonitorSolution

# 2) Ensure no running instance is locking output (optional)
# Close from tray first if running, otherwise force-kill (ignore errors if not running)
try { taskkill /im GlucoseMonitor.UI.exe /f } catch {}

# 3) Clean & restore
 dotnet clean; dotnet restore

# 4) Publish UI as self-contained, single-file (Release, win-x64)
 dotnet publish GlucoseMonitor.UI -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true

# Publish output will be in:
# .\GlucoseMonitor.UI\bin\Release\net9.0-windows\win-x64\publish

# 5) Build the WiX v6 installer (SDK-style project), harvesting from the publish folder
 dotnet build GlucoseMonitor.Installer\GlucoseMonitor.Installer.wixproj -c Release -p:HarvestDirectory="GlucoseMonitor.UI\\bin\\Release\\net9.0-windows\\win-x64\\publish"

# 6) Find the generated MSI and install
 Get-ChildItem .\GlucoseMonitor.Installer\bin\Release\*.msi
 Start-Process .\GlucoseMonitor.Installer\bin\Release\GlucoseMonitor.Installer.msi
# If the exact name is unknown, use Start-Process on the wildcard result:
# $msi = (Get-ChildItem .\GlucoseMonitor.Installer\bin\Release\*.msi | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
# Start-Process $msi

# --- Alternative: Use the wix command-line .NET tool (per https://docs.firegiant.com/wix/using-wix/#command-line-net-tool) ---
# Install the wix tool once (global):
 dotnet tool install --global wix
# Ensure your PATH contains the dotnet tools path (usually %USERPROFILE%\.dotnet\tools)
# Build the MSI directly from Product.wxs, passing the HarvestDirectory preprocessor variable:
 wix build .\GlucoseMonitor.Installer\Product.wxs -o .\GlucoseMonitor.Installer\bin\Release\GlucoseMonitor.Installer.msi -dHarvestDirectory=".\\GlucoseMonitor.UI\\bin\\Release\\net9.0-windows\\win-x64\\publish"
# Notes for wix CLI:
# - You still need to publish/build the UI app first (step 4) so the EXE exists in the publish folder.
# - You can add -c:candle and -c:light options for advanced scenarios, but defaults usually work.

# Notes
# - To bump version, edit Version in Product.wxs and rebuild.
# - Run PowerShell as Administrator if your corporate policy requires elevation for MSI installs.
