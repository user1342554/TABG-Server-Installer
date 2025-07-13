using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TabgInstaller.Core.Model; // Adjusted from .Models to .Model
// using TabgInstaller.Core.Services; // This namespace is for GitHubService, NewServerConfigManager is in TabgInstaller.Core

namespace TabgInstaller.Core
{
    public class NewServerConfigManager
    { 
        private readonly IProgress<string> _log;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull // Optional: omit null values
        };

        public NewServerConfigManager(IProgress<string> log)
        {
            _log = log;
        }

        // --- Server Name Sanitization ---
        public string SanitizeName(string name, HashSet<string> allowedWords, string fallbackName)
        {
            if (string.IsNullOrWhiteSpace(name)) return fallbackName;

            var words = name.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(word => Regex.Replace(word, @"[^a-zA-Z0-9]", "")) // Remove special chars from each word
                            .Where(word => !string.IsNullOrWhiteSpace(word))
                            .ToList();

            var sanitizedWords = new List<string>();
            bool usedFallbackWord = false;
            foreach (var word in words)
            {
                if (allowedWords.Contains(word))
                {
                    sanitizedWords.Add(word);
                }
                else
                {
                    _log.Report($"[INFO] Server name part '{word}' not in allowed list. Replacing or omitting.");
                    // Simple strategy: try to pick a random allowed word if the original is invalid and list is not empty
                    if (allowedWords.Count > 0 && !usedFallbackWord) // Only one random fallback to avoid long random names
                    {
                        // Attempt to get a cryptographically secure random word
                        try 
                        { 
                            sanitizedWords.Add(allowedWords.ElementAt(RandomNumberGenerator.GetInt32(allowedWords.Count)));
                            usedFallbackWord = true;
                        }
                        catch (Exception ex)
                        {
                            _log.Report($"[DEBUG] RandomNumberGenerator failed: {ex.Message}. Falling back to simpler random word selection for server name.");
                            if(allowedWords.Any()) sanitizedWords.Add(allowedWords.First()); // Basic fallback if crypto random fails
                            usedFallbackWord = true;
                        }
                    }
                }
            }

            if (!sanitizedWords.Any())
            {
                _log.Report($"[WARN] Server name '{name}' resulted in no valid words. Using fallback '{fallbackName}'.");
                return fallbackName;
            }

            var finalName = string.Join(" ", sanitizedWords).Trim();
            return string.IsNullOrWhiteSpace(finalName) ? fallbackName : finalName;
        }

        public string GeneratePassword(HashSet<string> allowedWords, int numWords = 2)
        {
            if (allowedWords == null || !allowedWords.Any())
            {
                _log.Report("[WARN] Allowed words list is empty. Generating a generic password.");
                return $"Password{RandomNumberGenerator.GetInt32(1000, 9999)}";
            }

            var passwordWords = new List<string>();
            for (int i = 0; i < numWords; i++)
            {
                try
                {
                    passwordWords.Add(allowedWords.ElementAt(RandomNumberGenerator.GetInt32(allowedWords.Count)));
                }
                catch (Exception ex)
                {
                     _log.Report($"[DEBUG] RandomNumberGenerator failed during password gen: {ex.Message}. Falling back to simpler random word selection.");
                     passwordWords.Add(allowedWords.First()); // Basic fallback
                }
            }
            return string.Join("", passwordWords.Select(w => w.Substring(0, Math.Min(w.Length, 5)) ) ); // Shorten words for password
        }

        // --- Config File Management ---

        public string SanitizeServerNameForGameSettings(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                _log.Report("  [INFO] Server name was empty, using default 'DefaultServer'.");
                return "DefaultServer";
            }
            // Erlaubte Zeichen laut TABG: alphanumerisch, Leerzeichen, Bindestrich, Unterstrich
            var allowed = raw
                .Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_')
                .ToArray();

