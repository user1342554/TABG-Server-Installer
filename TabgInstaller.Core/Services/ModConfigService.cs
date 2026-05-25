using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    /// <summary>
    /// Reads/writes MatchCore's BepInEx config. Legacy Freddo config files
    /// are still read as a migration fallback, but new writes use owned names.
    /// </summary>
    public static class ModConfigService
    {
        // ── TabgInstaller.MatchCore.cfg ──

        private static string MatchCoreConfigPath(string serverDir) =>
            Path.Combine(serverDir, "BepInEx", "config", "TabgInstaller.MatchCore.cfg");

        private static string LegacyCommissionPath(string serverDir) =>
            Path.Combine(serverDir, "BepInEx", "config", "FreddoTABGCommission.cfg");

        private static string LegacyFixesPath(string serverDir) =>
            Path.Combine(serverDir, "BepInEx", "config", "FreddoFixStarterPack.cfg");

        private static string LegacySpawnPath(string serverDir) =>
            Path.Combine(serverDir, "BepInEx", "config", "FreddoCustomSpawnpoints.cfg");

        public static string ServerLoggerConfigPath(string serverDir) =>
            Path.Combine(serverDir, "BepInEx", "config", "tabginstaller.serverlogger.cfg");

        public static string PluginConfigPath(string gameDir, PluginConfigDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.ConfigFileName))
                return "";

            return Path.Combine(gameDir, "BepInEx", "config", definition.ConfigFileName);
        }

        public static Dictionary<string, string> ReadPluginConfigValues(string gameDir, PluginConfigDefinition definition)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var setting in definition.Settings)
                values[setting.FullKey] = setting.DefaultValue;

            var path = PluginConfigPath(gameDir, definition);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return values;

            var cfg = ParseCfg(path);
            foreach (var setting in definition.Settings)
            {
                if (cfg.TryGetValue(setting.FullKey, out var value))
                    values[setting.FullKey] = value;
                else if (cfg.TryGetValue(setting.Key, out value))
                    values[setting.FullKey] = value;
            }

            return values;
        }

        public static void WritePluginConfigValues(
            string gameDir,
            PluginConfigDefinition definition,
            IReadOnlyDictionary<string, string> values)
        {
            if (definition.Settings.Length == 0 || string.IsNullOrWhiteSpace(definition.ConfigFileName))
                return;

            var path = PluginConfigPath(gameDir, definition);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var lines = new List<string>
            {
                $"## Settings file for {definition.DisplayName}",
                "## Managed by TABG Server Installer",
                ""
            };

            foreach (var section in definition.Settings.Select(s => s.Section).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(section))
                {
                    lines.Add($"[{section}]");
                    lines.Add("");
                }

                foreach (var setting in definition.Settings.Where(s => s.Section.Equals(section, StringComparison.OrdinalIgnoreCase)))
                {
                    var value = values.TryGetValue(setting.FullKey, out var configured)
                        ? configured
                        : setting.DefaultValue;

                    if (!string.IsNullOrWhiteSpace(setting.Description))
                        lines.Add($"## {setting.Description}");

                    lines.Add($"# Setting type: {ToBepInExSettingType(setting.ValueType)}");
                    lines.Add($"# Default value: {setting.DefaultValue}");
                    lines.Add($"{setting.Key} = {NormalizePluginValue(setting.ValueType, value)}");
                    lines.Add("");
                }
            }

            File.WriteAllLines(path, lines);
        }

        public static string GetServerLoggerCsvPath(string serverDir, ServerLoggerSettings settings)
        {
            var directory = string.IsNullOrWhiteSpace(settings.LogDirectory)
                ? "server-logs"
                : settings.LogDirectory;

            if (!Path.IsPathRooted(directory))
                directory = Path.Combine(serverDir, "BepInEx", directory);

            var fileName = string.IsNullOrWhiteSpace(settings.CsvFileName)
                ? "players.csv"
                : settings.CsvFileName;

            return Path.IsPathRooted(fileName) ? fileName : Path.Combine(directory, fileName);
        }

        public static string GetServerLoggerLegacyPath(string serverDir, ServerLoggerSettings settings)
        {
            var fileName = string.IsNullOrWhiteSpace(settings.LegacyFileName)
                ? "ServerLogger.txt"
                : settings.LegacyFileName;

            return Path.IsPathRooted(fileName) ? fileName : Path.Combine(serverDir, fileName);
        }

        private static string ResolveReadPath(string primaryPath, string legacyPath)
        {
            if (File.Exists(primaryPath)) return primaryPath;
            return legacyPath;
        }

        public static FreddoCommissionSettings ReadCommission(string serverDir)
        {
            var s = new FreddoCommissionSettings();
            var path = ResolveReadPath(MatchCoreConfigPath(serverDir), LegacyCommissionPath(serverDir));
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
            WriteMatchCoreConfig(serverDir, s, ReadFixes(serverDir), ReadSpawnPoints(serverDir));
        }

        public static StarterPackFixesSettings ReadFixes(string serverDir)
        {
            var s = new StarterPackFixesSettings();
            var path = ResolveReadPath(MatchCoreConfigPath(serverDir), LegacyFixesPath(serverDir));
            if (!File.Exists(path)) return s;

            var dict = ParseCfg(path);
            // Key stored as bare "EnableLootDrops" or "Fixes.EnableLootDrops"
            if (dict.TryGetValue("EnableLootDrops", out var eld)) s.EnableLootDrops = ParseBool(eld);
            else if (dict.TryGetValue("Fixes.EnableLootDrops", out eld)) s.EnableLootDrops = ParseBool(eld);
            return s;
        }

        public static void WriteFixes(string serverDir, StarterPackFixesSettings s)
        {
            WriteMatchCoreConfig(serverDir, ReadCommission(serverDir), s, ReadSpawnPoints(serverDir));
        }

        /// <summary>Reads match spawn points as "x,z;x,z;..." string.</summary>
        public static string ReadSpawnPoints(string serverDir)
        {
            var path = ResolveReadPath(MatchCoreConfigPath(serverDir), LegacySpawnPath(serverDir));
            if (!File.Exists(path)) return "";

            var dict = ParseCfg(path);
            if (dict.TryGetValue("Spawnpoints", out var sp)) return sp;
            return "";
        }

        public static void WriteSpawnPoints(string serverDir, string spawnPoints)
        {
            WriteMatchCoreConfig(serverDir, ReadCommission(serverDir), ReadFixes(serverDir), spawnPoints);
        }

        public static ServerLoggerSettings ReadServerLogger(string serverDir)
        {
            var settings = new ServerLoggerSettings();
            var path = ServerLoggerConfigPath(serverDir);
            if (!File.Exists(path)) return settings;

            var dict = ParseCfg(path);
            if (dict.TryGetValue("LogToBepInExConsole", out var logConsole)) settings.LogToBepInExConsole = ParseBool(logConsole);
            if (dict.TryGetValue("WriteCsv", out var writeCsv)) settings.WriteCsv = ParseBool(writeCsv);
            if (dict.TryGetValue("WriteLegacyServerLoggerTxt", out var writeLegacy)) settings.WriteLegacyServerLoggerTxt = ParseBool(writeLegacy);
            if (dict.TryGetValue("FallbackPlayerScan", out var fallbackScan)) settings.FallbackPlayerScan = ParseBool(fallbackScan);
            if (dict.TryGetValue("FallbackScanIntervalSeconds", out var interval) && float.TryParse(interval, NumberStyles.Float, CultureInfo.InvariantCulture, out var intervalValue))
                settings.FallbackScanIntervalSeconds = intervalValue;
            if (dict.TryGetValue("LogDirectory", out var logDirectory)) settings.LogDirectory = logDirectory;
            if (dict.TryGetValue("CsvFileName", out var csvFileName)) settings.CsvFileName = csvFileName;
            if (dict.TryGetValue("LegacyFileName", out var legacyFileName)) settings.LegacyFileName = legacyFileName;

            return settings;
        }

        public static void WriteServerLogger(string serverDir, ServerLoggerSettings settings)
        {
            var path = ServerLoggerConfigPath(serverDir);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var lines = new List<string>
            {
                "## Settings file for TABG Server Logger v1.0.0",
                "## Plugin GUID: tabginstaller.serverlogger",
                "",
                "[Logging]",
                "",
                "## Log player identities to the BepInEx console/log.",
                "# Setting type: Boolean",
                "# Default value: true",
                $"LogToBepInExConsole = {settings.LogToBepInExConsole.ToString().ToLowerInvariant()}",
                "",
                "## Append player identities to the CSV log.",
                "# Setting type: Boolean",
                "# Default value: true",
                $"WriteCsv = {settings.WriteCsv.ToString().ToLowerInvariant()}",
                "",
                "## Keep writing the old ServerLogger.txt format for existing tools.",
                "# Setting type: Boolean",
                "# Default value: true",
                $"WriteLegacyServerLoggerTxt = {settings.WriteLegacyServerLoggerTxt.ToString().ToLowerInvariant()}",
                "",
                "## Also scan connected players in case another mod changes the Epic token callback.",
                "# Setting type: Boolean",
                "# Default value: true",
                $"FallbackPlayerScan = {settings.FallbackPlayerScan.ToString().ToLowerInvariant()}",
                "",
                "## Seconds between fallback connected-player scans.",
                "# Setting type: Single",
                "# Default value: 2",
                $"FallbackScanIntervalSeconds = {settings.FallbackScanIntervalSeconds.ToString(CultureInfo.InvariantCulture)}",
                "",
                "[Paths]",
                "",
                "## Relative to BepInEx, or an absolute path.",
                "# Setting type: String",
                "# Default value: server-logs",
                $"LogDirectory = {settings.LogDirectory ?? ""}",
                "",
                "## CSV file name inside LogDirectory, or an absolute path.",
                "# Setting type: String",
                "# Default value: players.csv",
                $"CsvFileName = {settings.CsvFileName ?? ""}",
                "",
                "## Legacy text file name, stored in the server root unless absolute.",
                "# Setting type: String",
                "# Default value: ServerLogger.txt",
                $"LegacyFileName = {settings.LegacyFileName ?? ""}",
                ""
            };

            File.WriteAllLines(path, lines);
        }

        private static void WriteMatchCoreConfig(
            string serverDir,
            FreddoCommissionSettings commission,
            StarterPackFixesSettings fixes,
            string spawnPoints)
        {
            var path = MatchCoreConfigPath(serverDir);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var lines = new List<string>
            {
                "## Settings file for TABG MatchCore v1.0.0",
                "## Plugin GUID: tabginstaller.matchcore",
                "",
                "[Bans]",
                "",
                "## A list of Epic IDs to ban, separated with semicolons.",
                "# Setting type: String",
                "# Default value: ",
                $"BanList = {commission.BanList}",
                "",
                "[Curses]",
                "",
                "## A list of curse IDs to apply by loadout index.",
                "# Setting type: String",
                "# Default value: ",
                $"LoadoutCurses = {commission.LoadoutCurses}",
                "",
                "[Blessings]",
                "",
                "## A list of blessing item IDs per loadout.",
                "# Setting type: String",
                "# Default value: ",
                $"LoadoutBlessings = {commission.LoadoutBlessings}",
                "",
                "[GrenadesOnDeath.Attacker]",
                "",
                "## Drops a grenade when a player kills another player.",
                "# Setting type: Boolean",
                "# Default value: false",
                $"Enabled = {commission.GrenadeAttackerEnabled.ToString().ToLower()}",
                "",
                "# Setting type: Single",
                "# Default value: 0.2",
                $"Chance = {commission.GrenadeAttackerChance.ToString(CultureInfo.InvariantCulture)}",
                "",
                "# Setting type: Int32",
                "# Default value: 198",
                $"ID = {commission.GrenadeAttackerId}",
                "",
                "[GrenadesOnDeath.Corpse]",
                "",
                "## Drops a grenade on the defeated player's body.",
                "# Setting type: Boolean",
                "# Default value: false",
                $"Enabled = {commission.GrenadeCorpseEnabled.ToString().ToLower()}",
                "",
                "# Setting type: Single",
                "# Default value: 0.2",
                $"Chance = {commission.GrenadeCorpseChance.ToString(CultureInfo.InvariantCulture)}",
                "",
                "# Setting type: Int32",
                "# Default value: 198",
                $"ID = {commission.GrenadeCorpseId}",
                "",
                "[Networking]",
                "",
                "## Nearby packet streaming distance. -1 uses TABG defaults, -2 sends to everyone.",
                "# Setting type: Single",
                "# Default value: -1",
                $"StreamingDistance = {commission.StreamingDistance.ToString(CultureInfo.InvariantCulture)}",
                "",
                "[Player]",
                "",
                "## Number of lives before lockout. 256 means effectively infinite.",
                "# Setting type: Int32",
                "# Default value: 256",
                $"Lives = {commission.Lives}",
                "",
                "[Fixes]",
                "",
                "## Enables world loot drops for MatchCore game modes.",
                "# Setting type: Boolean",
                "# Default value: true",
                $"EnableLootDrops = {fixes.EnableLootDrops.ToString().ToLower()}",
                "",
                "[Spawn]",
                "",
                "## Match spawn points in x,z or x,y,z form, separated by semicolons.",
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

        private static string ToBepInExSettingType(PluginSettingValueType valueType)
        {
            return valueType switch
            {
                PluginSettingValueType.Boolean => "Boolean",
                PluginSettingValueType.Int32 => "Int32",
                PluginSettingValueType.Single => "Single",
                PluginSettingValueType.KeyCode => "KeyCode",
                _ => "String"
            };
        }

        private static string NormalizePluginValue(PluginSettingValueType valueType, string value)
        {
            value ??= "";
            if (valueType == PluginSettingValueType.Boolean)
                return ParseBool(value).ToString().ToLowerInvariant();

            return value.Trim();
        }
    }
}
