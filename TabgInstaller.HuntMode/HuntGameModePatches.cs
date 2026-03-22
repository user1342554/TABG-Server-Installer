using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Landfall.Network;
using Landfall.Network.GameModes;
using UnityEngine;
using TabgInstaller.HuntMode.Shared;

namespace TabgInstaller.HuntMode
{
    // =========================================================================
    // Task 8 -- BattleRoyaleGameMode patches
    // =========================================================================

    // -------------------------------------------------------------------------
    // Patch 1: ServerClient.Awake -- Postfix
    // Captures the ServerClient reference so other patches can access it.
    // -------------------------------------------------------------------------
    [HarmonyPatch(typeof(ServerClient), "Awake")]
    public static class CaptureServerPatch
    {
        public static void Postfix(ServerClient __instance)
        {
            HuntModePlugin.ServerRef = __instance;
            HuntModePlugin.Log("ServerClient.Awake captured.");
        }
    }

    // -------------------------------------------------------------------------
    // Patch 2: BattleRoyaleGameMode.Run -- Postfix
    // Main tick loop: timers, crate interaction, bleedout, escape, tracker, etc.
    // -------------------------------------------------------------------------
    [HarmonyPatch(typeof(BattleRoyaleGameMode), "Run")]
    public static class HuntRunPatch
    {
        private const float MatchStateBroadcastInterval = 2f;
        private static float _lastMatchStateBroadcast;

        private static bool _resolved;
        private static PropertyInfo _playerIndexProp;
        private static FieldInfo _isAliveFlagField;
        private static FieldInfo _inCarSeatField;
        private static MethodInfo _killPlayerMethod;
        private static MethodInfo _handlePlayerDead;
        private static MethodInfo _getPositionMethod;

        private static void EnsureResolved()
        {
            if (_resolved) return;
            _resolved = true;

            System.Type playerType = AccessTools.TypeByName("TABGPlayerBase");
            if (playerType != null)
            {
                _playerIndexProp  = AccessTools.Property(playerType, "PlayerIndex");
                _isAliveFlagField = AccessTools.Field(playerType, "IsAlive");
                if (_isAliveFlagField == null)
                    _isAliveFlagField = AccessTools.Field(playerType, "isAlive");
                _inCarSeatField = AccessTools.Field(playerType, "InCarSeat");
                if (_inCarSeatField == null)
                    _inCarSeatField = AccessTools.Field(playerType, "inCarSeat");
                _getPositionMethod = AccessTools.Method(playerType, "GetPosition");
            }

            System.Type roomType = AccessTools.TypeByName("Landfall.Network.GameRoom");
            if (roomType != null)
                _killPlayerMethod = AccessTools.Method(roomType, "KillPlayer");

            System.Type gmType = AccessTools.TypeByName("Landfall.Network.GameModes.TABGBaseGameMode");
            if (gmType != null)
                _handlePlayerDead = AccessTools.Method(gmType, "HandlePlayerDead");
        }

        private static byte GetPlayerIndex(object player)
        {
            EnsureResolved();
            if (_playerIndexProp != null)
                return (byte)_playerIndexProp.GetValue(player, null);
            return byte.MaxValue;
        }

        private static bool IsPlayerAlive(object player)
        {
            EnsureResolved();
            if (_isAliveFlagField != null)
            {
                object val = _isAliveFlagField.GetValue(player);
                if (val is bool b) return b;
            }
            return true;
        }

        private static bool IsInCar(object player)
        {
            EnsureResolved();
            if (_inCarSeatField != null)
                return _inCarSeatField.GetValue(player) != null;
            return false;
        }

        private static Vector3 GetPlayerPosition(object player)
        {
            EnsureResolved();
            if (_getPositionMethod != null)
            {
                try { return (Vector3)_getPositionMethod.Invoke(player, null); }
                catch { }
            }
            Component comp = player as Component;
            if (comp != null) return comp.transform.position;
            return Vector3.zero;
        }

