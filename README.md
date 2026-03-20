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

</div>

---

## About

This project is **not** an original creation from scratch. It is a **collection and wrapper** around several existing community-made mods, tools, and libraries for TABG, bundled together into a single installer with a configuration GUI. The goal is to make setting up a modded TABG dedicated server as painless as possible, without having to manually download and configure each component separately.

I (anonymer_hase) wrote the installer GUI and the glue code that ties everything together. The actual mods and core libraries that make the server work are the hard work of other developers listed in the [Credits](#credits) section below.

## Features

- **One-click server setup** — Installs BepInEx, plugins, and generates default configs automatically
- **Config editor** — GUI for editing game settings, match rules, ring behavior, spawn points, and loadouts
- **Preset templates** — Battle Royale, Deathmatch, Gun Game, Scavenge, and more
- **Weapon spawn config** — Control weapon spawn rates per category
- **Admin panel** — In-app player/admin management
- **Backup system** — Create and restore server config backups
- **Auto-updater** — Checks GitHub Releases for new versions on startup
- **Self-contained** — No .NET installation required, just extract and run

## Download

1. Go to the [latest release](https://github.com/user1342554/TABG-Server-Installer/releases/latest)
2. Download the `.zip` file
3. Extract anywhere and run `TabgInstaller.Gui.exe`

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
| [StarterPack](https://github.com/ContagiouslyStupid/TABGStarterPack) | **ContagiouslyStupid** | Server configuration and match mechanics — the backbone of modded TABG servers |
| [Citruslib](https://github.com/CyrusTheLesser/Citruslib) | **CyrusTheLesser** | Code library for TABG-DS modding — custom chat commands, loot tables, settings, player management |
| [ModerationTools](https://github.com/CyrusTheLesser/ModerationTools) | **CyrusTheLesser** | Server moderation — blacklist/whitelist, kick/ban via Epic IDs |
| FreddoTABGCommission | **Freddo** | Commission/loadout system, bans, curses, grenade-on-kill mechanics |
| FreddoFixStarterPack | **Freddo** | Loot drop fixes for StarterPack |
| FreddoCustomSpawnpoints | **Freddo** | Custom spawn location support |
| [TASM](https://github.com/RedBigz/TASM) | **RedBigz** | Totally Accurate Server Mod — plugin support and command system |
| ComputeryLib | **Computery** | Core server library — CLI handler, chat commands, message logging, visitor tracking, config improvements |
| LandfallPlzFixServer | **Computery** | Server-side game fixes |
| LandfallPlzFixClient | **Computery** | Client-side game fixes |
| SteamworksEnforcer | **Computery** | Steam authentication enforcement |
| TokenAuthFixer | **Computery** | Token authentication fixes |
| BinsCinematicMod | **Bins** | Camera/cinematic mod |
| MatchAndPreMatchTimeout | — | Match timeout management |
| ServerLogger | — | Server-side logging |
| VoteToStart | — | Vote-to-start functionality |
| Enhanced TABG | — | Client-side enhancements |
| Pop-up Blocker | — | Client-side pop-up removal |

### Community Tools & References

| Project | Author | Used For |
|---------|--------|----------|
| [ComputerysUltimateTABGServer](https://github.com/C0mputery/AntiCheatBootErrorRemover) | **Computery** | Full community server rewrite — room management, tick system, packet handling, admin commands |
| [AntiCheatBootErrorRemover](https://github.com/C0mputery/AntiCheatBootErrorRemover) | **Computery** | Anti-cheat bypass reference for dedicated servers |
| [TABGCommunityServer](https://github.com/JIBSIL/TABGCommunityServer) | **JIBSIL** | Original community server foundation (CUTS is based on this) |
| [tabg-word-list](https://github.com/landfallgames/tabg-word-list) | **Landfall Games** | Official word list for server name validation |
| [dnSpyEx](https://github.com/dnSpyEx/dnSpy) | dnSpyEx Team | .NET decompiler used for modding research |

### Installer (this project)

- **anonymer_hase** — Installer GUI, configuration editor, backup system, auto-updater, preset system, and all the glue code tying everything together

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
