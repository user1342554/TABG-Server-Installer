# Modded gameplay: review snapshot

This draft collects the locally developed gameplay mods for code review. It is not a finished release. Existing bundled DLLs are intentionally unchanged; build the source plugins before testing these changes.

## Where to start

| Area | Source entry points |
| --- | --- |
| FPV grenade, camera, controls, signal, sound | `TabgInstaller.FlyingControls/FpvClient.cs`, `FpvItem.cs`, `FpvWire.cs`; `TabgInstaller.UnusedVehicles/FpvServer.cs` |
| Helicopter/UFO controls, landing and ejection | `FlyingControls/HelicopterFlight.cs`, `HelicopterLanding.cs`, `AircraftSafetyClient.cs`; server `AircraftSafety.cs`, `AircraftCombat.cs` |
| Missile locks, shootable rockets, smoke and feedback | client `HelicopterSight.cs`, `RocketModel.cs`, `MissileSmoke.cs`, `BlastHitmarker.cs`, `CombatAudio.cs`; server `HelicopterMissiles.cs`, `BlastFeedback.cs` |
| Hover road autopilot and boost | client `GroundTaxi.cs`, `RoadNetwork.cs`; server `ServerHoverTaxi.cs`; shared `RoadGraph.cs`, `RoadFollower.cs`, `HoverBoost.cs` |
| Bot decisions, loot, teams, vehicles and grenades | `TabgInstaller.FakePlayers/AiDummy*.cs`, `BotTactics.cs`, `BotLootIndex.cs`, `BotGrenades.cs`, `BotRescue.cs`; server `BotVehicleBrain.cs` |
| Bot movement repairs in progress | `AiDummyLocomotion.cs`, `AiDummyDetour.cs`, `BotNavigationTest.cs`; shared `BotRecoveryState.cs`, `BotGridSearch.cs` |
| Radar team colors and state | `TabgInstaller.AdminRadar.Client`, `TabgInstaller.AdminRadar.Server` |
| Pure rules and executable assertions | `TabgInstaller.Vehicles.Shared`, `TabgInstaller.Vehicles.Tests` |

Client/server entries abbreviated above are under `TabgInstaller.FlyingControls` and `TabgInstaller.UnusedVehicles` respectively. The installer also handles the newer Windows/Proton Doorstop configuration. MatchCore excludes bots from initial human loadouts and human heal-on-kill handling.

## Build and test

Use the .NET 8 SDK. The framework-independent checks can run without a game installation:

```sh
dotnet run --project TabgInstaller.Vehicles.Tests -c Release
```

The flight/hover projects additionally need `EasyRoads3Dv3.dll` from the matching, locally installed TABG client/server. It is not added to this PR. The defaults look in each project's `Libs` folder; alternatively pass the appropriate Managed directory:

```sh
dotnet build TabgInstaller.FakePlayers -c Release
dotnet build TabgInstaller.FlyingControls -c Release -p:TabgClientManagedDir="/path/to/TotallyAccurateBattlegrounds_Data/Managed"
dotnet build TabgInstaller.UnusedVehicles -c Release -p:TabgServerManagedDir="/path/to/TABG_Data/Managed"
dotnet build TabgInstaller.AdminRadar.Client -c Release
dotnet build TabgInstaller.AdminRadar.Server -c Release
dotnet build TabgInstaller.MatchCore -c Release
```

These builds copy their plugin output into `bundled`. That generated output is separate from the source-only review commit. A vanilla CI checkout needs the additional native reference for a full flight/server build; the pure-rule test does not.

## Test context and open review points

Previous local tests exercised native server scenes, collision geometry, two bot teams at Area, and network/assembly compatibility. Those historical checks are useful evidence, not a guarantee that every gameplay feature is complete. No private server logs, player identifiers, local permissions or test-server settings are included.

In the most recent movement run, fourteen synthetic geometry checks passed. Tree, building-corner and floor cases reached their requested destinations; two older slope/rock cases used substitute targets. The substitute-target success test needs review: a no-target fallback can resolve to the current position, and the diagnostic currently lacks a separate explicit flag excluding that case from success. Do not count every logged PASS as proof that the original requested point was reached. Keep this PR a draft until that diagnostic and longer live movement tests are completed.

Additional areas for live review include collision behavior on slopes/ledges, abandoning unreachable loot, multiplayer hover authority with a passenger, native bot shot presentation, and flight/missile/FPV balancing. The native grappling-hook projectile is excluded from the bot combat-weapon catalog.

`/bottest` adds an unarmed individual, `/bottest team` adds a three-bot test alliance, and `/bottest navigation` starts targeted movement diagnostics after the player has dropped. Observer immunity is opt-in through `Testing.IgnoredPlayerNames`; the source default is empty. Normal automatic bot counts and personal server settings are not changed by this review snapshot.

The FPV control reference attribution is included in `THIRD-PARTY-LICENSES`. SAIN was a design reference in the development discussion; this PR does not import SAIN as a runtime dependency.

## Fresh review-branch validation (2026-09-19)

- `TabgInstaller.Vehicles.Tests`: 63,683 assertions passed.
- Release builds of FakePlayers, FlyingControls, UnusedVehicles, AdminRadar.Client, AdminRadar.Server and MatchCore: zero compilation errors; analyzer warnings remain.
- Flight/server builds used the explicit native Managed-directory properties above. A complete solution/GUI build and GitHub-hosted CI were not claimed as passing.
- All five procedural WAVs regenerated byte-for-byte from the included generator.
- `git diff --cached --check` passed.

The source-only review does not change the running installation, and it does not complete the pending movement-repair acceptance test.
