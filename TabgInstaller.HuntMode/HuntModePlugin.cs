using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;
using TabgInstaller.HuntMode.Shared;

namespace TabgInstaller.HuntMode
{
    [BepInPlugin("tabginstaller.huntmode", "Hunt Mode (4v1)", "1.0.0")]
    [BepInDependency("com.cyrusthelesser.citruslib", BepInDependency.DependencyFlags.SoftDependency)]
    public class HuntModePlugin : BaseUnityPlugin
    {
        public static HuntModePlugin Instance { get; private set; }
        public static HuntGameState GameState { get; private set; }
        public static Harmony HarmonyInstance { get; private set; }
        public static ServerClient ServerRef { get; set; }

        // Config entries
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> MatchDuration;
        public static ConfigEntry<float> ZoneRadius;
        public static ConfigEntry<float> ZoneCenterX;
        public static ConfigEntry<float> ZoneCenterY;
        public static ConfigEntry<float> ZoneCenterZ;
        public static ConfigEntry<float> VehicleSpawnX;
        public static ConfigEntry<float> VehicleSpawnY;
        public static ConfigEntry<float> VehicleSpawnZ;

        private void Awake()
        {
            Instance = this;
            GameState = new HuntGameState();

            Enabled = Config.Bind("HuntMode", "Enabled", true, "Enable Hunt Mode (4v1)");
            MatchDuration = Config.Bind("HuntMode", "MatchDuration", HuntConstants.MatchDuration, "Match duration in seconds");
            ZoneRadius = Config.Bind("HuntMode", "ZoneRadius", 200f, "Play zone radius");
            ZoneCenterX = Config.Bind("HuntMode", "ZoneCenterX", -191f, "Zone center X coordinate");
            ZoneCenterY = Config.Bind("HuntMode", "ZoneCenterY", 132f, "Zone center Y coordinate");
            ZoneCenterZ = Config.Bind("HuntMode", "ZoneCenterZ", -205f, "Zone center Z coordinate");
            VehicleSpawnX = Config.Bind("HuntMode", "VehicleSpawnX", -100f, "Escape vehicle spawn X");
            VehicleSpawnY = Config.Bind("HuntMode", "VehicleSpawnY", 132f, "Escape vehicle spawn Y");
            VehicleSpawnZ = Config.Bind("HuntMode", "VehicleSpawnZ", -100f, "Escape vehicle spawn Z");

            if (!Enabled.Value)
            {
                Log("Hunt Mode is disabled in config.");
                return;
            }

            HarmonyInstance = new Harmony("tabginstaller.huntmode");
            HarmonyInstance.PatchAll();

            // Apply manual patches for types that are MonoBehaviours and cannot
            // be referenced at compile time (TheRing, TABGPlayerBase.Revive).
            HuntRingFreezePatch.Apply(HarmonyInstance);
            HuntReviveHPPatch.Apply(HarmonyInstance);

            Log("Hunt Mode (4v1) loaded and patches applied.");
        }

        private void OnDestroy()
        {
            HarmonyInstance?.UnpatchSelf();
        }

        public static void Log(string message)
        {
            if (Instance != null)
                Instance.Logger.LogInfo($"[HuntMode] {message}");
        }

        public static void LogWarning(string message)
        {
            if (Instance != null)
                Instance.Logger.LogWarning($"[HuntMode] {message}");
        }

        public static Vector3 GetZoneCenter()
        {
            return new Vector3(ZoneCenterX.Value, ZoneCenterY.Value, ZoneCenterZ.Value);
        }

        public static Vector3 GetVehicleSpawnPos()
        {
            return new Vector3(VehicleSpawnX.Value, VehicleSpawnY.Value, VehicleSpawnZ.Value);
        }

        // --- Match orchestration ---

