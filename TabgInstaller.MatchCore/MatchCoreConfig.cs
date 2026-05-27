using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using UnityEngine;

[assembly: InternalsVisibleTo("TabgInstaller.Tests")]

namespace TabgInstaller.MatchCore
{
    internal sealed class MatchCoreConfig
    {
        private const string FileName = "TheStarterPack.txt";

        public bool ForceDropAtStart = false;
        public bool DropItemsOnDeath = true;
        public bool CanGoDown = true;
        public bool CanLockOut = true;
        public bool HealOnKill = false;
        public float HealOnKillAmount = 100f;
        public bool SpellDropsEnabled = true;
        public float MinSpellDropDelay = 60f;
        public float MaxSpellDropDelay = 100f;
        public float SpellDropOffset = 0f;
        public float VotePercent = 60f;
        public int VoteMinimumPlayers = 1;
        public float VoteStartCountdown = 5f;
        public float PreMatchTimeout = 0f;
        public float MatchTimeout = 0f;
        public WinConditionMode WinCondition = WinConditionMode.Default;
        public int KillsToWin = 20;
        public int LobbySpawnPoint = -1;
        public Vector3 CustomSpawnPoint = Vector3.zero;
        public List<Vector3> MatchSpawnPoints = new List<Vector3>();
        public List<LootItem> ItemsGiven = new List<LootItem>();
        public List<LoadoutDefinition> Loadouts = new List<LoadoutDefinition>();
        public List<RingProfile> Rings = new List<RingProfile>();

        public static MatchCoreConfig LoadOrCreate(ManualLogSource logger)
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
                WriteDefault(path);

            var cfg = new MatchCoreConfig();
            try
            {
                cfg.ApplyLines(File.ReadAllLines(path));
            }
            catch (Exception ex)
            {
                logger?.LogWarning("[MatchCore] Failed to read TheStarterPack.txt: " + ex.Message);
            }

            ReadSupplementalConfigs(cfg, logger);
            cfg.Normalize();
            logger?.LogInfo("[MatchCore] Loaded config from " + path);
            return cfg;
        }

        internal static MatchCoreConfig Parse(IEnumerable<string> lines)
        {
            var cfg = new MatchCoreConfig();
            cfg.ApplyLines(lines);
            cfg.Normalize();
            return cfg;
        }

        private void ApplyLines(IEnumerable<string> lines)
        {
            if (lines == null) return;

            foreach (var raw in lines)
            {
                var line = StripComment(raw ?? string.Empty).Trim();
                if (line.Length == 0) continue;

                int equals = line.IndexOf('=');
                if (equals <= 0) continue;

                string key = line.Substring(0, equals).Trim();
                string value = line.Substring(equals + 1).Trim();
                Apply(key, value);
            }
        }

        private static string GetConfigPath()
        {
            return Path.Combine(GetServerRoot(), FileName);
        }

        private static string GetServerRoot()
        {
            var parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Application.dataPath;
        }

        private static void ReadSupplementalConfigs(MatchCoreConfig cfg, ManualLogSource logger)
        {
            string root = GetServerRoot();
            var paths = new[]
            {
                Path.Combine(root, "BepInEx", "config", "TabgInstaller.MatchCore.cfg"),
                Path.Combine(root, "BepInEx", "config", "FreddoCustomSpawnpoints.cfg"),
                Path.Combine(root, "BepInEx", "config", "FreddoFixStarterPack.cfg")
            };

            foreach (string path in paths)
            {
                if (!File.Exists(path)) continue;

                try
                {
                    foreach (var raw in File.ReadAllLines(path))
                    {
                        var line = StripComment(raw).Trim();
                        if (line.Length == 0 || line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal)) continue;

                        int equals = line.IndexOf('=');
                        if (equals <= 0) continue;

                        cfg.Apply(line.Substring(0, equals).Trim(), line.Substring(equals + 1).Trim());
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogWarning("[MatchCore] Failed to read " + path + ": " + ex.Message);
                }
            }
        }

        private static string StripComment(string value)
        {
            int hash = value.IndexOf('#');
            int slash = value.IndexOf("//", StringComparison.Ordinal);
            int comment = hash >= 0 && slash >= 0 ? Math.Min(hash, slash) : Math.Max(hash, slash);
            return comment >= 0 ? value.Substring(0, comment) : value;
        }

        private static void WriteDefault(string path)
        {
            File.WriteAllLines(path, new[]
            {
                "# TABG MatchCore config. Kept compatible with existing launcher StarterPack screens.",
                "ForceKillAtStart=false",
                "DropItemsOnDeath=true",
                "CanGoDown=true",
                "CanLockOut=true",
                "HealOnKill=false",
                "HealOnKillAmount=100",
                "ItemsGiven=",
                "Loadouts=Default:100%1:1,2:30/",
                "ValidSpawnPoints=-1",
                "CustomSpawnPoint=0,150,0",
                "RingLocation=0,160,0",
                "RingSizes=4240,3450,1710,830,360,140",
                "RingSpeeds=25,3,1.5,1.5,2,2",
                "PercentOfVotes=60",
                "MinNumberOfPlayers=1",
                "TimeToStart=5",
                "PreMatchTimeout=0",
                "MatchTimeout=0",
                "WinCondition=Default",
                "KillsToWin=20",
                "SpelldropEnabled=true",
                "MinSpellDropDelay=60",
                "MaxSpellDropDelay=100",
                "SpellDropOffset=0"
            });
        }

