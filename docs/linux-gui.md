# Avalonia App

This repo now includes a functional cross-platform Avalonia desktop app:

```bash
./run-linux-gui.sh
```

Publish a self-contained Linux build with:

```bash
dotnet publish TabgInstaller.App/TabgInstaller.App.csproj -c Release -r linux-x64 --self-contained true
dotnet publish TabgInstaller.App/TabgInstaller.App.csproj -c Release -r win-x64 --self-contained true
```

The Avalonia app covers the main installer workflows:

- server path detection and manual folder selection
- creating a default server folder
- installing/updating the TABG dedicated server with SteamCMD when available
- server install/repair with plugin selection
- BepInEx Linux package installation and `run_bepinex.sh` configuration
- server start/stop with log output
- raw `game_settings.txt` editing
- backups
- client mod copy/install
- local registry marketplace install/uninstall
- knowledge/reference JSON viewing

The existing WPF app is still present for Windows until the Avalonia app reaches full parity. `TabgInstaller.LinuxGui` also remains buildable during the migration, but `TabgInstaller.App` is the new cross-platform target.

Runtime logs are written to:

```text
~/.local/share/TabgInstaller/tabg-installer-app.log
```
