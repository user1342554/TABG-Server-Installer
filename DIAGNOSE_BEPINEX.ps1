# TABG BepInEx Diagnostic Script
Write-Host "TABG BepInEx Diagnostic Tool" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan
Write-Host ""

# Find TABG server
$serverPath = "C:\Program Files (x86)\Steam\steamapps\common\TotallyAccurateBattlegroundsDedicatedServer"
if (-not (Test-Path $serverPath)) {
    Write-Host "[ERROR] TABG server not found at: $serverPath" -ForegroundColor Red
    exit
}

Set-Location $serverPath
Write-Host "Server location: $serverPath" -ForegroundColor Green
Write-Host ""

# Check BepInEx installation
Write-Host "Checking BepInEx Installation..." -ForegroundColor Yellow
$checks = @{
    "BepInEx folder" = Test-Path "BepInEx"
    "BepInEx\core folder" = Test-Path "BepInEx\core"
    "BepInEx.Preloader.dll" = Test-Path "BepInEx\core\BepInEx.Preloader.dll"
    "winhttp.dll" = Test-Path "winhttp.dll"
    "version.dll" = Test-Path "version.dll"
    "doorstop_config.ini" = Test-Path "doorstop_config.ini"
    "WeaponSpawnConfig plugin" = Test-Path "BepInEx\plugins\TabgInstaller.WeaponSpawnConfig.dll"
}

foreach ($check in $checks.GetEnumerator()) {
    if ($check.Value) {
        Write-Host "[OK] $($check.Key) found" -ForegroundColor Green
    } else {
        Write-Host "[MISSING] $($check.Key)" -ForegroundColor Red
    }
}

# Check file sizes
Write-Host "`nFile sizes:" -ForegroundColor Yellow
if (Test-Path "winhttp.dll") {
    $size = (Get-Item "winhttp.dll").Length
    Write-Host "winhttp.dll: $size bytes" -ForegroundColor White
}
if (Test-Path "version.dll") {
    $size = (Get-Item "version.dll").Length
    Write-Host "version.dll: $size bytes" -ForegroundColor White
}

# Check doorstop config
Write-Host "`nDoorstop Configuration:" -ForegroundColor Yellow
if (Test-Path "doorstop_config.ini") {
    Get-Content "doorstop_config.ini" | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
}

# Check Unity version
Write-Host "`nUnity Version:" -ForegroundColor Yellow
if (Test-Path "TABG.exe") {
    $version = (Get-Item "TABG.exe").VersionInfo
    Write-Host "TABG.exe version: $($version.FileVersion)" -ForegroundColor White
    Write-Host "Product version: $($version.ProductVersion)" -ForegroundColor White
}

# Security checks
Write-Host "`nSecurity Checks:" -ForegroundColor Yellow
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if ($isAdmin) {
    Write-Host "[OK] Running as Administrator" -ForegroundColor Green
} else {
    Write-Host "[WARN] NOT running as Administrator" -ForegroundColor Yellow
}

if ($serverPath.Contains("Program Files")) {
    Write-Host "[WARN] Server is in Program Files - requires admin rights for DLL injection" -ForegroundColor Yellow
}

# Check for log files
Write-Host "`nChecking for BepInEx logs:" -ForegroundColor Yellow
$logFiles = @(
    "LogOutput.log",
    "BepInEx\LogOutput.log",
    "doorstop_is_alive.txt"
)

$foundLogs = $false
foreach ($log in $logFiles) {
    if (Test-Path $log) {
        Write-Host "[FOUND] $log" -ForegroundColor Green
        $foundLogs = $true
    }
}

if (-not $foundLogs) {
    Write-Host "[WARN] No BepInEx log files found - BepInEx has never loaded" -ForegroundColor Yellow
}

# Solutions
Write-Host "`n=== SOLUTIONS ===" -ForegroundColor Cyan
Write-Host "1. Run TABG.exe as Administrator:" -ForegroundColor White
Write-Host "   - Right-click TABG.exe -> Run as administrator" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Add Windows Defender exclusion:" -ForegroundColor White
Write-Host "   - Windows Security -> Virus & threat protection" -ForegroundColor Gray
Write-Host "   - Manage settings -> Add or remove exclusions" -ForegroundColor Gray
Write-Host "   - Add folder: $serverPath" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Test with environment variables:" -ForegroundColor White
Write-Host '   $env:DOORSTOP_ENABLE = "TRUE"' -ForegroundColor Gray
Write-Host '   $env:DOORSTOP_INVOKE_DLL_PATH = "BepInEx\core\BepInEx.Preloader.dll"' -ForegroundColor Gray
Write-Host '   Start-Process "TABG.exe" -WorkingDirectory $PWD' -ForegroundColor Gray
Write-Host ""
Write-Host "4. Alternative: Copy server outside Program Files" -ForegroundColor White
Write-Host "   - Copy entire server folder to C:\TABGServer" -ForegroundColor Gray
Write-Host "   - Run from there without admin restrictions" -ForegroundColor Gray

Write-Host "`nPress any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 