using Landfall.Network.GameModes;
using Landfall.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace StarterPack
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
                        LandLog.Log("Lobby Timed Out", null);

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

                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write("[");
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                        Console.Write("MatchTimer");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write("] - ");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine(string.Format("{0}/{1} minutes remaining", Config.periMatchTimer - MatchTimeout.minutesPass, Config.periMatchTimer));
                    }

                    if (MatchTimeout.timer >= (Config.periMatchTimer * 60))
                    {
                        KeyValuePair<byte, int> highestKillingTeam = m_GameRoom.CurrentGameKills.GetHighestKillingTeam();

                        LandLog.Log("Match Timed Out", null);
                        foreach (TABGPlayerServer player in m_GameRoom.Players)
                        {
                            Console.Write(string.Format("{0} : {1}", player.PlayerName, player.NumberOfKills));
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
