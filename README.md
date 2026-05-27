# TABG Server Installer

Installer, launcher, and config UI for Totally Accurate Battlegrounds dedicated servers and bundled TABG mods.

This repository owns the installer code, Linux UI, Windows UI, config editors, presets, and the bundled `TabgInstaller.*` plugins. It installs BepInEx, Citruslib, and the selected server/client plugin DLLs, then exposes the important settings in the installer so you do not have to edit every config file by hand.

## Current Status

- Linux GUI and Windows GUI both use the same bundled plugin registry.
- Server mod settings are available in the installer, including MatchCore, loadouts, rings, spawns, Proximity Chat, ServerLogger, AdminRadar, FakePlayers, SoloTesting, UnusedVehicles, CustomGrenades, Hunt Mode, and Juggernaut Mode settings.
- Client mod installation is supported for a separate modded TABG client copy.
- Runtime tested on Linux with every bundled server/client mod except Hunt Mode and Juggernaut Mode. The tested server started, loaded plugins, spawned unused vehicles, and produced relay join codes.

## Available Server Mods

| Mod | DLLs | Default | Client mod needed | Notes |
| --- | --- | --- | --- | --- |
| Citruslib | `Citruslib.dll` | Yes | No | Required TABG server modding API dependency. |
| MatchCore | `TabgInstaller.MatchCore.dll` | Yes | No | Match rules, rings, loadouts, vote start, drops, spell drops, timeouts, and win conditions. |
| ServerLogger | `TabgInstaller.ServerLogger.dll` | Yes | No | Logs player name, PlayFab ID, and Epic ID. |
| UnusedVehicles | `TabgInstaller.UnusedVehicles.dll` | Yes | No | Spawns hidden TABG vehicles. Patched for Linux headless server audio callbacks. |
| Big Smoke Grenade | `TabgInstaller.CustomGrenades.dll` | Yes | Yes | Makes smoke grenades use the big smoke behavior. |
| MGL Flashbang | `TabgInstaller.CustomGrenades.dll` | Yes | Yes | Makes MGL fire flashbang rounds. |
| Solo Testing | `TabgInstaller.SoloTesting.dll` | No | No | Local testing helpers. |
| Proximity Chat Server | `TabgInstaller.ProximityChat.Server.dll` | Yes | Yes | Relays nearby voice packets through the game network. |
| Hunt Mode | `TabgInstaller.HuntMode.dll`, `TabgInstaller.HuntMode.Shared.dll` | No | Yes | 4v1 asymmetric survival mode. |
| Juggernaut Mode | `JuggernautMode.Server.dll` | No | Yes | Boss player versus everyone mode. |
| Fake Players | `TabgInstaller.FakePlayers.dll` | No | No | Dummy players and AI test targets. |
| Admin Radar Server | `TabgInstaller.AdminRadar.Server.dll` | No | Yes | Sends admin-only player telemetry. |

## Available Client Mods

| Mod | DLLs | Default | Notes |
| --- | --- | --- | --- |
| Flying Controls | `TabgInstaller.FlyingControls.dll` | Yes | Client controls for flying/unused vehicles. |
| Custom Grenades Client | `TabgInstaller.CustomGrenades.dll` | Yes | Client visuals and behavior support for custom grenades. |
| Coords Display | `TabgInstaller.CoordsDisplay.dll` | Yes | Coordinate overlay. |
| Mod Settings | `TabgInstaller.ModSettings.dll` | Yes | In-game mod settings UI. Press `F9` in-game. |
| Enhanced Client | `TabgInstaller.EnhancedClient.dll` | Yes | LOD, draw distance, haze, HUD controls, and LAN menu label option. |
| Popup Blocker | `TabgInstaller.PopupBlocker.dll` | Yes | Suppresses modded-client anti-cheat popups. |
| Proximity Chat Client | `TabgInstaller.ProximityChat.Client.dll` | Yes | Captures microphone audio and plays nearby voice. |
| Hunt Mode Client | `TabgInstaller.HuntMode.Client.dll`, `TabgInstaller.HuntMode.Shared.dll` | No | HUD support for Hunt Mode. |
| Juggernaut Client | `JuggernautMode.Client.dll` | No | Boss bar, loadout picker, and scoreboard support. |
| Admin Radar Client | `TabgInstaller.AdminRadar.Client.dll` | No | Admin-only radar overlay. |

## Installer Configuration Areas

The installer writes and edits the same files the server reads:

