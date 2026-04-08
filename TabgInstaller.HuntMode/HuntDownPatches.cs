using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;
using TabgInstaller.HuntMode.Shared;

namespace TabgInstaller.HuntMode
{
    // =========================================================================
    // Task 4 — Down / Revive patches
    // =========================================================================

    // -------------------------------------------------------------------------
    // Patch 1: PlayerDeadWithDownBehaviourCommand.Run — full replacement Prefix
    // -------------------------------------------------------------------------
    [HarmonyPatch(typeof(PlayerDeadWithDownBehaviourCommand), "Run")]
    public static class HuntDownBehaviourPatch
    {
        // Cached reflection handles (resolved once, lazily)
        private static bool _resolved;
        private static MethodInfo _downMethod;           // TABGPlayerBase.Down()
        private static MethodInfo _killPlayerMethod;     // GameRoom.KillPlayer(TABGPlayerBase, ...)
        private static MethodInfo _handlePlayerDead;     // TABGBaseGameMode.HandlePlayerDead(...)
        private static PropertyInfo _playerIndexProp;
        private static FieldInfo _inVehicleField;        // TABGPlayerBase field for vehicle seat
        private static MethodInfo _ejectFromVehicle;     // Car/vehicle eject method

        private static void EnsureResolved()
        {
            if (_resolved) return;
            _resolved = true;

            System.Type playerType = AccessTools.TypeByName("TABGPlayerBase");
            if (playerType != null)
            {
                _playerIndexProp = AccessTools.Property(playerType, "PlayerIndex");
                _downMethod      = AccessTools.Method(playerType, "Down");
                // TABGPlayerBase.InCarSeat or similar
                _inVehicleField  = AccessTools.Field(playerType, "InCarSeat");
                if (_inVehicleField == null)
                    _inVehicleField = AccessTools.Field(playerType, "inCarSeat");
            }

            System.Type gmType = AccessTools.TypeByName("Landfall.Network.GameModes.TABGBaseGameMode");
            if (gmType != null)
                _handlePlayerDead = AccessTools.Method(gmType, "HandlePlayerDead");

            System.Type roomType = AccessTools.TypeByName("Landfall.Network.GameRoom");
            if (roomType != null)
                _killPlayerMethod = AccessTools.Method(roomType, "KillPlayer");
        }

        private static byte GetPlayerIndex(object player)
        {
            if (_playerIndexProp != null)
                return (byte)_playerIndexProp.GetValue(player, null);
            return byte.MaxValue;
        }

        private static void KillPlayer(object player)
        {
            ServerClient server = HuntModePlugin.ServerRef;
            if (server == null || player == null) return;
            if (_killPlayerMethod == null) return;
            // KillPlayer(TABGPlayerBase player) or (TABGPlayerBase player, ServerClient server)
            ParameterInfo[] parms = _killPlayerMethod.GetParameters();
            if (parms.Length == 1)
                _killPlayerMethod.Invoke(server.GameRoomReference, new object[] { player });
            else if (parms.Length >= 2)
                _killPlayerMethod.Invoke(server.GameRoomReference, new object[] { player, server });
        }

        private static void CallHandlePlayerDead(object player)
        {
            ServerClient server = HuntModePlugin.ServerRef;
            if (server == null || _handlePlayerDead == null) return;
            object gm = server.GameRoomReference.CurrentGameMode;
            if (gm == null) return;
            try
            {
                ParameterInfo[] parms = _handlePlayerDead.GetParameters();
                if (parms.Length == 1)
                    _handlePlayerDead.Invoke(gm, new object[] { player });
                else if (parms.Length == 2)
                    _handlePlayerDead.Invoke(gm, new object[] { player, server });
            }
            catch (Exception ex)
            {
                HuntModePlugin.LogWarning("HandlePlayerDead error: " + ex.Message);
            }
        }

        private static void BroadcastDownState(byte playerIndex)
        {
            ServerClient server = HuntModePlugin.ServerRef;
            if (server == null) return;
            byte[] data = HuntModePlugin.GameState.SerializeDownState(playerIndex);
            server.SendMessageToClients(
                (EventCode)HuntEventCodes.DownState,
                data,
                byte.MaxValue,
                true,
                false
            );
        }

        private static void BroadcastPlayerDamaged(byte victimIndex, byte killerIndex)
        {
            // Re-broadcast a PlayerDamaged event so clients see the hit
            ServerClient server = HuntModePlugin.ServerRef;
            if (server == null) return;
            // Simple: send DownState which clients use for UI; the kill/down event is separate
        }