        private void Apply(string key, string value)
        {
            switch (key.Trim().ToLowerInvariant())
            {
                case "forcekillatstart":
                case "forcedropatstart":
                    ForceDropAtStart = ParseBool(value, ForceDropAtStart);
                    break;
                case "dropitemsondeath":
                    DropItemsOnDeath = ParseBool(value, DropItemsOnDeath);
                    break;
                case "cangodown":
                    CanGoDown = ParseBool(value, CanGoDown);
                    break;
                case "canlockout":
                    CanLockOut = ParseBool(value, CanLockOut);
                    break;
                case "healonkill":
                    HealOnKill = ParseBool(value, HealOnKill);
                    break;
                case "healonkillamount":
                    HealOnKillAmount = ParseFloat(value, HealOnKillAmount);
                    break;
                case "itemsgiven":
                    ItemsGiven = ParseLootItems(value);
                    break;
                case "loadouts":
                    Loadouts = ParseLoadouts(value);
                    break;
                case "validspawnpoints":
                case "lobbyspawnpoint":
                    LobbySpawnPoint = ParseInt(value, LobbySpawnPoint);
                    break;
                case "customspawnpoint":
                    CustomSpawnPoint = ParseVector(value, CustomSpawnPoint);
                    break;
                case "spawnpoints":
                case "matchspawnpoints":
                    MatchSpawnPoints = ParseSpawnPoints(value, CustomSpawnPoint.y > 0f ? CustomSpawnPoint.y : 150f);
                    break;
                case "ringsettings":
                    Rings = ParseRingProfiles(value);
                    break;
                case "ringlocation":
                    EnsureDefaultRing().Center = ParseVector(value, EnsureDefaultRing().Center);
                    break;
                case "ringsizes":
                    EnsureDefaultRing().Sizes = ParseFloatList(value);
                    break;
                case "ringspeeds":
                    EnsureDefaultRing().Speeds = ParseFloatList(value);
                    break;
                case "percentofvotes":
                    VotePercent = ParseFloat(value, VotePercent);
                    break;
                case "minnumberofplayers":
                    VoteMinimumPlayers = ParseInt(value, VoteMinimumPlayers);
                    break;
                case "timetostart":
                    VoteStartCountdown = ParseFloat(value, VoteStartCountdown);
                    break;
                case "prematchtimeout":
                    PreMatchTimeout = ParseFloat(value, PreMatchTimeout);
                    break;
                case "perimatchtimeout":
                case "matchtimeout":
                    MatchTimeout = ParseFloat(value, MatchTimeout);
                    break;
                case "wincondition":
                    WinCondition = ParseWinCondition(value);
                    break;
                case "killstowin":
                    KillsToWin = ParseInt(value, KillsToWin);
                    break;
                case "spelldropenabled":
                case "spelldropsenabled":
                    SpellDropsEnabled = ParseBool(value, SpellDropsEnabled);
                    break;
                case "minspelldropdelay":
                    MinSpellDropDelay = ParseFloat(value, MinSpellDropDelay);
                    break;
                case "maxspelldropdelay":
                    MaxSpellDropDelay = ParseFloat(value, MaxSpellDropDelay);
                    break;
                case "spelldropoffset":
                    SpellDropOffset = ParseFloat(value, SpellDropOffset);
                    break;
            }
        }

        private void Normalize()
        {
            VotePercent = Mathf.Clamp(VotePercent, 1f, 100f);
            VoteMinimumPlayers = Math.Max(1, VoteMinimumPlayers);
            VoteStartCountdown = Math.Max(0f, VoteStartCountdown);
            PreMatchTimeout = Math.Max(0f, PreMatchTimeout);
            MatchTimeout = Math.Max(0f, MatchTimeout);
            KillsToWin = Math.Max(1, KillsToWin);
            HealOnKillAmount = Math.Max(0f, HealOnKillAmount);
            MinSpellDropDelay = Math.Max(0f, MinSpellDropDelay);
            MaxSpellDropDelay = Math.Max(MinSpellDropDelay, MaxSpellDropDelay);
            if (Rings.Count == 0)
                Rings.Add(new RingProfile { Name = "Default", Rarity = 100f, Center = Vector3.zero });
        }

        private RingProfile EnsureDefaultRing()
        {
            if (Rings.Count == 0)
                Rings.Add(new RingProfile { Name = "Default", Rarity = 100f, Center = Vector3.zero });
            return Rings[0];
        }

