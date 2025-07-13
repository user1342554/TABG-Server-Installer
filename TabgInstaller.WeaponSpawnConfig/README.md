# TABG Weapon Spawn Configuration Mod

This BepInEx plugin allows you to configure the spawn rates of all weapons, items, and blessings in Totally Accurate Battlegrounds.

## Features

- **Individual Weapon Control**: Set spawn rate multipliers for every weapon in the game
- **Category Multipliers**: Apply multipliers to entire weapon categories (e.g., all SMGs, all Snipers)
- **Global Multiplier**: Control overall spawn rates with a single setting
- **Preset Configurations**: Quick presets for common scenarios:
  - Disable all blessings
  - Weapons only mode
  - Melee madness
  - Sniper paradise
  - Rarity balance (more legendary, less common)
- **GUI Integration**: Configure everything through the TabgInstaller GUI

## How It Works

The mod uses Harmony to patch the game's loot spawning system. Each weapon has three multipliers that are combined:

1. **Individual Multiplier**: The specific weapon's spawn rate (0.0 - 10.0)
2. **Category Multiplier**: Applied to all weapons in that category (0.0 - 10.0)
3. **Global Multiplier**: Applied to all spawns (0.0 - 10.0)

Final spawn rate = Individual × Category × Global

Example:
- AK47 individual rate: 2.0
- Assault Rifles category: 1.5
- Global multiplier: 0.8
- Final AK47 spawn rate: 2.0 × 1.5 × 0.8 = 2.4x normal

## Configuration

The mod creates a configuration file in `BepInEx/config/tabginstaller.weaponspawnconfig.cfg` which can be edited manually or through the GUI.

### Using the GUI

1. Open TabgInstaller
2. Go to the "Weapon Spawn Config" tab
3. Install the mod if not already installed
4. Configure spawn rates using:
   - Category Multipliers tab for broad changes
   - Individual Weapons tab for specific weapons
   - Presets tab for quick configurations
5. Click "Save Configuration" to apply changes

### Manual Configuration

Edit the `.cfg` file directly. Example:

```ini
[Global]
Global Spawn Multiplier = 1.0

[Category Multipliers]
Assault Rifles = 1.5
SMGs = 0.8
Legendary Blessings = 2.0

[Assault Rifles]
AK47 = 1.0
M16 = 0.5
```

## Weapon Categories

The mod organizes weapons into these categories:

- **Special Weapons**: Crossbows, Grappling Hook, Boss Weapons, etc.
- **Assault Rifles**: AK47, M16, AUG, Famas, etc.
- **SMGs**: MP5, P90, Vector, UMP, etc.
- **Pistols**: Glock, Desert Eagle, Revolver, etc.
- **Shotguns**: AA12, Mossberg, Blunderbuss, etc.
- **Snipers**: AWP, Barrett, Kar98, etc.
- **Heavy**: Minigun, Rocket Launcher, MG-42, etc.
- **Melee**: Katana, Pan, Shield, Knife, etc.
- **Grenades**: All grenade types
- **Spells**: Fireball, Ice Bolt, Teleport, etc.
- **Blessings**: Common, Rare, Epic, Legendary
- **Attachments**: Scopes, Barrels, Underbarrel
- **Consumables**: Ammo, Healing items

## Requirements

- TABG with BepInEx installed
- TabgInstaller (for GUI configuration)

## Installation

1. Build the project or use the installer GUI
2. Copy `TabgInstaller.WeaponSpawnConfig.dll` to `BepInEx/plugins/`
3. Launch the game
4. Configure through GUI or edit the config file

## Technical Details

The mod patches loot spawning methods using Harmony. It intercepts spawn attempts and applies the configured multipliers to adjust spawn probabilities. The actual implementation depends on how TABG's loot system works internally and may need adjustments based on game updates. 