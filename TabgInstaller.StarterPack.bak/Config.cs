using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

namespace TabgInstaller.StarterPack
{
    public class Config
    {
        private static StarterPackConfig _config;
        private static string _configPath;
        
        // Match Settings
        public static WinCondition winCondition => _config?.MatchSettings?.WinCondition ?? WinCondition.Default;
        public static int killsToWin => _config?.MatchSettings?.KillsToWin ?? 30;
        public static bool forceKillOffStart => _config?.MatchSettings?.ForceKillOffStart ?? false;

        // Drop Settings
        public static bool dropItemsOnDeath => _config?.DropSettings?.DropItemsOnDeath ?? false;
        public static string givenItems => _config?.DropSettings?.ItemsGivenOnKill ?? "";

        // Ring Manager
        public static List<RingContainer> ringPositions => _config?.RingSettings?.RingPositions ?? new List<RingContainer>();
        public static RingContainer chosenRing { get; set; }

        // Respawning Players
        public static List<Loadout> loadouts => _config?.RespawnSettings?.Loadouts ?? new List<Loadout>();
        public static bool HealOnKill => _config?.PlayerSettings?.HealOnKill ?? false;
        public static float HealOnKillAmount => _config?.PlayerSettings?.HealOnKillAmount ?? 20f;
        public static bool canGoDown => _config?.PlayerSettings?.CanGoDown ?? true;
        public static bool canLockOut => _config?.PlayerSettings?.CanLockOut ?? true;

        // Lobby Spawn Controller
        public static int[] spawnPoints => _config?.LobbySettings?.ValidSpawnPoints ?? new int[] { 2 };
        public static Vector3 CustomSpawnPoint => _config?.LobbySettings?.CustomSpawnPoint ?? Vector3.zero;

        // Vote To Start
        public static int percentOfVotes => _config?.VoteSettings?.PercentOfVotes ?? 50;
        public static int minNumberOfPlayers => _config?.VoteSettings?.MinNumberOfPlayers ?? 2;
        public static int timeToStart => _config?.VoteSettings?.TimeToStart ?? 30;

        // Spell Drop Controller
        public static bool spelldropEnabled => _config?.SpellDropSettings?.Enabled ?? true;
        public static int minSpellDropDelay => _config?.SpellDropSettings?.MinDelay ?? 180;
        public static int maxSpellDropDelay => _config?.SpellDropSettings?.MaxDelay ?? 420;
        public static int spellDropOffset => _config?.SpellDropSettings?.StartOffset ?? 30;

        // Match Timeout
        public static float preMatchTimeout => _config?.TimeoutSettings?.PreMatchTimeout ?? 15f;
        public static float periMatchTimer => _config?.TimeoutSettings?.PeriMatchTimeout ?? 15f;

