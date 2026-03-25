using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TabgInstaller.Core.Services
{
    public class KnownPlayer
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("epicId")]
        public string EpicId { get; set; } = "";
    }

    public sealed class KnownPlayersService
    {
        private static readonly Regex GuestbookLineRegex = new(
            @"^([0-9a-fA-F]{32}):(.+?),\s*Playfab=",
            RegexOptions.Compiled);

        private readonly string _persistPath;
        private List<KnownPlayer> _players = new();

        public IReadOnlyList<KnownPlayer> Players => _players;

        public KnownPlayersService()
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TabgInstaller");
            Directory.CreateDirectory(appData);
            _persistPath = Path.Combine(appData, "KnownPlayers.json");
            Load();
        }

        private void Load()
        {
            if (!File.Exists(_persistPath)) return;
            try
            {
                var json = File.ReadAllText(_persistPath);
                _players = JsonSerializer.Deserialize<List<KnownPlayer>>(json) ?? new();
            }
            catch { _players = new(); }
        }

        private void Save()
        {
            var json = JsonSerializer.Serialize(_players, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_persistPath, json);
        }

        /// <summary>
        /// Scan Guestbook.txt files from sibling directories of the given server path
        /// (up to 2 levels deep) and merge into the known players list.
        /// </summary>
        public int ScanGuestbooks(string currentServerDir)
        {
            var parent = Path.GetDirectoryName(currentServerDir);
            if (parent == null || !Directory.Exists(parent)) return 0;

            var guestbooks = new List<string>();
            try
            {
                guestbooks = Directory.GetFiles(parent, "Guestbook.txt", SearchOption.AllDirectories)
                    .ToList();
            }
            catch { /* permission errors */ }

            // Also scan the current server dir itself
            var currentGb = Path.Combine(currentServerDir, "Guestbook.txt");
            if (File.Exists(currentGb) && !guestbooks.Contains(currentGb, StringComparer.OrdinalIgnoreCase))
                guestbooks.Add(currentGb);

            int newCount = 0;
            foreach (var gbPath in guestbooks)
            {
                try
                {
                    foreach (var line in File.ReadAllLines(gbPath))
                    {
                        var match = GuestbookLineRegex.Match(line);
                        if (!match.Success) continue;

                        var epicId = match.Groups[1].Value.ToLowerInvariant();
                        var name = match.Groups[2].Value.Trim();
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        var existing = _players.FirstOrDefault(p =>
                            p.EpicId.Equals(epicId, StringComparison.OrdinalIgnoreCase));

                        if (existing != null)
                        {
                            if (existing.Name != name) existing.Name = name;
                        }
                        else
                        {
                            _players.Add(new KnownPlayer { Name = name, EpicId = epicId });
                            newCount++;
                        }
                    }
                }
                catch { /* skip unreadable files */ }
            }

            _players = _players.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
            Save();
            return newCount;
        }

        public string? ResolveEpicId(string playerName)
        {
            return _players.FirstOrDefault(p =>
                p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase))?.EpicId;
        }

        public List<string> GetPlayerNames()
        {
            return _players.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
