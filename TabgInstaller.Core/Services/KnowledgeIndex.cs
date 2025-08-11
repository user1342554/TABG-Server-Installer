using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace TabgInstaller.Core.Services
{
    public class KnowledgeIndex
    {
        private static readonly object Sync = new();
        private FileSystemWatcher? _watcher;
        private Timer? _debounceTimer;
        private readonly string _knowledgeDir;

        public static KnowledgeIndex Current { get; } = new KnowledgeIndex(ResolveKnowledgeDirectory());

        public event EventHandler? KnowledgeReloaded;

        public IReadOnlyDictionary<string, GameSettingInfo> GameSettings { get; private set; } =
            new Dictionary<string, GameSettingInfo>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, StarterPackSettingInfo> StarterPackSettings { get; private set; } =
            new Dictionary<string, StarterPackSettingInfo>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, WeaponInfo> WeaponsById { get; private set; } =
            new Dictionary<string, WeaponInfo>(StringComparer.OrdinalIgnoreCase);

        private KnowledgeIndex(string knowledgeDir)
        {
            _knowledgeDir = knowledgeDir;
            Reload();
            StartWatcher();
        }

        public void Reload()
        {
            var gameSettingsPath = Path.Combine(_knowledgeDir, "Game settings explanation.json");
            var starterPackPath = Path.Combine(_knowledgeDir, "The starter pack explained.json");
            var weaponsPath = Path.Combine(_knowledgeDir, "Weaponlist.json");

            try
            {
                if (File.Exists(gameSettingsPath))
                {
                    var text = File.ReadAllText(gameSettingsPath);
                    GameSettings = ParseGameSettings(text);
                }
            }
            catch { }

            try
            {
                if (File.Exists(starterPackPath))
                {
                    var text = File.ReadAllText(starterPackPath);
                    StarterPackSettings = ParseStarterPack(text);
                }
            }
            catch { }

            try
            {
                if (File.Exists(weaponsPath))
                {
                    var text = File.ReadAllText(weaponsPath);
                    WeaponsById = ParseWeapons(text);
                }
            }
            catch { }

            try { KnowledgeReloaded?.Invoke(this, EventArgs.Empty); } catch { }
        }

        public IEnumerable<SearchResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) yield break;
            query = query.Trim();

            foreach (var kv in GameSettings)
            {
                if (kv.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (kv.Value.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    yield return new SearchResult("GameSetting", kv.Key, kv.Value.Description ?? kv.Key);
                }
            }
            foreach (var kv in StarterPackSettings)
            {
                if (kv.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (kv.Value.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    yield return new SearchResult("StarterPack", kv.Key, kv.Value.Description ?? kv.Key);
                }
            }
            foreach (var kv in WeaponsById)
            {
                if (kv.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    kv.Value.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    yield return new SearchResult("Weapon", kv.Key, kv.Value.Name);
                }
            }
        }

        private void StartWatcher()
        {
            _watcher?.Dispose();
            if (!Directory.Exists(_knowledgeDir)) return;
            var watcher = new FileSystemWatcher(_knowledgeDir)
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
                Filter = "*.json",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };
            _watcher = watcher;
            watcher.Changed += (_, __) => ScheduleReload();
            watcher.Created += (_, __) => ScheduleReload();
            watcher.Deleted += (_, __) => ScheduleReload();
            watcher.Renamed += (_, __) => ScheduleReload();
        }

        private void ScheduleReload()
        {
            lock (Sync)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(_ => Reload(), null, 400, Timeout.Infinite);
            }
        }

        private static IReadOnlyDictionary<string, GameSettingInfo> ParseGameSettings(string json)
        {
            var doc = JsonDocument.Parse(json);
            var dict = new Dictionary<string, GameSettingInfo>(StringComparer.OrdinalIgnoreCase);
            if (!doc.RootElement.TryGetProperty("settings", out var settings)) return dict;
            foreach (var prop in settings.EnumerateObject())
            {
                var el = prop.Value;
                var info = new GameSettingInfo
                {
                    Name = prop.Name,
                    Type = el.TryGetProperty("type", out var t) ? t.GetString() ?? "string" : "string",
                    Default = el.TryGetProperty("default", out var d) ? d.ToString() : null,
                    Description = el.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    LineFormat = el.TryGetProperty("line_format", out var lf) ? lf.GetString() : null,
                    Allowed = el.TryGetProperty("allowed", out var al) ? al.EnumerateArray().Select(x => x.ToString()).ToArray() : Array.Empty<string>(),
                    Range = el.TryGetProperty("range", out var rng) && rng.ValueKind == JsonValueKind.Array && rng.GetArrayLength() == 2
                        ? new RangeConstraint(rng[0].ToString(), rng[1].ToString()) : null
                };
                dict[prop.Name] = info;
            }
            return dict;
        }

        private static IReadOnlyDictionary<string, StarterPackSettingInfo> ParseStarterPack(string json)
        {
            var doc = JsonDocument.Parse(json);
            var dict = new Dictionary<string, StarterPackSettingInfo>(StringComparer.OrdinalIgnoreCase);
            if (!doc.RootElement.TryGetProperty("settings", out var settings)) return dict;
            foreach (var prop in settings.EnumerateObject())
            {
                var el = prop.Value;
                var info = new StarterPackSettingInfo
                {
                    Name = prop.Name,
                    Type = el.TryGetProperty("type", out var t) ? t.GetString() ?? "string" : "string",
                    Description = el.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    Syntax = el.TryGetProperty("syntax", out var syn) ? syn.GetString() : null,
                    LineFormat = el.TryGetProperty("line_format", out var lf) ? lf.GetString() : null
                };
                dict[prop.Name] = info;
            }
            return dict;
        }

        private static IReadOnlyDictionary<string, WeaponInfo> ParseWeapons(string json)
        {
            var doc = JsonDocument.Parse(json);
            var dict = new Dictionary<string, WeaponInfo>(StringComparer.OrdinalIgnoreCase);
            if (!doc.RootElement.TryGetProperty("items", out var items)) return dict;
            foreach (var prop in items.EnumerateObject())
            {
                var el = prop.Value;
                var info = new WeaponInfo
                {
                    Id = prop.Name,
                    Name = el.TryGetProperty("name", out var n) ? n.GetString() ?? prop.Name : prop.Name,
                    Category = el.TryGetProperty("category", out var c) ? c.GetString() : null,
                    Tags = el.EnumerateObject().Where(p => p.Name != "name" && p.Name != "category")
                        .ToDictionary(p => p.Name, p => p.Value.ToString())
                };
                dict[prop.Name] = info;
            }
            return dict;
        }

        private static string ResolveKnowledgeDirectory()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new List<string>
            {
                Path.Combine(baseDir, "Knowledge")
            };
            var parent = Directory.GetParent(baseDir);
            if (parent != null) candidates.Add(Path.Combine(parent.FullName, "Knowledge"));
            var parent2 = parent?.Parent;
            if (parent2 != null) candidates.Add(Path.Combine(parent2.FullName, "Knowledge"));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "Knowledge"));
            foreach (var c in candidates)
            {
                if (Directory.Exists(c)) return c;
            }
            return Path.Combine(baseDir, "Knowledge");
        }
    }

    public record SearchResult(string Kind, string Key, string Title);

    public class GameSettingInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "string";
        public string? Default { get; set; }
        public string? Description { get; set; }
        public string? LineFormat { get; set; }
        public string[] Allowed { get; set; } = Array.Empty<string>();
        public RangeConstraint? Range { get; set; }
    }

    public class StarterPackSettingInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "string";
        public string? Description { get; set; }
        public string? Syntax { get; set; }
        public string? LineFormat { get; set; }
    }

    public class WeaponInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Category { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    public record RangeConstraint(string Min, string Max);
}


