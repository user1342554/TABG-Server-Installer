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
            // Keys stored both as "section.key" and bare "key" by ParseCfg
            if (dict.TryGetValue("BanList", out var bl)) s.BanList = bl;
            if (dict.TryGetValue("LoadoutCurses", out var lc)) s.LoadoutCurses = lc;
            if (dict.TryGetValue("LoadoutBlessings", out var lb)) s.LoadoutBlessings = lb;

            // GrenadesOnDeath sections use dotted section names in the cfg file itself
            if (dict.TryGetValue("GrenadesOnDeath.Attacker.Enabled", out var gae)) s.GrenadeAttackerEnabled = ParseBool(gae);
            else if (dict.TryGetValue("Enabled", out gae)) s.GrenadeAttackerEnabled = ParseBool(gae); // fallback
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
                "## Settings file was created by plugin Freddo TABG Commission v1.0.0",
                "## Plugin GUID: FreddoTABGCommission",
                "",
                "[Bans]",
                "",
                "## A list of Epic IDs to ban, separated with semicolons.",
                "# Setting type: String",
                "# Default value: ",
                $"BanList = {s.BanList}",
                "",
                "[Curses]",
                "",
                "## A list of curse IDs to inflict the player using a loadout at that index (e.g. loadout 1 = group 1), Ex: 0,1/1,2/2,3",
                "# Setting type: String",
                "# Default value: ",
                $"LoadoutCurses = {s.LoadoutCurses}",
                "",
                "[Blessings]",
                "",
                "## A list of blessing item IDs per loadout (e.g. loadout 1 = group 1). Blessings are items added to the loadout. Ex: 53,42/45,47/50",
                "# Setting type: String",
                "# Default value: ",
                $"LoadoutBlessings = {s.LoadoutBlessings}",
                "",
                "[GrenadesOnDeath.Attacker]",
                "",
                "## Drops a grenade if a player kills another player, chance can be configured.",
                "# Setting type: Boolean",
                "# Default value: false",
                $"Enabled = {s.GrenadeAttackerEnabled.ToString().ToLower()}",
                "",
                "## The chance a grenade drops on kill.",
                "# Setting type: Single",
                "# Default value: 0.2",
                $"Chance = {s.GrenadeAttackerChance.ToString(CultureInfo.InvariantCulture)}",
                "",
                "## The ID of the grenade to throw (can be any throwable).",
                "# Setting type: Int32",
                "# Default value: 198",
                $"ID = {s.GrenadeAttackerId}",
                "",
                "[GrenadesOnDeath.Corpse]",
                "",
                "## Drops a grenade on a corpse if a player kills another player, chance can be configured.",
                "# Setting type: Boolean",
                "# Default value: false",
                $"Enabled = {s.GrenadeCorpseEnabled.ToString().ToLower()}",
                "",
                "## The chance a grenade drops on kill.",
                "# Setting type: Single",
                "# Default value: 0.2",
                $"Chance = {s.GrenadeCorpseChance.ToString(CultureInfo.InvariantCulture)}",
                "",
                "## The ID of the grenade to throw (can be any throwable).",
                "# Setting type: Int32",
                "# Default value: 198",
                $"ID = {s.GrenadeCorpseId}",
                "",
                "[Networking]",
                "",
                "## The distance (in metres) that packets can be sent to nearby players without being cut off. (-1 means normal TABG, -2 means all players)",
                "# Setting type: Int32",
                "# Default value: -1",
                $"StreamingDistance = {s.StreamingDistance.ToString(CultureInfo.InvariantCulture)}",
                "",
                "[Player]",
                "",
                "## The number of lives that the player has before being kicked from the game (256 means infinite).",
                "# Setting type: Int32",
                "# Default value: 256",
                $"Lives = {s.Lives}",
                ""
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
            // Key stored as bare "EnableLootDrops" or "Fixes.EnableLootDrops"
            if (dict.TryGetValue("EnableLootDrops", out var eld)) s.EnableLootDrops = ParseBool(eld);
            else if (dict.TryGetValue("Fixes.EnableLootDrops", out eld)) s.EnableLootDrops = ParseBool(eld);
            return s;
        }

        public static void WriteFixes(string serverDir, StarterPackFixesSettings s)
        {
            var path = FixesPath(serverDir);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var lines = new List<string>
            {
                "## Settings file was created by plugin FreddoFixStarterPack v1.0.0",
                "## Plugin GUID: FreddoFixStarterPack",
                "",
                "[Fixes]",
                "",
                "## Enable loot drops for items since StarterPack broke it",
                "# Setting type: Boolean",
                "# Default value: true",
                $"EnableLootDrops = {s.EnableLootDrops.ToString().ToLower()}",
                ""
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
                "## Settings file was created by plugin Freddo Custom Spawnpoints v1.0.0",
                "## Plugin GUID: FreddoCustomSpawnpoints",
                "",
                "[Spawn]",
                "",
                "## The spawnpoints you want to spawn. Leave empty to use the default system. Spawns are in this format: x,y;x,y;x,y...",
                "# Setting type: String",
                "# Default value: 0,0;100,100",
                $"Spawnpoints = {spawnPoints}",
                ""
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
