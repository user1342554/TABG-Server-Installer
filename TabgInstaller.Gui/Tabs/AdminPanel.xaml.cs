using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class AdminPanel : UserControl
    {
        private string _serverDir = "";
        private readonly ObservableCollection<AdminEntry> _admins = new();
        private readonly KnownPlayersService _knownPlayers = new();

        public AdminPanel()
        {
            InitializeComponent();
            LstAdmins.ItemsSource = _admins;
        }

        public void Initialize(string serverDir)
        {
            _serverDir = serverDir;
            RefreshKnownPlayers();
            LoadAdmins();
        }

        // Fixed: save to server root, not BepInEx/config/CitrusLib/
        private string GetPermsPath() => Path.Combine(_serverDir, "PlayerPerms.json");

        private void RefreshKnownPlayers()
        {
            var count = _knownPlayers.ScanGuestbooks(_serverDir);
            var names = _knownPlayers.GetPlayerNames();
            CmbPlayerName.ItemsSource = names;
            if (count > 0)
                TxtStatus.Text = $"Found {names.Count} known players ({count} new)";
        }

        private void RefreshPlayers_Click(object sender, RoutedEventArgs e)
        {
            RefreshKnownPlayers();
            ToastService.Instance.Info($"Scanned Guestbooks — {_knownPlayers.Players.Count} players known");
        }

        private void LoadAdmins()
        {
            _admins.Clear();
            var path = GetPermsPath();
            if (!File.Exists(path)) return;

            try
            {
                var json = File.ReadAllText(path);
                var root = JsonSerializer.Deserialize<PlayerPermsRoot[]>(json);
                if (root == null || root.Length == 0) return;

                var section = root[0];
                if (section.Players == null) return;

                foreach (var p in section.Players)
                {
                    if (string.IsNullOrWhiteSpace(p.Epic)) continue;
                    _admins.Add(new AdminEntry
                    {
                        Name = p.Name ?? "",
                        EpicId = p.Epic ?? "",
                        PermLevel = p.PermLevel
                    });
                }
                TxtStatus.Text = $"Loaded {_admins.Count} admin(s)";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error loading: {ex.Message}";
            }
        }

        private void AddAdmin_Click(object sender, RoutedEventArgs e)
        {
            var playerName = CmbPlayerName.Text?.Trim();
            if (string.IsNullOrEmpty(playerName))
            {
                ToastService.Instance.Warning("Please select or type a player name.");
                return;
            }

            var epicId = _knownPlayers.ResolveEpicId(playerName);
            if (epicId == null)
            {
                ToastService.Instance.Warning($"Player '{playerName}' not found in Guestbooks. Use manual entry below.");
                return;
            }

            if (_admins.Any(a => a.EpicId.Equals(epicId, StringComparison.OrdinalIgnoreCase)))
            {
                ToastService.Instance.Warning($"'{playerName}' is already an admin.");
                return;
            }

            var level = CmbPermLevel.SelectedIndex + 1;
            _admins.Add(new AdminEntry { Name = playerName, EpicId = epicId, PermLevel = level });
            CmbPlayerName.Text = "";
            TxtStatus.Text = $"Added {playerName}";
        }

        private void AddManualAdmin_Click(object sender, RoutedEventArgs e)
        {
            var name = TxtManualName.Text.Trim();
            var epicId = TxtManualEpicId.Text.Trim();
            var level = CmbManualPermLevel.SelectedIndex + 1;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(epicId))
            {
                ToastService.Instance.Warning("Please enter both a name and an Epic ID.");
                return;
            }

            if (_admins.Any(a => a.EpicId.Equals(epicId, StringComparison.OrdinalIgnoreCase)))
            {
                ToastService.Instance.Warning($"'{name}' is already an admin.");
                return;
            }

            _admins.Add(new AdminEntry { Name = name, EpicId = epicId, PermLevel = level });
            TxtManualName.Clear();
            TxtManualEpicId.Clear();
            TxtStatus.Text = $"Added {name} (manual)";
        }

        private void RemoveAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (LstAdmins.SelectedItem is AdminEntry entry)
            {
                _admins.Remove(entry);
                TxtStatus.Text = $"Removed {entry.Name}";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var players = _admins.Select(a => new PlayerPermsPlayer
                {
                    Name = a.Name,
                    Epic = a.EpicId,
                    PermLevel = a.PermLevel
                }).ToArray();

                var root = new[]
                {
                    new PlayerPermsRoot
                    {
                        Name = "players",
                        Description = "List of players with modified permission level. Default permission level is 0.",
                        Players = players
                    }
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(root, options);
                File.WriteAllText(GetPermsPath(), json);
                ToastService.Instance.Success("Admins saved. Restart server to apply changes.");
                TxtStatus.Text = $"Saved {_admins.Count} admin(s)";
            }
            catch (Exception ex)
            {
                ToastService.Instance.Error($"Failed to save admins: {ex.Message}");
            }
        }

        public class AdminEntry
        {
            public string Name { get; set; } = "";
            public string EpicId { get; set; } = "";
            public int PermLevel { get; set; } = 4;
        }

        private class PlayerPermsRoot
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "players";
            [JsonPropertyName("description")]
            public string Description { get; set; } = "";
            [JsonPropertyName("players")]
            public PlayerPermsPlayer[]? Players { get; set; }
        }

        private class PlayerPermsPlayer
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";
            [JsonPropertyName("epic")]
            public string Epic { get; set; } = "";
            [JsonPropertyName("permlevel")]
            public int PermLevel { get; set; } = 4;
        }
    }
}
