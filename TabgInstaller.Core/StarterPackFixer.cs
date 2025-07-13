using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TabgInstaller.Core
{
    internal static class StarterPackFixer
    {
        private const string FileName      = "TheStarterPack.json";
        private const string SafeItems     = "152:1";          // bandage
        private const string SafeLoadout   = "152:1/152:1";    // single-slot

        private static readonly Regex ItemsRegex =
            new(@"^\d+:\d+(,\d+:\d+)*$",                         RegexOptions.Compiled);

        private static readonly Regex LoadoutRegex =
            new(@"^\d+:\d+(,\d+:\d+)*(\/\d+:\d+(,\d+:\d+)*)*$",  RegexOptions.Compiled);

        /// <summary>
        /// Guarantee that <c>TheStarterPack.json</c> exists **and** cannot make the
        /// StarterPack plugin throw.
        /// </summary>
        internal static void EnsureValid(string serverDir, string fallbackName = "MyTABGServer")
        {
            string path = Path.Combine(serverDir, FileName);
            JObject cfg = TryLoad(path) ?? CreateDefault(fallbackName);

            // ---------- sanitise ------------------------------------------------
            cfg["ServerName"] = SanitiseName(cfg["ServerName"]?.ToString(), fallbackName);
            cfg["ItemsGiven"] = SanitiseField(cfg["ItemsGiven"]?.ToString(), ItemsRegex,   SafeItems);
            cfg["Loadouts"]   = SanitiseField(cfg["Loadouts"]?.ToString(),   LoadoutRegex, SafeLoadout);

            File.WriteAllText(path, cfg.ToString(Formatting.Indented));
        }

        // ----------------------------------------------------------------------

        private static JObject? TryLoad(string path)
        {
            try { return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : null; }
            catch { return null; }
        }

        private static JObject CreateDefault(string name) => new()
        {
            ["ServerName"] = name,
            ["ItemsGiven"] = SafeItems,
            ["Loadouts"]   = SafeLoadout
        };

        private static string SanitiseName(string? input, string fallback)
        {
            if (string.IsNullOrWhiteSpace(input)) return fallback;
            return Regex.Replace(input, @"unnamed", "TABG", RegexOptions.IgnoreCase);
        }

        private static string SanitiseField(string? input, Regex rule, string fallback)
        {
            return !string.IsNullOrEmpty(input) && rule.IsMatch(input) ? input : fallback;
        }
    }
} 