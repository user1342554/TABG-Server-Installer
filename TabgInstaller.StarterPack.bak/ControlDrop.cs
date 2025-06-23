using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TabgInstaller.StarterPack
{
    internal class ControlDrop
    {
        // Cache types and methods
        private static Type _serverClientType;
        private static Type _gameRoomType;
        private static Type _tabgPlayerServerType;
        private static Type _tabgPlayerLootItemType;
        private static Type _itemManipulationType;
        private static Type _serverChunksType;
        private static Type _pickupType;
        
        private static MethodInfo _getNewWeaponIndexMethod;
        private static MethodInfo _spawnItemDropMethod;
        private static MethodInfo _sendMessageToClientsMethod;
        private static MethodInfo _clearLootMethod;
        private static MethodInfo _getWatchersMethod;
        private static MethodInfo _validateLootDropMethod;
        private static MethodInfo _removeItemFromPlayerMethod;
        private static MethodInfo _getItemMethod;
        
        static ControlDrop()
        {
            try
            {
                // Find types
                _serverClientType = Type.GetType("Landfall.Network.ServerClient, Assembly-CSharp");
                _gameRoomType = Type.GetType("Landfall.Network.GameRoom, Assembly-CSharp");
                _tabgPlayerServerType = Type.GetType("Landfall.Network.TABGPlayerServer, Assembly-CSharp");
                _tabgPlayerLootItemType = Type.GetType("Landfall.Network.TABGPlayerLootItem, Assembly-CSharp");
                _itemManipulationType = Type.GetType("Landfall.Network.ItemManipulation, Assembly-CSharp");
                _serverChunksType = Type.GetType("Landfall.Network.ServerChunks, Assembly-CSharp");
                _pickupType = Type.GetType("Pickup, Assembly-CSharp");
                
                // Cache methods
                if (_gameRoomType != null)
                {
                    _getNewWeaponIndexMethod = _gameRoomType.GetMethod("GetNewWeaponIndex");
                    _getItemMethod = _gameRoomType.GetMethod("GetItem");
                }
                
                if (_itemManipulationType != null)
                {
                    _spawnItemDropMethod = _itemManipulationType.GetMethod("SpawnItemDrop", BindingFlags.Static | BindingFlags.Public);
                    _validateLootDropMethod = _itemManipulationType.GetMethod("ValidateLootDrop", BindingFlags.Static | BindingFlags.Public);
                    _removeItemFromPlayerMethod = _itemManipulationType.GetMethod("RemoveItemFromPlayer", BindingFlags.Static | BindingFlags.Public);
                }
                
                if (_serverClientType != null)
                {
                    _sendMessageToClientsMethod = _serverClientType.GetMethod("SendMessageToClients");
                }
                
                if (_tabgPlayerServerType != null)
                {
                    _clearLootMethod = _tabgPlayerServerType.GetMethod("ClearLoot");
                }
                
                if (_serverChunksType != null)
                {
                    var instanceProp = _serverChunksType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
                    var instance = instanceProp?.GetValue(null);
                    if (instance != null)
                    {
                        _getWatchersMethod = instance.GetType().GetMethod("GetWatchers");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Failed to initialize ControlDrop: {ex}");
            }
        }
        
        public static bool DropAllLootCommandRun(object world, System.Collections.IList players)
        {
            if (Config.dropItemsOnDeath)
            {
                try
                {
                    // Get game room
                    var gameRoomRefProp = _serverClientType.GetProperty("GameRoomReference");
                    var gameRoom = gameRoomRefProp?.GetValue(world);
                    if (gameRoom == null) return false;
                    
                    byte playerCount = (byte)players.Count;
                    
                    for (int i = 0; i < playerCount; i++)
                    {
                        var player = players[i];
                        
                        // Get player properties
                        var lootProp = _tabgPlayerServerType.GetProperty("Loot");
                        var numberOfLootItemsProp = _tabgPlayerServerType.GetProperty("NumberOfLootItems");
                        var playerPositionProp = _tabgPlayerServerType.GetProperty("PlayerPosition");
                        var chunkDataProp = _tabgPlayerServerType.GetProperty("ChunkData");
                        var playerIndexProp = _tabgPlayerServerType.GetProperty("PlayerIndex");
                        
                        var lootList = lootProp?.GetValue(player) as System.Collections.IList;
                        int lootCount = (int)(numberOfLootItemsProp?.GetValue(player) ?? 0);
                        Vector3 playerPos = (Vector3)(playerPositionProp?.GetValue(player) ?? Vector3.zero);
                        var chunkData = chunkDataProp?.GetValue(player);
                        byte playerIndex = (byte)(playerIndexProp?.GetValue(player) ?? 0);
                        
                        if (lootList == null || lootCount == 0) continue;
                        
                        byte[] buffer = new byte[14 + lootCount * 12];
                        
                        using (MemoryStream ms = new MemoryStream(buffer))
                        using (BinaryWriter bw = new BinaryWriter(ms))
                        {
                            ushort lootCountShort = (ushort)lootCount;
                            bw.Write(lootCountShort);
                            bw.Write(playerPos.x);
                            bw.Write(playerPos.y);
                            bw.Write(playerPos.z);
                            
                            for (int j = 0; j < lootCount; j++)
                            {
                                var lootItem = lootList[j];
                                
                                // Get loot item properties
                                var itemIdentifierProp = _tabgPlayerLootItemType.GetProperty("ItemIdentifier");
                                var itemCountProp = _tabgPlayerLootItemType.GetProperty("ItemCount");
                                
                                int itemId = (int)(itemIdentifierProp?.GetValue(lootItem) ?? 0);
                                int itemCount = (int)(itemCountProp?.GetValue(lootItem) ?? 0);
                                
                                int newWeaponIndex = (int)_getNewWeaponIndexMethod.Invoke(gameRoom, null);
                                bw.Write(newWeaponIndex);
                                bw.Write(itemId);
                                bw.Write(itemCount);
                                
                                // Calculate drop position
                                Vector3 dropPos = playerPos;
                                Vector3 randomOffset = playerPos + UnityEngine.Random.onUnitSphere * 0.5f;
                                Ray ray = new Ray(randomOffset + Vector3.up * 0.5f, Vector3.down + UnityEngine.Random.onUnitSphere * 0.3f);
                                RaycastHit hit;
                                if (Physics.Raycast(ray, out hit, 500f))
                                {
                                    dropPos = hit.point;
                                }
                                
                                // Spawn item drop
                                _spawnItemDropMethod?.Invoke(null, new object[] { world, gameRoom, newWeaponIndex, itemId, itemCount, dropPos, true, false });
                            }
                            
                            // Clear player loot
                            _clearLootMethod?.Invoke(player, null);
                            
                            if (lootCountShort > 0)
                            {
                                // Get watchers
                                var serverChunksInstance = _serverChunksType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                                var watchers = _getWatchersMethod?.Invoke(serverChunksInstance, new object[] { chunkData }) as System.Collections.IList;
                                
                                if (watchers != null)
                                {
                                    byte[] watcherIndices = new byte[watchers.Count];
                                    for (int k = 0; k < watcherIndices.Length; k++)
                                    {
                                        var watcher = watchers[k];
                                        var watcherIndexProp = _tabgPlayerServerType.GetProperty("PlayerIndex");
                                        watcherIndices[k] = (byte)watcherIndexProp.GetValue(watcher);
                                    }
                                    
                                    const int EventCode_PlayerDeathLootDrop = 172; // You may need to find correct value
                                    _sendMessageToClientsMethod?.Invoke(world, new object[] { EventCode_PlayerDeathLootDrop, buffer, watcherIndices, true, false });
                                }
                            }
                        }
                    }
                    
                    return false; // Skip original method
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"Error in DropAllLootCommandRun: {ex}");
                }
            }
            
            return false;
        }
        
        public static bool ItemDropCommandRun(byte[] msgData, object world, byte sender)
        {
            // This method prevents item drops when disabled
            if (false) // Always prevent drops in this implementation
            {
                // Original drop logic would go here
                return false;
            }
            
            return false; // Skip original method
        }
        
        public static bool ItemThrownCommandRun(byte[] msgData, object world, byte sender)
        {
            try
            {
                // Parse message data
                byte playerIndex;
                int itemId;
                int itemCount;
                Vector3 position;
                Vector3 velocity;
                
                using (MemoryStream ms = new MemoryStream(msgData))
                using (BinaryReader br = new BinaryReader(ms))
                {
                    playerIndex = br.ReadByte();
                    itemId = br.ReadInt32();
                    itemCount = br.ReadInt32();
                    position.x = br.ReadSingle();
                    position.y = br.ReadSingle();
                    position.z = br.ReadSingle();
                    velocity.x = br.ReadSingle();
                    velocity.y = br.ReadSingle();
                    velocity.z = br.ReadSingle();
                }
                
                // Get game room
                var gameRoomRefProp = _serverClientType.GetProperty("GameRoomReference");
                var gameRoom = gameRoomRefProp?.GetValue(world);
                if (gameRoom == null) return false;
                
                // Find player
                var playersField = _gameRoomType.GetField("Players");
                var playersList = playersField?.GetValue(gameRoom) as System.Collections.IList;
                object player = null;
                
                if (playersList != null)
                {
                    foreach (var p in playersList)
                    {
                        var indexProp = _tabgPlayerServerType.GetProperty("PlayerIndex");
                        if ((byte)indexProp.GetValue(p) == playerIndex)
                        {
                            player = p;
                            break;
                        }
                    }
                }
                
                if (player == null)
                {
                    Plugin.Log?.LogError($"Can't find player throwing item: {playerIndex}");
                    return false;
                }
                
                // Get item pickup
                var item = _getItemMethod?.Invoke(gameRoom, new object[] { itemId });
                if (item == null)
                {
                    Plugin.Log?.LogError($"Can't find pickup with index: {itemId}");
                    return false;
                }
                
                // Validate loot drop
                int validatedCount = (int)_validateLootDropMethod.Invoke(null, new object[] { player, itemId, itemCount });
                if (validatedCount <= 0)
                {
                    Plugin.Log?.LogError("Don't have loot, ignoring for now");
                }
                
                // Get new weapon index
                int newWeaponIndex = (int)_getNewWeaponIndexMethod.Invoke(gameRoom, null);
                
                // Check if item needs network sync
                var networkSyncProp = _pickupType.GetProperty("NetworkSyncThis");
                bool networkSync = (bool)(networkSyncProp?.GetValue(item) ?? false);
                
                if (networkSync)
                {
                    var addNewProjectileSyncIndexMethod = _gameRoomType.GetMethod("AddNewProjectileSyncIndex");
                    addNewProjectileSyncIndexMethod?.Invoke(gameRoom, new object[] { newWeaponIndex });
                }
                
                // Check if this is a blessing (IDs 187-216)
                int[] blessingIds = new int[] { 187, 188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 216 };
                if (Array.Exists(blessingIds, id => id == itemId))
                {
                    byte[] buffer = new byte[38];
                    
                    using (MemoryStream ms = new MemoryStream(buffer))
                    using (BinaryWriter bw = new BinaryWriter(ms))
                    {
                        bw.Write(playerIndex);
                        bw.Write(newWeaponIndex);
                        bw.Write(itemId);
                        bw.Write(validatedCount);
                        bw.Write(position.x);
                        bw.Write(position.y);
                        bw.Write(position.z);
                        bw.Write(velocity.x);
                        bw.Write(velocity.y);
                        bw.Write(velocity.z);
                        bw.Write(networkSync);
                    }
                    
                    // Remove item from player
                    _removeItemFromPlayerMethod?.Invoke(null, new object[] { player, itemId, itemCount });
                    
                    // Spawn item drop
                    _spawnItemDropMethod?.Invoke(null, new object[] { world, gameRoom, newWeaponIndex, itemId, itemCount, position, true, false });
                    
                    // Get watchers and send message
                    var chunkDataProp = _tabgPlayerServerType.GetProperty("ChunkData");
                    var chunkData = chunkDataProp?.GetValue(player);
                    
                    var serverChunksInstance = _serverChunksType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                    var watchers = _getWatchersMethod?.Invoke(serverChunksInstance, new object[] { chunkData }) as System.Collections.IList;
                    
                    if (watchers != null)
                    {
                        byte[] watcherIndices = new byte[watchers.Count];
                        for (int i = 0; i < watcherIndices.Length; i++)
                        {
                            var watcher = watchers[i];
                            var watcherIndexProp = _tabgPlayerServerType.GetProperty("PlayerIndex");
                            watcherIndices[i] = (byte)watcherIndexProp.GetValue(watcher);
                        }
                        
                        const int EventCode_ItemThrown = 120; // You may need to find correct value
                        _sendMessageToClientsMethod?.Invoke(world, new object[] { EventCode_ItemThrown, buffer, watcherIndices, true, false });
                    }
                }
                
                return false; // Skip original method
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in ItemThrownCommandRun: {ex}");
                return true;
            }
        }
    }
} 
