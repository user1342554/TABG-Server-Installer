using Landfall.Network.GameModes;
using Landfall.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace StarterPack
{
    internal class WinConditions
    {
        public static bool CheckGameState(GameState state, TABGBaseGameMode __instance)
        {
            FieldInfo field = typeof(TABGBaseGameMode).GetField("m_GameRoom", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                object value = field.GetValue(__instance);
                GameRoom m_GameRoom = (GameRoom)value;
                if (state == GameState.Started)
                {
                    if (Config.winCondition == WinCondition.KillsToWin) { 
                        KeyValuePair<byte, int> highestKillingTeam = m_GameRoom.CurrentGameKills.GetHighestKillingTeam();
                        if (highestKillingTeam.Value >= (int)Config.killsToWin || m_GameRoom.CurrentGameStats.GetAllTeams().Count <= 1)
                        {
                            m_GameRoom.EndMatch(m_GameRoom.CurrentGameStats.GetTeam(highestKillingTeam.Key));
                            foreach (TABGPlayerServer tabgplayerServer in m_GameRoom.Players)
                            {
                                //Log(string.Format("{0} : {1}/{2}", tabgplayerServer.PlayerName, tabgplayerServer.NumberOfKills, tabgplayerServer.NumberOfDeaths));
                            }
                            //TDM.Log("Changing Game State to: Ended");
                            m_GameRoom.ChangeGameState(GameState.Ended, false, null);
                        }
                    }
                    else if(Config.winCondition == WinCondition.Default)
                    {
                        int aliveTeams = m_GameRoom.CurrentGameStats.GetAliveTeams();
                        LandLog.Log("Checking Game State: Teams Alive: " + aliveTeams.ToString(), null);
                        if (aliveTeams <= 1)
                        {
                            m_GameRoom.EndMatch(null);
                            m_GameRoom.ChangeGameState(GameState.Ended, false, null);
                        }
                    }
                    else if(Config.winCondition == WinCondition.Debug)
                    {
                        LandLog.Log("Checking Game State: Teams Alive: " + m_GameRoom.CurrentGameStats.GetAliveTeams().ToString(), null);
                    }
                    // Do not skip the game's original CheckGameState; allow it to run as well
                    return true;
                }
            }
            return true;
        }
    }
}
