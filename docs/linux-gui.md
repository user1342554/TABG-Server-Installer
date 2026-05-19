# Linux GUI

This repo now includes a functional Avalonia desktop app for Linux:

```bash
./run-linux-gui.sh
```

Publish a self-contained Linux build with:

```bash
dotnet publish TabgInstaller.LinuxGui/TabgInstaller.LinuxGui.csproj -c Release -r linux-x64 --self-contained true
```

The Linux GUI is intentionally plain. It covers the main installer workflows:

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

The existing WPF app is still present for Windows. Build the Linux app project directly on Linux instead of relying on the whole solution until every old Windows-only utility project is portable.

Runtime logs are written to:

```text
~/.local/share/TabgInstaller/linux-gui.log
```
