using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;
using TabgInstaller.HuntMode.Shared;

namespace TabgInstaller.HuntMode
{
    /// <summary>
    /// Task 7 — Hunt crate system. Manages crate positions, looting interactions,
    /// and per-crate rewards. Called from the BattleRoyaleGameMode.Run postfix tick.
    /// </summary>
    public static class HuntCrateSystem
    {
        // Default crate positions — spread around the zone center.
        // These are overridden by config if added; for now they are hardcoded defaults.
        private static readonly Vector3[] DefaultCratePositions = new Vector3[]
        {
            new Vector3(-250f, 132f, -205f), // Crate 0 (index) / reward 1
            new Vector3(-191f, 132f, -280f), // Crate 1 / reward 2
            new Vector3(-120f, 132f, -220f), // Crate 2 / reward 3
            new Vector3(-191f, 132f, -130f), // Crate 3 / reward 4
            new Vector3(-310f, 132f, -150f), // Crate 4 / reward 5
        };

        // Per-player loot-start timestamps: key = playerIndex, value = (Time.time when they started)
        private static Dictionary<byte, float> _lootStartTimes = new Dictionary<byte, float>();
        // Per-player the crate index they are currently looting
        private static Dictionary<byte, int> _lootingCrateIndex = new Dictionary<byte, int>();
        // Active crate positions (set on SpawnCrates, can be updated by config later)
        private static Vector3[] _cratePositions;

        // Reflection cache
        private static bool _reflectionResolved;
        private static PropertyInfo _playerIndexProp;
        private static PropertyInfo _isDownedProp;
        private static FieldInfo _isAliveFlagField;
        private static MethodInfo _takeDamageMethod;
        private static MethodInfo _getPositionMethod;   // for getting player world position

        private static void EnsureReflection()
        {
            if (_reflectionResolved) return;
            _reflectionResolved = true;

            System.Type playerType = AccessTools.TypeByName("TABGPlayerBase");
            if (playerType != null)
            {
                _playerIndexProp  = AccessTools.Property(playerType, "PlayerIndex");
                _isDownedProp     = AccessTools.Property(playerType, "IsDowned");
                if (_isDownedProp == null)
                    _isDownedProp = AccessTools.Property(playerType, "Downed");
                _isAliveFlagField = AccessTools.Field(playerType, "IsAlive");
                if (_isAliveFlagField == null)
                    _isAliveFlagField = AccessTools.Field(playerType, "isAlive");
                _takeDamageMethod = AccessTools.Method(playerType, "TakeDamage");
                _getPositionMethod = AccessTools.Method(playerType, "GetPosition");
            }
        }

        private static byte GetPlayerIndex(object player)
        {
            EnsureReflection();
            if (_playerIndexProp != null)
                return (byte)_playerIndexProp.GetValue(player, null);
            return byte.MaxValue;
        }

        private static bool IsPlayerAlive(object player)
        {
            EnsureReflection();
            if (_isAliveFlagField != null)
            {
                object val = _isAliveFlagField.GetValue(player);
                if (val is bool b) return b;
            }
            return true; // assume alive if we can't tell
        }

        private static bool IsPlayerDowned(object player)
        {
            EnsureReflection();
            if (_isDownedProp != null)
            {
                try
                {
                    object val = _isDownedProp.GetValue(player, null);
                    if (val is bool b) return b;
                }
                catch (Exception ex) { HuntModePlugin.LogWarning($"IsDowned check failed: {ex.Message}"); }
            }
            // Fall back to checking DownTimestamps
            byte idx = GetPlayerIndex(player);
            return HuntModePlugin.GameState.DownTimestamps.ContainsKey(idx);
        }

