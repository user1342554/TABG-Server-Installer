using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    /// <summary>
    /// Reads/writes FreddoTABGCommission.cfg, FreddoFixStarterPack.cfg,
    /// and FreddoCustomSpawnpoints.cfg (BepInEx config format).
    /// </summary>
    public static class ModConfigService
    {
        // ── FreddoTABGCommission.cfg ──

        private static string CommissionPath(string serverDir) =>
            Path.Combine(serverDir, "BepInEx", "config", "FreddoTABGCommission.cfg");

        public static FreddoCommissionSettings ReadCommission(string serverDir)
        {
            var s = new FreddoCommissionSettings();
            var path = CommissionPath(serverDir);
            if (!File.Exists(path)) return s;

            var dict = ParseCfg(path);
            if (dict.TryGetValue("BanList", out var bl)) s.BanList = bl;
            if (dict.TryGetValue("LoadoutCurses", out var lc)) s.LoadoutCurses = lc;
            if (dict.TryGetValue("GrenadesOnDeath.Attacker.Enabled", out var gae)) s.GrenadeAttackerEnabled = ParseBool(gae);
            if (dict.TryGetValue("GrenadesOnDeath.Attacker.Chance", out var gac) && float.TryParse(gac, NumberStyles.Float, CultureInfo.InvariantCulture, out var gacV)) s.GrenadeAttackerChance = gacV;
            if (dict.TryGetValue("GrenadesOnDeath.Attacker.ID", out var gai) && int.TryParse(gai, out var gaiV)) s.GrenadeAttackerId = gaiV;
            if (dict.TryGetValue("GrenadesOnDeath.Corpse.Enabled", out var gce)) s.GrenadeCorpseEnabled = ParseBool(gce);
            if (dict.TryGetValue("GrenadesOnDeath.Corpse.Chance", out var gcc) && float.TryParse(gcc, NumberStyles.Float, CultureInfo.InvariantCulture, out var gccV)) s.GrenadeCorpseChance = gccV;
            if (dict.TryGetValue("GrenadesOnDeath.Corpse.ID", out var gci) && int.TryParse(gci, out var gciV)) s.GrenadeCorpseId = gciV;
            if (dict.TryGetValue("StreamingDistance", out var sd) && float.TryParse(sd, NumberStyles.Float, CultureInfo.InvariantCulture, out var sdV)) s.StreamingDistance = sdV;
            if (dict.TryGetValue("Lives", out var li) && int.TryParse(li, out var liV)) s.Lives = liV;

            return s;
        }

        public static void WriteCommission(string serverDir, FreddoCommissionSettings s)
        {
            var path = CommissionPath(serverDir);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var lines = new List<string>
            {
                "[General]",
                "",
                $"BanList = {s.BanList}",
                "",
                $"LoadoutCurses = {s.LoadoutCurses}",
                "",
                "[GrenadesOnDeath.Attacker]",
                "",
                $"Enabled = {s.GrenadeAttackerEnabled.ToString().ToLower()}",
                "",
                $"Chance = {s.GrenadeAttackerChance.ToString(CultureInfo.InvariantCulture)}",
                "",
                $"ID = {s.GrenadeAttackerId}",
                "",
                "[GrenadesOnDeath.Corpse]",
                "",
                $"Enabled = {s.GrenadeCorpseEnabled.ToString().ToLower()}",
                "",
                $"Chance = {s.GrenadeCorpseChance.ToString(CultureInfo.InvariantCulture)}",
                "",
                $"ID = {s.GrenadeCorpseId}",
                "",
                "[Advanced]",
                "",
                $"StreamingDistance = {s.StreamingDistance.ToString(CultureInfo.InvariantCulture)}",
                "",
                $"Lives = {s.Lives}"
            };

            File.WriteAllLines(path, lines);
        }

        // ── FreddoFixStarterPack.cfg ──

        private static string FixesPath(string serverDir) =>
            Path.Combine(serverDir, "BepInEx", "config", "FreddoFixStarterPack.cfg");

        public static StarterPackFixesSettings ReadFixes(string serverDir)
        {
            var s = new StarterPackFixesSettings();
            var path = FixesPath(serverDir);
            if (!File.Exists(path)) return s;

            var dict = ParseCfg(path);
            if (dict.TryGetValue("EnableLootDrops", out var eld)) s.EnableLootDrops = ParseBool(eld);
            return s;
        }

        public static void WriteFixes(string serverDir, StarterPackFixesSettings s)
        {
            var path = FixesPath(serverDir);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var lines = new List<string>
            {
                "[General]",
                "",
                $"EnableLootDrops = {s.EnableLootDrops.ToString().ToLower()}"
            };

            File.WriteAllLines(path, lines);
        }

        // ── FreddoCustomSpawnpoints.cfg ──

        private static string SpawnPath(string serverDir) =>
            Path.Combine(serverDir, "BepInEx", "config", "FreddoCustomSpawnpoints.cfg");

        /// <summary>Reads match spawn points as "x,z;x,z;..." string.</summary>
        public static string ReadSpawnPoints(string serverDir)
        {
            var path = SpawnPath(serverDir);
            if (!File.Exists(path)) return "";

            var dict = ParseCfg(path);
            if (dict.TryGetValue("Spawnpoints", out var sp)) return sp;
            return "";
        }

        public static void WriteSpawnPoints(string serverDir, string spawnPoints)
        {
            var path = SpawnPath(serverDir);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var lines = new List<string>
            {
                "[General]",
                "",
                "## Match spawn points as 2D coordinates (x,z) separated by semicolons",
                $"Spawnpoints = {spawnPoints}"
            };

            File.WriteAllLines(path, lines);
        }

        // ── BepInEx .cfg parser ──

        private static Dictionary<string, string> ParseCfg(string path)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var section = "";

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2);
                    continue;
                }

                var idx = line.IndexOf('=');
                if (idx < 1) continue;

                var key = line.Substring(0, idx).Trim();
                var value = line.Substring(idx + 1).Trim();

                // Store with section prefix for dotted keys
                var fullKey = string.IsNullOrEmpty(section) ? key : $"{section}.{key}";
                dict[fullKey] = value;

                // Also store without section for simple lookups
                dict[key] = value;
            }

            return dict;
        }

        private static bool ParseBool(string value) =>
            value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