        public static void LoadConfig()
        {
            var serverPath = AppDomain.CurrentDomain.BaseDirectory;
            _configPath = Path.Combine(serverPath, "BepInEx", "config", "TabgInstaller", "StarterPack.json");
            
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    _config = JsonConvert.DeserializeObject<StarterPackConfig>(json);
                    Plugin.Log?.LogInfo("StarterPack configuration loaded from JSON");
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"Failed to load StarterPack config: {ex.Message}");
                    _config = new StarterPackConfig();
                }
            }
            else
            {
                _config = new StarterPackConfig();
                SaveConfig();
            }
            
            // Choose initial ring
            if (ringPositions != null && ringPositions.Count > 0)
            {
                chosenRing = ChooseRing();
            }
        }
        
        public static void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
                var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Failed to save StarterPack config: {ex.Message}");
            }
        }
        
        public static void Setup(object __instance)
        {
            // This is called when the server initializes
            // Reload config in case it was modified
            LoadConfig();
            Plugin.Log?.LogInfo("StarterPack configuration initialized");
        }
        
        public static Loadout ChooseLoadout()
        {
            if (loadouts == null || loadouts.Count == 0) return null;
            
            int totalWeight = loadouts.Sum(l => l.Rarity);
            int randomNumber = UnityEngine.Random.Range(1, totalWeight + 1);

            foreach (var loadout in loadouts)
            {
                if (randomNumber <= loadout.Rarity)
                {
                    return loadout;
                }
                randomNumber -= loadout.Rarity;
            }
            
            return loadouts.FirstOrDefault();
        }

        public static RingContainer ChooseRing()
        {
            if (ringPositions == null || ringPositions.Count == 0) 
            {
                return new RingContainer 
                { 
                    Name = "Default", 
                    Rarity = 1, 
                    Sizes = new int[] { 4000, 1300, 300 },
                    Location = Vector3.zero 
                };
            }
            
            int totalWeight = ringPositions.Sum(r => r.Rarity);
            int randomNumber = UnityEngine.Random.Range(1, totalWeight + 1);

            foreach (var ring in ringPositions)
            {
                if (randomNumber <= ring.Rarity)
                {
                    return ring;
                }
                randomNumber -= ring.Rarity;
            }
            
            return ringPositions.FirstOrDefault();
        }
    }

    public enum WinCondition
    {
        Default,
        KillsToWin,
        Debug
    }

    [Serializable]
    public class StarterPackConfig
    {
        public MatchSettings MatchSettings { get; set; } = new MatchSettings();
        public DropSettings DropSettings { get; set; } = new DropSettings();
        public RingSettings RingSettings { get; set; } = new RingSettings();
        public RespawnSettings RespawnSettings { get; set; } = new RespawnSettings();
        public PlayerSettings PlayerSettings { get; set; } = new PlayerSettings();
        public LobbySettings LobbySettings { get; set; } = new LobbySettings();
        public VoteSettings VoteSettings { get; set; } = new VoteSettings();
        public SpellDropSettings SpellDropSettings { get; set; } = new SpellDropSettings();
        public TimeoutSettings TimeoutSettings { get; set; } = new TimeoutSettings();
    }

    [Serializable]
    public class MatchSettings
    {
        public WinCondition WinCondition { get; set; } = WinCondition.Default;
        public int KillsToWin { get; set; } = 30;
        public bool ForceKillOffStart { get; set; } = false;
    }

    [Serializable]
    public class DropSettings
    {
        public bool DropItemsOnDeath { get; set; } = false;
        public string ItemsGivenOnKill { get; set; } = "";
    }

    [Serializable]
    public class RingSettings
    {
        public List<RingContainer> RingPositions { get; set; } = new List<RingContainer>
        {
            new RingContainer 
            { 
                Name = "Default", 
                Rarity = 1, 
                Sizes = new int[] { 4000, 1300, 300 },
                Location = new Vector3(0, 0, 0)
            }
        };
    }

    [Serializable]
    public class RespawnSettings
    {
        public List<Loadout> Loadouts { get; set; } = new List<Loadout>();
    }

    [Serializable]
    public class PlayerSettings
    {
        public bool HealOnKill { get; set; } = false;
        public float HealOnKillAmount { get; set; } = 20f;
        public bool CanGoDown { get; set; } = true;
        public bool CanLockOut { get; set; } = true;
    }

    [Serializable]
    public class LobbySettings
    {
        public int[] ValidSpawnPoints { get; set; } = new int[] { 2 };
        public Vector3 CustomSpawnPoint { get; set; } = Vector3.zero;
    }

    [Serializable]
    public class VoteSettings
    {
        public int PercentOfVotes { get; set; } = 50;
        public int MinNumberOfPlayers { get; set; } = 2;
        public int TimeToStart { get; set; } = 30;
    }

    [Serializable]
    public class SpellDropSettings
    {
        public bool Enabled { get; set; } = true;
        public int MinDelay { get; set; } = 180;
        public int MaxDelay { get; set; } = 420;
        public int StartOffset { get; set; } = 30;
    }

    [Serializable]
    public class TimeoutSettings
    {
        public float PreMatchTimeout { get; set; } = 15f;
        public float PeriMatchTimeout { get; set; } = 15f;
    }

    [Serializable]
    public class Loadout
    {
        public string Name { get; set; }
        public int Rarity { get; set; }
        public List<int> ItemIds { get; set; }
        public List<int> ItemQuantities { get; set; }

        public Loadout()
        {
            ItemIds = new List<int>();
            ItemQuantities = new List<int>();
        }

        public Loadout(string name, int rarity, List<int> itemIds, List<int> itemQuantities)
        {
            Name = name;
            Rarity = rarity;
            ItemIds = itemIds;
            ItemQuantities = itemQuantities;
        }
    }

    [Serializable]
    public class RingContainer
    {
        public string Name { get; set; }
        public int Rarity { get; set; }
        public int[] Sizes { get; set; }
        public float[] Speeds { get; set; }
        public Vector3 Location { get; set; }

        public RingContainer()
        {
            Sizes = new int[] { 4000, 1300, 300 };
            Speeds = new float[] { 6f, 6f, 0f };
        }

        public RingContainer(string name, int rarity, int[] sizes, float[] speeds, Vector3 location)
        {
            Name = name;
            Rarity = rarity;
            Sizes = sizes;
            Speeds = speeds;
            Location = location;
        }
    }
} 