        private static void GrantKillerAmmo()
        {
            HuntGameState gs = HuntModePlugin.GameState;
            ServerClient server = HuntModePlugin.ServerRef;
            if (server == null || gs == null) return;

            int ammo = gs.GetAmmoPerDown();
            byte[] ammoData = gs.SerializeAmmoGrant(ammo);
            server.SendMessageToClients(
                (EventCode)HuntEventCodes.DownState,
                ammoData,
                gs.KillerIndex,
                true,
                false
            );
        }

        // Prefix signature matches the struct's Run method.
        // Run(ServerClient serverClient, byte[] msgData, byte senderIndex)
        // (typical TABG command signature — we use object[] style via Harmony)
        public static bool Prefix(ServerClient serverClient, byte[] msgData, byte senderIndex)
        {
            HuntGameState gs = HuntModePlugin.GameState;
            if (gs == null || !gs.MatchActive)
                return true; // let original run

            EnsureResolved();

            // msgData layout for PlayerDeadWithDownBehaviour: [0]=victimIndex, [1]=killerIndex
            if (msgData == null || msgData.Length < 2)
                return true;

            byte victimIndex = msgData[0];
            byte killerIndex = msgData[1];

            // Only intercept if victim is a survivor
            if (!gs.IsSurvivor(victimIndex))
                return true;

            // Find victim player object
            object victimPlayer = null;
            foreach (var p in serverClient.GameRoomReference.Players)
            {
                if (GetPlayerIndex(p) == victimIndex)
                {
                    victimPlayer = p;
                    break;
                }
            }

            if (victimPlayer == null)
                return false; // absorb but can't act

            // If victim is already downed, absorb the hit (no double-down)
            bool alreadyDowned = gs.DownTimestamps.ContainsKey(victimIndex);
            if (alreadyDowned)
                return false;

            // Increment down count
            int currentDowns = gs.GetDownCount(victimIndex) + 1;
            gs.DownCounts[victimIndex] = currentDowns;

            int maxDowns = gs.GetMaxDowns(victimIndex);

            if (currentDowns >= maxDowns)
            {
                // Final death
                KillPlayer(victimPlayer);
                CallHandlePlayerDead(victimPlayer);
            }
            else
            {
                // Down the player
                if (_downMethod != null)
                {
                    try { _downMethod.Invoke(victimPlayer, null); }
                    catch (Exception ex) { HuntModePlugin.LogWarning("Down() error: " + ex.Message); }
                }

                // Record down timestamp
                gs.DownTimestamps[victimIndex] = Time.time;

                // Eject from vehicle if seated
                if (_inVehicleField != null)
                {
                    object seat = _inVehicleField.GetValue(victimPlayer);
                    if (seat != null)
                    {
                        MethodInfo eject = AccessTools.Method(seat.GetType(), "RemovePlayerFromSeat");
                        if (eject == null)
                            eject = AccessTools.Method(seat.GetType(), "Eject");
                        if (eject != null)
                        {
                            try { eject.Invoke(seat, new object[] { victimPlayer }); }
                            catch (Exception ex) { HuntModePlugin.LogWarning($"Seat eject failed: {ex.Message}"); }
                        }
                    }
                }
            }

            // Grant killer ammo on any down
            if (gs.IsKiller(killerIndex))
                GrantKillerAmmo();

            // Broadcast down state to all
            BroadcastDownState(victimIndex);

            return false; // skip original (suppresses team-wipe logic)
        }
    }

    // -------------------------------------------------------------------------
    // Patch 2: ReviveStateCommand.Run — Prefix (validate revive timing)
    // -------------------------------------------------------------------------
    [HarmonyPatch(typeof(ReviveStateCommand), "Run")]
    public static class HuntReviveStatePatch
    {
        // ReviveState values known from TABG decompile:
        //   0 = Start, 1 = Progress, 2 = Finished, 3 = Cancelled
        private const byte ReviveStart    = 0;
        private const byte ReviveFinished = 2;
        private const float TimingTolerance = 0.5f;

