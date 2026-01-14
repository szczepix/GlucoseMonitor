<#
.SYNOPSIS
  Cleans the solution, publishes the UI as self-contained single-file (win-x64),
  and builds the WiX v6 MSI installer harvesting from the publish output.

.DESCRIPTION
  This script automates the steps described in BUILD-INSTALLER-COMMANDS.md.
  It ensures any running instance of GlucoseMonitor.UI is closed to avoid
  file lock issues, cleans/restores, publishes the UI, builds the installer,
  and prints the path to the generated MSI. Optionally opens the MSI when done.

.PARAMETER Configuration
  Build configuration (Default: Release)

.PARAMETER Runtime
  Target runtime identifier for publish (Default: win-x64)

.PARAMETER SelfContained
  Publish as self-contained (Default: $true). Creates a single executable with all
  dependencies bundled. When $false, creates a framework-dependent build requiring
  .NET 9 runtime to be installed.

.PARAMETER OpenMsi
  Open the resulting MSI after build (Default: $false)

.EXAMPLE
  ./buildInstaller.ps1

.EXAMPLE
  ./buildInstaller.ps1 -Configuration Release -Runtime win-x64 -SelfContained:$true -OpenMsi

.NOTES
  - Requires .NET SDK 9.x and WiX v6 SDK-style project in GlucoseMonitor.Installer.
  - Run PowerShell as Administrator if corporate policy requires elevation to install MSI.
#>
param(
  [string]$Configuration = "Release",
  [string]$Runtime = "win-x64",
  [bool]$SelfContained = $true,
  [bool]$OpenMsi = $false
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Info($msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host "[WARN] $msg" -ForegroundColor Yellow }
function Write-Err ($msg) { Write-Host "[ERROR] $msg" -ForegroundColor Red }

# Resolve important paths relative to this script
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionRoot = Resolve-Path (Join-Path $scriptDir "..")

$uiProject = Join-Path $solutionRoot "GlucoseMonitor.UI"
$installerProj = Join-Path $solutionRoot "GlucoseMonitor.Installer\GlucoseMonitor.Installer.wixproj"

# Publish directory depends on self-contained vs framework-dependent
if ($SelfContained) {
  $publishDir = Join-Path $uiProject "bin\$Configuration\net9.0-windows10.0.26100.0\$Runtime\publish"
} else {
  # framework-dependent: harvest from build output
  $publishDir = Join-Path $uiProject "bin\$Configuration\net9.0-windows10.0.26100.0"
}

Write-Info "Solution root: $solutionRoot"
Write-Info "UI project: $uiProject"
Write-Info "Installer project: $installerProj"

# Step 1: Ensure no running instance locks output (best-effort)
Write-Info "Closing any running GlucoseMonitor.UI.exe (best-effort)"
try { & taskkill /im GlucoseMonitor.UI.exe /f } catch { Write-Warn "No running instance found or could not terminate (continuing)." }
Start-Sleep -Milliseconds 300

# Step 2: Clean & restore
Write-Info "dotnet clean"
& dotnet clean $solutionRoot
Write-Info "dotnet restore"
& dotnet restore $solutionRoot

# Step 3: Build/Publish UI
if ($SelfContained) {
  Write-Info "Publishing UI as self-contained ($Configuration, $Runtime)"
  Write-Info "This creates a single executable with all .NET and Windows App SDK dependencies"
  & dotnet publish $uiProject -c $Configuration -r $Runtime --self-contained true -o $publishDir
  
  if ($LASTEXITCODE -ne 0) {
    Write-Err "dotnet publish failed with exit code $LASTEXITCODE"
    exit 1
  }
} else {
  Write-Info "Building UI as framework-dependent ($Configuration)"
  & dotnet build (Join-Path $uiProject "GlucoseMonitor.UI.csproj") -c $Configuration
  
  if ($LASTEXITCODE -ne 0) {
    Write-Err "dotnet build failed with exit code $LASTEXITCODE"
    exit 1
  }
}

if (-not (Test-Path $publishDir)) {
  Write-Err "Publish/Build output not found: $publishDir"
  Write-Err "Expected location: $publishDir"
  exit 1
}

# Verify executable exists
$exePath = Join-Path $publishDir "GlucoseMonitor.UI.exe"
if (-not (Test-Path $exePath)) {
  Write-Err "Executable not found: $exePath"
  Write-Err "Available files in publish directory:"
  Get-ChildItem $publishDir | ForEach-Object { Write-Host "  $_" }
  exit 1
}

Write-Info "Harvest directory: $publishDir"
Write-Info "Executable found: $(Get-Item $exePath | Select-Object -ExpandProperty Length) bytes"

# Step 4: Build the installer (WiX v6 SDK-style project)
Write-Info "Building MSI via WiX v6 SDK project"
& dotnet build $installerProj -c $Configuration -p:HarvestDirectory="$publishDir"

# Step 5: Locate generated MSI
$candidateDirs = @(
  (Join-Path $solutionRoot "GlucoseMonitor.Installer\bin\$Configuration"),
  (Join-Path $solutionRoot "GlucoseMonitor.Installer\bin\x64\$Configuration")
)
$existingDirs = @($candidateDirs | Where-Object { Test-Path $_ })
if (-not $existingDirs -or @($existingDirs).Count -eq 0) {
  Write-Err "MSI output directory not found in any of: $($candidateDirs -join ', ')"
  exit 1
}
# Find the newest MSI across all existing candidate directories
$msi = $existingDirs |
  ForEach-Object { Get-ChildItem -Path $_ -Filter *.msi -ErrorAction SilentlyContinue } |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
if (-not $msi) {
  Write-Err "MSI not found in: $($existingDirs -join ', ')"
  exit 1
}

Write-Host ""; Write-Host ("="*70)
Write-Host "MSI successfully built:" -ForegroundColor Green
Write-Host $msi.FullName -ForegroundColor Green
Write-Host ("="*70); Write-Host ""

if ($OpenMsi) {
  Write-Info "Opening MSI: $($msi.Name)"
  Start-Process $msi.FullName
}

exit 0
