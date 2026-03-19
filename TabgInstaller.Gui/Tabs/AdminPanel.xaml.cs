using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace TabgInstaller.Gui.Tabs
{
    public partial class AdminPanel : UserControl
    {
        private string _serverDir = "";
        private readonly ObservableCollection<AdminEntry> _admins = new();

        public AdminPanel()
        {
            InitializeComponent();
            LstAdmins.ItemsSource = _admins;
        }

        public void Initialize(string serverDir)
        {
            _serverDir = serverDir;
            LoadAdmins();
        }

        private string GetPermsPath() => Path.Combine(_serverDir, "PlayerPerms.json");

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
            var name = TxtName.Text.Trim();
            var epicId = TxtEpicId.Text.Trim();
            var level = CmbPermLevel.SelectedIndex + 1;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(epicId))
            {
                MessageBox.Show("Please enter both a name and an Epic ID.", "Missing Info",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _admins.Add(new AdminEntry { Name = name, EpicId = epicId, PermLevel = level });
            TxtName.Clear();
            TxtEpicId.Clear();
            TxtStatus.Text = $"Added {name}";
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
                var players = new PlayerPermsPlayer[_admins.Count];
                for (int i = 0; i < _admins.Count; i++)
                {
                    players[i] = new PlayerPermsPlayer
                    {
                        Name = _admins[i].Name,
                        Epic = _admins[i].EpicId,
                        PermLevel = _admins[i].PermLevel
                    };
                }

                var root = new[]
                {
                    new PlayerPermsRoot
                    {
                        Name = "players",
                        Description = "List of players with modified permission level.",
                        Players = players
                    }
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(root, options);
                File.WriteAllText(GetPermsPath(), json);
                TxtStatus.Text = $"Saved {_admins.Count} admin(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- View model ---
        public class AdminEntry
        {
            public string Name { get; set; } = "";
            public string EpicId { get; set; } = "";
            public int PermLevel { get; set; } = 4;
        }

        // --- JSON models matching PlayerPerms.json ---
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
