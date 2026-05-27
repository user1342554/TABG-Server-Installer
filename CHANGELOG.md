# Changelog

## Maintenance upgrade pass

- ProximityChat now stops microphone capture immediately when disabled, drains capture while push-to-talk is muted so stale audio is not sent later, uses `MicSensitivity` for voice activation, and enforces server-side sender rate limits for versioned voice packets.
- CustomGrenades is registered as one combined plugin instead of separate BigSmoke/MGLFlashbang fake entries pointing at the same DLL.
- MGLFlashbang now associates flashes with TABG's actual ProjectileHit effect-spawn path instead of every attempted hit, and disables its client-only visuals on dedicated/headless servers.
- ModSettings now uses explicit BepInEx range metadata where present, supports bool/int/float/string/enum/keybind entries, avoids duplicate registrations, and restores the previous `Player.usingInterface` state.
- DummyDebugRadar defaults to dummy-only server broadcasts, keeps real player positions disabled unless explicitly configured, and leaves client body mutation compatibility flags off by default.
- FakePlayers commands now require Citrus permissions by default; open test access requires `Safety.DevelopmentMode=true`.
- UnusedVehicles no longer stores server car objects backed by destroyed Unity GameObjects after successful spawn.
- MatchCore keeps endless/debug win-condition behavior explicit and parses ring profile speeds.
- ServerLogger can update earlier incomplete identity records and no longer removes meaningful whitespace from player names.
- SoloTesting is development-mode gated and resets countdown state when rooms change.
- EnhancedClient is no longer selected by default and uses bounded draw-distance defaults.
- WeaponSpawnConfig is explicitly source-only experimental code, not a bundled installer plugin or registry entry.

Unsafe for public servers unless explicitly intended: `FakePlayers` development command bypass, `DummyDebugRadar` real-player broadcasting, `SoloTesting`, and EnhancedClient's LOD/chunk controls.
