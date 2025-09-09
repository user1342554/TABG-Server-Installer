using Landfall.Network.GameModes;
using Landfall.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using XInputDotNetPure;

namespace StarterPack
{
    internal class RespawningPlayers
    {
        public static bool AutoDropAllPlayersRun(byte[] msgData, ServerClient world)
        {
            Dropper spawnedPlane = world.GetSpawnedPlane();
            Vector3 dropPosition = spawnedPlane.GetDropPosition();
            List<TABGPlayerServer> players = world.GameRoomReference.Players;
            byte[] array = new byte[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                array[i] = players[i].PlayerIndex;
            }
            byte[] buffer = new byte[12];
            using (MemoryStream memoryStream = new MemoryStream(buffer))
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write(dropPosition.x);
                    binaryWriter.Write(dropPosition.y);
                    binaryWriter.Write(dropPosition.z);
                }
            }
            world.SendMessageToClients(EventCode.AllDrop, buffer, array, true, false);
            spawnedPlane.Destroy();
            int num = 0;
            using (List<TABGPlayerServer>.Enumerator enumerator = world.GameRoomReference.Players.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TABGPlayerServer player = enumerator.Current;
                    if (!player.HasDropped)
                    {
                        world.DropPlayer(player);
                        num++;
                        world.WaitThenDoAction((float)num / 30f, delegate
                        {
                            PlayerDeadDeadBehaviourCommand.Run(player, world);
                        });
                    }
                }
            }
            return false;
        }

        public static void StartFlying(GameRoom __instance)
        {
            if (Config.forceKillOffStart)
            {
                FieldInfo field = typeof(GameRoom).GetField("m_server", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    object value = field.GetValue(__instance);
                    ServerClient m_server = (ServerClient)value;
                    AutoDropAllPlayersCommand.Run(null, m_server);
                }
            }
        }


        private static Vector3 GroundPos(TABGPlayerServer player, ServerClient world)
        {
            Vector3 currentWhiteRingPosition = world.SpawnedRing.currentWhiteRingPosition;
            float currentWhiteSize = world.SpawnedRing.currentWhiteSize;

            Vector2 vector = UnityEngine.Random.insideUnitCircle * currentWhiteSize * 0.5f;
            Vector3 positionInWhiteRing = currentWhiteRingPosition + new Vector3(vector.x, 0f, vector.y);

            positionInWhiteRing.y = 1000f;
            RaycastHit raycastHit = default(RaycastHit);
            Physics.Raycast(new Ray(positionInWhiteRing, Vector3.down), out raycastHit, 10000f, LayerMask.GetMask(new string[]
            {
                "Terrain",
                "Map"
            }));

            return raycastHit.point + new Vector3(0f, 10f, 0f);
        }

        public static bool Run(TABGPlayerServer victimPlayer, ServerClient world)
        {
            if (victimPlayer == null)
            {
                LandLog.LogError("Cant Find victom player that is dead", null);
                return false;
            }
            GameRoom gameRoomReference = world.GameRoomReference;
            PlayerDeadWithDownBehaviourCommand.HandleDownPlayer(victimPlayer);
            victimPlayer.Kill();
            victimPlayer.TakeDamage(200f);
            DropAllLootCommand.Run(world, victimPlayer);
            byte value = gameRoomReference.InitSpectator(victimPlayer);
            if (gameRoomReference.CurrentGameSettings.AllowRespawnMinigame && world.GameRoomReference.CurrentGameMode is BattleRoyaleGameMode)
            {
                TABGPlayerServer tabgplayerServer = world.GameRoomReference.FindPlayer(victimPlayer.LastAttacker);
                if (tabgplayerServer != null)
                {
                    LandLog.Log(string.Concat(new object[]
                    {
                        victimPlayer.PlayerName,
                        " has died to ",
                        tabgplayerServer.PlayerName,
                        ". They're at ",
                        (int)(tabgplayerServer.NumberOfKills + 1),
                        " kills",
                    }), null);
                    if (Config.HealOnKill)
                    {
                        float num = (float)Config.HealOnKillAmount;
                        byte[] buffer = new byte[60];
                        using (MemoryStream memoryStream = new MemoryStream(buffer))
                        {
                            using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                            {
                                binaryWriter.Write(tabgplayerServer.PlayerIndex);
                                binaryWriter.Write(num + tabgplayerServer.Health);
                            }
                        }
                        tabgplayerServer.TakeDamage(-num);
                        world.SendMessageToClients(EventCode.PlayerHealed, buffer, byte.MaxValue, true, false);
                    }
                    if(Config.givenItems.Length != 0)
                    {
                        List<int> givenID = new List<int>();
                        List<byte> givenQuan = new List<byte>();

                        string[] array = Config.givenItems.Split(new char[] { ',' },StringSplitOptions.RemoveEmptyEntries);
                        foreach(string str in array) 
                        {
                            string[] array2 = str.Split(':');
                            givenID.Add(int.Parse(array2[0]));
                            givenQuan.Add((byte)int.Parse(array2[1]));
                        }
                        if (givenID.Count > 0)
                        {
                            GivePickUpCommand.Run(null, world, tabgplayerServer.PlayerIndex, givenID.ToArray(), givenQuan.ToArray());
                        }
                    }
                }
                else
                {
                    LandLog.Log(string.Concat(new object[]
                    {
                        victimPlayer.PlayerName,
                        " has died"
                    }), null);
                }
                if (victimPlayer == null)
                {
                    LandLog.LogError("Cant Find Victim Player", null);
                    return false;
                }
                victimPlayer.EnterBoss();
                world.WaitThenDoAction(7f, delegate
                {
                    if (victimPlayer == null)
                    {
                        LandLog.LogError("Cant Find Victim Player", null);
                        return;
                    }
                    RespawnEntityCommand.Run(world, victimPlayer, RespawningPlayers.GroundPos(victimPlayer, world), byte.MaxValue);
                    world.WaitThenDoAction(1.25f, delegate
                    {
                        if (victimPlayer == null)
                        {
                            LandLog.LogError("Cant Find Victim Player", null);
                            return;
                        }
                        byte playerIndex = victimPlayer.PlayerIndex;
                        victimPlayer.Land();
                        List<int> list = new List<int>();
                        List<byte> list2 = new List<byte>();
                        if (Config.loadouts.Count >= 1)
                        {
                            Loadout indexedList = Config.ChooseLoadout();
                            for(int i = 0; i < indexedList.itemIds.Count; i++)
                            {
                                list.Add((int)indexedList.itemIds[i]);
                                list2.Add((byte)indexedList.itemQuantities[i]);
                            }
                        }
                        if (list.Count > 0)
                        {
                            GivePickUpCommand.Run(null, world, playerIndex, list.ToArray(), list2.ToArray());
                        }
                    });
                });
                Easy_AC_Server.Instance.LogPlayerDeSpawned(victimPlayer._handle);
            }
            TABGPlayerServer tabgplayerServer2 = gameRoomReference.FindPlayer(victimPlayer.LastAttacker);
            if (tabgplayerServer2 != null && tabgplayerServer2.GroupIndex != victimPlayer.GroupIndex)
            {
                tabgplayerServer2.AddKill();
                byte groupIndex = tabgplayerServer2.GroupIndex;
                gameRoomReference.CurrentGameKills.AddKillForTeam(groupIndex);
            }
            byte[] bytes = new UnicodeEncoding().GetBytes(victimPlayer.PlayerName);
            byte[] buffer2 = new byte[5 + bytes.Length + 4];
            using (MemoryStream memoryStream2 = new MemoryStream(buffer2))
            {
                using (BinaryWriter binaryWriter2 = new BinaryWriter(memoryStream2))
                {
                    binaryWriter2.Write(victimPlayer.PlayerIndex);
                    binaryWriter2.Write(victimPlayer.LastAttacker);
                    binaryWriter2.Write(value);
                    binaryWriter2.Write((byte)bytes.Length);
                    binaryWriter2.Write(bytes);
                    if (tabgplayerServer2 != null)
                    {
                        binaryWriter2.Write(tabgplayerServer2.WeaponType);
                    }
                    else
                    {
                        binaryWriter2.Write(int.MaxValue);
                    }
                    binaryWriter2.Write(false);
                }
            }
            world.SendMessageToClients(EventCode.PlayerDead, buffer2, byte.MaxValue, true, false);
            gameRoomReference.CheckGameState();
            return false;
        }

        public static bool HandlePlayerDead(TABGPlayerServer victimPlayer, TABGPlayerServer attackerPlayer, byte[] buffer, byte[] receivers, byte senderIndex, BattleRoyaleGameMode __instance)
        {
            FieldInfo field = typeof(TABGBaseGameMode).GetField("m_Server", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                object value = field.GetValue(__instance);
                ServerClient m_Server = (ServerClient)value;
                if (Config.canGoDown)
                {
                    PlayerDeadWithDownBehaviourCommand.Run(m_Server, victimPlayer, attackerPlayer, buffer, receivers, senderIndex);
                }
                else
                {
                    PlayerDeadDeadBehaviourCommand.Run(victimPlayer, m_Server);
                }
            }
            return false;
        }

        public static bool RingDeathRun(byte[] msgData, ServerClient world, byte playerIndex)
        {
            LandLog.Log("Player RingDeath: " + playerIndex.ToString(), null);
            if (world.GameRoomReference.FindPlayer(playerIndex) == null)
            {
                LandLog.LogError("Cant find player that left! " + playerIndex.ToString(), null);
                return false;
            }
            TABGPlayerServer tabgplayerServer = world.GameRoomReference.FindPlayer(playerIndex);
            if (tabgplayerServer != null)
            {
                if (Config.canLockOut)
                {
                    PlayerDeadCommand.Run(msgData, world, playerIndex, true);
                }
                else
                {
                    PlayerDeadDeadBehaviourCommand.Run(tabgplayerServer, world);
                }
            }
            return false;
        }
    }
}
