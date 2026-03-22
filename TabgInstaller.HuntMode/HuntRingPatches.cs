using System;
using System.Reflection;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;
using TabgInstaller.HuntMode.Shared;

namespace TabgInstaller.HuntMode
{
    // =========================================================================
    // Task 5 — Ring patches
    // =========================================================================

    // -------------------------------------------------------------------------
    // Patch 1: TheRing.NextRing — Prefix to freeze ring while match is active.
    // TheRing is a MonoBehaviour, so we patch it manually at startup.
    // -------------------------------------------------------------------------
    public static class HuntRingFreezePatch
    {
        private static bool _applied;

        public static void Apply(Harmony harmony)
        {
            if (_applied) return;
            _applied = true;

            System.Type ringType = AccessTools.TypeByName("TheRing");
            if (ringType == null)
            {
                HuntModePlugin.LogWarning("TheRing type not found — ring freeze patch skipped.");
                return;
            }

            MethodInfo nextRing = AccessTools.Method(ringType, "NextRing");
            if (nextRing == null)
            {
                HuntModePlugin.LogWarning("TheRing.NextRing not found — ring freeze patch skipped.");
                return;
            }

            MethodInfo prefix = AccessTools.Method(typeof(HuntRingFreezePatch), nameof(NextRingPrefix));
            harmony.Patch(nextRing, prefix: new HarmonyMethod(prefix));
            HuntModePlugin.Log("TheRing.NextRing freeze patch applied.");
        }

        public static bool NextRingPrefix()
        {
            HuntGameState gs = HuntModePlugin.GameState;
            if (gs != null && gs.MatchActive)
            {
                // Block ring progression — keep current ring frozen
                return false;
            }
            return true; // let original run when match is not active
        }
    }

    // -------------------------------------------------------------------------
    // Patch 2: PlayerRingDeathCommand.Run — full replacement Prefix
    // -------------------------------------------------------------------------
    [HarmonyPatch(typeof(PlayerRingDeathCommand), "Run")]
    public static class HuntRingDeathPatch
    {
        private static bool _resolved;
        private static PropertyInfo _playerIndexProp;
        private static MethodInfo _downMethod;
        private static MethodInfo _killPlayerMethod;
        private static MethodInfo _handlePlayerDead;
        private static FieldInfo _inVehicleField;

        private static void EnsureResolved()
        {
            if (_resolved) return;
            _resolved = true;

            System.Type playerType = AccessTools.TypeByName("TABGPlayerBase");
            if (playerType != null)
            {
                _playerIndexProp = AccessTools.Property(playerType, "PlayerIndex");
                _downMethod      = AccessTools.Method(playerType, "Down");
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

        private static bool IsInUnlockedVehicle(object player)
        {
            HuntGameState gs = HuntModePlugin.GameState;
            if (gs == null || !gs.VehicleUnlocked) return false;
            if (_inVehicleField == null) return false;
            object seat = _inVehicleField.GetValue(player);
            return seat != null;
        }

        private static void KillPlayer(object player)
        {
            ServerClient server = HuntModePlugin.ServerRef;
            if (server == null || player == null || _killPlayerMethod == null) return;
            try
            {
                ParameterInfo[] parms = _killPlayerMethod.GetParameters();
                if (parms.Length == 1)
                    _killPlayerMethod.Invoke(server.GameRoomReference, new object[] { player });
                else if (parms.Length >= 2)
                    _killPlayerMethod.Invoke(server.GameRoomReference, new object[] { player, server });
            }
            catch (Exception ex)
            {
                HuntModePlugin.LogWarning("KillPlayer error: " + ex.Message);
            }
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

        // msgData layout: [0] = playerIndex (ring victim)
        public static bool Prefix(ServerClient serverClient, byte[] msgData, byte senderIndex)
        {
            HuntGameState gs = HuntModePlugin.GameState;
            if (gs == null || !gs.MatchActive)
                return true; // let original run

            EnsureResolved();

            if (msgData == null || msgData.Length < 1)
                return true;

            byte victimIndex = msgData[0];

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
                return false;

            // --- Case: victim is in an unlocked escape vehicle ---
            if (IsInUnlockedVehicle(victimPlayer))
            {
                // Escape attempt — ignore ring death
                return false;
            }

            // --- Case: victim is the killer ---
            if (gs.IsKiller(victimIndex))
            {
                KillPlayer(victimPlayer);
                CallHandlePlayerDead(victimPlayer);
                HuntModePlugin.Log("Killer died to the ring — Survivors win!");
                gs.MatchActive = false;
                return false;
            }

            // --- Case: victim is a survivor ---
            if (gs.IsSurvivor(victimIndex))
            {
                bool isDowned = gs.DownTimestamps.ContainsKey(victimIndex);

                if (isDowned)
                {
                    // Already downed — ring kills them permanently
                    KillPlayer(victimPlayer);
                    CallHandlePlayerDead(victimPlayer);
                    gs.DownTimestamps.Remove(victimIndex);
                    BroadcastDownState(victimIndex);
                    return false;
                }

                // Standing survivor hit by ring — apply down logic
                int currentDowns = gs.GetDownCount(victimIndex) + 1;
                gs.DownCounts[victimIndex] = currentDowns;
                int maxDowns = gs.GetMaxDowns(victimIndex);

                if (currentDowns >= maxDowns)
                {
                    KillPlayer(victimPlayer);
                    CallHandlePlayerDead(victimPlayer);
                }
                else
                {
                    if (_downMethod != null)
                    {
                        try { _downMethod.Invoke(victimPlayer, null); }
                        catch (Exception ex) { HuntModePlugin.LogWarning("Down() error: " + ex.Message); }
                    }
                    gs.DownTimestamps[victimIndex] = Time.time;
                }

                BroadcastDownState(victimIndex);
                return false;
            }

            // Unknown player — let original handle
            return true;
        }
    }
}
