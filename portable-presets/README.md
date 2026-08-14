# Portable TABG presets

These folders preserve complete, paired server/client setups independently of a
live TABG installation. A preset contains only mode-specific files; BepInEx and
the base game must already be installed.

## Layout

- `preset.json` describes the preset and lists every payload file.
- `server/` is copied over the dedicated-server root.
- `client/` is copied over each player's TABG game root.
- `checksums.sha256` detects missing or changed payload files.
- `README.md` contains preset-specific behavior and switching notes.

## Installing a preset

1. Stop the dedicated server and TABG clients.
2. Back up the live `game_settings.txt` and relevant BepInEx configuration.
3. Remove the other paired map plugin from both server and clients.
4. Copy the contents of the preset's `server/` folder into the server root.
5. Copy the contents of its `client/` folder into every player's TABG root.
6. Set a private password in the live `game_settings.txt` if required, then
   start the server through BepInEx.

Do not store passwords, `PlayerPerms.json`, logs, join codes, or player IDs in a
portable preset. They are deliberately absent from the saved bundles.

## Saving another preset later

Copy `_template/`, rename the folder and fill in its files. Keep only the DLLs
and configuration needed by that mode. Update `preset.json`, then regenerate the
checksum file from inside the new preset folder:

```bash
find server client -type f -print0 | sort -z | xargs -0 sha256sum > checksums.sha256
```