        private static Vector3 GetPlayerPosition(object player)
        {
            EnsureReflection();
            if (_getPositionMethod != null)
            {
                try
                {
                    return (Vector3)_getPositionMethod.Invoke(player, null);
                }
                catch (Exception ex) { HuntModePlugin.LogWarning($"GetPosition failed: {ex.Message}"); }
            }
            // Try transform via reflection
            FieldInfo tfField = AccessTools.Field(player.GetType(), "transform");
            if (tfField != null)
            {
                Transform tf = tfField.GetValue(player) as Transform;
                if (tf != null) return tf.position;
            }
            // Try component cast
            Component comp = player as Component;
            if (comp != null) return comp.transform.position;
            return Vector3.zero;
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called at match start. Records crate positions and clears loot tracking state.
        /// </summary>
        public static void SpawnCrates(ServerClient server, HuntGameState state)
        {
            _cratePositions = DefaultCratePositions;
            _lootStartTimes.Clear();
            _lootingCrateIndex.Clear();
            state.CratesLooted = 0;
            state.LootedCrateIndices.Clear();

            HuntModePlugin.Log($"Crates spawned at {_cratePositions.Length} positions.");
        }

        /// <summary>
        /// Called every tick from the BattleRoyaleGameMode.Run postfix.
        /// For each alive, non-downed survivor: check proximity to unlooted crates.
        /// </summary>
        public static void TickCrateInteraction(ServerClient server)
        {
            HuntGameState gs = HuntModePlugin.GameState;
            if (gs == null || !gs.MatchActive) return;
            if (_cratePositions == null) return;

            EnsureReflection();

            float now = Time.time;

            foreach (var player in server.GameRoomReference.Players)
            {
                byte idx = GetPlayerIndex(player);
                if (!gs.IsSurvivor(idx)) continue;
                if (!IsPlayerAlive(player)) continue;
                if (IsPlayerDowned(player)) continue;

                Vector3 pos = GetPlayerPosition(player);

                // Find the nearest unlooted crate within 3m
                int nearestCrate = -1;
                float nearestDist = float.MaxValue;
                for (int c = 0; c < _cratePositions.Length; c++)
                {
                    if (gs.LootedCrateIndices.Contains(c)) continue;
                    float dist = Vector3.Distance(pos, _cratePositions[c]);
                    if (dist <= 3f && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestCrate = c;
                    }
                }

                if (nearestCrate >= 0)
                {
                    // Check if this player is looting the same crate they started
                    if (_lootingCrateIndex.TryGetValue(idx, out int currentCrate) && currentCrate == nearestCrate)
                    {
                        // Already tracking — check if loot is complete
                        float lootDuration = gs.GetLootTime(idx);
                        if (_lootStartTimes.TryGetValue(idx, out float startTime))
                        {
                            if (now - startTime >= lootDuration)
                            {
                                CompleteCrateLoot(server, gs, idx, nearestCrate);
                            }
                        }
                    }
                    else
                    {
                        // Start looting this crate
                        _lootStartTimes[idx]     = now;
                        _lootingCrateIndex[idx]  = nearestCrate;
                    }
                }
                else
                {
                    // Not near any crate — cancel any in-progress loot
                    _lootStartTimes.Remove(idx);
                    _lootingCrateIndex.Remove(idx);
                }
            }
        }

        /// <summary>
        /// Marks crate as looted, increments count, applies reward, broadcasts CrateUpdate.
        /// </summary>
        private static void CompleteCrateLoot(ServerClient server, HuntGameState gs, byte playerIndex, int crateIndex)
        {
            // Guard against double-loot
            if (gs.LootedCrateIndices.Contains(crateIndex)) return;

            gs.LootedCrateIndices.Add(crateIndex);
            gs.CratesLooted++;

            // Clean up loot tracking for this player
            _lootStartTimes.Remove(playerIndex);
            _lootingCrateIndex.Remove(playerIndex);

            HuntModePlugin.Log($"Crate {crateIndex + 1} looted by player {playerIndex} ({gs.CratesLooted}/{HuntConstants.TotalCrates}).");

            // Apply reward (crate number is 1-based)
            ApplyCrateReward(server, gs, crateIndex + 1);

            // Broadcast CrateUpdate to all players
            byte[] data = gs.SerializeCrateUpdate(crateIndex);
            server.SendMessageToClients(
                (EventCode)HuntEventCodes.CrateUpdate,
                data,
                byte.MaxValue,
                true,
                false
            );
        }

        /// <summary>
        /// Applies the reward for crate number (1-based).
        /// </summary>
        private static void ApplyCrateReward(ServerClient server, HuntGameState gs, int crateNumber)
        {
            switch (crateNumber)
            {
                case 1:
                    // Reveal vehicle location to all (locked)
                    SendVehicleState(server, gs, unlocked: false);
                    HuntModePlugin.Log("Crate 1: Vehicle location revealed (locked).");
                    break;

                case 2:
                    // Heal all living survivors +50 HP
                    HealAllSurvivors(server, gs, HuntConstants.CrateHealAmount);
                    HuntModePlugin.Log("Crate 2: All survivors healed +50 HP.");
                    break;

                case 3:
                    // Give each living survivor a smoke grenade
                    GiveSmokeToCrates(server, gs);
                    HuntModePlugin.Log("Crate 3: Smoke grenades distributed.");
                    break;

                case 4:
                    // Unlock escape vehicle
                    gs.VehicleUnlocked = true;
                    SendVehicleState(server, gs, unlocked: true);
                    HuntModePlugin.Log("Crate 4: Vehicle unlocked!");
                    break;

                case 5:
                    // Apply speed reduction to killer
                    ApplyCrate5ToKiller(server, gs);
                    HuntModePlugin.Log("Crate 5: Killer speed reduced.");
                    break;

                default:
                    HuntModePlugin.LogWarning($"Unknown crate number: {crateNumber}");
                    break;
            }
        }

        // -----------------------------------------------------------------------
        // Reward helpers
        // -----------------------------------------------------------------------

        private static void SendVehicleState(ServerClient server, HuntGameState gs, bool unlocked)
        {
            Vector3 vehiclePos = HuntModePlugin.GetVehicleSpawnPos();
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(unlocked);
                bw.Write(vehiclePos.x);
                bw.Write(vehiclePos.y);
                bw.Write(vehiclePos.z);
                byte[] data = ms.ToArray();
                server.SendMessageToClients(
                    (EventCode)HuntEventCodes.VehicleState,
                    data,
                    byte.MaxValue,
                    true,
                    false
                );
            }
        }

