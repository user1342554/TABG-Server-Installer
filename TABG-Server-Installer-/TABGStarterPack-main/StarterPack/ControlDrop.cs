using Landfall.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace StarterPack
{
    internal class ControlDrop
    {
        public static bool DropAllLootCommandRun(ServerClient world, List<TABGPlayerServer> players)
        {
            if(Config.dropItemsOnDeath) {
            GameRoom gameRoomReference = world.GameRoomReference;
            byte b = (byte)players.Count;
            for (int i = 0; i < (int)b; i++)
            {
                TABGPlayerServer tabgplayerServer = players[i];
                List<TABGPlayerLootItem> loot = tabgplayerServer.Loot;
                byte[] buffer = new byte[14 + tabgplayerServer.NumberOfLootItems * 12];
                    using (MemoryStream memoryStream = new MemoryStream(buffer))
                    {
                        using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                        {
                            ushort num = (ushort)tabgplayerServer.NumberOfLootItems;
                            binaryWriter.Write(num);
                            binaryWriter.Write(tabgplayerServer.PlayerPosition.x);
                            binaryWriter.Write(tabgplayerServer.PlayerPosition.y);
                            binaryWriter.Write(tabgplayerServer.PlayerPosition.z);
                            for (int j = 0; j < tabgplayerServer.NumberOfLootItems; j++)
                            {
                                TABGPlayerLootItem tabgplayerLootItem = loot[j];
                                
                                    int newWeaponIndex = gameRoomReference.GetNewWeaponIndex();
                                    binaryWriter.Write(newWeaponIndex);
                                    binaryWriter.Write(tabgplayerLootItem.ItemIdentifier);
                                    binaryWriter.Write(tabgplayerLootItem.ItemCount);
                                    Vector3 pos = tabgplayerServer.PlayerPosition;
                                    Vector3 a = tabgplayerServer.PlayerPosition + UnityEngine.Random.onUnitSphere * 0.5f;
                                    Ray ray = new Ray(a + Vector3.up * 0.5f, Vector3.down + UnityEngine.Random.onUnitSphere * 0.3f);
                                    RaycastHit raycastHit = default(RaycastHit);
                                    Physics.Raycast(ray, out raycastHit, 500f);
                                    if (raycastHit.transform)
                                    {
                                        pos = raycastHit.point;
                                    }
                                    ItemManipulation.SpawnItemDrop(world, gameRoomReference, newWeaponIndex, tabgplayerLootItem.ItemIdentifier, tabgplayerLootItem.ItemCount, pos, true, false);
                                
                            }
                            tabgplayerServer.ClearLoot();
                            if (num > 0)
                            {
                                List<TABGPlayerServer> watchers = ServerChunks.Instance.GetWatchers(tabgplayerServer.ChunkData);
                                byte[] array = new byte[watchers.Count];
                                for (int k = 0; k < array.Length; k++)
                                {
                                    array[k] = watchers[k].PlayerIndex;
                                }
                                world.SendMessageToClients(EventCode.PlayerDeathLootDrop, buffer, array, true, false);
                            }
                        }
                    }
                }
            }
            return false;
        }

        public static bool ItemDropCommandRun(byte[] msgData, ServerClient world, byte sender)
        {
            if (false)
            {
                GameRoom gameRoomReference = world.GameRoomReference;
                byte indexOfPlayer;
                int num;
                int num3;
                Vector3 vector;
                using (MemoryStream memoryStream = new MemoryStream(msgData))
                {
                    using (BinaryReader binaryReader = new BinaryReader(memoryStream))
                    {
                        indexOfPlayer = binaryReader.ReadByte();
                        num = binaryReader.ReadInt32();
                        num3 = binaryReader.ReadInt32();
                        vector.x = binaryReader.ReadSingle();
                        vector.y = binaryReader.ReadSingle();
                        vector.z = binaryReader.ReadSingle();
                    }
                }
                TABGPlayerServer tabgplayerServer = gameRoomReference.Players.Find((TABGPlayerServer p) => p.PlayerIndex == indexOfPlayer);
                if (gameRoomReference.GetItem(num) == null)
                {
                    LandLog.LogError("INVALID DROP BUT I DONT CARE", null);
                }
                if (tabgplayerServer == null)
                {
                    LandLog.LogError("Cant find player: " + indexOfPlayer.ToString() + " Dropping loot", null);
                    return false;
                }
                int num2 = ItemManipulation.ValidateLootDrop(tabgplayerServer, num, num3);
                if (num2 <= 0)
                {
                    return false;
                }
                int newWeaponIndex = gameRoomReference.GetNewWeaponIndex();
                byte[] buffer = new byte[24];

                using (MemoryStream memoryStream2 = new MemoryStream(buffer))
                {
                    using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream2))
                    {
                        binaryWriter.Write(newWeaponIndex);
                        binaryWriter.Write(num);
                        binaryWriter.Write(num2);
                        binaryWriter.Write(vector.x);
                        binaryWriter.Write(vector.y);
                        binaryWriter.Write(vector.z);
                    }
                }
                ItemManipulation.RemoveItemFromPlayer(tabgplayerServer, num, num3);
                ItemManipulation.SpawnItemDrop(world, gameRoomReference, newWeaponIndex, num, num3, vector, false, false);
                List<TABGPlayerServer> watchers = ServerChunks.Instance.GetWatchers(tabgplayerServer.ChunkData);
                byte[] array = new byte[watchers.Count];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = watchers[i].PlayerIndex;
                }
                world.SendMessageToClients(EventCode.ItemDrop, buffer, array, true, false);
            }
                return false;
        }


        public static bool ItemThrownCommandRun(byte[] msgData, ServerClient world, byte sender)
        {
            GameRoom gameRoomReference = world.GameRoomReference;
            byte indexOfPlayer;
            int num;
            int num3;
            Vector3 vector;
            Vector3 vector2;
            using (MemoryStream memoryStream = new MemoryStream(msgData))
            {
                using (BinaryReader binaryReader = new BinaryReader(memoryStream))
                {
                    indexOfPlayer = binaryReader.ReadByte();
                    num = binaryReader.ReadInt32();
                    num3 = binaryReader.ReadInt32();
                    vector.x = binaryReader.ReadSingle();
                    vector.y = binaryReader.ReadSingle();
                    vector.z = binaryReader.ReadSingle();
                    vector2.x = binaryReader.ReadSingle();
                    vector2.y = binaryReader.ReadSingle();
                    vector2.z = binaryReader.ReadSingle();
                }
            }
            TABGPlayerServer tabgplayerServer = gameRoomReference.Players.Find((TABGPlayerServer p) => p.PlayerIndex == indexOfPlayer);
            if (tabgplayerServer == null)
            {
                LandLog.LogError("Cant find player throwing item: " + indexOfPlayer.ToString(), null);
                return false;
            }
            Pickup item = gameRoomReference.GetItem(num);
            if (item == null)
            {
                LandLog.LogError("Cant find pickup with index: " + num.ToString(), null);
                return false;
            }
            int num2 = ItemManipulation.ValidateLootDrop(tabgplayerServer, num, num3);
            if (num2 <= 0)
            {
                LandLog.LogError("Dont have loot, ignoring for now", null);
            }
            int newWeaponIndex = gameRoomReference.GetNewWeaponIndex();
            if (item.NetworkSyncThis)
            {
                gameRoomReference.AddNewProjectileSyncIndex(newWeaponIndex);
            }
            byte[] buffer = new byte[38];
            if (Array.Exists<int>(new int[] { 187, 188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 216 }, (int x) => x == num))
            {
                using (MemoryStream memoryStream2 = new MemoryStream(buffer))
                {
                    using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream2))
                    {
                        binaryWriter.Write(indexOfPlayer);
                        binaryWriter.Write(newWeaponIndex);
                        binaryWriter.Write(num);
                        binaryWriter.Write(num2);
                        binaryWriter.Write(vector.x);
                        binaryWriter.Write(vector.y);
                        binaryWriter.Write(vector.z);
                        binaryWriter.Write(vector2.x);
                        binaryWriter.Write(vector2.y);
                        binaryWriter.Write(vector2.z);
                        binaryWriter.Write(item.NetworkSyncThis);
                    }
                }
                ItemManipulation.RemoveItemFromPlayer(tabgplayerServer, num, num3);
                ItemManipulation.SpawnItemDrop(world, gameRoomReference, newWeaponIndex, num, num3, vector, true, false);
                List<TABGPlayerServer> watchers = ServerChunks.Instance.GetWatchers(tabgplayerServer.ChunkData);
                byte[] array = new byte[watchers.Count];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = watchers[i].PlayerIndex;
                }
                world.SendMessageToClients(EventCode.ItemThrown, buffer, array, true, false);
            }
            return false;
        }
    }
}