| Area | Files |
| --- | --- |
| Server settings | `game_settings.txt` |
| Match settings | `TheStarterPack.txt` |
| Rings and spawns | `game_settings.txt`, `TheStarterPack.txt` |
| Loadouts | `TheStarterPack.txt` |
| Admins | `BepInEx/config/CitrusLib/PlayerPerms.json` |
| Mod settings | `BepInEx/config/*.cfg` |
| Server logs | `BepInEx/server-logs/players.csv`, optional `ServerLogger.txt` |

The generated `game_settings.txt` uses numeric values for TABG settings that the dedicated server parses as numbers, such as `NoRing=0`, `DEBUG_DEATHMATCH=0`, and `AllowRejoins=0`.

## Linux Quick Start

1. Build or download the Linux release.
2. Run the Linux GUI:

```bash
DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" ./TabgInstaller.LinuxGui
```

3. Pick the TABG dedicated server path.
4. Select the server mods you want.
5. Open the config/mod settings panels and configure the selected mods.
6. Install or update the server.
7. Start the server from the installer or with the generated server start script.

## Windows Quick Start

1. Download the latest release zip.
2. Extract it.
3. Run `TabgInstaller.Gui.exe`.
4. Pick the TABG dedicated server path.
5. Select server mods and configure them in the config panels.
6. Install or update the server.
7. Start the server from the installer.

## Client Mod Setup

1. Open the client mod section in the installer.
2. Select the Steam TABG install folder.
3. Choose a separate destination for the modded client copy.
4. Select client mods.
5. Install client mods.
6. Launch the modded client from the installer or through Proton/Wine using the modded copy.

Do not launch the modded copy through the normal Steam TABG entry unless you know Steam is pointing at that modded copy.

## Proximity Chat

Proximity Chat uses the existing game network path. It does not require a separate voice server or extra forwarded ports.

- Server plugin relays nearby voice packets.
- Client plugin captures microphone audio and plays nearby players.
- Range and audio behavior are configurable in mod settings.
- The client log should show `Microphone started` when capture is working.

## Runtime Test Notes

The Linux runtime test used:

Server mods enabled:

- Citruslib
- MatchCore
- ServerLogger
- UnusedVehicles
- Big Smoke Grenade
- MGL Flashbang
- SoloTesting
- Proximity Chat Server
- FakePlayers
- Admin Radar Server

Server mods excluded for that test:

- Hunt Mode
- Juggernaut Mode

Client mods enabled:

- Flying Controls
- Custom Grenades Client
- Coords Display
- Mod Settings
- Enhanced Client
- Popup Blocker
- Proximity Chat Client
- Admin Radar Client

Client mods excluded for that test:

- Hunt Mode Client
- Juggernaut Client

The final test server loaded 10 BepInEx plugin entries and reached a heartbeat/join-code state. The final test client loaded 9 BepInEx plugin entries and started the Proximity Chat microphone.

## Development

Bundled plugins are BepInEx 5 plugins targeting `netstandard2.0`.

Common development flow:

```bash
DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet build TabgInstaller.sln -c Release /p:EnableWindowsTargeting=true
```

When adding or changing a bundled plugin:

1. Update the plugin source project.
2. Build in `Release`.
3. Copy the DLL into `TabgInstaller.Gui/plugins` for server mods or `TabgInstaller.Gui/client-plugins` for client mods.
4. Register the mod in `TabgInstaller.Core/PluginRegistry.cs`.
5. Add installer UI/config support if the mod has settings.
6. Runtime test the server/client if the change affects BepInEx loading or gameplay startup.

## Known Build Notes

- On Linux, building Windows-targeted projects requires `/p:EnableWindowsTargeting=true`.
- The full solution build currently completes with warnings.
- Running the Windows-targeted testhost on Linux requires the Windows Desktop runtime; without it, `dotnet test` can build but the testhost will not start.

## Disclaimer

This project is not affiliated with, endorsed by, or associated with Landfall Games or Totally Accurate Battlegrounds.

Use at your own risk. Modifying game servers or clients may violate game terms or platform rules. The anti-cheat bypass component is intended for private dedicated server/modded-client testing, not cheating in public matches.

## Credits

- `anonymer_hase` - launcher, installer, config UI, presets, remote tools, and bundled `TabgInstaller.*` plugin code.
- BepInEx Team - BepInEx plugin framework.
- Andreas Pardeike and Harmony contributors - Harmony runtime patching.
- CyrusTheLesser - Citruslib TABG server modding API.

## License

Released under the MIT License. Bundled third-party libraries keep their own licenses.