        private static void HealAllSurvivors(ServerClient server, HuntGameState gs, float healAmount)
        {
            EnsureReflection();
            foreach (var player in server.GameRoomReference.Players)
            {
                byte idx = GetPlayerIndex(player);
                if (!gs.IsSurvivor(idx)) continue;
                if (!IsPlayerAlive(player)) continue;
                if (IsPlayerDowned(player)) continue;

                // Negative damage = heal
                if (_takeDamageMethod != null)
                {
                    try
                    {
                        ParameterInfo[] parms = _takeDamageMethod.GetParameters();
                        if (parms.Length >= 1)
                            _takeDamageMethod.Invoke(player, new object[] { -healAmount, player, false });
                    }
                    catch (Exception ex)
                    {
                        HuntModePlugin.LogWarning($"HealAllSurvivors TakeDamage error: {ex.Message}");
                    }
                }
            }
        }

        private static void GiveSmokeToCrates(ServerClient server, HuntGameState gs)
        {
            foreach (var player in server.GameRoomReference.Players)
            {
                byte idx = GetPlayerIndex(player);
                if (!gs.IsSurvivor(idx)) continue;
                if (!IsPlayerAlive(player)) continue;
                if (IsPlayerDowned(player)) continue;

                GiveItem(server, player, HuntConstants.SmokeGrenadeItemId, 1);
            }
        }

        private static void ApplyCrate5ToKiller(ServerClient server, HuntGameState gs)
        {
            foreach (var player in server.GameRoomReference.Players)
            {
                byte idx = GetPlayerIndex(player);
                if (!gs.IsKiller(idx)) continue;

                HuntPerkEffects.ApplyCrate5SpeedReduction(player);
                break;
            }
        }

        /// <summary>
        /// Attempts to give an item to a player via GivePickUpCommand (Citruslib reflection).
        /// Logs a warning if the command is unavailable.
        /// </summary>
        public static void GiveItem(ServerClient server, object player, int itemId, int count)
        {
            byte playerIndex = GetPlayerIndex(player);

            // Try GivePickUpCommand (used in HuntModePlugin.StartMatch for smoke grenades)
            try
            {
                GivePickUpCommand.Run(
                    null,
                    server,
                    playerIndex,
                    new int[] { itemId },
                    new byte[] { (byte)count }
                );
                return;
            }
            catch (Exception ex)
            {
                HuntModePlugin.LogWarning($"GiveItem via GivePickUpCommand failed: {ex.Message}");
            }

            // Fallback: try Citruslib reflection
            try
            {
                System.Type citrusType = AccessTools.TypeByName("CitrusLib.ItemGiver");
                if (citrusType == null)
                    citrusType = AccessTools.TypeByName("Citruslib.ItemGiver");
                if (citrusType != null)
                {
                    MethodInfo giveMethod = AccessTools.Method(citrusType, "GiveItem");
                    if (giveMethod != null)
                    {
                        giveMethod.Invoke(null, new object[] { server, playerIndex, itemId, count });
                        return;
                    }
                }
                HuntModePlugin.LogWarning($"GiveItem: Citruslib ItemGiver not found — item {itemId} x{count} not given to player {playerIndex}.");
            }
            catch (Exception ex2)
            {
                HuntModePlugin.LogWarning($"GiveItem Citruslib fallback error: {ex2.Message}");
            }
        }
    }
}