        private static WinConditionMode ParseWinCondition(string value)
        {
            if (Enum.TryParse(value, true, out WinConditionMode parsed))
                return parsed;
            return WinConditionMode.Default;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (bool.TryParse(value, out bool result)) return result;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) return number != 0;
            if (value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value.Equals("on", StringComparison.OrdinalIgnoreCase)) return true;
            if (value.Equals("no", StringComparison.OrdinalIgnoreCase) || value.Equals("off", StringComparison.OrdinalIgnoreCase)) return false;
            return fallback;
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : fallback;
        }

        private static float ParseFloat(string value, float fallback)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : fallback;
        }

        private static Vector3 ParseVector(string value, Vector3 fallback)
        {
            var parts = value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return fallback;
            return new Vector3(
                ParseFloat(parts[0], fallback.x),
                ParseFloat(parts[1], fallback.y),
                ParseFloat(parts[2], fallback.z));
        }

        private static List<Vector3> ParseSpawnPoints(string value, float fallbackY)
        {
            var points = new List<Vector3>();
            foreach (var token in value.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    float x = ParseFloat(parts[0], float.NaN);
                    float z = ParseFloat(parts[1], float.NaN);
                    if (!float.IsNaN(x) && !float.IsNaN(z))
                        points.Add(new Vector3(x, fallbackY, z));
                }
                else if (parts.Length >= 3)
                {
                    points.Add(new Vector3(
                        ParseFloat(parts[0], 0f),
                        ParseFloat(parts[1], fallbackY),
                        ParseFloat(parts[2], 0f)));
                }
            }
            return points;
        }

        private static float[] ParseFloatList(string value)
        {
            return value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => ParseFloat(v, 0f))
                .Where(v => v > 0f)
                .ToArray();
        }

        private static List<LootItem> ParseLootItems(string value)
        {
            var items = new List<LootItem>();
            foreach (var token in value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split(':');
                int id = parts.Length > 0 ? ParseInt(parts[0], -1) : -1;
                int amount = parts.Length > 1 ? ParseInt(parts[1], 1) : 1;
                if (id >= 0 && amount > 0)
                    items.Add(new LootItem(id, amount));
            }
            return items;
        }

        private static List<LoadoutDefinition> ParseLoadouts(string value)
        {
            var result = new List<LoadoutDefinition>();
            foreach (var chunk in value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int percent = chunk.IndexOf('%');
                if (percent <= 0) continue;

                string header = chunk.Substring(0, percent);
                string items = chunk.Substring(percent + 1);
                int colon = header.LastIndexOf(':');
                string name = colon > 0 ? header.Substring(0, colon) : "Loadout";
                int weight = colon > 0 ? ParseInt(header.Substring(colon + 1), 1) : 1;
                int paren = name.IndexOf('(');
                if (paren >= 0) name = name.Substring(0, paren);

                var parsedItems = ParseLootItems(items);
                if (parsedItems.Count > 0)
                    result.Add(new LoadoutDefinition(name.Trim(), Math.Max(1, weight), parsedItems));
            }
            return result;
        }

        private static List<RingProfile> ParseRingProfiles(string value)
        {
            var rings = new List<RingProfile>();
            foreach (var chunk in value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int percent = chunk.IndexOf('%');
                if (percent <= 0) continue;

                string header = chunk.Substring(0, percent);
                string data = chunk.Substring(percent + 1);
                int headerColon = header.LastIndexOf(':');
                string name = headerColon > 0 ? header.Substring(0, headerColon) : "Ring";
                float rarity = headerColon > 0 ? ParseFloat(header.Substring(headerColon + 1), 1f) : 1f;

                int dataColon = data.IndexOf(':');
                Vector3 center = dataColon >= 0 ? ParseVector(data.Substring(0, dataColon), Vector3.zero) : ParseVector(data, Vector3.zero);
                float[] sizes = dataColon >= 0 ? ParseFloatList(data.Substring(dataColon + 1)) : Array.Empty<float>();

                rings.Add(new RingProfile { Name = name.Trim(), Rarity = Math.Max(1f, rarity), Center = center, Sizes = sizes });
            }
            return rings;
        }
    }

    internal enum WinConditionMode
    {
        Default,
        KillsToWin,
        Debug
    }

    internal sealed class RingProfile
    {
        public string Name = "Default";
        public float Rarity = 100f;
        public Vector3 Center = Vector3.zero;
        public float[] Sizes = Array.Empty<float>();
        public float[] Speeds = Array.Empty<float>();
    }

    internal sealed class LoadoutDefinition
    {
        public readonly string Name;
        public readonly int Weight;
        public readonly List<LootItem> Items;

        public LoadoutDefinition(string name, int weight, List<LootItem> items)
        {
            Name = name;
            Weight = weight;
            Items = items;
        }
    }

    internal sealed class LootItem
    {
        public readonly int Id;
        public readonly int Amount;

        public LootItem(int id, int amount)
        {
            Id = id;
            Amount = amount;
        }
    }
}
