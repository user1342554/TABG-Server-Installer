# Avalonia Migration Parity Checklist

Status values:

- done: implemented in `TabgInstaller.App` through real installer/Core logic.
- partial: usable, but not yet equivalent to `TabgInstaller.Gui`.
- missing: not implemented in `TabgInstaller.App`.
- intentionally unsupported cross-platform: deliberately not shipped as a cross-platform feature.

This checklist compares the WPF app (`TabgInstaller.Gui`) with the target cross-platform Avalonia app (`TabgInstaller.App`) as of the migration slice after `f5619bd`.

## Setup / Install

| Feature | WPF surface | Avalonia status | Notes |
| --- | --- | --- | --- |
| Browse/select server folder | `InstallerPanel` | done | Uses Avalonia folder picker service. |
| Create server folder | `InstallerPanel` | done | Creates the selected folder. |
| Detect TABG dedicated server path | `InstallerPanel` / path provider | partial | Uses shared `Installer.TryFindTabgServerPath`; Linux fallback path is provided when detection fails. Needs Windows smoke test. |
| SteamCMD install/update dedicated server | WPF installer flow | done | Runs `steamcmd`/`steamcmd.sh` with Steam credentials and streams output. |
| Prepare/repair server with BepInEx and bundled server plugins | WPF installer flow | done | Calls `Installer.InstallAsync` and backs up first. |
| Server plugin preset selection | WPF default/Sigma controls | partial | Select all/none/default/Sigma preset exists, but WPF setup wizard polish is not ported. |
| Setup wizard window | `SetupWizardWindow` | missing | Avalonia uses inline setup tabs instead of a wizard. |

## Server Run / Console / Logs

| Feature | WPF surface | Avalonia status | Notes |
| --- | --- | --- | --- |
| Start server | `DashboardPanel`, `ConsolePanel` | done | Uses shared `ServerProcessService`. |
| Stop server | `DashboardPanel`, `ConsolePanel` | done | Uses shared `ServerProcessService`. |
| Quick restart | `ConsolePanel` | done | Stops then restarts with standard headless args. |
| Stream/show server output | `ConsolePanel`, dashboard preview | done | Output is appended to the visible log and persisted to local app log. |
| Clear visible console | `ConsolePanel` | done | Clears visible text. |
| Export visible console | `ConsolePanel` | done | Writes an export log file. |
| Search visible console | `ConsolePanel` | done | Logs the first matching line. |
| Send stdin command | `ConsolePanel` | intentionally unsupported cross-platform | The Avalonia UI logs the command as unsupported because the current process service does not expose reliable stdin. |
| Dashboard health cards/watchdog view | `DashboardPanel` | partial | Start/stop and log preview exist; WPF health-card/watchdog UI is not fully ported. |

## Config Editor

| Feature | WPF surface | Avalonia status | Notes |
| --- | --- | --- | --- |
| Raw `game_settings.txt` load/save | `ConfigPanel` | done | Reads/writes the selected server file. |
| Typed game settings editor | `GameSettingsGrid` | done | Reflects `GameSettingsData` properties. |
| Match/StarterPack settings | `MatchSettingsPanel` | done | Uses `StarterPackConfigService`. |
| Ring and spawn settings | `RingSpawnsPanel` | done | Uses `ConfigIO`, `StarterPackConfigService`, and `ModConfigService`. |
| Loadout editor | `LoadoutEditorPanel` | partial | Raw loadout text is editable; WPF item-search/grid editor is not fully ported. |
| Admin permissions editor | `AdminPanel` | done | Reads/writes `PlayerPerms.json`. |
| Presets | `PresetsGrid` | done | Lists built-in presets and saved user presets. |
| Mod/plugin config editor | `ModSettingsPanel` | done | Covers Commission, MatchCore loot drops, ProximityChat, ServerLogger, additional server plugin config, and client plugin config. |
| Config validation/dirty autosave behavior | WPF view models | partial | Manual load/save exists; WPF autosave/validation status behavior is not equivalent. |

## Server Mods

| Feature | WPF surface | Avalonia status | Notes |
| --- | --- | --- | --- |
| Installed server DLL list | `ServerModsPanel` | done | Shows enabled and disabled DLLs. |
| Enable/disable installed DLL | `ServerModsPanel` | done | Moves DLLs between plugin root and disabled state. |
| Remove installed DLL | `ServerModsPanel` | done | Deletes selected DLL. |
| Add external server DLL | `ServerModsPanel` | done | Uses Avalonia file picker service. |
| Install bundled server DLL | `ServerModsPanel` | done | Copies from shared `bundled/plugins`. |
| Catalog install/enable/disable | `ServerModsPanel` | done | Uses shared plugin grouping logic and bundled registry definitions. |
| Registry/bundled manifest refresh | WPF plugin catalog | done | Calls `PluginRegistry.LoadBundledManifests`. |

## Client Mods

| Feature | WPF surface | Avalonia status | Notes |
| --- | --- | --- | --- |
| Detect/browse TABG Steam folder | `ClientPanel` | done | Uses shared detection plus Avalonia folder picker. |
| Browse modded client folder | `ClientPanel` | done | Uses Avalonia folder picker. |
| Prepare/update modded client | `ClientPanel` | done | Calls `ClientModInstaller.InstallAsync`. |
| Start modded client | `ClientPanel` | partial | Supports native launch and Linux Proton launch; needs Windows smoke test. |
| Installed client DLL list | `ClientPanel` | done | Shows enabled and disabled DLLs. |
| Enable/disable/remove/add external client DLL | `ClientPanel` | done | Same behavior as server plugin list. |
| Install bundled client DLL | `ClientPanel` | done | Copies from shared `bundled/client-plugins`. |
| Catalog install/enable/disable | `ClientPanel` | done | Uses shared plugin grouping logic and bundled registry definitions. |

