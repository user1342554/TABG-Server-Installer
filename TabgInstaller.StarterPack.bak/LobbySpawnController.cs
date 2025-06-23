using Landfall.Network.GameModes;
using Landfall.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TabgInstaller.StarterPack
{
    internal class LobbySpawnController
    {
        public static bool GetNewSpawnPoint(TABGPlayerServer player, TABGBaseGameMode __instance, ref SpawnPointWrapper __result)
        {
            FieldInfo field = typeof(TABGBaseGameMode).GetField("m_GameRoom", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                object value = field.GetValue(__instance);
                GameRoom m_GameRoom = (GameRoom)value;
                List<SpawnPointWrapper> spawnPoints = m_GameRoom.GetSpawnPoints(0);
                int sp = Config.spawnPoints[UnityEngine.Random.Range(0, Config.spawnPoints.Length)];
                if (sp == 6)
                {
                    __result = new SpawnPointWrapper(Config.CustomSpawnPoint, 0);
                }
                else
                {
                    __result = spawnPoints[sp];
                }
                //new SpawnPointWrapper(new UnityEngine.Vector3(427,0,-216),0);//spawnPoints[sp];
                return false;
            }
            return true;
        }
    }
}
