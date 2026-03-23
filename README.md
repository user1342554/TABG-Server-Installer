<div align="center">

<img src="logo/tabg-mod-manager-icon-256.png" alt="TABG Server Installer" width="128" />

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

</div>

---

## About

This project is **not** an original creation from scratch. It is a **collection and wrapper** around several existing community-made mods, tools, and libraries for TABG, bundled together into a single installer with a configuration GUI. The goal is to make setting up a modded TABG dedicated server as painless as possible, without having to manually download and configure each component separately.

I (anonymer_hase) wrote the installer GUI and the glue code that ties everything together. The actual mods and core libraries that make the server work are the hard work of other developers listed in the [Credits](#credits) section below.

## Features

- **One-click server setup** — Installs BepInEx, plugins, and generates default configs automatically
- **Client mod installer** — Creates a modded TABG copy with BepInEx + client plugins (separate from Steam to bypass EAC)
- **Config editor** — GUI for editing game settings, match rules, ring behavior, spawn points, and loadouts
- **Preset templates** — Battle Royale, Deathmatch, Gun Game, Scavenge, and more
- **Weapon spawn config** — Control weapon spawn rates per category
- **Admin panel** — In-app player/admin management
- **Backup system** — Create and restore server config backups
- **Auto-updater** — Checks GitHub Releases for new versions on startup
- **Self-contained** — No .NET installation required, just extract and run
- **Proximity voice chat** — Built-in proximity-based voice chat through the game's network (no extra ports needed, works with relay)
- **Custom vehicles** — Spawns cut vehicles (Helicopter, UFO, Mustang, VW, Hover Bike, Hover Car, Box Car)
- **Flying controls** — Client mod to steer flying vehicles (W/S/A/D + Space/Ctrl)
- **Custom grenades** — Giant purple smoke grenades + MGL flashbang rounds
- **In-game settings menu** — Press # to adjust mod settings in-game
- **Coords display** — Press F5 to show X/Y/Z position

## Download

1. Go to the [latest release](https://github.com/user1342554/TABG-Server-Installer/releases/latest)
2. Download the `.zip` file
3. Extract anywhere and run `TabgInstaller.Gui.exe`

## Quick Start

### Server Setup

1. Download and extract the latest release
2. Run `TabgInstaller.Gui.exe`
3. Select your TABG Dedicated Server path (auto-detected from Steam)
4. Check the plugins you want
5. Click **INSTALL**
6. Configure settings in the **Config** tab
7. Start the server from the installer

### Client Setup (for players)

1. Go to the **Client** tab
2. Select your TABG Steam folder
3. Choose a destination for the modded copy
4. Check the mods you want (including Proximity Chat)
5. Click **INSTALL CLIENT MODS**
6. Launch the modded TABG from the installer (**NOT** from Steam)

## Plugins

### Server Plugins

| Plugin | Default | Description |
|--------|---------|-------------|
| Citruslib | ON | Core server library — admin commands, permissions, loot tables |
| StarterPack | ON | Match mechanics, loadouts, win conditions |
| StarterPackFixes | ON | Loot drop control |
| CustomSpawnpoints | ON | Custom spawn locations |
| FreddoTABGCommission | ON | Curses, grenades on kill, bans |
| MatchAndPreMatchTimeout | ON | Match timing and auto-start |
| ServerLogger | ON | Player logging |
| VoteToStart | ON | Vote-to-start command |
| UnusedVehicles | ON | Spawns cut vehicles (Heli, UFO, Mustang, VW, etc.) |
| BigSmokeGrenade | ON | Giant purple smoke grenades |
| MGLFlashbang | ON | MGL shoots flashbang rounds |
| ProximityChat | ON | Proximity voice chat server relay |
| HuntMode | OFF | 4v1 survival — 1 Killer vs 4 Survivors |
| JuggernautMode | OFF | One massive player vs everyone, score-based |
| TABGVR Server | OFF | VR hand sync for VR players (requires client mod) |
| FakePlayers | OFF | Spawn dummy players via admin commands |
| SoloTesting | OFF | Solo play without instant win |

### Client Plugins

| Plugin | Default | Description |
|--------|---------|-------------|
| FlyingControls | ON | Steer helicopters, UFOs, hover vehicles |
| Enhanced TABG | ON | Infinite LOD (F1), HUD toggle (F2), fog removal (F3) |
| BigSmokeGrenade | ON | See the purple smoke effect client-side |
| MGLFlashbang | ON | See the flashbang effect client-side |
| CoordsDisplay | ON | F5 to show X/Y/Z coordinates |
| ModSettings | ON | In-game settings menu (press #) |
| Pop-up Blocker | ON | Disable anti-cheat popups |
| ProximityChat | ON | Proximity voice chat — mic capture + playback |
| HuntMode Client | OFF | HUD and perk selection UI for Hunt Mode |
| JuggernautMode Client | OFF | Boss bar, loadout picker, scoreboard |
| TABGVR | OFF | Play TABG in Virtual Reality (requires VR headset) |

## Proximity Voice Chat

Voice communication is built directly into the game's existing network connection — no additional ports, no separate voice servers, and it works transparently through a relay.

- Voice data travels through the game's relay network — no direct port forwarding required
- The server relays voice packets only to nearby players based on in-game distance
- Configurable maximum range (default: 50 m)
- HUD indicator shows who is currently talking
- Open microphone with noise gate to suppress background noise
- 16 kHz audio quality

## Disclaimer

This software is provided **as-is** with no warranty of any kind. Use at your own risk.

- This project is **not affiliated with, endorsed by, or associated with** [Landfall Games](https://landfall.se/) or Totally Accurate Battlegrounds in any way.
- This installer bundles third-party mods and tools. While every effort has been made to credit original authors, if you are a mod author and want your work removed or credited differently, please contact me.
- Modifying game servers may violate the game's Terms of Service. The authors of this installer are **not responsible** for any bans, account actions, or other consequences resulting from its use.
- The anti-cheat bypass component is intended solely for running private dedicated servers and is **not** meant for use in cheating or gaining unfair advantages in public matches.

## Credits

This project would not exist without the work of these developers and communities. If you contributed something and aren't listed here (or want to be credited differently), please reach out!

### Core Libraries & Frameworks

| Component | Author | Link |
|-----------|--------|------|
| [BepInEx](https://github.com/BepInEx/BepInEx) | BepInEx Team | Unity/Mono game plugin framework (v5.4.22) |
| [HarmonyLib](https://github.com/pardeike/Harmony) | Andreas Pardeike | Runtime method patching (bundled with BepInEx) |

### TABG Mod Authors

| Plugin | Author | Description |
|--------|--------|-------------|
| StarterPack | [**ContagiouslyStupid**](https://github.com/ContagiouslyStupid) | Server configuration and match mechanics — the backbone of modded TABG servers |
| MatchAndPreMatchTimeout | [**ContagiouslyStupid**](https://github.com/ContagiouslyStupid) | Ends the game or restarts the lobby after a configurable amount of time |
| ServerLogger | [**ContagiouslyStupid**](https://github.com/ContagiouslyStupid) | Logs the name, PlayFab ID, and Epic ID of every new player |
| VoteToStart | [**ContagiouslyStupid**](https://github.com/ContagiouslyStupid) | `/votestart` command to vote-start the server |
| [Citruslib](https://github.com/CyrusTheLesser/Citruslib) | [**CyrusTheLesser**](https://github.com/CyrusTheLesser) | Code library for TABG-DS modding — custom chat commands, loot tables, settings, player management |
| [ModerationTools](https://github.com/CyrusTheLesser/ModerationTools) | [**CyrusTheLesser**](https://github.com/CyrusTheLesser) | Server moderation — blacklist/whitelist, kick/ban via Epic IDs |
| FreddoTABGCommission | **Freddo** | Commission/loadout system, bans, curses, grenade-on-kill mechanics *(no public repo)* |
| FreddoFixStarterPack | **Freddo** | Loot drop fixes for StarterPack *(no public repo)* |
| FreddoCustomSpawnpoints | **Freddo** | Custom spawn location support *(no public repo)* |
| Enhanced TABG | **Freddo** | Client-side enhancements *(no public repo)* |
| Pop-up Blocker | **Freddo** | Disables anti-cheat pop-ups on client *(no public repo)* |
| [TASM](https://github.com/RedBigz/TASM) | [**RedBigz**](https://github.com/RedBigz) | Totally Accurate Server Mod — plugin support and command system |
| [ComputeryLib](https://github.com/C0mputery/ComputerysTabgMods) | [**C0mputery**](https://github.com/C0mputery) | Core server library — CLI handler, chat commands, message logging, visitor tracking, config improvements |
| [LandfallPlzFixServer](https://github.com/C0mputery/ComputerysTabgMods) | [**C0mputery**](https://github.com/C0mputery) | Server-side game fixes |
| [LandfallPlzFixClient](https://github.com/C0mputery/ComputerysTabgMods) | [**C0mputery**](https://github.com/C0mputery) | Client-side game fixes |
| [SteamworksEnforcer](https://github.com/C0mputery/ComputerysTabgMods) | [**C0mputery**](https://github.com/C0mputery) | Steam authentication enforcement |
| [TokenAuthFixer](https://github.com/C0mputery/ComputerysTabgMods) | [**C0mputery**](https://github.com/C0mputery) | Token authentication fixes |
| [BinsCinematicMod](https://github.com/C0mputery/ComputerysTabgMods) | **Bins** | Camera/cinematic mod *(hosted in C0mputery's monorepo)* |

### Community Tools & References

| Project | Author | Used For |
|---------|--------|----------|
| [ComputerysUltimateTABGServer](https://github.com/C0mputery/ComputerysUltimateTABGServer) | [**C0mputery**](https://github.com/C0mputery) | Full community server rewrite — room management, tick system, packet handling, admin commands |
| [AntiCheatBootErrorRemover](https://github.com/C0mputery/AntiCheatBootErrorRemover) | [**C0mputery**](https://github.com/C0mputery) | Anti-cheat bypass reference for dedicated servers |
| [TABGCommunityServer](https://github.com/JIBSIL/TABGCommunityServer) | [**JIBSIL**](https://github.com/JIBSIL) | Original community server foundation (CUTS is based on this) |
| [Citruslib-FixedUp](https://github.com/RedBigz/Citruslib-FixedUp) | [**RedBigz**](https://github.com/RedBigz) | Modernized fork of Citruslib for newer .NET |
| [tabg-word-list](https://github.com/landfallgames/tabg-word-list) | [**Landfall Games**](https://github.com/landfallgames) | Official word list for server name validation |
| [dnSpyEx](https://github.com/dnSpyEx/dnSpy) | dnSpyEx Team | .NET decompiler used for modding research |

### Installer (this project)

- **anonymer_hase** — Installer GUI, configuration editor, backup system, auto-updater, preset system, and all the glue code tying everything together

| Plugin | Author | Description |
|--------|--------|-------------|
| UnusedVehicles | anonymer_hase | Spawns cut/unused vehicles on the map |
| FlyingControls | anonymer_hase | Client-side flying vehicle steering |
| BigSmokeGrenade | anonymer_hase | Giant purple smoke grenades |
| MGLFlashbang | anonymer_hase | MGL flashbang rounds |
| CoordsDisplay | anonymer_hase | In-game coordinate display |
| ModSettings | anonymer_hase | In-game settings menu for all mods |
| SoloTesting | anonymer_hase | Solo testing mode |
| ProximityChat | anonymer_hase | Proximity voice chat (server + client) |
| HuntMode | anonymer_hase | 4v1 survival game mode (server + client) |
| JuggernautMode | anonymer_hase | One massive player vs everyone (server + client) |
| TABGVR | anonymer_hase | VR support for TABG (server + client) |
| FakePlayers | anonymer_hase | Spawn dummy players for testing |

### Third-Party Libraries

- [Octokit](https://github.com/octokit/octokit.net) — GitHub API (auto-updater)
- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) by James Newton-King — JSON handling
- [Polly](https://github.com/App-vNext/Polly) — Resilience and retry policies
- [RestSharp](https://github.com/restsharp/RestSharp) — HTTP client

> **Know someone who should be credited?** Open an issue or DM me on Discord.

## Contact

Have questions, feature requests, or found a bug?

- **Discord:** `anonymer__hase_22156`
- **GitHub Issues:** [Open an issue](https://github.com/user1342554/TABG-Server-Installer/issues)

## License

Released under the **MIT License** — see [`LICENSE`](./LICENSE).

This license applies to the installer code only. Bundled third-party components retain their own respective licenses.
