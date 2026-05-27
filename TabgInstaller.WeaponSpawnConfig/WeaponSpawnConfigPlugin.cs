using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace TabgInstaller.WeaponSpawnConfig
{
    [BepInPlugin("tabginstaller.weaponspawnconfig", "TABG Weapon Spawn Config", "1.0.0")]
    public class WeaponSpawnConfigPlugin : BaseUnityPlugin
    {
        public static WeaponSpawnConfigPlugin Instance { get; private set; }
        private Harmony _harmony;
        private Dictionary<string, ConfigEntry<float>> _weaponSpawnRates = new Dictionary<string, ConfigEntry<float>>();
        private readonly Dictionary<string, string> _itemNameAliases = new Dictionary<string, string>();
        private readonly Dictionary<int, string> _itemIndexToConfigName = new Dictionary<int, string>();
        private readonly HashSet<string> _unknownLogged = new HashSet<string>();
        private int _modifiedRolls;
        private int _zeroWeightRolls;
        private int _lootRolls;
        
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
            RegisterConfiguredNames();

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
            _harmony.Patch(
                AccessTools.Method(typeof(LootPreset), nameof(LootPreset.GetWeaponToSpawn)),
                prefix: new HarmonyMethod(typeof(WeaponSpawnConfigPlugin), nameof(LootPresetGetWeaponToSpawnPrefix)));

            _harmony.Patch(
                AccessTools.Method(typeof(LootDatabase), nameof(LootDatabase.Init)),
                postfix: new HarmonyMethod(typeof(WeaponSpawnConfigPlugin), nameof(LootDatabaseInitPostfix)));

            _harmony.Patch(
                AccessTools.Method(typeof(GameRoom), nameof(GameRoom.SearchForGuns)),
                prefix: new HarmonyMethod(typeof(WeaponSpawnConfigPlugin), nameof(SearchForGunsPrefix)),
                postfix: new HarmonyMethod(typeof(WeaponSpawnConfigPlugin), nameof(SearchForGunsPostfix)));

            Logger.LogInfo("[WeaponSpawnConfig] Patched LootPreset.GetWeaponToSpawn, LootDatabase.Init, and GameRoom.SearchForGuns");
        }

        public static bool LootPresetGetWeaponToSpawnPrefix(LootPreset __instance, ref Loot[] __result)
        {
            if (Instance == null || __instance == null || __instance.loot == null)
            {
                return true;
            }

            try
            {
                var weightedEntries = new List<WeightedLootEntry>(__instance.loot.Count);
                float totalWeight = 0f;

                foreach (var entry in __instance.loot)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    string configName = Instance.ResolveConfigName(entry);
                    float multiplier = Instance.GetFinalSpawnRate(configName);
                    float adjustedWeight = Mathf.Max(0f, entry.spawnRate * multiplier);

                    if (Math.Abs(multiplier - 1f) > 0.001f)
                    {
                        Instance._modifiedRolls++;
                    }

                    if (adjustedWeight <= 0f)
                    {
                        Instance._zeroWeightRolls++;
                        continue;
                    }

                    weightedEntries.Add(new WeightedLootEntry(entry, adjustedWeight));
                    totalWeight += adjustedWeight;
                }

                Instance._lootRolls++;

                if (weightedEntries.Count == 0 || totalWeight <= 0f)
                {
                    __result = new Loot[0];
                    return false;
                }

                float roll = UnityEngine.Random.Range(0f, totalWeight);
                float cursor = 0f;
                for (int i = 0; i < weightedEntries.Count; i++)
                {
                    cursor += weightedEntries[i].Weight;
                    if (roll < cursor)
                    {
                        __result = weightedEntries[i].Entry.m_loot ?? new Loot[0];
                        return false;
                    }
                }

                __result = weightedEntries[weightedEntries.Count - 1].Entry.m_loot ?? new Loot[0];
                return false;
            }
            catch (Exception ex)
            {
                Instance.Logger.LogWarning($"[WeaponSpawnConfig] Loot roll patch failed; falling back to original selector: {ex.Message}");
                return true;
            }
        }

        public static void LootDatabaseInitPostfix(LootDatabase __instance)
        {
            Instance?.BuildRuntimeItemMap(__instance);
        }

        public static void SearchForGunsPrefix()
        {
            if (Instance == null) return;

            Instance._modifiedRolls = 0;
            Instance._zeroWeightRolls = 0;
            Instance._lootRolls = 0;
        }

        public static void SearchForGunsPostfix(GameRoom __instance)
        {
            if (Instance == null) return;

            int spawned = __instance?.Weapons?.Count ?? 0;
            Instance.Logger.LogInfo(
                $"[WeaponSpawnConfig] Map loot generated: rolls={Instance._lootRolls}, adjusted={Instance._modifiedRolls}, disabledChoices={Instance._zeroWeightRolls}, spawnedNetworkLoot={spawned}");
        }
        
        private void RegisterConfiguredNames()
        {
            _itemNameAliases.Clear();

            foreach (var category in WeaponCategories)
            {
                foreach (var itemName in category.Value)
                {
                    RegisterAlias(itemName, itemName);
                }
            }
        }

        private void BuildRuntimeItemMap(LootDatabase database)
        {
            if (database == null) return;

            int mapped = 0;
            _itemIndexToConfigName.Clear();

            var itemsField = AccessTools.Field(typeof(LootDatabase), "items");
            var items = itemsField?.GetValue(database) as Dictionary<int, ItemDataEntry>;
            if (items == null)
            {
                Logger.LogWarning("[WeaponSpawnConfig] Could not read LootDatabase item dictionary; runtime ID mapping will be populated lazily.");
                return;
            }

            foreach (var item in items)
            {
                var pickup = item.Value.pickup;
                if (pickup == null)
                {
                    continue;
                }

                string configName = ResolveConfigName(pickup);
                if (string.IsNullOrEmpty(configName))
                {
                    continue;
                }

                _itemIndexToConfigName[item.Key] = configName;
                RegisterAlias(pickup.itemName, configName);
                RegisterAlias(pickup.name, configName);
                RegisterAlias(item.Value.prefab != null ? item.Value.prefab.name : null, configName);
                mapped++;
            }

            Logger.LogInfo($"[WeaponSpawnConfig] Item map ready: {mapped} loot IDs mapped to config entries");
        }

        private string ResolveConfigName(LootDropWrapper entry)
        {
            if (entry?.m_loot == null) return null;

            foreach (var loot in entry.m_loot)
            {
                if (loot.loot == null) continue;

                var pickup = loot.loot.GetComponent<Pickup>();
                string configName = ResolveConfigName(pickup);
                if (!string.IsNullOrEmpty(configName))
                {
                    return configName;
                }

                configName = ResolveConfigName(loot.loot.name);
                if (!string.IsNullOrEmpty(configName))
                {
                    return configName;
                }
            }

            return null;
        }

        private string ResolveConfigName(Pickup pickup)
        {
            if (pickup == null) return null;

            if (_itemIndexToConfigName.TryGetValue(pickup.m_itemIndex, out var mappedName))
            {
                return mappedName;
            }

            string configName = ResolveConfigName(pickup.itemName) ?? ResolveConfigName(pickup.name);
            if (!string.IsNullOrEmpty(configName))
            {
                _itemIndexToConfigName[pickup.m_itemIndex] = configName;
            }

            return configName;
        }

        private string ResolveConfigName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return null;

            string normalized = NormalizeName(itemName);
            if (_itemNameAliases.TryGetValue(normalized, out var configName))
            {
                return configName;
            }

            if (_itemNameAliases.TryGetValue(CompactName(normalized), out configName))
            {
                return configName;
            }

            if (_unknownLogged.Add(normalized))
            {
                Logger.LogDebug($"[WeaponSpawnConfig] No config mapping for loot item '{itemName}'");
            }

            return null;
        }

        private void RegisterAlias(string alias, string configName)
        {
            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(configName))
            {
                return;
            }

            _itemNameAliases[NormalizeName(alias)] = configName;
            _itemNameAliases[CompactName(NormalizeName(alias))] = configName;
        }

        private static string NormalizeName(string name)
        {
            return name
                .Replace("(Clone)", string.Empty)
                .Replace("_", " ")
                .Replace("-", " ")
                .Trim()
                .ToLowerInvariant();
        }

        private static string CompactName(string normalizedName)
        {
            return normalizedName.Replace(" ", string.Empty);
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
            if (string.IsNullOrWhiteSpace(weaponName))
            {
                return multiplier;
            }

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

        public float GetSpawnRateMultiplier(string weaponName)
        {
            if (string.IsNullOrWhiteSpace(weaponName))
            {
                return 1f;
            }

            var weaponKey = SanitizeWeaponName(weaponName);
            return _weaponSpawnRates.TryGetValue(weaponKey, out var weaponConfig) ? weaponConfig.Value : 1f;
        }

        public float GetCategoryMultiplier(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return 1f;
            }

            var categoryKey = $"Category_{SanitizeWeaponName(categoryName)}";
            return _weaponSpawnRates.TryGetValue(categoryKey, out var categoryConfig) ? categoryConfig.Value : 1f;
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
            Logger.LogMessage("[WeaponSpawnConfig] Shutting down.");
            _harmony?.UnpatchSelf();
        }

        private readonly struct WeightedLootEntry
        {
            public WeightedLootEntry(LootDropWrapper entry, float weight)
            {
                Entry = entry;
                Weight = weight;
            }

            public LootDropWrapper Entry { get; }

            public float Weight { get; }
        }
    }
}
