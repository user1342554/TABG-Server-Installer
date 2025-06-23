# TabgInstaller Fixes und Neue Features

## WICHTIG: StarterPack wurde komplett entfernt!
Nach deiner Anfrage wurde die gesamte StarterPack-Funktionalität aus dem Installer entfernt. Der Server wird nun ohne StarterPack installiert.

## Neue Features

### 1. TABGCommunityServer Integration
- Automatischer Download und Installation von TABGCommunityServer
- Checkbox in der Hauptoberfläche zum Aktivieren
- **Neue Datei**: `TabgInstaller.Core/Services/CommunityServerService.cs`

### 2. AntiCheatBootErrorRemover Integration
- Automatische Installation des AntiCheat Removers
- Standardmäßig aktiviert (kann deaktiviert werden)
- **Neue Datei**: `TabgInstaller.Core/Services/AntiCheatRemoverService.cs`

### 3. Verbesserte Konfigurationen
- Ring Settings sind jetzt standardmäßig deaktiviert (verhindert instant death)
- Bessere Standard-Loadouts mit verschiedenen Waffentypen
- Heal on Kill aktiviert (25 HP)
- Can Go Down aktiviert (Spieler können wiederbelebt werden)

### 4. Weapon Spawn Configuration Mod (NEU!)
- Vollständige Kontrolle über Weapon Spawn Rates
- GUI-Tab für einfache Konfiguration
- Individuelle Waffen-Multiplikatoren (0.0 - 10.0)
- Kategorie-Multiplikatoren (z.B. alle SMGs, alle Snipers)
- Globaler Spawn-Multiplikator
- Voreinstellungen für gängige Szenarien:
  - Keine Blessings
  - Nur Waffen (keine Items/Consumables)
  - Melee Madness (10x Melee, 0.1x Ranged)
  - Sniper Paradise (10x Snipers, 0.5x andere)
  - Seltenheits-Balance (mehr Legendary, weniger Common)
- **Neue Dateien**: 
  - `TabgInstaller.WeaponSpawnConfig/` (BepInEx Plugin)
  - `TabgInstaller.Core/Services/WeaponSpawnConfigService.cs`
  - `TabgInstaller.Gui/Tabs/WeaponSpawnGrid.xaml`

## Build-Anweisungen

1. Öffne PowerShell im Projektverzeichnis
2. Führe aus: `.\build.ps1`
3. Die kompilierte Anwendung findest du in: `TabgInstaller.Gui\bin\Release\net8.0-windows\`

## Weitere geplante Features (noch nicht implementiert)

- Integration von ComputerysUltimateTABGServer
- TabgLootDumper Integration
- TacticalToolkit Integration
- CitrusLib erweiterte Features

## Hinweise

- StarterPackSetup.exe wird weiterhin für erweiterte Konfigurationen benötigt
- Die Quick Settings im GUI werden direkt in `TheStarterPack.txt` gespeichert
- Community Server Installation erstellt einen `CommunityServer` Ordner im Serververzeichnis 