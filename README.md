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
- **Preset templates** - Battle Royale, Deathmatch, Gun Game, Scavenge, and more.
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
- **Mod Settings** - MatchCore fixes, grenade-on-death, Proximity Chat, and ServerLogger settings.
- **Admins** - `PlayerPerms.json` management.
- **Presets** - apply built-in templates or save custom config sets.


## Bundled Plugins

### Server Plugins

| Plugin | DLL | Description | Default |
|--------|-----|-------------|---------|
| Citruslib | `Citruslib.dll` | Third-party TABG server modding API dependency | Yes |
| AntiCheatBypass | `TabgInstaller.AntiCheatBypass.dll` | Private server EAC/EOS compatibility bypass | Yes |
| MatchCore | `TabgInstaller.MatchCore.dll` | Rings, loadouts, vote-start, drops, spell drops, timeouts, win rules, and match fixes | Yes |
| ServerLogger | `TabgInstaller.ServerLogger.dll` | Player name, PlayFab ID, and Epic ID logging with CSV and legacy log support | Yes |
| UnusedVehicles | `TabgInstaller.UnusedVehicles.dll` | Spawns and manages hidden TABG vehicles | Yes |
| CustomGrenades | `TabgInstaller.CustomGrenades.dll` | Big Smoke grenade behavior; client-only MGL flashbang code disables itself on headless servers | Yes |
| ProximityChat | `TabgInstaller.ProximityChat.Server.dll` | Nearby voice relay over the existing game network | Yes |
| SoloTesting | `TabgInstaller.SoloTesting.dll` | Local testing helpers | No |
| FakePlayers | `TabgInstaller.FakePlayers.dll` | Dummy players and AI test targets for private testing | No |
| DummyDebugRadar | `TabgInstaller.AdminRadar.Server.dll` | Dummy/debug telemetry server, real player positions and real target names disabled by default | No |
| RangeMap | `TabgInstaller.RangeMap.Server.dll` | Multiplayer WilhelmTest shooting range with authoritative items and infinite respawns; paired client required | No |

### Client Plugins

| Plugin | DLL | Description | Default |
|--------|-----|-------------|---------|
| FlyingControls | `TabgInstaller.FlyingControls.dll` | Client steering for custom flying vehicles | Yes |
| CustomGrenades | `TabgInstaller.CustomGrenades.dll` | Client visuals/effects for custom grenades and MGL flashbangs | Yes |
| CoordsDisplay | `TabgInstaller.CoordsDisplay.dll` | Coordinate overlay | Yes |
| ModSettings | `TabgInstaller.ModSettings.dll` | In-game mod settings support | Yes |
| EnhancedClient | `TabgInstaller.EnhancedClient.dll` | Experimental LOD, draw distance, haze, and HUD controls | No |
| PopupBlocker | `TabgInstaller.PopupBlocker.dll` | Suppresses modded-client anti-cheat popups | Yes |
| ProximityChatClient | `TabgInstaller.ProximityChat.Client.dll` | Captures and plays proximity voice | Yes |
| DummyDebugRadarClient | `TabgInstaller.AdminRadar.Client.dll` | Dummy/debug radar overlay | No |
| RangeMapClient | `TabgInstaller.RangeMap.Client.dll` | Loads WilhelmTest and adds a searchable F6 all-items menu | No |

## Multiplayer Shooting Range

Apply the **Multiplayer Shooting Range** preset, install `RangeMap` on the server, and install `RangeMapClient` on every player. Compatible clients are redirected from the stock Test map to TABG's built-in `WilhelmTest` shooting range. Press **F6** in the Range to request any item from the server. Ammo requests give a full stack, the starter weapon/ammo return after every death, and the Test match is kept running for unlimited respawns.

## Proximity Voice Chat

Voice communication is built directly into the game's existing network connection. No additional ports or separate voice server are required.

- Voice data travels through the game's relay network as versioned v1 16 kHz unsigned PCM packets.
- The server relays voice packets only to nearby players based on in-game distance and rate-limits each sender.
- Configurable maximum range, defaulting to 50 m.
- HUD indicator shows who is currently talking.
- Voice activation uses the `MicSensitivity` RMS threshold; optional push-to-talk drains muted mic frames so stale pre-key audio is not sent later.
- Turning the client config off immediately stops microphone recording and packet sending.
- 16 kHz audio quality.

### Private Server Safety

Some bundled plugins exist for custom/private server operation and should not be treated as public-server defaults:

- **FakePlayers** commands require Citrus permissions by default. The `CommandsUsableByEveryone` bypass is ignored unless `Safety.DevelopmentMode=true`.
- **AntiCheatBypass** and **PopupBlocker** are compatibility tooling for this custom server ecosystem, not general-purpose public-server anti-cheat changes.
- **DummyDebugRadar** defaults to dummy-only broadcasts. Real player positions and real bot target names require `Visibility.IncludeRealPlayers=true`; with it off, bot debug target names are kept only for dummy targets or generic threat markers such as `last-heard`.
- **SoloTesting** is inactive unless `Safety.DevelopmentMode=true`.
- **EnhancedClient** is disabled by default and uses bounded draw-distance settings.
- **ProximityChat** records microphone audio only while enabled and in a game session; disabling it stops capture immediately.
- **CustomGrenades** is one combined DLL with separate `BigSmoke` and `Flashbang` config sections. The MGL flashbang feature is client-only and disables itself on dedicated/headless servers.

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
2. Copy the release DLL into `bundled/plugins` or `bundled/client-plugins`.
3. Add or update `registry/plugins/<PluginId>/manifest.json`.
4. Build the launcher projects and tests project before release.

`TabgInstaller.WeaponSpawnConfig` is currently source-only experimental work. It is not part of `TabgInstaller.sln`, has no registry manifest, and is not copied into the bundled server/client payloads.

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
