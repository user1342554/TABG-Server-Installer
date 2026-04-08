using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public class AdminEntry
    {
        public string Name { get; set; } = "";
        public string EpicId { get; set; } = "";
        public int PermLevel { get; set; } = 4;
    }

    internal class PlayerPermsRoot
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "players";
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
        [JsonPropertyName("players")]
        public PlayerPermsPlayer[]? Players { get; set; }
    }

    internal class PlayerPermsPlayer
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("epic")]
        public string Epic { get; set; } = "";
        [JsonPropertyName("permlevel")]
        public int PermLevel { get; set; } = 4;
    }

    public partial class AdminPanelViewModel : ObservableObject
    {
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IKnownPlayersService _knownPlayers;
        private readonly IToastService _toast;

        [ObservableProperty] private ObservableCollection<AdminEntry> _admins = new();
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private string _selectedPlayerName = "";
        [ObservableProperty] private ObservableCollection<string> _knownPlayerNames = new();
        [ObservableProperty] private int _selectedPermLevel = 3;
        [ObservableProperty] private string _manualName = "";
        [ObservableProperty] private string _manualEpicId = "";
        [ObservableProperty] private int _manualPermLevel = 3;

        public AdminPanelViewModel(
            IServerPathProvider serverPathProvider,
            IKnownPlayersService knownPlayers,
            IToastService toast)
        {
            _serverPathProvider = serverPathProvider;
            _knownPlayers = knownPlayers;
            _toast = toast;

            _serverPathProvider.PathChanged += OnServerPathChanged;
        }

        private void OnServerPathChanged()
        {
            LoadAdmins();
            RefreshKnownPlayers();
        }

        private string GetPermsPath() =>
            Path.Combine(_serverPathProvider.ServerPath, "PlayerPerms.json");

        private void RefreshKnownPlayers()
        {
            var count = _knownPlayers.ScanGuestbooks(_serverPathProvider.ServerPath);
            var names = _knownPlayers.GetPlayerNames();
            KnownPlayerNames = new ObservableCollection<string>(names);
            if (count > 0)
                StatusText = $"Found {names.Count} known players ({count} new)";
        }

        private void LoadAdmins()
        {
            Admins.Clear();
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
                    Admins.Add(new AdminEntry
                    {
                        Name = p.Name ?? "",
                        EpicId = p.Epic ?? "",
                        PermLevel = p.PermLevel
                    });
                }
                StatusText = $"Loaded {Admins.Count} admin(s)";
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading: {ex.Message}";
            }
        }

        [RelayCommand]
        private void AddAdmin()
        {
            var playerName = SelectedPlayerName?.Trim();
            if (string.IsNullOrEmpty(playerName))
            {
                _toast.Warning("Please select or type a player name.");
                return;
            }

            var epicId = _knownPlayers.ResolveEpicId(playerName);
            if (epicId == null)
            {
                _toast.Warning($"Player '{playerName}' not found in Guestbooks. Use manual entry below.");
                return;
            }

            if (Admins.Any(a => a.EpicId.Equals(epicId, StringComparison.OrdinalIgnoreCase)))
            {
                _toast.Warning($"'{playerName}' is already an admin.");
                return;
            }

            var level = SelectedPermLevel + 1;
            Admins.Add(new AdminEntry { Name = playerName, EpicId = epicId, PermLevel = level });
            SelectedPlayerName = "";
            StatusText = $"Added {playerName}";
        }

        [RelayCommand]
        private void AddManualAdmin()
        {
            var name = ManualName.Trim();
            var epicId = ManualEpicId.Trim();
            var level = ManualPermLevel + 1;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(epicId))
            {
                _toast.Warning("Please enter both a name and an Epic ID.");
                return;
            }

            if (Admins.Any(a => a.EpicId.Equals(epicId, StringComparison.OrdinalIgnoreCase)))
            {
                _toast.Warning($"'{name}' is already an admin.");
                return;
            }

            Admins.Add(new AdminEntry { Name = name, EpicId = epicId, PermLevel = level });
            ManualName = "";
            ManualEpicId = "";
            StatusText = $"Added {name} (manual)";
        }

        [RelayCommand]
        private void RemoveAdmin(AdminEntry entry)
        {
            Admins.Remove(entry);
            StatusText = $"Removed {entry.Name}";
        }

        [RelayCommand]
        private void Save()
        {
            try
            {
                var players = Admins.Select(a => new PlayerPermsPlayer
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
                _toast.Success("Admins saved. Restart server to apply changes.");
                StatusText = $"Saved {Admins.Count} admin(s)";
            }
            catch (Exception ex)
            {
                _toast.Error($"Failed to save admins: {ex.Message}");
            }
        }

        [RelayCommand]
        private void RefreshPlayers()
        {
            RefreshKnownPlayers();
            _toast.Info($"Scanned Guestbooks — {_knownPlayers.Players.Count} players known");
        }
    }
}
