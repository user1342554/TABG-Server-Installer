# -------------------------------------------------------------------
# Skript: upgrade-configsanitizer.ps1
# Zweck: 
#   1. altes "net472"-Projekt in ein .NET 8 SDK-Style‐Projekt umwandeln
#   2. fehlende PackageReference für Newtonsoft.Json hinzufügen
#   3. erneut restore + build durchführen
# -------------------------------------------------------------------

# 1) WICHTIG: Sicherstellen, dass eine minimale App.config existiert:
if (-Not (Test-Path .\App.config)) {
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
</configuration>
"@ | Out-File -Encoding UTF8 .\App.config
    Write-Host "→ App.config wurde angelegt."
} else {
    Write-Host "→ App.config existiert bereits, überspringe Erzeugung."
}

# 2) Original .csproj einlesen
$csprojPath = ".\ConfigSanitizer.csproj"
if (-Not (Test-Path $csprojPath)) {
    Write-Error "ConfigSanitizer.csproj wurde nicht gefunden: $csprojPath"
    exit 1
}

$xmlContent = Get-Content $csprojPath -Raw

# 3) <TargetFramework>net472</TargetFramework> → <TargetFramework>net8.0</TargetFramework>
if ($xmlContent -match "<TargetFramework>\s*net472\s*</TargetFramework>") {
    $xmlContent = $xmlContent -replace '<TargetFramework>\s*net472\s*</TargetFramework>', '<TargetFramework>net8.0</TargetFramework>'
    Write-Host "→ TargetFramework auf net8.0 umgestellt."
} else {
    Write-Host "→ WARNUNG: Kein <TargetFramework>net472</TargetFramework> gefunden. Bitte manuell prüfen."
}

# 4) <LangVersion>8.0</LangVersion> in das erste <PropertyGroup> einfügen, falls nicht schon vorhanden:
if ($xmlContent -match '<LangVersion>') {
    Write-Host "→ LangVersion ist bereits definiert, überspringe."
} else {
    # Wir fügen direkt nach dem ersten <PropertyGroup> einen LangVersion-Eintrag ein.
    $xmlContent = $xmlContent -replace '(?s)(<PropertyGroup>)(.*?)', '$1`n    <LangVersion>8.0</LangVersion>$2'
    Write-Host "→ <LangVersion>8.0</LangVersion> in PropertyGroup eingefügt."
}

# 5) PackageReference for Newtonsoft.Json ergänzen, falls noch keine Referenz existiert
if ($xmlContent -match 'Newtonsoft\.Json') {
    Write-Host "→ Newtonsoft.Json findet sich bereits im .csproj, überspringe PackageReference."
} else {
    # Wir hängen am Ende vor </Project> ein neues ItemGroup mit PackageReference an:
    $packageBlock = @"
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
"@

    # Ersetzen von </Project> durch obigen Block plus </Project>
    $xmlContent = $xmlContent -replace '</Project>', $packageBlock
    Write-Host "→ PackageReference für Newtonsoft.Json v13.0.3 angefügt."
}

# 6) Überprüfen, ob wir jetzt eine SDK-Style-Projektzeile haben: <Project Sdk="Microsoft.NET.Sdk">
if (-Not ($xmlContent -match '<Project\s+Sdk="Microsoft\.NET\.Sdk"')) {
    # Alte “toolsVersion”‐Zeile oder <Project> ohne Sdk="…" → Wir müssen auf SDK-Style umstellen
    Write-Host "→ Alte non-SDK-Style-Projektdatei erkannt. Führe kompletten Tausch zu SDK-Style durch …"

    # a) Extrahiere den <PropertyGroup>-Inhalt (mit Description, OutputType o. ä.) 
    #    und packe ihn in eine neue SDK-Style-Vorlage.
    #    Wir bauen ein minimales SDK-Style-Gerüst mit dem existierenden PropertyGroup-Teil:
    $propertyGroupMatch = [Regex]::Match($xmlContent, '(?s)<PropertyGroup>(.*?)</PropertyGroup>')
    if ($propertyGroupMatch.Success) {
        $oldPG = $propertyGroupMatch.Groups[1].Value.Trim()
    } else {
        $oldPG = ""
        Write-Host "   WARNUNG: Kein <PropertyGroup>-Block gefunden. Das neue SDK-Style könnte unvollständig sein."
    }

    # b) Erzeuge eine neue SDK-Style .csproj‐Grundstruktur:
    $newSdkProj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
$oldPG
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>8.0</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
"@

    # c) Überschreibe die alte .csproj mit der neuen SDK-Style-Version:
    $newSdkProj | Set-Content $csprojPath -Encoding UTF8
    Write-Host "→ .csproj wurde komplett durch eine SDK-Style-Datei ersetzt."
} else {
    # Falls schon SDK-Style war, speichern wir nur die aktualisierten Zeilen
    $xmlContent | Set-Content $csprojPath -Encoding UTF8
    Write-Host "→ .csproj im SDK-Style aktualisiert und gespeichert."
}

# 7) Jetzt Paketquellen erneut prüfen und Build durchführen
Write-Host "`n--- Starte dotnet restore + build für ConfigSanitizer … ---`n"
dotnet restore

$buildResult = dotnet build .\ConfigSanitizer.csproj
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Build von ConfigSanitizer schlug fehl. Siehe obige Fehlermeldungen."
    exit 1
} else {
    Write-Host "✅ ConfigSanitizer wurde erfolgreich kompiliert."
}

# 8) Zurück in den Hauptordner und gesamte Solution bauen
cd .. 
Write-Host "`n--- Starte dotnet build für TabgInstaller.sln … ---`n"
dotnet build .\TabgInstaller.sln

if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Build der gesamten Solution schlug fehl. Bitte meld dich mit den Fehlermeldungen!"
    exit 1
} else {
    Write-Host "✅ Die komplette TabgInstaller.Solution wurde erfolgreich kompiliert."
}

# Ende des Skripts
