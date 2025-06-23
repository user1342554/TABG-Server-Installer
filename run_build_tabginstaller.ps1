# PowerShell Script to update ConfigSanitizer to SDK-style .NET 8 and build the entire solution

param(
    [string]$TabgServerDir = ""
)

# 1. In den ConfigSanitizer-Ordner wechseln
$ErrorActionPreference = "Stop" # Stellt sicher, dass das Skript bei Fehlern anhält

$ConfigSanitizerPath = "C:\Users\diene\Downloads\TabgInstaller\ConfigSanitizer"

if (-not (Test-Path $ConfigSanitizerPath -PathType Container)) {
    Write-Error "FEHLER: ConfigSanitizer-Verzeichnis nicht gefunden unter $ConfigSanitizerPath"
    exit 1
}

Set-Location -Path $ConfigSanitizerPath
Write-Host "Aktueller Pfad: $(Get-Location)"

# 2. (Optional) Altes .csproj sichern
if (Test-Path .\ConfigSanitizer.csproj) {
    Copy-Item .\ConfigSanitizer.csproj .\ConfigSanitizer.csproj.bak -Force
    Write-Host "→ Originales ConfigSanitizer.csproj gesichert als ConfigSanitizer.csproj.bak"
} else {
    Write-Host "→ Kein ConfigSanitizer.csproj im Ordner $ConfigSanitizerPath zum Sichern gefunden."
}

# 3. Neues, minimales SDK-Style-.csproj erzeugen (überschreibt die alte Datei)
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
"@ | Out-File -Encoding UTF8 .\ConfigSanitizer.csproj -Force
Write-Host "→ Neues SDK-Style-Projekt ConfigSanitizer.csproj angelegt (TargetFramework: net8.0, Newtonsoft.Json v13.0.3)"

# 4. Sicherstellen, dass (leere) App.config existiert – falls nicht, wird sie hier angelegt
if (-Not (Test-Path .\App.config)) {
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
</configuration>
"@ | Out-File -Encoding UTF8 .\App.config -Force
    Write-Host "→ Leere App.config wurde neu angelegt."
} else {
    Write-Host "→ App.config existiert bereits, überspringe Anlage."
}

# 5. NuGet-Pakete wiederherstellen und ConfigSanitizer bauen
Write-Host "Führe dotnet restore für ConfigSanitizer aus..."
dotnet restore .
if ($LASTEXITCODE -ne 0) { Write-Error "❌ dotnet restore für ConfigSanitizer fehlgeschlagen."; exit 1 }

Write-Host "Führe dotnet build für ConfigSanitizer.csproj aus..."
dotnet build .\ConfigSanitizer.csproj

if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Build von ConfigSanitizer ist fehlgeschlagen. Bitte Fehlermeldung prüfen."
    exit 1 # Beendet das Skript hier bei Fehler
} else {
    Write-Host "✅ ConfigSanitizer wurde erfolgreich gebaut (SDK-Style / net8.0)."
}

# 6. Zurück in den Hauptordner und komplette Solution bauen
$SolutionRootPath = "C:\Users\diene\Downloads\TabgInstaller"
Write-Host "Wechsle zurück zum Hauptordner ($SolutionRootPath)..."
Set-Location -Path $SolutionRootPath
Write-Host "Aktueller Pfad: $(Get-Location)"

if (-not (Test-Path .\TabgInstaller.sln)) {
    Write-Error "FEHLER: TabgInstaller.sln nicht gefunden im Verzeichnis $SolutionRootPath"
    exit 1
}

Write-Host "Führe dotnet build für die gesamte Solution TabgInstaller.sln aus..."
dotnet build .\TabgInstaller.sln

if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Build der Solution TabgInstaller.sln fehlgeschlagen."
    exit 1 # Corrected from End-ScriptExecution
} else {
    Write-Host "✅ TabgInstaller.sln wurde vollständig und fehlerfrei kompiliert."
    
    # AntiCheatBypass wurde aus der Solution entfernt – kein Kopiervorgang mehr erforderlich.
}

