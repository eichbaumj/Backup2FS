# ============================================================================
#  PublishBackup2FS.ps1  -  publish-only build prep for the Backup2FS installer.
#
#  Publishes Backup2FS (win-x64, self-contained) into publish\Backup2FS\, which
#  is the folder Backup2FS.iss packages. Self-contained means the .NET runtime is
#  bundled, so target machines need no separate .NET install.
#
#  Usage:
#     powershell -ExecutionPolicy Bypass -File PublishBackup2FS.ps1
# ============================================================================

$ErrorActionPreference = 'Stop'

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Backup2FS - publish prep (for Inno Setup)"   -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# --- Paths ---
$ProjectDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project     = Join-Path $ProjectDir "Backup2FS\Backup2FS.csproj"
$PublishRoot = Join-Path $ProjectDir "publish"
$PublishOut  = Join-Path $PublishRoot "Backup2FS"

# --- Sanity checks ---
Write-Host ""
Write-Host "Checking for .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = & dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: .NET SDK is not installed or not in PATH." -ForegroundColor Red
    Write-Host "Install the .NET 7.0 SDK from https://dotnet.microsoft.com/download/dotnet/7.0" -ForegroundColor Red
    exit 1
}
Write-Host "Found .NET SDK version: $dotnetVersion" -ForegroundColor Green

if (-not (Test-Path $Project)) {
    Write-Host "ERROR: project not found: $Project" -ForegroundColor Red
    exit 1
}

# --- Clean previous publish folder ---
Write-Host ""
Write-Host "Cleaning previous publish folder..." -ForegroundColor Yellow
if (Test-Path $PublishOut) { Remove-Item -Path $PublishOut -Recurse -Force }
New-Item -ItemType Directory -Path $PublishRoot -Force | Out-Null

# --- Publish (self-contained win-x64) ---
Write-Host ""
Write-Host "Publishing Backup2FS (win-x64, self-contained) -> $PublishOut" -ForegroundColor Yellow
& dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -o $PublishOut
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Publish failed." -ForegroundColor Red
    exit 1
}

# --- Verify the app exe is present ---
if (-not (Test-Path (Join-Path $PublishOut "Backup2FS.exe"))) {
    Write-Host "ERROR: publish\Backup2FS\Backup2FS.exe is missing after publish." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "Publish complete - ready for Inno Setup"     -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Bundle: $PublishOut" -ForegroundColor Gray
Write-Host "Next:   compile Backup2FS.iss (or run BuildBackup2FSInstaller.ps1)" -ForegroundColor Gray
Write-Host ""
