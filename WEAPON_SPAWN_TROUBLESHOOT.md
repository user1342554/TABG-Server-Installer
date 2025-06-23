# Weapon Spawn Config - Troubleshooting Guide

## The mod DLL is built successfully!
`TabgInstaller.WeaponSpawnConfig.dll` (32KB) is in the installer directory.

## Check these things:

### 1. Is BepInEx Installed on Your Server?
Look for these folders in your TABG server directory:
- `BepInEx/`
- `BepInEx/plugins/`
- `BepInEx/config/`

If not, BepInEx needs to be installed first.

### 2. Check if Mod is Installed
After clicking "Install Mod" in the Weapon Spawn Config tab, check:
- Does `BepInEx/plugins/TabgInstaller.WeaponSpawnConfig.dll` exist?
- Is it the same size as the one in the installer (about 32KB)?

### 3. Check BepInEx Console Output
When starting the server, you should see BepInEx loading plugins:
```
[Info   :   BepInEx] Loading [TABG Weapon Spawn Config 1.0.0]
```

### 4. Check BepInEx Log File
Look in `BepInEx/LogOutput.log` for:
- Any errors loading TabgInstaller.WeaponSpawnConfig
- The `[WeaponSpawnConfig]` messages

### 5. Enable BepInEx Console (if not visible)
Edit `BepInEx/config/BepInEx.cfg`:
```ini
[Logging.Console]
Enabled = true
```

### 6. Enable More Logging
In the same file:
```ini
[Logging.Disk]
LogLevels = All
```

## Quick Test Sequence:

1. **Open TabgInstaller GUI**
2. **Go to Weapon Spawn Config tab**
3. **Click "Install Mod"** (should see success message)
4. **Verify the DLL exists** in `YourServer/BepInEx/plugins/`
5. **Start the server** via Console tab
6. **Look for BepInEx messages** at startup:
   - `Loading [TABG Weapon Spawn Config 1.0.0]`
   - `[WeaponSpawnConfig] WEAPON SPAWN CONFIG v1.0.0 STARTING UP`
   - `[WeaponSpawnConfig] INITIALIZATION COMPLETE`

## If You See No BepInEx Output At All:

This means BepInEx isn't running. The server logs you showed (`Searching for guns...`) are vanilla TABG server messages with no BepInEx.

### To Install BepInEx:
1. Download BepInEx 5.x for Unity games
2. Extract to your TABG server folder
3. Run the server once to generate BepInEx folders
4. Then install mods

## Share These Details:
1. Does `BepInEx/plugins/TabgInstaller.WeaponSpawnConfig.dll` exist after installation?
2. What's in `BepInEx/LogOutput.log`?
3. Do you see ANY BepInEx messages when starting the server? 