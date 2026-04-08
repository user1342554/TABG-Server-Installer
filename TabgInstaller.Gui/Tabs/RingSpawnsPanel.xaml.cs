using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class RingSpawnsPanel : UserControl
    {
        private string _serverDir = "";
        private StarterPackTextSettings? _starterPackSettings;
        private FileSystemWatcher? _watcherRoot;
        private FileSystemWatcher? _watcherCfg;
        private Timer? _debounce;
        private bool _saving;

        public RingSpawnsPanel()
        {
            InitializeComponent();
        }

        public void Initialize(string serverDir)
        {
            _serverDir = serverDir;
            LoadSettings();
            SetupWatchers();
        }

        private void SetupWatchers()
        {
            _watcherRoot?.Dispose();
            _watcherCfg?.Dispose();

            void OnChange(object? s, FileSystemEventArgs e)
            {
                if (_saving) return;
                _debounce?.Dispose();
                _debounce = new Timer(_ => Dispatcher.Invoke(() => { try { LoadSettings(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[RingSpawns] Debounce reload failed: {ex.Message}"); } }), null, 500, Timeout.Infinite);
            }

            // Watch server root for game_settings.txt and TheStarterPack.txt
            if (Directory.Exists(_serverDir))
            {
                _watcherRoot = new FileSystemWatcher(_serverDir)
                {
                    Filter = "*.txt",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                _watcherRoot.Changed += OnChange;
            }

            // Watch BepInEx/config for FreddoCustomSpawnpoints.cfg
            var cfgDir = Path.Combine(_serverDir, "BepInEx", "config");
            if (Directory.Exists(cfgDir))
            {
                _watcherCfg = new FileSystemWatcher(cfgDir)
                {
                    Filter = "FreddoCustomSpawnpoints.cfg",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                _watcherCfg.Changed += OnChange;
            }
        }

        private void LoadSettings()
        {
            try
            {
                // Populate location picker
                CmbLocation.ItemsSource = ItemDatabase.SpawnLocations;
                if (ItemDatabase.SpawnLocations.Count > 0)
                    CmbLocation.SelectedIndex = 0;

                // Load StarterPack settings
                _starterPackSettings = StarterPackConfigService.Read(_serverDir);
                TxtCustomSpawnPoint.Text = _starterPackSettings.CustomSpawnPoint;
                ChkUseCustomSpawn.IsChecked = _starterPackSettings.ValidSpawnPoints.Contains("6");

                // Load game_settings.txt for ring sizes/speeds
                var gsPath = Path.Combine(_serverDir, "game_settings.txt");
                if (File.Exists(gsPath))
                {
                    var gs = ConfigIO.ReadGameSettings(gsPath);
                    TxtRingSizes.Text = gs.RingSizes;
                    TxtRingSpeeds.Text = gs.RingSpeeds;
                }

                // Load match spawn points
                var spawnPoints = ModConfigService.ReadSpawnPoints(_serverDir);
                TxtMatchSpawns.Text = SpawnPointsToMultiline(spawnPoints);
                UpdateSpawnCount();

                StatusText.Text = "Settings loaded.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading: {ex.Message}";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save StarterPack settings (CustomSpawnPoint, ValidSpawnPoints)
                if (_starterPackSettings == null)
                    _starterPackSettings = StarterPackConfigService.Read(_serverDir);

                _starterPackSettings.CustomSpawnPoint = TxtCustomSpawnPoint.Text.Trim();
                if (ChkUseCustomSpawn.IsChecked == true)
                    _starterPackSettings.ValidSpawnPoints = "6";
                // Only reset if we previously set it to 6
                else if (_starterPackSettings.ValidSpawnPoints == "6")
                    _starterPackSettings.ValidSpawnPoints = "2";

                StarterPackConfigService.Write(_serverDir, _starterPackSettings);

                // Save match spawn points
                var spawnPointsStr = MultilineToSpawnPoints(TxtMatchSpawns.Text);
                ModConfigService.WriteSpawnPoints(_serverDir, spawnPointsStr);

                // Save ring sizes/speeds to game_settings.txt
                var gsPath = Path.Combine(_serverDir, "game_settings.txt");
                var gs = File.Exists(gsPath)
                    ? ConfigIO.ReadGameSettings(gsPath)
                    : new GameSettingsData();

                gs.RingSizes = TxtRingSizes.Text.Trim();
                gs.RingSpeeds = TxtRingSpeeds.Text.Trim();
                _saving = true;
                ConfigIO.WriteGameSettings(gs, gsPath);
                _saving = false;

                StatusText.Text = "Settings saved.";
            }
            catch (Exception ex)
            {
                _saving = false;
                StatusText.Text = $"Error saving: {ex.Message}";
            }
        }

        private void CmbLocation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbLocation.SelectedItem is SpawnLocation loc)
            {
                TxtLocationInfo.Text = $"Ring Size: {loc.RingSize}  |  Lobby: {loc.LobbySpawn}  |  Spawns: {loc.MatchSpawns.Split(';').Length} points";
            }
        }

        private void ApplyLocation_Click(object sender, RoutedEventArgs e)
        {
            if (CmbLocation.SelectedItem is not SpawnLocation loc) return;

            // Fill ring sizes with the location's ring size repeated 3 times
            TxtRingSizes.Text = $"{loc.RingSize}, {loc.RingSize}, {loc.RingSize}";

            // Fill lobby spawn
            TxtCustomSpawnPoint.Text = loc.LobbySpawn;
            ChkUseCustomSpawn.IsChecked = true;

            // Fill match spawns
            TxtMatchSpawns.Text = SpawnPointsToMultiline(loc.MatchSpawns);
            UpdateSpawnCount();

            StatusText.Text = $"Applied location: {loc.Name}";
        }

        private void PresetDeathmatch_Click(object sender, RoutedEventArgs e)
        {
            // Use current location ring size if one is selected, otherwise keep existing
            if (CmbLocation.SelectedItem is SpawnLocation loc)
            {
                TxtRingSizes.Text = $"{loc.RingSize}, {loc.RingSize}, {loc.RingSize}";
            }
            TxtRingSpeeds.Text = "0.001, 50, 0.001";
            StatusText.Text = "Applied deathmatch ring preset.";
        }

        private void PresetStandardBR_Click(object sender, RoutedEventArgs e)
        {
            TxtRingSizes.Text = "4240.0, 3450.0, 1710.0";
            TxtRingSpeeds.Text = "120, 120, 150";
            StatusText.Text = "Applied standard BR ring preset.";
        }

        private void ChkUseCustomSpawn_Changed(object sender, RoutedEventArgs e)
        {
            // Just update the info text
            if (ChkUseCustomSpawn.IsChecked == true)
                TxtSpawnInfo.Text = "ValidSpawnPoints will be set to 6 (custom).";
            else
                TxtSpawnInfo.Text = "Custom spawn disabled. ValidSpawnPoints will revert to default.";
        }

        private void TxtMatchSpawns_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSpawnCount();
        }

        private void UpdateSpawnCount()
        {
            if (TxtSpawnCount == null) return;
            var lines = TxtMatchSpawns.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();
            TxtSpawnCount.Text = $"{lines.Length} spawn point(s)";
        }

        /// <summary>
        /// Converts semicolon-separated "x,z;x,z;..." to multiline "x,z\nx,z\n..."
        /// </summary>
        private static string SpawnPointsToMultiline(string spawnPoints)
        {
            if (string.IsNullOrWhiteSpace(spawnPoints)) return "";
            var points = spawnPoints.Split(';')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p));
            return string.Join(Environment.NewLine, points);
        }

        /// <summary>
        /// Converts multiline "x,z\nx,z\n..." back to semicolon-separated "x,z;x,z;..."
        /// </summary>
        private static string MultilineToSpawnPoints(string multiline)
        {
            if (string.IsNullOrWhiteSpace(multiline)) return "";
            var points = multiline.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p));
            return string.Join(";", points);
        }
    }
}