            var sanitized = new string(allowed).Trim();
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                _log.Report($"  [INFO] Sanitized server name for '{raw}' resulted in empty string, using default 'DefaultServer'.");
                return "DefaultServer";
            }
            _log.Report($"  → Sanitized server name (for game_settings.txt): \"{raw}\" → \"{sanitized}\"");
            return sanitized;
        }

        public void WriteGameSettingsTxt(string serverDir, string sanitizedServerName)
        {
            var settingsPath = Path.Combine(serverDir, "game_settings.txt");
            try
            {
                var lines = new List<string>
                {
                    $"server_name={sanitizedServerName}",
                    "port=7777",
                    "max_players=20",
                    "map_rotation=DefaultMap"
                };
                File.WriteAllLines(settingsPath, lines);
                _log.Report($"  → Wrote game_settings.txt with sanitized server name: {sanitizedServerName}");
            }
            catch (Exception ex)
            {
                _log.Report($"[ERROR] Fehler beim Schreiben von game_settings.txt: {ex.Message}");
            }
        }
        
        // Placeholder for UpdateTheStarterPackJsonAsync if it's called from new Installer.cs, 
        // though the new Installer.cs diff seems to serialize TheStarterPackConfig directly.
        // If direct serialization is used, this method might not be needed from NewServerConfigManager.
        public async Task UpdateTheStarterPackJsonAsync(string serverInstallPath, TheStarterPackConfig configData)
        {
            var filePath = Path.Combine(serverInstallPath, "TheStarterPack.json");
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true }; 
                var jsonString = System.Text.Json.JsonSerializer.Serialize(configData, options);
                await File.WriteAllTextAsync(filePath, jsonString, System.Text.Encoding.UTF8);
                _log.Report($"  → Updated TheStarterPack.json (via NewServerConfigManager) with ServerName: {configData.ServerName}");
            }
            catch (Exception ex)
            {
                _log.Report($"[ERROR] Failed to write TheStarterPack.json (via NewServerConfigManager): {ex.Message}");
            }
        }

        // EnsureCitrusLibConfigAsync from previous version, might be useful or replaced by direct calls in Installer
        public async Task EnsureCitrusLibConfigAsync<T>(string serverInstallPath, string fileName, T defaultConfig, IProgress<string> log) where T : new()
        {
            var citrusConfigDir = Path.Combine(serverInstallPath, "BepInEx", "config", "CitrusLib");
            Directory.CreateDirectory(citrusConfigDir);
            var filePath = Path.Combine(citrusConfigDir, fileName);

            try
            {
                if (!File.Exists(filePath))
                {
                    var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                    var jsonString = System.Text.Json.JsonSerializer.Serialize(defaultConfig ?? new T(), options);
                    await File.WriteAllTextAsync(filePath, jsonString, System.Text.Encoding.UTF8);
                    log.Report($"  → Created default CitrusLib config: {fileName}");
                }
                else
                {
                    log.Report($"  → CitrusLib config {fileName} already exists. No changes made by installer.");
                }
            }
            catch (Exception ex)
            {
                log.Report($"[ERROR] Failed to ensure CitrusLib config {fileName}: {ex.Message}");
            }
        }

        // Example of how ItemsGiven and Loadouts could be structured from your previous logic if needed:
        // This is conceptual and would need integration if you're parsing old string formats.
        // Otherwise, you'd populate TheStarterPackConfig.ItemsGiven and .Loadouts directly with lists of records.

        // private List<StarterPackItemEntry> ParseItemsGivenString(string itemsGivenStr)
        // {
        //    // ... logic adapted from your original JObject parser ...
        //    // e.g., split by ',', then by ':', parse ints, create StarterPackItemEntry objects
        //    return new List<StarterPackItemEntry>(); 
        // }

        // private List<StarterPackLoadout> ParseLoadoutsString(string loadoutsStr)
        // {
        //    // ... logic adapted from your original JObject parser ...
        //    // e.g., split by '/', then by ',', then by ':', parse, create StarterPackLoadout and StarterPackItemEntry objects
        //    return new List<StarterPackLoadout>();
        // }
    }
} 