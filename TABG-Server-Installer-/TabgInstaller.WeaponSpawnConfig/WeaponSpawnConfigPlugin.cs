using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TabgInstaller.WeaponSpawnConfig
{
    [BepInPlugin("tabginstaller.weaponspawnconfig", "TABG Weapon Spawn Config", "1.0.0")]
    public class WeaponSpawnConfigPlugin : BaseUnityPlugin
    {
        public static WeaponSpawnConfigPlugin Instance { get; private set; }
        private Harmony _harmony;
        private Dictionary<string, ConfigEntry<float>> _weaponSpawnRates = new Dictionary<string, ConfigEntry<float>>();
        private int _modifiedWeapons = 0;
        
        // Loot pool entry structure based on user's example
        [Serializable]
        public class LootPoolEntry
        {
            public string name;
            public float weight;
            public List<ItemEntry> entries;
        }
        
        [Serializable]
        public class ItemEntry
        {
            public int id;
            public int amount;
        }

        // Weapon categories and their items
        private static readonly Dictionary<string, List<string>> WeaponCategories = new Dictionary<string, List<string>>
        {
            ["Special Weapons"] = new List<string>
            {
                "Auto Crossbow", "Balloon Crossbow", "Crossbow", "Taser Crossbow", "Firework Crossbow",
                "Gaussbow", "The Promise", "Grappling Hook", "Harpoon", "Boss Weapon Full Auto",
                "Boss Weapon Grenade Launcher", "Money Stack", "Water Gun", "Really Big Deagle"
            },
            ["Assault Rifles"] = new List<string>
            {
                "AK2K", "AK47", "AKS-74u", "AUG", "Beam AR", "Burstgun", "Cursed Famas", "Famas", "H1",
                "Liberating M16", "M16", "MP44", "ScarH"
            },
            ["SMGs"] = new List<string>
            {
                "AKS", "Money Mac", "Glockinator", "Liberating Thompson", "Thompson", "Mac 10",
                "MP40", "MP5", "P90", "PPSH", "Tec 9", "UMP", "Vector", "Z4"
            },
            ["Pistols"] = new List<string>
            {
                "Beretta", "Crossbow Pistol", "Desert Eagle", "Flintlock", "Taser Flintlock",
                "Auto Revolver", "Wind Up Pistol", "Glock", "Glue Gun", "Hand Gun", "Hand Cannon",
                "Liberating M1911", "Luger", "M1911", "Real Gun", "Revolver", "Holy Revolver",
                "Reverse Revolver", "Hardballer", "Taser"
            },
            ["Legendary Blessings"] = new List<string>
            {
                "Legendary Battlecry", "Legendary Bloodlust", "Legendary Cardio", "Legendary Charge",
                "Legendary Dash", "Legendary Healing Words", "Legendary Health", "Legendary Hunt",
                "Legendary Ice", "Legendary Jump", "Legendary Lit Beats", "Legendary Poison",
                "Legendary Recycling", "Legendary Regen", "Legendary Relax", "Legendary Shield",
                "Legendary Speed", "Legendary Spray", "Legendary Stormcall", "Legendary Storm",
                "Legendary Vampire", "Legendary Weapon Mastery", "Legendary Words Of Justice"
            },
            ["Epic Blessings"] = new List<string>
            {
                "Epic Battlecry", "Epic Bloodlust", "Epic Cardio", "Epic Charge", "Epic Dash",
                "Epic Healing Words", "Epic Health", "Epic Hunt", "Epic Ice", "Epic Jump",
                "Epic Lit Beats", "Epic Poison", "Epic Recycling", "Epic Regeneration", "Epic Relax",
                "Epic Shield", "Epic Small", "Epic Speed", "Epic Spray", "Epic Stormcall",
                "Epic Storm", "Epic Vampire", "Epic Weapon Mastery", "Epic Words of Justice",
                "Assassin", "Mad Mechanic"
            },
            ["Rare Blessings"] = new List<string>
            {
                "Rare Airstrike", "Rare Bloodlust", "Rare Cardio", "Rare Dash", "Rare Health",
                "Rare Hunt", "Rare Ice", "Rare Insight", "Rare Jump", "Rare Lit Beats",
                "Rare Poison", "Rare Pull", "Rare Recycling", "Rare Regeneration", "Rare Relax",
                "Rare Shield", "Rare Speed", "Rare Spray", "Rare Storm", "Rare Vampire",
                "Rare Weapon Mastery"
            },
            ["Common Blessings"] = new List<string>
            {
                "Common Bloodlust", "Common Cardio", "Common Dash", "Common Health", "Common Ice",
                "Common Jump", "Common Poison", "Common Hunt", "Common Recycling", "Common Regeneration",
                "Common Relax", "Common Shield", "Common Speed", "Common Spray", "Common Storm",
                "Common Vampire", "Common Weapon Mastery"
            },
            ["Grenades"] = new List<string>
            {
                "Big Healing Grenade", "Black Hole Grenade", "Bombardment Grenade", "Bouncy Grenade",
                "Cage Grenade", "Taser Cage Grenade", "Cluster Grenade", "Cluster Dummy Grenade",
                "Dummy Grenade", "Fire Grenade", "Grenade", "Healing Grenade", "Implosion Grenade",
                "Knockback Grenade", "Big Knockback Grenade", "Launchpad Grenade", "Orbital Taser Grenade",
                "Orbital Strike Grenade", "Poof Grenade", "Shield Grenade", "Smoke Grenade",
                "Snow Storm Grenade", "Splinter Grenade", "Taser Splinter Grenade", "Flash Grenade",
                "Time Slow Grenade", "Dynamite", "Volley Grenade", "Wall Grenade"
            },
            ["Spells"] = new List<string>
            {
                "Blinding Light", "Gravity Field", "Gust", "Healing Aura", "Speed Aura",
                "Summon Rock", "Teleport", "Track", "Fireball", "Ice Bolt", "Magic Missile",
                "Mirage", "Orb Of Sight", "Reveal", "Shockwave", "Summon Tree"
            },
            ["Melee"] = new List<string>
            {
                "Ballistic Shield", "Triple Ballistic Shield", "Taser Ballistic Shield", "Black Katana",
                "Baton", "Boxing Glove", "Cleaver", "Crowbar", "Crusader Sword", "Taser Crusader Sword",
                "Fish", "Taser Fish", "Holy Sword", "Inflatable Hammer", "Jarl Axe", "Taser Jarl Axe",
                "Katana", "Knife", "Rapier", "Riot Shield", "Sabre", "Pan", "Medieval Shield",
                "Shovel", "Viking Axe", "Weights"
            },
            ["Shotguns"] = new List<string>
            {
                "AA12", "Blunderbuss", "Sawed Off Shotgun", "Flying Blunderbuss", "Liberating AA12",
                "Mossberg", "Mossberg 5000", "Taser Mossberg", "Rainmaker", "Arnold"
            },
            ["Heavy"] = new List<string>
            {
                "Leaf Blower", "Liberating Minigun", "Megagun", "Minigun", "Taser Minigun",
                "Missile Launcher", "Smoke Rocket Launcher", "Rocket Launcher", "MGL",
                "Browning M2", "BAR", "M8", "MG-42"
            },
            ["Snipers"] = new List<string>
            {
                "Beam DMR", "FAL", "Garand", "Liberating Garand", "M14", "S7", "Winchester",
                "AWPS", "AWP", "Taser AWP", "Barret", "Beam Sniper", "Kar98", "Liberating Barret",
                "Musket", "Taser Musket", "Really Big Barret", "Sniper Shotgun", "Two Shot", "VSS"
            },
            ["Attachments"] = new List<string>
            {
                // Barrels
                "Compensator", "Suppressor", "Suppressor 2", "Healing Barrel", "Double Barrel",
                "Fast Barrel", "Accuracy Barrel", "Fire Rate Barrel", "Periscope Barrel", "Heavy Barrel",
                // Underbarrel
                "Damage Analyser", "Health Analyser", "Laser Sight", "Recycler",
                // Scopes
                "Red Dot", "0.5x Scope", "2x Scope", "4x Scope", "8x Scope", "Periscope"
            },
            ["Consumables"] = new List<string>
            {
                // Ammo
                "Big Ammo", "Bolts", "Money Ammo", "Musket Ammo", "Normal Ammo", "Rocket Ammo",
                "Shotgun Ammo", "Small Ammo", "Taser Ammo", "Water Ammo",
                // Healing
                "Bandage", "Medkit"
            }
        };

        private void Awake()
        {
            Instance = this;
            
            Logger.LogMessage("[WeaponSpawnConfig] ========================================");
            Logger.LogMessage("[WeaponSpawnConfig] WEAPON SPAWN CONFIG v1.0.0 STARTING UP");
            Logger.LogMessage("[WeaponSpawnConfig] ========================================");
            
            try
            {
                _harmony = new Harmony("tabginstaller.weaponspawnconfig");
                Logger.LogInfo("[WeaponSpawnConfig] Harmony instance created");
                
                // Create configuration entries
                CreateConfigurations();
                
                // Apply harmony patches
                ApplyPatches();
                
                Logger.LogMessage($"[WeaponSpawnConfig] Initialization complete!");
                Logger.LogMessage($"[WeaponSpawnConfig] Loaded {_weaponSpawnRates.Count} weapon configurations");
                Logger.LogMessage("[WeaponSpawnConfig] ========================================");
            }
            catch (Exception e)
            {
                Logger.LogError($"[WeaponSpawnConfig] CRITICAL ERROR: {e}");
                Logger.LogError($"[WeaponSpawnConfig] Stack trace: {e.StackTrace}");
            }
        }
        
        private void CreateConfigurations()
        {
            // Global multiplier
            _weaponSpawnRates["Global"] = Config.Bind("Global", "Global Spawn Multiplier", 1.0f,
                new ConfigDescription("Global multiplier for all weapon spawns", 
                    new AcceptableValueRange<float>(0f, 10f)));
            
            // Category multipliers
            foreach (var category in WeaponCategories.Keys)
            {
                var categoryKey = $"Category_{SanitizeWeaponName(category)}";
                _weaponSpawnRates[categoryKey] = Config.Bind("Category Multipliers", category, 1.0f,
                    new ConfigDescription($"Multiplier for all {category} (applies on top of individual rates)", 
                        new AcceptableValueRange<float>(0f, 10f)));
            }
            
            // Individual weapon multipliers
            foreach (var category in WeaponCategories)
            {
                foreach (var weapon in category.Value)
                {
                    var configKey = SanitizeWeaponName(weapon);
                    var description = $"Spawn rate multiplier for {weapon} (0.0 = never spawn, 1.0 = normal, 2.0 = double chance)";
                    _weaponSpawnRates[configKey] = Config.Bind(category.Key, weapon, 1.0f, 
                        new ConfigDescription(description, new AcceptableValueRange<float>(0f, 10f)));
                }
            }
            
            Logger.LogInfo($"[WeaponSpawnConfig] Created {_weaponSpawnRates.Count} configuration entries");
        }
        
        private void ApplyPatches()
        {
            // Hook into server messages to detect when loot is being generated
            HookServerMessages();
            
            // Scan for loot pool related types
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            ScanForLootPoolTypes();
        }
        
        private void HookServerMessages()
        {
            try
            {
                // Patch Console.WriteLine to intercept server messages
                var writeLineMethod = typeof(Console).GetMethod("WriteLine", new[] { typeof(string) });
                if (writeLineMethod != null)
                {
                    _harmony.Patch(writeLineMethod, 
                        postfix: new HarmonyMethod(typeof(WeaponSpawnConfigPlugin).GetMethod(nameof(ConsoleWriteLinePostfix))));
                }
                
                // Patch Debug.Log
                var logMethod = typeof(Debug).GetMethod("Log", new[] { typeof(object) });
                if (logMethod != null)
                {
                    _harmony.Patch(logMethod, 
                        postfix: new HarmonyMethod(typeof(WeaponSpawnConfigPlugin).GetMethod(nameof(DebugLogPostfix))));
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning($"[WeaponSpawnConfig] Could not hook server messages: {e.Message}");
            }
        }
        
        public static void ConsoleWriteLinePostfix(string value)
        {
            if (Instance == null || string.IsNullOrEmpty(value)) return;
            
            // Look for the "Searching for guns..." message
            if (value.Contains("Searching for guns"))
            {
                Instance.Logger.LogMessage("[WeaponSpawnConfig] >>> Server is searching for guns! Weapon spawn config is active!");
            }
            else if (value.Contains("Found:") && value.Contains("Weapons"))
            {
                Instance.Logger.LogMessage($"[WeaponSpawnConfig] >>> {value}");
                Instance.Logger.LogMessage($"[WeaponSpawnConfig] >>> Modified {Instance._modifiedWeapons} weapon spawn rates");
            }
        }
        
        public static void DebugLogPostfix(object message)
        {
            if (Instance == null || message == null) return;
            
            var msgStr = message.ToString();
            if (msgStr.Contains("loot") || msgStr.Contains("Loot") || msgStr.Contains("spawn") || msgStr.Contains("Spawn"))
            {
                Instance.Logger.LogInfo($"[WeaponSpawnConfig] Game message: {msgStr}");
            }
        }
        
        private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            var assemblyName = args.LoadedAssembly.GetName().Name;
            if (assemblyName.Contains("Assembly-CSharp") || assemblyName.Contains("TABG") || assemblyName.Contains("Game"))
            {
                Logger.LogInfo($"[WeaponSpawnConfig] Game assembly loaded: {assemblyName}");
                ScanAssemblyForLootPool(args.LoadedAssembly);
            }
        }
        
        private void ScanForLootPoolTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (name.Contains("Assembly-CSharp") || name.Contains("TABG") || name.Contains("Game") || 
                    name.Contains("Server") || name.Contains("Landfall"))
                {
                    ScanAssemblyForLootPool(assembly);
                }
            }
        }
        
        private void ScanAssemblyForLootPool(Assembly assembly)
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    // Look for types that might handle loot pools
                    if (type.Name.Contains("Loot") || type.Name.Contains("Item") || type.Name.Contains("Spawn") || 
                        type.Name.Contains("Weapon") || type.Name.Contains("Drop") || type.Name.Contains("Generate"))
                    {
                        // Check fields for loot pool structures
                        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                        {
                            var fieldType = field.FieldType;
                            
                            // Look for arrays/lists that might contain loot pool entries
                            if (fieldType.IsArray || (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>)))
                            {
                                Logger.LogInfo($"[WeaponSpawnConfig] Found potential loot pool field: {type.Name}.{field.Name} ({fieldType})");
                                
                                // Try to patch getter/setter if it's a property
                                var property = type.GetProperty(field.Name);
                                if (property != null && property.GetMethod != null)
                                {
                                    try
                                    {
                                        _harmony.Patch(property.GetMethod, 
                                            postfix: new HarmonyMethod(typeof(WeaponSpawnConfigPlugin).GetMethod(nameof(LootPoolGetterPostfix))));
                                        Logger.LogInfo($"[WeaponSpawnConfig] Patched property getter: {type.Name}.{property.Name}");
                                    }
                                    catch { }
                                }
                            }
                        }
                        
                        // Look for methods that might generate or modify loot
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                        {
                            var methodName = method.Name.ToLower();
                            if (methodName.Contains("spawn") || methodName.Contains("generate") || methodName.Contains("create") ||
                                methodName.Contains("loot") || methodName.Contains("drop") || methodName.Contains("search"))
                            {
                                try
                                {
                                    _harmony.Patch(method, 
                                        prefix: new HarmonyMethod(typeof(WeaponSpawnConfigPlugin).GetMethod(nameof(UniversalLootPrefix))),
                                        postfix: new HarmonyMethod(typeof(WeaponSpawnConfigPlugin).GetMethod(nameof(UniversalLootPostfix))));
                                    Logger.LogInfo($"[WeaponSpawnConfig] Patched method: {type.Name}.{method.Name}");
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"[WeaponSpawnConfig] Error scanning assembly {assembly.GetName().Name}: {e.Message}");
            }
        }
        
        public static void LootPoolGetterPostfix(ref object __result)
        {
            if (__result == null || Instance == null) return;
            
            try
            {
                // Check if this is a collection of loot pool entries
                if (__result is System.Collections.IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        ProcessPotentialLootPoolEntry(item);
                    }
                }
            }
            catch { }
        }
        
        public static void UniversalLootPrefix(MethodBase __originalMethod)
        {
            if (Instance != null)
            {
                Instance.Logger.LogInfo($"[WeaponSpawnConfig] Pre-hook: {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}");
            }
        }
        
        public static void UniversalLootPostfix(MethodBase __originalMethod, object __result)
        {
            if (Instance == null) return;
            
            Instance.Logger.LogInfo($"[WeaponSpawnConfig] Post-hook: {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}");
            
            if (__result != null)
            {
                ProcessPotentialLootPoolEntry(__result);
            }
        }
        
        private static void ProcessPotentialLootPoolEntry(object obj)
        {
            if (obj == null || Instance == null) return;
            
            try
            {
                var type = obj.GetType();
                
                // Check for "name" and "weight" fields/properties
                var nameField = type.GetField("name") ?? type.GetField("Name");
                var nameProp = type.GetProperty("name") ?? type.GetProperty("Name");
                var weightField = type.GetField("weight") ?? type.GetField("Weight");
                var weightProp = type.GetProperty("weight") ?? type.GetProperty("Weight");
                
                string itemName = null;
                float? currentWeight = null;
                
                // Get name
                if (nameField != null)
                    itemName = nameField.GetValue(obj) as string;
                else if (nameProp != null)
                    itemName = nameProp.GetValue(obj) as string;
                
                // Get weight
                if (weightField != null && weightField.FieldType == typeof(float))
                    currentWeight = (float)weightField.GetValue(obj);
                else if (weightProp != null && weightProp.PropertyType == typeof(float))
                    currentWeight = (float)weightProp.GetValue(obj);
                
                // If we found both name and weight, this is likely a loot pool entry
                if (!string.IsNullOrEmpty(itemName) && currentWeight.HasValue)
                {
                    var multiplier = Instance.GetFinalSpawnRate(itemName);
                    if (Math.Abs(multiplier - 1.0f) > 0.001f) // If multiplier is not 1.0
                    {
                        var newWeight = currentWeight.Value * multiplier;
                        
                        // Set new weight
                        if (weightField != null && !weightField.IsInitOnly && !weightField.IsLiteral)
                        {
                            weightField.SetValue(obj, newWeight);
                            Instance._modifiedWeapons++;
                            Instance.Logger.LogMessage($"[WeaponSpawnConfig] Modified {itemName}: {currentWeight.Value} -> {newWeight} (x{multiplier})");
                        }
                        else if (weightProp != null && weightProp.CanWrite)
                        {
                            weightProp.SetValue(obj, newWeight);
                            Instance._modifiedWeapons++;
                            Instance.Logger.LogMessage($"[WeaponSpawnConfig] Modified {itemName}: {currentWeight.Value} -> {newWeight} (x{multiplier})");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Instance?.Logger.LogWarning($"[WeaponSpawnConfig] Error processing potential loot entry: {e.Message}");
            }
        }
        
        private string SanitizeWeaponName(string name)
        {
            return name.Replace(" ", "_").Replace("-", "_");
        }
        
        public float GetFinalSpawnRate(string weaponName)
        {
            var multiplier = 1.0f;
            
            // Global multiplier
            if (_weaponSpawnRates.TryGetValue("Global", out var globalConfig))
                multiplier *= globalConfig.Value;
            
            // Category multiplier
            string category = GetCategoryForWeapon(weaponName);
            if (!string.IsNullOrEmpty(category))
            {
                var categoryKey = $"Category_{SanitizeWeaponName(category)}";
                if (_weaponSpawnRates.TryGetValue(categoryKey, out var categoryConfig))
                    multiplier *= categoryConfig.Value;
            }
            
            // Individual weapon multiplier
            var weaponKey = SanitizeWeaponName(weaponName);
            if (_weaponSpawnRates.TryGetValue(weaponKey, out var weaponConfig))
                multiplier *= weaponConfig.Value;
            
            return multiplier;
        }
        
        private string GetCategoryForWeapon(string weaponName)
        {
            foreach (var category in WeaponCategories)
            {
                if (category.Value.Any(w => w.Equals(weaponName, StringComparison.OrdinalIgnoreCase)))
                {
                    return category.Key;
                }
            }
            return "";
        }
        
        private void OnDestroy()
        {
            Logger.LogMessage($"[WeaponSpawnConfig] Shutting down. Modified {_modifiedWeapons} weapon spawn rates during session.");
            _harmony?.UnpatchSelf();
        }
    }
} 