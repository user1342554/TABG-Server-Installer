#!/usr/bin/env pwsh

Write-Host "TABG Server Installer - AI Bootstrap Script" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as administrator (recommended for Ollama installation)
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Host "Warning: Not running as administrator. Ollama installation might require admin rights." -ForegroundColor Yellow
    Write-Host ""
}

# Check for API keys in environment
$hasApiKey = $false
$apiKeyTypes = @("OPENAI_KEY", "ANTHROPIC_KEY", "GOOGLE_AI_KEY", "XAI_KEY")
foreach ($keyType in $apiKeyTypes) {
    if (Test-Path env:$keyType) {
        Write-Host "Found $keyType in environment" -ForegroundColor Green
        $hasApiKey = $true
    }
}

if (-not $hasApiKey) {
    Write-Host "No API keys found in environment." -ForegroundColor Yellow
    Write-Host "The installer will help you set up Ollama for free local AI." -ForegroundColor Yellow
    Write-Host ""
    
    # Check if Ollama is already installed
    try {
        $ollamaVersion = & ollama --version 2>$null
        if ($ollamaVersion) {
            Write-Host "Ollama is already installed: $ollamaVersion" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "Ollama not found. It will be installed when you run the application." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Building and running TABG Server Installer..." -ForegroundColor Cyan

# Ensure we're in the correct directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($scriptDir) {
    Set-Location $scriptDir
}

# Check if we're in the TABG-Server-Installer- subdirectory
if (Test-Path "TABG-Server-Installer-\TabgInstaller.sln") {
    Set-Location "TABG-Server-Installer-"
}

# Restore and build
Write-Host "Restoring NuGet packages..." -ForegroundColor Gray
dotnet restore TabgInstaller.sln

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to restore packages" -ForegroundColor Red
    exit 1
}

Write-Host "Building solution..." -ForegroundColor Gray
dotnet build TabgInstaller.sln --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build solution" -ForegroundColor Red
    exit 1
}

# Copy models.json to output directory
$outputDir = "TabgInstaller.Gui\bin\Release\net8.0-windows"
if (Test-Path "models.json") {
    Copy-Item "models.json" -Destination $outputDir -Force
    Write-Host "Copied models.json to output directory" -ForegroundColor Green
}

# Copy Knowledge folder
if (Test-Path "..\Knowledge") {
    Copy-Item "..\Knowledge" -Destination $outputDir -Recurse -Force
    Write-Host "Copied Knowledge folder to output directory" -ForegroundColor Green
}

Write-Host ""
Write-Host "Starting TABG Server Installer..." -ForegroundColor Green
Write-Host ""

# Run the application
& "$outputDir\TabgInstaller.Gui.exe" 