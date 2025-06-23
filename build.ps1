# TabgInstaller Build Script
# This script builds the entire TabgInstaller solution

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "===== TabgInstaller Build Script =====" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow

# Check if dotnet is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host "Found .NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "ERROR: .NET SDK not found. Please install .NET 8.0 SDK from https://dotnet.microsoft.com/download" -ForegroundColor Red
    exit 1
}

# Find solution file
$solutionFile = Get-ChildItem -Path . -Filter "*.sln" | Select-Object -First 1
if (-not $solutionFile) {
    Write-Host "ERROR: No solution file found in current directory" -ForegroundColor Red
    exit 1
}

Write-Host "Building solution: $($solutionFile.Name)" -ForegroundColor Cyan

# Clean previous build
Write-Host "`nCleaning previous build..." -ForegroundColor Yellow
dotnet clean $solutionFile.FullName -c $Configuration

# Restore packages
Write-Host "`nRestoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $solutionFile.FullName

# Build solution
Write-Host "`nBuilding solution..." -ForegroundColor Yellow
$buildResult = dotnet build $solutionFile.FullName -c $Configuration --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nBUILD FAILED!" -ForegroundColor Red
    exit 1
}

# Find output directory
$outputDir = Join-Path (Get-Location) "TabgInstaller.Gui\bin\$Configuration\net8.0-windows"
if (Test-Path $outputDir) {
    Write-Host "`nBuild completed successfully!" -ForegroundColor Green
    Write-Host "Output directory: $outputDir" -ForegroundColor Cyan
    
    # List main executables
    $exeFiles = Get-ChildItem -Path $outputDir -Filter "*.exe" | Where-Object { $_.Name -notlike "*.vshost.exe" }
    if ($exeFiles) {
        Write-Host "`nExecutables:" -ForegroundColor Yellow
        foreach ($exe in $exeFiles) {
            Write-Host "  - $($exe.Name)" -ForegroundColor White
        }
    }
} else {
    Write-Host "`nWARNING: Expected output directory not found: $outputDir" -ForegroundColor Yellow
}

Write-Host "`n===== Build Complete =====" -ForegroundColor Green 