        public static void InitializeMatch(ServerClient server)
        {
            ServerRef = server;
            GameState.Reset();

            byte[] playerIndices = GetAllPlayerIndices(server);
            if (playerIndices.Length < HuntConstants.MinPlayers)
            {
                LogWarning($"Not enough players for Hunt Mode ({playerIndices.Length}/{HuntConstants.MinPlayers}).");
                return;
            }

            // First player is the killer (group 0), rest are survivors (group 1)
            GameState.KillerIndex = playerIndices[0];
            for (int i = 1; i < playerIndices.Length; i++)
            {
                GameState.SurvivorIndices.Add(playerIndices[i]);
            }

            // Assign groups via reflection (GroupIndex setter is not public)
            var groupProp = typeof(TABGPlayerBase).GetProperty("GroupIndex",
                BindingFlags.Public | BindingFlags.Instance);
            var groupSetter = groupProp?.GetSetMethod(true);
            foreach (var player in server.GameRoomReference.Players)
            {
                byte group = (byte)(player.PlayerIndex == GameState.KillerIndex ? 0 : 1);
                if (groupSetter != null)
                {
                    groupSetter.Invoke(player, new object[] { group });
                }
            }

            // Broadcast role assignment
            byte[] roleData = GameState.SerializeRoleAssignment();
            server.SendMessageToClients(
                (EventCode)HuntEventCodes.RoleAssignment,
                roleData,
                byte.MaxValue,
                true,
                false
            );

            Log($"Match initialized: Killer={GameState.KillerIndex}, Survivors={GameState.SurvivorIndices.Count}");
        }

        public static void StartMatch()
        {
            if (ServerRef == null)
            {
                LogWarning("Cannot start match: no server reference.");
                return;
            }

            GameState.MatchStartTime = Time.time;
            GameState.MatchActive = true;
            GameState.PerkSelectionComplete = true;

            // Apply perk-based stats
            HuntPerkEffects.ApplyPerkStats(ServerRef, GameState);

            // Give Smokescreen grenades to players with the perk
            foreach (byte survivorIdx in GameState.SurvivorIndices)
            {
                if (GameState.HasSurvivorPerk(survivorIdx, SurvivorPerk.Smokescreen))
                {
                    GivePickUpCommand.Run(
                        null,
                        ServerRef,
                        survivorIdx,
                        new int[] { HuntConstants.SmokeGrenadeItemId },
                        new byte[] { 2 }
                    );
                }
            }

            // Lock ring to zone center via reflection (SyncRing signature varies)
            var ring = ServerRef.SpawnedRing;
            if (ring != null)
            {
                Vector3 center = GetZoneCenter();
                float radius = ZoneRadius.Value;
                var syncMethod = ring.GetType().GetMethod("SyncRing",
                    BindingFlags.Public | BindingFlags.Instance);
                if (syncMethod != null)
                {
                    var parameters = syncMethod.GetParameters();
                    if (parameters.Length == 5)
                    {
                        syncMethod.Invoke(ring, new object[] { center, radius, center, radius, 0f });
                    }
                    else if (parameters.Length == 4)
                    {
                        syncMethod.Invoke(ring, new object[] { center, radius, center, radius });
                    }
                    else
                    {
                        LogWarning($"SyncRing has unexpected parameter count: {parameters.Length}");
                    }
                }
                else
                {
                    LogWarning("Could not find SyncRing method on ring object.");
                }
            }

            // Spawn hunt crates
            HuntCrateSystem.SpawnCrates(ServerRef, GameState);

            // Broadcast match state
            byte[] matchData = GameState.SerializeMatchState(Time.time);
            ServerRef.SendMessageToClients(
                (EventCode)HuntEventCodes.MatchState,
                matchData,
                byte.MaxValue,
                true,
                false
            );

            // Broadcast perk confirm
            ServerRef.SendMessageToClients(
                (EventCode)HuntEventCodes.PerkConfirm,
                new byte[0],
                byte.MaxValue,
                true,
                false
            );

            Log("Match started!");
        }

        public static byte[] GetAllPlayerIndices(ServerClient server)
        {
            var players = server.GameRoomReference.Players;
            var indices = new List<byte>();
            foreach (var player in players)
            {
                indices.Add(player.PlayerIndex);
            }
            return indices.ToArray();
        }
    }
}
