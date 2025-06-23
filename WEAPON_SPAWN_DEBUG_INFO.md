# Weapon Spawn Config Mod - Debug Information

## What's New
I've added comprehensive logging and error handling to the weapon spawn config mod. Here's what you should see:

## When the Server Starts

Look for these messages in the server console/BepInEx logs:

```
[WeaponSpawnConfig] ========================================
[WeaponSpawnConfig] WEAPON SPAWN CONFIG v1.0.0 INITIALIZING
[WeaponSpawnConfig] ========================================
[WeaponSpawnConfig] Harmony instance created successfully
[WeaponSpawnConfig] Created XXX weapon configurations
[WeaponSpawnConfig] Created 16 category multipliers
[WeaponSpawnConfig] Example configs loaded:
[WeaponSpawnConfig]   - AK47: 1.0x
[WeaponSpawnConfig]   - Assault Rifles category: 1.0x
[WeaponSpawnConfig]   - Global multiplier: 1.0x
[WeaponSpawnConfig] Scanning XX loaded assemblies...
```

## Key Messages to Look For

1. **Successful Initialization**:
   - `INITIALIZATION COMPLETE`
   - `Total configs: XXX`
   - `Patched methods: X` (should be > 0)

2. **If Patching Works**:
   - `Successfully patched: ClassName.MethodName`
   - `Method called: ClassName.MethodName`
   - `>>> WEAPON DETECTED: WeaponName, Final Rate: X.Xx`

3. **If Patching Fails**:
   - `WARNING: No methods were patched!`
   - `Failed to patch...`
   - `CRITICAL ERROR...`

## Periodic Status Updates

Every 30-60 seconds, you'll see:
```
[WeaponSpawnConfig] Status - Active: Yes, Patched Methods: X, Config Items: XXX
```

## Testing Instructions

1. **Start the installer** and go to "Weapon Spawn Config" tab
2. **Install the mod** if not already installed
3. **Set some extreme values** for testing:
   - Set "AK47" to 10.0 (10x spawn rate)
   - Set "Grenades" category to 0.0 (no grenades spawn)
   - Save Configuration
4. **Start the server** through the Console tab
5. **Watch the console** for the log messages above
6. **Play the game** and see if spawn rates are affected

## Troubleshooting

If you see "WARNING: No methods were patched!", it means:
- TABG's internal structure may be different than expected
- The mod needs to know the exact class/method names TABG uses for spawning items

In this case, look for messages like:
- `Found potentially relevant type: XXX`
- `Assembly scan complete - Types: X, Methods found: X`

These will help identify what classes TABG is using for its loot system.

## What to Report

If the mod doesn't work, please share:
1. The full server console output from startup
2. Any lines containing `[WeaponSpawnConfig]`
3. Whether you see "Patched methods: 0" or a number > 0
4. Any error messages

The enhanced logging will help us identify exactly what TABG's loot spawning system looks like internally. 