## Backups

| Feature | WPF surface | Avalonia status | Notes |
| --- | --- | --- | --- |
| Create backup | `BackupsPanel` | done | Uses shared `BackupService`. |
| List backups | `BackupsPanel` | done | Uses shared `BackupService`. |
| Restore selected backup | `BackupsPanel` | done | Uses shared `BackupService`. |
| Delete selected backup | `BackupsPanel` | done | Uses shared `BackupService`. |
| Confirmation dialogs before destructive backup actions | WPF view models | partial | Avalonia destructive actions are real but currently do not prompt everywhere WPF prompts. |

## Settings / Reference

| Feature | WPF surface | Avalonia status | Notes |
| --- | --- | --- | --- |
| Commands reference | `ReferencePanel` | done | Inline reference text. |
| Item reference/search | `ReferencePanel` | done | Uses item database. |
| Spawn/loadout reference | `ReferencePanel` | done | Inline reference text. |
| Path/status summary | `SettingsPanel` | done | Shows app folder, log path, selected paths, bundled folders, and platform. |
| Reset detected paths | `SettingsPanel` | done | Clears selected paths and reruns detection. |
| Theme/localization settings | WPF settings/theme services | missing | Avalonia does not yet port WPF theme/localization controls. |

## Path Detection / Browse Flows

| Feature | WPF surface | Avalonia status | Notes |
| --- | --- | --- | --- |
| Folder picker abstraction | WPF code-behind dialogs | done | `TabgInstaller.UI.Services.IStoragePickerService` with Avalonia implementation. |
| File picker abstraction | WPF code-behind dialogs | done | `TabgInstaller.UI.Services.IStoragePickerService` with Avalonia implementation. |
| External path launching abstraction | WPF `explorer` calls | done | `IExternalLauncher` abstracts platform launch command. |
| Steam path detection abstraction | WPF registry/Steam lookup | done | `ISteamPathDetector` abstracts detection behind installer/Core lookup. |
| Confirmation dialog abstraction | WPF `MessageBox` calls | partial | Interface and Avalonia implementation exist; not every destructive flow has been rewired yet. |
| Clipboard abstraction | WPF clipboard calls | partial | Interface and Avalonia implementation exist; Avalonia loadout import/export UI is not fully ported. |
| UI dispatcher abstraction | WPF/Avalonia dispatcher calls | done | Avalonia app uses `IUiDispatcher`. |

## Plugin Install / Update / Remove Flows

| Feature | WPF surface | Avalonia status | Notes |
| --- | --- | --- | --- |
| Server install selected default plugins | `InstallerPanel` | done | Selected registry definitions feed `Installer.InstallAsync`. |
| Server catalog install/enable/disable/remove | `ServerModsPanel` | done | Real file operations against `BepInEx/plugins`. |
| Client catalog install/enable/disable/remove | `ClientPanel` | done | Real file operations against modded client `BepInEx/plugins`. |
| Bundled lookup from shared payload folders | WPF/App catalogs | done | Uses shared `BundledAssetLocator` and shared UI catalog grouping. |
| Plugin update from registry/release metadata | WPF update services | partial | Core update service still exists; Avalonia does not expose every WPF update/status surface. |

## Windows-only / Platform-specific Features

| Feature | WPF surface | Avalonia status | Notes |
| --- | --- | --- | --- |
| Sigma Mode overlays/music/window polling | `SuperSecretSettingsPanel`, `SigmaModeApp` | intentionally unsupported cross-platform | WPF-only feature uses WPF windows, Windows APIs, and system integration. Do not expose on Linux as cosmetic UI. |
| Wallpaper capture/set | `WallpaperService` | intentionally unsupported cross-platform | Windows-only behavior. |
| Fan control | `FanControlManager` | intentionally unsupported cross-platform | Windows registry/process integration. |
| Registry-only Steam lookup | `SteamLauncher` | intentionally unsupported cross-platform | Steam path detection must stay behind platform services/Core lookup. |
| WPF overlay windows | `SigmaOverlayWindow` | intentionally unsupported cross-platform | Not ported to Avalonia. |

## Current deletion blockers

- Keep `TabgInstaller.Gui` until Windows smoke testing confirms the Avalonia app can install/update, manage server and client plugins, start/stop/log a server, edit configs, and manage backups.
- Keep `TabgInstaller.LinuxGui` for now as a temporary comparison app until `TabgInstaller.App` has completed Linux and Windows smoke tests.
- Remaining parity gaps are mainly WPF polish and safety prompts, full loadout editor ergonomics, dashboard health/watchdog UI, theme/localization settings, and update/status surfaces.

## Verification snapshot

Completed on Linux:

- `dotnet build TabgInstaller.sln --configuration Release` succeeds.
- `dotnet test TabgInstaller.Tests/TabgInstaller.Tests.csproj --framework net8.0 --configuration Release --no-build` succeeds.
- `dotnet publish TabgInstaller.App/TabgInstaller.App.csproj --configuration Release --runtime linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true` succeeds.
- `dotnet publish TabgInstaller.App/TabgInstaller.App.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true` succeeds.
- The published Linux binary starts on Wayland and logs `Main window opened`.

Not completed in this Linux-only pass:

- Solution-wide `dotnet test` reaches `net8.0-windows` tests and aborts without `Microsoft.WindowsDesktop.App` installed.
- Windows app launch and Windows smoke tests.
- Live install/update, server plugin install, client plugin install, server start/stop/log, config save/load, and backup smoke tests through the Avalonia UI.