        private static void KillPlayer(ServerClient server, object player)
        {
            EnsureResolved();
            if (_killPlayerMethod == null || player == null) return;
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

        private static void CallHandlePlayerDead(ServerClient server, object player)
        {
            EnsureResolved();
            if (_handlePlayerDead == null) return;
            object gm = server.GameRoomReference.CurrentGameMode;
            if (gm == null) return;
            try
            {
                ParameterInfo[] parms = _handlePlayerDead.GetParameters();
                if (parms.Length == 1)
                    _handlePlayerDead.Invoke(gm, new object[] { player });
                else if (parms.Length >= 2)
                    _handlePlayerDead.Invoke(gm, new object[] { player, server });
            }
            catch (Exception ex)
            {
                HuntModePlugin.LogWarning("HandlePlayerDead error: " + ex.Message);
            }
        }

        private static void EndMatch(ServerClient server, HuntGameState gs, bool killerWins)
        {
            gs.MatchActive = false;
            HuntModePlugin.Log(killerWins ? "Match ended: Killer wins!" : "Match ended: Survivors win!");
            byte[] data = gs.SerializeMatchState(Time.time);
            server.SendMessageToClients((EventCode)HuntEventCodes.MatchState, data, byte.MaxValue, true, false);
        }

        public static void Postfix(BattleRoyaleGameMode __instance)
        {
            ServerClient server = HuntModePlugin.ServerRef;
            if (server == null) return;

            HuntGameState gs = HuntModePlugin.GameState;
            if (gs == null || !gs.MatchActive) return;

            EnsureResolved();
            float now = Time.time;

            // 1. Match timer check
            float matchEnd = gs.MatchStartTime + HuntModePlugin.MatchDuration.Value;
            if (now >= matchEnd)
            {
                HuntModePlugin.Log("Match timer expired -- killer wins by time limit.");
                EndMatch(server, gs, killerWins: true);
                return;
            }

            // 2. Crate interaction tick
            HuntCrateSystem.TickCrateInteraction(server);

            // 3. Bleedout timers -- check downed survivors
            var toKill = new List<byte>();
            foreach (var kvp in gs.DownTimestamps)
            {
                if (now - kvp.Value >= gs.GetBleedoutTime(kvp.Key))
                    toKill.Add(kvp.Key);
            }
            foreach (byte idx in toKill)
            {
                gs.DownTimestamps.Remove(idx);
                object victim = FindPlayer(server, idx);
                if (victim != null)
                {
                    KillPlayer(server, victim);
                    CallHandlePlayerDead(server, victim);
                    HuntModePlugin.Log(string.Format("Survivor {0} bled out.", idx));
                }
            }

            // 4. Escape detection (vehicle must be unlocked)
            if (gs.VehicleUnlocked)
            {
                Vector3 zoneCenter    = HuntModePlugin.GetZoneCenter();
                float zoneRadius      = HuntModePlugin.ZoneRadius.Value;
                float escapeThreshold = zoneRadius + HuntConstants.EscapeDistancePastRing;

                foreach (var player in server.GameRoomReference.Players)
                {
                    byte idx = GetPlayerIndex(player);
                    if (!gs.IsSurvivor(idx)) continue;
                    if (!IsPlayerAlive(player)) continue;
                    if (gs.EscapedPlayers.Contains(idx)) continue;
                    if (!IsInCar(player)) continue;

                    Vector3 pos = GetPlayerPosition(player);
                    float dist  = Vector3.Distance(
                        new Vector3(pos.x, zoneCenter.y, pos.z),
                        new Vector3(zoneCenter.x, zoneCenter.y, zoneCenter.z));
                    if (dist > escapeThreshold)
                    {
                        gs.EscapedPlayers.Add(idx);
                        HuntModePlugin.Log(string.Format("Survivor {0} has escaped!", idx));
                        if (gs.EscapeGraceStartTime < 0f)
                            gs.EscapeGraceStartTime = now;
                    }
                }

                if (gs.EscapeGraceStartTime >= 0f &&
                    now - gs.EscapeGraceStartTime >= HuntConstants.PostEscapeGracePeriod)
                {
                    HuntModePlugin.Log(string.Format("Escape grace period over -- survivors win (escaped: {0}).", gs.EscapedPlayers.Count));
                    EndMatch(server, gs, killerWins: false);
                    return;
                }
            }

            // 5. Tracker perk -- every 30s, ping nearest alive survivor to killer
            if (gs.HasKillerPerk(KillerPerk.Tracker))
            {
                if (now - gs.LastTrackerPingTime >= HuntConstants.TrackerInterval)
                {
                    gs.LastTrackerPingTime = now;
                    SendTrackerPing(server, gs);
                }
            }

            // 6. Sprinter perk expiry
            var expiredSprinters = new List<byte>();
            foreach (var kvp in gs.SprinterActiveTimes)
            {
                if (now - kvp.Value >= HuntConstants.SprinterDuration)
                    expiredSprinters.Add(kvp.Key);
            }
            foreach (byte idx in expiredSprinters)
            {
                gs.SprinterActiveTimes.Remove(idx);
                gs.SprinterCooldownTimes[idx] = now;
                object player = FindPlayer(server, idx);
                if (player != null)
                    HuntPerkEffects.ApplySurvivorSpeedRestore(player);
            }

            // 7. All-dead check -- if every survivor is dead, killer wins
            bool anySurvivorAlive = false;
            foreach (var player in server.GameRoomReference.Players)
            {
                byte idx = GetPlayerIndex(player);
                if (!gs.IsSurvivor(idx)) continue;
                if (IsPlayerAlive(player)) { anySurvivorAlive = true; break; }
            }
            if (!anySurvivorAlive && gs.SurvivorIndices.Count > 0)
            {
                HuntModePlugin.Log("All survivors dead -- killer wins!");
                EndMatch(server, gs, killerWins: true);
                return;
            }

            // 8. Periodic match state broadcast (~every 2 seconds)
            if (now - _lastMatchStateBroadcast >= MatchStateBroadcastInterval)
            {
                _lastMatchStateBroadcast = now;
                byte[] matchData = gs.SerializeMatchState(now);
                server.SendMessageToClients((EventCode)HuntEventCodes.MatchState, matchData, byte.MaxValue, true, false);
            }
        }

        private static object FindPlayer(ServerClient server, byte playerIndex)
        {
            foreach (var p in server.GameRoomReference.Players)
            {
                if (GetPlayerIndex(p) == playerIndex)
                    return p;
            }
            return null;
        }

        private static void SendTrackerPing(ServerClient server, HuntGameState gs)
        {
            object killerPlayer = FindPlayer(server, gs.KillerIndex);
            if (killerPlayer == null) return;
            Vector3 killerPos = GetPlayerPosition(killerPlayer);

            byte nearestIdx   = byte.MaxValue;
            float nearestDist = float.MaxValue;
            Vector3 nearestPos = Vector3.zero;

            foreach (var player in server.GameRoomReference.Players)
            {
                byte idx = GetPlayerIndex(player);
                if (!gs.IsSurvivor(idx)) continue;
                if (!IsPlayerAlive(player)) continue;
                Vector3 pos = GetPlayerPosition(player);
                float dist  = Vector3.Distance(killerPos, pos);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestIdx  = idx;
                    nearestPos  = pos;
                }
            }

            if (nearestIdx == byte.MaxValue) return;

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(nearestIdx);
                bw.Write(nearestPos.x);
                bw.Write(nearestPos.y);
                bw.Write(nearestPos.z);
                server.SendMessageToClients(
                    (EventCode)HuntEventCodes.TrackerPing,
                    ms.ToArray(),
                    gs.KillerIndex,
                    true,
                    false);
            }
            HuntModePlugin.Log(string.Format("Tracker ping: nearest survivor={0} at {1}", nearestIdx, nearestPos));
        }
    }

    // -------------------------------------------------------------------------
    // Patch 3: BattleRoyaleGameMode.GetNewSpawnPoint -- Prefix
    // Killer spawns at zone center; survivors spawn at zone edge (radius * 0.8).
    // -------------------------------------------------------------------------
    [HarmonyPatch(typeof(BattleRoyaleGameMode), "GetNewSpawnPoint")]
    public static class HuntSpawnPatch
    {
        public static bool Prefix(byte playerIndex, ref object __result)
        {
            HuntGameState gs = HuntModePlugin.GameState;
            if (gs == null || !gs.MatchActive) return true;

            Vector3 spawnPos;
            if (gs.IsKiller(playerIndex))
            {
                spawnPos = HuntModePlugin.GetZoneCenter();
            }
            else
            {
                float radius   = HuntModePlugin.ZoneRadius.Value * 0.8f;
                float angle    = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                Vector3 center = HuntModePlugin.GetZoneCenter();
                spawnPos = new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y,
                    center.z + Mathf.Sin(angle) * radius);
            }

            __result = TryBuildSpawnPoint(spawnPos);
            return __result == null;
        }

        private static object TryBuildSpawnPoint(Vector3 position)
        {
            System.Type spType = AccessTools.TypeByName("SpawnPointWrapper");
            if (spType == null)
                spType = AccessTools.TypeByName("Landfall.Network.SpawnPointWrapper");
            if (spType == null)
            {
                HuntModePlugin.LogWarning("SpawnPointWrapper type not found -- using default spawn.");
                return null;
            }

            try
            {
                ConstructorInfo ctor1 = AccessTools.Constructor(spType, new System.Type[] { typeof(Vector3) });
                if (ctor1 != null) return ctor1.Invoke(new object[] { position });
            }
            catch { }

            try
            {
                ConstructorInfo ctor2 = AccessTools.Constructor(spType, new System.Type[] { typeof(Vector3), typeof(Quaternion) });
                if (ctor2 != null) return ctor2.Invoke(new object[] { position, Quaternion.identity });
            }
            catch { }

            try
            {
                ConstructorInfo ctorDefault = AccessTools.Constructor(spType, new System.Type[] { });
                if (ctorDefault != null)
                {
                    object sp = ctorDefault.Invoke(new object[] { });
                    FieldInfo posField = AccessTools.Field(spType, "Position");
                    if (posField == null) posField = AccessTools.Field(spType, "position");
                    if (posField != null) posField.SetValue(sp, position);
                    PropertyInfo posProp = AccessTools.Property(spType, "Position");
                    if (posProp == null) posProp = AccessTools.Property(spType, "position");
                    if (posProp != null)
                    {
                        MethodInfo setter = posProp.GetSetMethod(true);
                        if (setter != null) setter.Invoke(sp, new object[] { position });
                    }
                    return sp;
                }
            }
            catch { }

            HuntModePlugin.LogWarning("Could not construct SpawnPointWrapper -- using default spawn.");
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Patch 4: BattleRoyaleGameMode.HandlePlayerLeave -- Postfix
    // Killer disconnect: survivors win. Survivor disconnect: remove from list.
    // -------------------------------------------------------------------------
    [HarmonyPatch(typeof(BattleRoyaleGameMode), "HandlePlayerLeave")]
    public static class HuntPlayerLeavePatch
    {
        public static void Postfix(byte playerIndex)
        {
            ServerClient server = HuntModePlugin.ServerRef;
            HuntGameState gs    = HuntModePlugin.GameState;
            if (gs == null || !gs.MatchActive || server == null) return;

            if (gs.IsKiller(playerIndex))
            {
                HuntModePlugin.Log(string.Format("Killer (player {0}) disconnected -- survivors win.", playerIndex));
                gs.MatchActive = false;
                byte[] data = gs.SerializeMatchState(Time.time);
                server.SendMessageToClients((EventCode)HuntEventCodes.MatchState, data, byte.MaxValue, true, false);
            }
            else if (gs.IsSurvivor(playerIndex))
            {
                gs.SurvivorIndices.Remove(playerIndex);
                gs.DownTimestamps.Remove(playerIndex);
                HuntModePlugin.Log(string.Format("Survivor {0} disconnected -- removed from match.", playerIndex));
            }
        }
    }

    // -------------------------------------------------------------------------
    // Patch 5: BattleRoyaleGameMode.ValidateLootPickup -- Prefix
    // Allow all pickups for now; weapon filtering can be added later.
    // -------------------------------------------------------------------------
    [HarmonyPatch(typeof(BattleRoyaleGameMode), "ValidateLootPickup")]
    public static class HuntLootPickupPatch
    {
        public static bool Prefix(ref bool __result)
        {
            HuntGameState gs = HuntModePlugin.GameState;
            if (gs == null || !gs.MatchActive) return true;
            __result = true;
            return false;
        }
    }
}
