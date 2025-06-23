using Landfall.Network.GameModes;
using Landfall.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TabgInstaller.StarterPack
{
    internal class MatchTimeout
    {
        public static float timer = 0;
        public static float elapsed = 0;
        public static float minutesPass = 0;

        public static void Run(GameState state, TABGBaseGameMode __instance)
        {
            MatchTimeout.timer += Time.unscaledDeltaTime;
            MatchTimeout.elapsed += Time.unscaledDeltaTime;

            FieldInfo field = typeof(TABGBaseGameMode).GetField("m_GameRoom", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                object value = field.GetValue(__instance);
                GameRoom m_GameRoom = (GameRoom)value;
                if (state == GameState.WaitingForPlayers)
                {

                    if (MatchTimeout.timer >= (Config.preMatchTimeout * 60) + 30)
                    {
                        Plugin.Log?.LogInfo("Lobby Timed Out");

                        m_GameRoom.EndMatch(null);
                        m_GameRoom.ChangeGameState(GameState.Ended, false, null);
                    }
                }
                if (state == GameState.Started)
                {
                    if (MatchTimeout.elapsed >= 60)
                    {
                        MatchTimeout.minutesPass++;
                        MatchTimeout.elapsed = 0;

                        Plugin.Log?.LogInfo($"[MatchTimer] {Config.periMatchTimer - MatchTimeout.minutesPass}/{Config.periMatchTimer} minutes remaining");
                    }

                    if (MatchTimeout.timer >= (Config.periMatchTimer * 60))
                    {
                        KeyValuePair<byte, int> highestKillingTeam = m_GameRoom.CurrentGameKills.GetHighestKillingTeam();

                        Plugin.Log?.LogInfo("Match Timed Out");
                        foreach (TABGPlayerServer player in m_GameRoom.Players)
                        {
                            Plugin.Log?.LogInfo($"{player.PlayerName} : {player.NumberOfKills}");
                        }

                        m_GameRoom.EndMatch(m_GameRoom.CurrentGameStats.GetTeam(highestKillingTeam.Key));
                        m_GameRoom.ChangeGameState(GameState.Ended, false, null);
                    }
                }
            }
        }

        public static void ResetTimer()
        {
            MatchTimeout.timer = 0;
            MatchTimeout.elapsed = 0;
            MatchTimeout.minutesPass = 0;
        }
    }
}
