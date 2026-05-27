using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
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
        private static GameRoom _countdownRoom;
        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<bool> _developmentMode;
        private static ConfigEntry<int> _minimumPlayersToStart;
        private static ConfigEntry<bool> _preventSoloWinWhenAlone;
        private Harmony _harmony;

        private void Awake()
        {
            _enabled = Config.Bind("SoloTesting", "Enabled", true, "Enable solo-testing game-state patches.");
            _developmentMode = Config.Bind("Safety", "DevelopmentMode", false, "Explicitly mark this as a private development/test server. SoloTesting patches are inactive until this is true.");
            _minimumPlayersToStart = Config.Bind("SoloTesting", "MinimumPlayersToStart", 1, "Minimum players required to start countdown.");
            _preventSoloWinWhenAlone = Config.Bind("SoloTesting", "PreventSoloWinWhenAlone", true, "Prevent immediate win checks when only one player is present.");

            _harmony = new Harmony("tabginstaller.solotesting");
            _harmony.PatchAll(typeof(SoloCheckGameStatePatch));
            Logger.LogInfo("[SoloTesting] Solo testing mode loaded. Patches require Safety.DevelopmentMode=true.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            ResetCountdownState();
        }

        private static void ResetCountdownState()
        {
            _countdownStarted = false;
            _countdownRoom = null;
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
                if (_enabled != null && !_enabled.Value)
                    return true;
                if (_developmentMode == null || !_developmentMode.Value)
                    return true;

                var roomField = typeof(TABGBaseGameMode).GetField("m_GameRoom",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var room = roomField?.GetValue(__instance) as GameRoom;
                if (room == null) return true;
                if (!ReferenceEquals(_countdownRoom, room))
                {
                    _countdownRoom = room;
                    _countdownStarted = false;
                }

                int playerCount = room.GetNumberOfPlayers();

                switch (state)
                {
                    case GameState.WaitingForPlayers:
                        // Force start with the configured minimum player count.
                        int requiredPlayers = Mathf.Max(1, _minimumPlayersToStart?.Value ?? 1);
                        if (playerCount >= requiredPlayers && !_countdownStarted)
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
                        if ((_preventSoloWinWhenAlone?.Value ?? true) && room.Players.Count <= 1)
                            return false;
                        return true;

                    default:
                        return true;
                }
            }
        }
    }
}
