using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using HarmonyLib;
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace TabgInstaller.GiveMod
{
    [BepInPlugin("tabginstaller.givemod", "Give Items Mod", "1.0.0")]
    public class GiveModPlugin : BaseUnityPlugin
    {
        private ConfigEntry<bool> _debugMode;
        private Harmony _harmony;
        private static GiveModPlugin _instance;
        private static bool _isDebugMode = false;
        
        // Cache for game items
        private static Dictionary<string, object> _weaponCache = new Dictionary<string, object>();
        private static Dictionary<string, object> _itemCache = new Dictionary<string, object>();
        private static Type _playerType;
        private static Type _inventoryType;
        private static Type _weaponType;
        private static Type _itemType;
        private static MethodInfo _giveWeaponMethod;
        private static MethodInfo _giveItemMethod;
        private static MethodInfo _spawnItemMethod;

        private void Awake()
        {
            _instance = this;
            
            _debugMode = Config.Bind("Debug", "EnableDebugLogging", true, "Enable detailed debug logging");
            _isDebugMode = _debugMode.Value;

            Logger.LogInfo("GiveMod v1.0.0 initializing...");

            _harmony = new Harmony("tabginstaller.givemod");

            // Apply patches
            ApplyChatCommandPatch();
            FindGameSystems();
            CacheAllItems();

            Logger.LogInfo("GiveMod initialized! Use /give <weapon/ammo> [amount]");
        }

        private void ApplyChatCommandPatch()
        {
            try
            {
                var chatTypes = new[]
                {
                    "ChatMessageCommand",
                    "Landfall.Network.ChatMessageCommand",
                    "Network.ChatMessageCommand"
                };

                Type chatMsgCmdType = null;
                foreach (var typeName in chatTypes)
                {
                    chatMsgCmdType = AccessTools.TypeByName(typeName) ?? Type.GetType($"{typeName}, Assembly-CSharp");
                    if (chatMsgCmdType != null) break;
                }

                var chatRun = chatMsgCmdType?.GetMethod("Run");
                if (chatRun != null)
                {
                    _harmony.Patch(chatRun, postfix: new HarmonyMethod(typeof(GiveModPlugin), nameof(ChatCommandPostfix)));
                    Logger.LogInfo("Chat command patch applied (/give command)");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error patching chat command: {ex}");
            }
        }

        private void FindGameSystems()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    if (assembly.FullName.Contains("System") || assembly.FullName.Contains("Unity")) continue;

                    foreach (var type in assembly.GetTypes())
                    {
                        var typeName = type.Name.ToLower();
                        
                        // Find player type
                        if (_playerType == null && (typeName.Contains("player") && (typeName.Contains("controller") || typeName == "player")))
                        {
                            _playerType = type;
                            Logger.LogInfo($"Found player type: {type.FullName}");
                        }
                        
                        // Find inventory type
                        if (_inventoryType == null && typeName.Contains("inventory"))
                        {
                            _inventoryType = type;
                            Logger.LogInfo($"Found inventory type: {type.FullName}");
                        }
                        
                        // Find weapon type
                        if (_weaponType == null && (typeName.Contains("weapon") || typeName.Contains("gun")))
                        {
                            _weaponType = type;
                            Logger.LogInfo($"Found weapon type: {type.FullName}");
                        }
                        
                        // Find item type
                        if (_itemType == null && (typeName == "item" || typeName.Contains("itemdata")))
                        {
                            _itemType = type;
                            Logger.LogInfo($"Found item type: {type.FullName}");
                        }
                        
                        // Look for spawning methods
                        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        foreach (var method in methods)
                        {
                            var methodName = method.Name.ToLower();
                            
                            if (methodName.Contains("giveweapon") || methodName.Contains("addweapon") || methodName.Contains("equipweapon"))
                            {
                                _giveWeaponMethod = method;
                                Logger.LogInfo($"Found give weapon method: {type.Name}.{method.Name}");
                            }
                            
                            if (methodName.Contains("giveitem") || methodName.Contains("additem") || methodName.Contains("spawnitem"))
                            {
                                _giveItemMethod = method;
                                Logger.LogInfo($"Found give item method: {type.Name}.{method.Name}");
                            }
                            
                            if (methodName.Contains("spawn") && (methodName.Contains("weapon") || methodName.Contains("item")))
                            {
                                _spawnItemMethod = method;
                                Logger.LogInfo($"Found spawn method: {type.Name}.{method.Name}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_isDebugMode)
                        Logger.LogWarning($"Error scanning assembly {assembly.FullName}: {ex.Message}");
                }
            }
        }

        private void CacheAllItems()
        {
            try
            {
                // Look for weapon/item databases or resource loaders
                var possibleDatabaseTypes = new[]
                {
                    "WeaponDatabase",
                    "ItemDatabase",
                    "GameDatabase",
                    "ResourceManager",
                    "WeaponManager",
                    "ItemManager",
                    "GameData"
                };

                foreach (var dbName in possibleDatabaseTypes)
                {
                    var dbType = AccessTools.TypeByName(dbName) ?? Type.GetType($"{dbName}, Assembly-CSharp");
                    if (dbType != null)
                    {
                        Logger.LogInfo($"Found database type: {dbType.FullName}");
                        ExtractItemsFromDatabase(dbType);
                    }
                }

                // Also try to find items through Resources
                var allWeapons = Resources.FindObjectsOfTypeAll(_weaponType ?? typeof(GameObject));
                foreach (var weapon in allWeapons)
                {
                    if (weapon != null)
                    {
                        var name = weapon.name.ToLower().Replace(" ", "");
                        if (!_weaponCache.ContainsKey(name))
                        {
                            _weaponCache[name] = weapon;
                            if (_isDebugMode)
                                Logger.LogInfo($"Cached weapon: {name}");
                        }
                    }
                }

                Logger.LogInfo($"Cached {_weaponCache.Count} weapons and {_itemCache.Count} items");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error caching items: {ex}");
            }
        }

        private void ExtractItemsFromDatabase(Type dbType)
        {
            // Look for static fields/properties that might contain item lists
            var fields = dbType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var properties = dbType.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                try
                {
                    var value = field.GetValue(null);
                    if (value is Array array)
                    {
                        foreach (var item in array)
                        {
                            CacheItem(item);
                        }
                    }
                    else if (value is System.Collections.IList list)
                    {
                        foreach (var item in list)
                        {
                            CacheItem(item);
                        }
                    }
                }
                catch { }
            }

            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(null);
                    if (value is Array array)
                    {
                        foreach (var item in array)
                        {
                            CacheItem(item);
                        }
                    }
                    else if (value is System.Collections.IList list)
                    {
                        foreach (var item in list)
                        {
                            CacheItem(item);
                        }
                    }
                }
                catch { }
            }
        }

        private void CacheItem(object item)
        {
            if (item == null) return;

            try
            {
                // Try to get name property
                var nameProperty = item.GetType().GetProperty("name") ?? item.GetType().GetProperty("Name");
                var name = nameProperty?.GetValue(item)?.ToString()?.ToLower().Replace(" ", "");
                
                if (string.IsNullOrEmpty(name)) return;

                var typeName = item.GetType().Name.ToLower();
                if (typeName.Contains("weapon") || typeName.Contains("gun"))
                {
                    _weaponCache[name] = item;
                }
                else
                {
                    _itemCache[name] = item;
                }
            }
            catch { }
        }

        private static void ChatCommandPostfix(byte[] __0, object __1, byte __2)
        {
            try
            {
                string msg;
                using (MemoryStream ms = new MemoryStream(__0))
                using (BinaryReader br = new BinaryReader(ms))
                {
                    br.ReadByte();
                    byte count = br.ReadByte();
                    msg = Encoding.Unicode.GetString(br.ReadBytes(count));
                }

                if (!msg.StartsWith("/give", StringComparison.OrdinalIgnoreCase)) return;

                var parts = msg.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    _instance.Logger.LogInfo("Usage: /give <item> [amount]");
                    _instance.Logger.LogInfo("Example: /give ak47 1");
                    _instance.Logger.LogInfo("Example: /give ammo 100");
                    ListAvailableItems();
                    return;
                }

                string itemName = parts[1].ToLower();
                int amount = 1;
                
                if (parts.Length >= 3)
                {
                    int.TryParse(parts[2], out amount);
                    amount = Math.Max(1, amount);
                }

                // Try to give the item
                bool success = false;
                
                // First try direct spawn
                success = TrySpawnItem(itemName, amount);
                
                // If that fails, try giving to player
                if (!success)
                {
                    success = TryGiveToPlayer(itemName, amount);
                }
                
                // If still fails, try alternate methods
                if (!success)
                {
                    success = TryAlternateSpawn(itemName, amount);
                }

                if (success)
                {
                    _instance.Logger.LogInfo($"Spawned {amount}x {itemName}");
                }
                else
                {
                    _instance.Logger.LogInfo($"Failed to spawn {itemName}. Type /give to see available items.");
                }
            }
            catch (Exception ex)
            {
                _instance.Logger.LogError($"Error processing /give command: {ex}");
            }
        }

        private static void ListAvailableItems()
        {
            _instance.Logger.LogInfo("Available weapons:");
            foreach (var weapon in _weaponCache.Keys.Take(20))
            {
                _instance.Logger.LogInfo($"  - {weapon}");
            }
            if (_weaponCache.Count > 20)
                _instance.Logger.LogInfo($"  ... and {_weaponCache.Count - 20} more");

            _instance.Logger.LogInfo("Available items:");
            foreach (var item in _itemCache.Keys.Take(10))
            {
                _instance.Logger.LogInfo($"  - {item}");
            }
            if (_itemCache.Count > 10)
                _instance.Logger.LogInfo($"  ... and {_itemCache.Count - 10} more");
        }

        private static bool TrySpawnItem(string itemName, int amount)
        {
            try
            {
                // Check if it's ammo
                if (itemName.Contains("ammo") || itemName.Contains("bullet"))
                {
                    return TrySpawnAmmo(itemName, amount);
                }

                // Try weapon cache
                if (_weaponCache.TryGetValue(itemName, out var weapon))
                {
                    for (int i = 0; i < amount; i++)
                    {
                        SpawnObject(weapon);
                    }
                    return true;
                }

                // Try item cache
                if (_itemCache.TryGetValue(itemName, out var item))
                {
                    for (int i = 0; i < amount; i++)
                    {
                        SpawnObject(item);
                    }
                    return true;
                }

                // Try partial match
                foreach (var kvp in _weaponCache)
                {
                    if (kvp.Key.Contains(itemName) || itemName.Contains(kvp.Key))
                    {
                        for (int i = 0; i < amount; i++)
                        {
                            SpawnObject(kvp.Value);
                        }
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                if (_isDebugMode)
                    _instance.Logger.LogError($"Error in TrySpawnItem: {ex}");
                return false;
            }
        }

        private static bool TrySpawnAmmo(string ammoType, int amount)
        {
            try
            {
                // Find the local player
                var player = GetLocalPlayer();
                if (player == null) return false;

                // Look for ammo-related methods
                var playerType = player.GetType();
                var methods = playerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                foreach (var method in methods)
                {
                    var methodName = method.Name.ToLower();
                    if (methodName.Contains("ammo") && (methodName.Contains("add") || methodName.Contains("give")))
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(int))
                        {
                            method.Invoke(player, new object[] { amount });
                            return true;
                        }
                    }
                }

                // Try to find ammo field and modify directly
                var fields = playerType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var field in fields)
                {
                    if (field.Name.ToLower().Contains("ammo"))
                    {
                        if (field.FieldType == typeof(int))
                        {
                            var currentAmmo = (int)field.GetValue(player);
                            field.SetValue(player, currentAmmo + amount);
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                if (_isDebugMode)
                    _instance.Logger.LogError($"Error spawning ammo: {ex}");
                return false;
            }
        }

        private static void SpawnObject(object itemTemplate)
        {
            try
            {
                var player = GetLocalPlayer();
                if (player == null) return;

                var playerTransform = (player as Component)?.transform ?? (player as GameObject)?.transform;
                if (playerTransform == null) return;

                // Calculate spawn position (in front of player)
                var spawnPos = playerTransform.position + playerTransform.forward * 2f + Vector3.up;

                // If it's a GameObject, instantiate it
                if (itemTemplate is GameObject go)
                {
                    var spawned = Instantiate(go, spawnPos, Quaternion.identity);
                    spawned.SetActive(true);
                }
                // If it's a component, instantiate its gameObject
                else if (itemTemplate is Component comp)
                {
                    var spawned = Instantiate(comp.gameObject, spawnPos, Quaternion.identity);
                    spawned.SetActive(true);
                }
                // Otherwise try to use spawn method
                else if (_spawnItemMethod != null)
                {
                    _spawnItemMethod.Invoke(null, new object[] { itemTemplate, spawnPos });
                }
            }
            catch (Exception ex)
            {
                if (_isDebugMode)
                    _instance.Logger.LogError($"Error spawning object: {ex}");
            }
        }

        private static bool TryGiveToPlayer(string itemName, int amount)
        {
            try
            {
                var player = GetLocalPlayer();
                if (player == null) return false;

                // Try to use give weapon method
                if (_giveWeaponMethod != null && _weaponCache.TryGetValue(itemName, out var weapon))
                {
                    for (int i = 0; i < amount; i++)
                    {
                        _giveWeaponMethod.Invoke(player, new object[] { weapon });
                    }
                    return true;
                }

                // Try to use give item method
                if (_giveItemMethod != null && _itemCache.TryGetValue(itemName, out var item))
                {
                    for (int i = 0; i < amount; i++)
                    {
                        _giveItemMethod.Invoke(player, new object[] { item });
                    }
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                if (_isDebugMode)
                    _instance.Logger.LogError($"Error giving to player: {ex}");
                return false;
            }
        }

        private static bool TryAlternateSpawn(string itemName, int amount)
        {
            try
            {
                // Try to find item through Resources.Load
                var prefab = Resources.Load(itemName);
                if (prefab != null)
                {
                    var player = GetLocalPlayer();
                    var playerTransform = (player as Component)?.transform ?? (player as GameObject)?.transform;
                    if (playerTransform != null)
                    {
                        var spawnPos = playerTransform.position + playerTransform.forward * 2f + Vector3.up;
                        for (int i = 0; i < amount; i++)
                        {
                            Instantiate(prefab as GameObject, spawnPos + Vector3.right * i * 0.5f, Quaternion.identity);
                        }
                        return true;
                    }
                }

                // Try common weapon names if exact match fails
                var commonNames = new[] { "ak", "m4", "awp", "glock", "deagle", "shotgun", "sniper", "rifle", "pistol", "smg" };
                foreach (var common in commonNames)
                {
                    if (itemName.Contains(common))
                    {
                        foreach (var kvp in _weaponCache)
                        {
                            if (kvp.Key.Contains(common))
                            {
                                for (int i = 0; i < amount; i++)
                                {
                                    SpawnObject(kvp.Value);
                                }
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                if (_isDebugMode)
                    _instance.Logger.LogError($"Error in alternate spawn: {ex}");
                return false;
            }
        }

        private static object GetLocalPlayer()
        {
            try
            {
                // Try to find local player through various methods
                
                // Method 1: Look for LocalPlayer property
                if (_playerType != null)
                {
                    var localPlayerProp = _playerType.GetProperty("LocalPlayer", BindingFlags.Static | BindingFlags.Public);
                    if (localPlayerProp != null)
                    {
                        return localPlayerProp.GetValue(null);
                    }
                }

                // Method 2: Find through GameObject
                var playerGO = GameObject.Find("LocalPlayer") ?? 
                              GameObject.Find("Player") ?? 
                              GameObject.FindGameObjectWithTag("Player");
                
                if (playerGO != null)
                {
                    if (_playerType != null)
                    {
                        return playerGO.GetComponent(_playerType);
                    }
                    return playerGO;
                }

                // Method 3: Find through FindObjectOfType
                if (_playerType != null)
                {
                    return FindObjectOfType(_playerType);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
} 