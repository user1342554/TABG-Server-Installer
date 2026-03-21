using System.Reflection;
using BepInEx;
using HarmonyLib;
using Landfall.Network;
using Landfall.Network.GameModes;
using UnityEngine;

namespace TabgInstaller.SoloTesting
{
    /// <summary>
    /// Server plugin for solo testing:
    /// 1. Forces match to start with just 1 player
    /// 2. Prevents "You Win" when you're the only player
    /// </summary>
    [BepInPlugin("tabginstaller.solotesting", "TABG Solo Testing", "1.0.0")]
    public class SoloTestingPlugin : BaseUnityPlugin
    {
        private static bool _countdownStarted = false;

        private void Awake()
        {
            var harmony = new Harmony("tabginstaller.solotesting");
            harmony.PatchAll(typeof(SoloCheckGameStatePatch));
            Logger.LogInfo("[SoloTesting] Solo testing mode loaded.");
        }

        /// <summary>
        /// Replaces CheckGameState entirely for solo play:
        /// - WaitingForPlayers: start countdown with 1 player
        /// - Started: skip win check when 1 player
        /// </summary>
        [HarmonyPatch(typeof(BattleRoyaleGameMode), "CheckGameState")]
        internal static class SoloCheckGameStatePatch
        {
            static bool Prefix(BattleRoyaleGameMode __instance, GameState state)
            {
                var roomField = typeof(TABGBaseGameMode).GetField("m_GameRoom",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var room = roomField?.GetValue(__instance) as GameRoom;
                if (room == null) return true;

                int playerCount = room.GetNumberOfPlayers();

                switch (state)
                {
                    case GameState.WaitingForPlayers:
                        // Force start with 1 player
                        if (playerCount >= 1 && !_countdownStarted)
                        {
                            _countdownStarted = true;
                            room.StartCountDown();
                            LandLog.Log("[SoloTesting] Starting countdown with " + playerCount + " player(s)");
                            return false;
                        }
                        // Still run original for force-start timer etc
                        return true;

                    case GameState.Started:
                        // Block win condition when solo
                        if (room.Players.Count <= 1)
                            return false;
                        return true;

                    default:
                        return true;
                }
            }
        }
    }
}
