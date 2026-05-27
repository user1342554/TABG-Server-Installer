<div align="center">

<pre>
 _____                                                               _____
( ___ )-------------------------------------------------------------( ___ )
 |   |                                                               |   |
 |   |  _____  _    ____   ____      ____                            |   |
 |   | |_   _|/ \  | __ ) / ___|    / ___|  ___ _ ____   _____ _ __  |   |
 |   |   | | / _ \ |  _ \| |  _     \___ \ / _ \ '__\ \ / / _ \ '__| |   |
 |   |   | |/ ___ \| |_) | |_| |     ___) |  __/ |   \ V /  __/ |    |   |
 |   |  _|_/_/   \_\____/ \____| _  |____/ \___|_|    \_/ \___|_|    |   |
 |   |                                                               |   |
 |   |           T A B G   S E R V E R   I N S T A L L E R           |   |
 |___|                                                               |___|
(_____)-------------------------------------------------------------(_____)
</pre>

**A one-click installer and manager for [Totally Accurate Battlegrounds](https://store.steampowered.com/app/823130/Totally_Accurate_Battlegrounds/) dedicated servers.**

[![GitHub release](https://img.shields.io/github/v/release/user1342554/TABG-Server-Installer)](https://github.com/user1342554/TABG-Server-Installer/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![CI](https://github.com/user1342554/TABG-Server-Installer/actions/workflows/ci.yml/badge.svg)](https://github.com/user1342554/TABG-Server-Installer/actions/workflows/ci.yml)

</div>

---

## About

TABG Server Installer is a launcher, installer, and plugin manager for TABG dedicated servers. It installs BepInEx, Citruslib, and the bundled server/client plugins, then exposes the important server and mod settings through the launcher instead of requiring manual file editing.

## Requirements

- Windows.
- A Steam installation of TABG.
- A Steam TABG Dedicated Server installation.

## Download

1. Go to the [latest release](https://github.com/user1342554/TABG-Server-Installer/releases/latest).
2. Download the release `.zip` for TABG Server Installer.
3. Extract the zip anywhere.
4. Run `TabgInstaller.Gui.exe`.

## Quick Start

### Server Setup

1. Run `TabgInstaller.Gui.exe`.
2. Select your TABG Dedicated Server path. The launcher will try to auto-detect the Steam path.
3. Check the server plugins you want to install.
4. Click **INSTALL**.
5. Configure settings in the **Config** tab.
6. Start the server from the installer.

### Client Setup (for players)

Client mods are installed into a separate TABG copy so players do not have to modify their main Steam install.

1. Go to the **Client** tab.
2. Select your TABG Steam folder.
3. Choose a destination folder for the modded copy.
4. Check the client mods you want, including Proximity Chat if your server uses it.
5. Click **INSTALL CLIENT MODS**.
6. Launch the modded TABG from the installer, not from Steam.

### Managing Bundled Plugins

1. Go to the **Server Mods** or **Client** tab.
2. Select the bundled DLLs you want.
3. Click **Install Selected**.
4. Enable or disable installed DLLs from the same panel.

## Features

### Core

- **One-click server setup** - installs BepInEx, Citruslib, bundled plugins, and default configs.
- **Client mod installer** - creates a separate modded TABG client with bundled client plugins.
- **Config editor** - edits game settings, match rules, ring behavior, spawn points, loadouts, admins, and mod settings.
- **Preset templates** - Battle Royale, Deathmatch, Gun Game, Scavenge, Juggernaut, and more.
- **Built-in plugin manager** - installs bundled DLLs directly from the launcher without a marketplace dependency.
- **Dashboard and console** - server health, player count, uptime, console output, and quick actions.
- **Backup system** - creates and restores server config backups.
- **Remote SSH tools** - manages remote dedicated servers where configured.
- **Auto-updater** - checks GitHub Releases for new versions.

### Config GUI

- **Server Settings** - `game_settings.txt` editor with validation.
- **Match Settings** - MatchCore-compatible `TheStarterPack.txt` settings for win rules, votes, spell drops, and timeouts.
- **Rings and Spawns** - ring sizes/speeds, lobby spawn, valid spawn points, and match spawn lists.
- **Loadouts** - loadout editor with item database support.
- **Mod Settings** - MatchCore fixes, grenade-on-death, Proximity Chat, ServerLogger, and Juggernaut settings.
- **Admins** - `PlayerPerms.json` management.
- **Presets** - apply built-in templates or save custom config sets.


## Bundled Plugins

### Server Plugins

| Plugin | DLL | Description | Default |
|--------|-----|-------------|---------|
| Citruslib | `Citruslib.dll` | Third-party TABG server modding API dependency | Yes |
| MatchCore | `TabgInstaller.MatchCore.dll` | Rings, loadouts, vote-start, drops, spell drops, timeouts, win rules, and match fixes | Yes |
| ServerLogger | `TabgInstaller.ServerLogger.dll` | Player name, PlayFab ID, and Epic ID logging with CSV and legacy log support | Yes |
| UnusedVehicles | `TabgInstaller.UnusedVehicles.dll` | Spawns and manages hidden TABG vehicles | Yes |
| BigSmoke / MGLFlashbang | `TabgInstaller.CustomGrenades.dll` | Custom grenade gameplay | Yes |
| ProximityChat | `TabgInstaller.ProximityChat.Server.dll` | Nearby voice relay over the existing game network | Yes |
| SoloTesting | `TabgInstaller.SoloTesting.dll` | Local testing helpers | No |
| HuntMode | `TabgInstaller.HuntMode.dll` | 4v1 survival mode | No |
| JuggernautMode | `JuggernautMode.Server.dll` | Boss player versus everyone | No |
| FakePlayers | `TabgInstaller.FakePlayers.dll` | Dummy players and AI test targets | No |
| AdminRadar | `TabgInstaller.AdminRadar.Server.dll` | Admin-only player telemetry server | No |

### Client Plugins

| Plugin | DLL | Description | Default |
|--------|-----|-------------|---------|
| FlyingControls | `TabgInstaller.FlyingControls.dll` | Client steering for custom flying vehicles | Yes |
| CustomGrenades | `TabgInstaller.CustomGrenades.dll` | Client visuals/effects for custom grenades | Yes |
| CoordsDisplay | `TabgInstaller.CoordsDisplay.dll` | Coordinate overlay | Yes |
| ModSettings | `TabgInstaller.ModSettings.dll` | In-game mod settings support | Yes |
| EnhancedClient | `TabgInstaller.EnhancedClient.dll` | LOD, draw distance, haze, and HUD controls | Yes |
| PopupBlocker | `TabgInstaller.PopupBlocker.dll` | Suppresses modded-client anti-cheat popups | Yes |
| ProximityChatClient | `TabgInstaller.ProximityChat.Client.dll` | Captures and plays proximity voice | Yes |
| HuntModeClient | `TabgInstaller.HuntMode.Client.dll` | Hunt Mode HUD | No |
| JuggernautClient | `JuggernautMode.Client.dll` | Boss bar, loadout picker, and scoreboard | No |
| AdminRadarClient | `TabgInstaller.AdminRadar.Client.dll` | Admin-only radar overlay | No |

## Proximity Voice Chat

Voice communication is built directly into the game's existing network connection. No additional ports or separate voice server are required.

- Voice data travels through the game's relay network.
- The server relays voice packets only to nearby players based on in-game distance.
- Configurable maximum range, defaulting to 50 m.
- HUD indicator shows who is currently talking.
- Open microphone with noise gate to suppress background noise.
- 16 kHz audio quality.

## Server Logger

ServerLogger is bundled as `TabgInstaller.ServerLogger.dll` and configured from the launcher under **Config -> Mod Settings**.

- Hooks TABG's Epic token verification callback to log new player identities.
- Uses a fallback connected-player scan so logging still works if another mod changes the callback path.
- Writes `BepInEx/server-logs/players.csv` by default.
- Optionally keeps legacy `ServerLogger.txt` in the server root for older tools.
- Exposes log path, CSV file name, legacy file name, scan interval, and output toggles in the GUI.

## Plugin Development

Bundled plugins are normal BepInEx 5.4.22 projects targeting `netstandard2.0`.

1. Add the plugin source project to the solution.
2. Copy the release DLL into `TabgInstaller.Gui/plugins` or `TabgInstaller.Gui/client-plugins`.
3. Register the plugin in `TabgInstaller.Core/PluginRegistry.cs`.
4. Add or update `registry/plugins/<PluginId>/manifest.json`.
5. Build the launcher projects and tests project before release.

## Disclaimer

This software is provided **as-is** with no warranty of any kind. Use at your own risk.

- This project is **not affiliated with, endorsed by, or associated with** [Landfall Games](https://landfall.se/) or Totally Accurate Battlegrounds in any way.
- This installer bundles BepInEx, Citruslib, and owned TABG plugins maintained in this repository.

## Credits

### Third-Party Runtime Dependencies

| Component | Author | Link |
|-----------|--------|------|
| [BepInEx](https://github.com/BepInEx/BepInEx) | BepInEx Team | Unity/Mono game plugin framework (v5.4.22) |
| [HarmonyLib](https://github.com/pardeike/Harmony) | Andreas Pardeike | Runtime method patching (bundled with BepInEx) |
| [Citruslib](https://github.com/CyrusTheLesser/Citruslib) | [**CyrusTheLesser**](https://github.com/CyrusTheLesser) | Code library for TABG-DS modding - custom chat commands, loot tables, settings, player management |

### NuGet Packages

- [Octokit](https://github.com/octokit/octokit.net) - GitHub API (auto-updater, registry fetching)
- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) by James Newton-King - JSON handling
- [Polly](https://github.com/App-vNext/Polly) - Resilience and retry policies
- [RestSharp](https://github.com/restsharp/RestSharp) - HTTP client
- [SSH.NET](https://github.com/sshnet/SSH.NET) - SSH remote server management
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM framework for WPF

> **Know someone who should be credited?** Open an issue or DM me on Discord.

## Contact

Have questions, feature requests, or found a bug?

- **Discord:** `anonymer__hase_22156`
- **GitHub Issues:** [Open an issue](https://github.com/user1342554/TABG-Server-Installer/issues)

## License

Released under the **MIT License** - see [`LICENSE`](./LICENSE).

Bundled third-party libraries retain their own respective licenses.
