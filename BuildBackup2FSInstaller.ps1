# ============================================================================
#  BuildBackup2FSInstaller.ps1  -  one-shot: publish + compile the installer.
#
#  Runs PublishBackup2FS.ps1, then compiles Backup2FS.iss with Inno Setup's
#  ISCC.exe. The finished installer lands in Installer\Backup2FS_Setup_3.0.0_x64.exe,
#  ready to upload to a GitHub release.
#
#  Requires: .NET SDK, and Inno Setup 6 (https://jrsoftware.org/isdl.php).
#
#  Usage:
#     powershell -ExecutionPolicy Bypass -File BuildBackup2FSInstaller.ps1
#     ...optional: -SkipPublish   (compile only, reuse an existing publish\Backup2FS)
# ============================================================================

param(
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$IssFile    = Join-Path $ProjectDir "Backup2FS.iss"

# --- 1) Publish ---
if (-not $SkipPublish) {
    & powershell -ExecutionPolicy Bypass -File (Join-Path $ProjectDir "PublishBackup2FS.ps1")
    if ($LASTEXITCODE -ne 0) { Write-Host "Publish step failed." -ForegroundColor Red; exit 1 }
} else {
    Write-Host "Skipping publish (-SkipPublish); using existing publish\Backup2FS." -ForegroundColor Yellow
}

# --- 2) Locate ISCC.exe (Inno Setup compiler) ---
$IsccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$Iscc = $IsccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $Iscc) {
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { $Iscc = $cmd.Source }
}
if (-not $Iscc) {
    Write-Host "ERROR: ISCC.exe (Inno Setup 6) not found." -ForegroundColor Red
    Write-Host "Install Inno Setup 6 from https://jrsoftware.org/isdl.php" -ForegroundColor Red
    exit 1
}
Write-Host "Using Inno Setup compiler: $Iscc" -ForegroundColor Green

# --- 3) Compile the installer ---
Write-Host ""
Write-Host "Compiling Backup2FS.iss..." -ForegroundColor Yellow
& $Iscc $IssFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Inno Setup compilation failed." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "Installer built successfully"                -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host "Output: $(Join-Path $ProjectDir 'Installer\Backup2FS_Setup_3.0.0_x64.exe')" -ForegroundColor Gray
Write-Host "Upload that .exe to your GitHub release." -ForegroundColor Gray
Write-Host ""