# ----- NEW: Optionally copy UnityEngine.dll into AntiCheatBypass\Libs ------------
if ([string]::IsNullOrWhiteSpace($TabgServerDir)) {
    Write-Host "Kein TABG-Serverpfad übergeben. Falls AntiCheatBypass gebaut werden soll, kann UnityEngine.dll fehlen."
} elseif (-not (Test-Path "$TabgServerDir\TABG_Data\Managed\UnityEngine.dll")) {
    Write-Warning "UnityEngine.dll wurde unter $TabgServerDir nicht gefunden. Stelle sicher, dass der Pfad korrekt ist."
} else {
    $unitySrc = Join-Path $TabgServerDir "TABG_Data/Managed/UnityEngine.dll"
    $unityDstDir = "TabgInstaller.AntiCheatBypass/Libs"
    if (-not (Test-Path $unityDstDir)) { New-Item -ItemType Directory -Path $unityDstDir | Out-Null }
    $unityDst = Join-Path $unityDstDir "UnityEngine.dll"
    Copy-Item $unitySrc $unityDst -Force
    Write-Host "→ UnityEngine.dll aus Serverpfad kopiert nach $unityDst"

    # Zusätzlich UnityEngine.CoreModule.dll kopieren
    $coreSrc = Join-Path $TabgServerDir "TABG_Data/Managed/UnityEngine.CoreModule.dll"
    if (Test-Path $coreSrc) {
        $coreDst = Join-Path $unityDstDir "UnityEngine.CoreModule.dll"
        Copy-Item $coreSrc $coreDst -Force
        Write-Host "→ UnityEngine.CoreModule.dll aus Serverpfad kopiert nach $coreDst"
    } else {
        Write-Warning "UnityEngine.CoreModule.dll nicht im Serverpfad gefunden."
    }
}
# -------------------------------------------------------------------------------

# Ensure BepInEx.dll is available for plugin compilation
$libsDir = "TabgInstaller.AntiCheatBypass/Libs"
if (-not (Test-Path $libsDir)) { New-Item -ItemType Directory -Path $libsDir | Out-Null }
$bePinExDllPath = Join-Path $libsDir "BepInEx.dll"
$harmonyDllPath = Join-Path $libsDir "0Harmony.dll"

# Download & extract BepInEx package if either BepInEx.dll or 0Harmony.dll is missing
if (-not (Test-Path $bePinExDllPath) -or -not (Test-Path $harmonyDllPath)) {
    Write-Host "BepInEx/0Harmony nicht vollständig vorhanden. Lade Minimalpaket zum Extrahieren..."
    $bepZip = Join-Path $env:TEMP "BepInEx_win_x64_5.4.23.3.zip"
    if (-not (Test-Path $bepZip)) {
        Invoke-WebRequest -Uri "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.3/BepInEx_win_x64_5.4.23.3.zip" -OutFile $bepZip
    }
    $tempExtract = Join-Path $env:TEMP "bepExtract"
    if (Test-Path $tempExtract) { Remove-Item $tempExtract -Recurse -Force }
    Expand-Archive -Path $bepZip -DestinationPath $tempExtract -Force
    Copy-Item -Path (Join-Path $tempExtract "BepInEx\core\BepInEx.dll") -Destination $bePinExDllPath -Force
    $harmonyCandidate = Get-ChildItem -Path (Join-Path $tempExtract "BepInEx\core") -Filter "0Harmony*.dll" | Select-Object -First 1
    if ($null -ne $harmonyCandidate) {
        Copy-Item -Path $harmonyCandidate.FullName -Destination $harmonyDllPath -Force
        Write-Host "→ 0Harmony.dll extracted to $(Join-Path $libsDir '0Harmony.dll')"
    } else {
        Write-Warning "Keine 0Harmony*.dll in extrahiertem BepInEx-Paket gefunden."
    }
    Remove-Item $tempExtract -Recurse -Force
    Write-Host "→ BepInEx.dll extracted to $bePinExDllPath"
}

# Build AntiCheatBypass plugin
Write-Host "Baue AntiCheatBypass Plugin..."
dotnet build "TabgInstaller.AntiCheatBypass\TabgInstaller.AntiCheatBypass.csproj" -c Release
if ($LASTEXITCODE -ne 0) { Write-Warning "⚠️ Build von AntiCheatBypass fehlgeschlagen." } else {
    $builtDll = "TabgInstaller.AntiCheatBypass\bin\Release\netstandard2.0\TabgInstaller.AntiCheatBypass.dll"
    $coreDest = "TabgInstaller.Core\bin\Debug\net8.0-windows\"
    $guiDest  = "TabgInstaller.Gui\bin\Debug\net8.0-windows\"
    $coreDestRel = "TabgInstaller.Core\bin\Release\net8.0-windows\"
    $guiDestRel  = "TabgInstaller.Gui\bin\Release\net8.0-windows\"
    foreach($dest in @($coreDest,$guiDest,$coreDestRel,$guiDestRel)) {
        if (-not (Test-Path $dest)) { New-Item -ItemType Directory -Path $dest | Out-Null }
        Copy-Item $builtDll $dest -Force
    }
    Write-Host "✅ AntiCheatBypass.dll wurde in Core- und GUI-Ausgabeverzeichnisse (Debug & Release) kopiert."
}

Write-Host "PowerShell-Skript-Ausführung beendet." 