        // msgData format (from spec):
        //   [0] = reviveState
        //   [1] = reviverIndex
        //   [2] = gap (unknown)
        //   [3] = targetIndex
        public static bool Prefix(ServerClient serverClient, byte[] msgData, byte senderIndex)
        {
            HuntGameState gs = HuntModePlugin.GameState;
            if (gs == null || !gs.MatchActive)
                return true;

            if (msgData == null || msgData.Length < 4)
                return true;

            byte reviveState  = msgData[0];
            byte reviverIndex = msgData[1];
            // msgData[2] is a gap byte
            byte targetIndex  = msgData[3];

            if (reviveState == ReviveStart)
            {
                // Record when this revive started
                gs.ReviveStartTimestamps[targetIndex] = Time.time;
                return true; // let original run
            }

            if (reviveState == ReviveFinished)
            {
                // Validate elapsed time
                if (!gs.ReviveStartTimestamps.TryGetValue(targetIndex, out float startTime))
                {
                    // No start recorded — reject
                    HuntModePlugin.LogWarning($"Revive Finished for {targetIndex} with no start timestamp — rejected.");
                    return false;
                }

                float elapsed  = Time.time - startTime;
                float required = gs.GetReviveTime(targetIndex, reviverIndex);

                if (elapsed < required - TimingTolerance)
                {
                    HuntModePlugin.LogWarning(
                        $"Revive too fast for player {targetIndex}: elapsed={elapsed:F2}s, required={required:F2}s — rejected.");
                    return false;
                }

                // Clean up timestamp
                gs.ReviveStartTimestamps.Remove(targetIndex);
                gs.DownTimestamps.Remove(targetIndex);

                return true; // let original finalize the revive
            }

            // Other states (progress, cancel) pass through
            return true;
        }
    }

    // -------------------------------------------------------------------------
    // Patch 3: TABGPlayerBase.Revive — Postfix (override hardcoded 15 HP)
    // -------------------------------------------------------------------------
    public static class HuntReviveHPPatch
    {
        private static bool _applied;

        /// <summary>
        /// Called by HuntModePlugin after HarmonyInstance is created.
        /// Applies a manual Postfix patch to TABGPlayerBase.Revive since
        /// the type is a MonoBehaviour and cannot be referenced at compile time.
        /// </summary>
        public static void Apply(Harmony harmony)
        {
            if (_applied) return;
            _applied = true;

            System.Type playerType = AccessTools.TypeByName("TABGPlayerBase");
            if (playerType == null)
            {
                HuntModePlugin.LogWarning("TABGPlayerBase not found — Revive HP patch skipped.");
                return;
            }

            MethodInfo reviveMethod = AccessTools.Method(playerType, "Revive");
            if (reviveMethod == null)
            {
                HuntModePlugin.LogWarning("TABGPlayerBase.Revive not found — HP patch skipped.");
                return;
            }

            MethodInfo postfix = AccessTools.Method(typeof(HuntReviveHPPatch), nameof(RevivePostfix));
            harmony.Patch(reviveMethod, postfix: new HarmonyMethod(postfix));
            HuntModePlugin.Log("TABGPlayerBase.Revive HP patch applied.");
        }

        // Postfix: after Revive() sets health to some value, we override it.
        // __instance is the TABGPlayerBase being revived.
        public static void RevivePostfix(object __instance)
        {
            HuntGameState gs = HuntModePlugin.GameState;
            if (gs == null || !gs.MatchActive) return;

            // Get player index
            PropertyInfo indexProp = AccessTools.Property(__instance.GetType(), "PlayerIndex");
            if (indexProp == null) return;
            byte playerIndex = (byte)indexProp.GetValue(__instance, null);

            if (!gs.IsSurvivor(playerIndex)) return;

            float targetHP = gs.GetReviveHP(playerIndex);

            // Try to set health via the Health property or field
            PropertyInfo healthProp = AccessTools.Property(__instance.GetType(), "Health");
            if (healthProp != null && healthProp.CanWrite)
            {
                healthProp.SetValue(__instance, targetHP, null);
                return;
            }

            // Try field
            FieldInfo healthField = AccessTools.Field(__instance.GetType(), "Health");
            if (healthField == null)
                healthField = AccessTools.Field(__instance.GetType(), "health");
            if (healthField != null)
            {
                healthField.SetValue(__instance, targetHP);
                return;
            }

            // Fallback: use TakeDamage to reduce from current HP down to target
            MethodInfo getHP = AccessTools.Method(__instance.GetType(), "GetHealth");
            MethodInfo takeDmg = AccessTools.Method(__instance.GetType(), "TakeDamage");
            if (getHP != null && takeDmg != null)
            {
                try
                {
                    float currentHP = (float)getHP.Invoke(__instance, null);
                    float delta = currentHP - targetHP;
                    if (delta > 0f)
                        takeDmg.Invoke(__instance, new object[] { delta, __instance, false });
                }
                catch (Exception ex)
                {
                    HuntModePlugin.LogWarning("ReviveHP TakeDamage fallback error: " + ex.Message);
                }
            }
        }
    }
}
