using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Resources;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class RingSpawnsViewModel : ObservableObject, IDisposable
    {
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IToastService _toast;

        private StarterPackTextSettings? _settings;
        private FileSystemWatcher? _watcherRoot;
        private FileSystemWatcher? _watcherCfg;
        private Timer? _debounce;
        private bool _saving;
        private bool _disposed;

        // ── Observable properties ────────────────────────────────────────────────

        [ObservableProperty] private string _ringSizes = "";
        [ObservableProperty] private string _ringSpeeds = "";
        [ObservableProperty] private string _baseRingTime = "";
        [ObservableProperty] private string _timeBeforeFirstRing = "";
        [ObservableProperty] private string _selectedLocation = "";
        [ObservableProperty] private bool _useCustomSpawn;
        [ObservableProperty] private string _matchSpawns = "";
        [ObservableProperty] private string _spawnCountText = "";
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private string _locationInfo = "";
        [ObservableProperty] private string _spawnInfo = "When checked, ValidSpawnPoints is set to 6 (custom).";
        [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<SpawnLocation> _spawnLocations
            = new(ItemDatabase.SpawnLocations);
        [ObservableProperty] private SpawnLocation? _selectedSpawnLocation;

        // ── Constructor ──────────────────────────────────────────────────────────

        public RingSpawnsViewModel(
            IServerPathProvider serverPathProvider,
            IToastService toast)
        {
            _serverPathProvider = serverPathProvider;
            _toast = toast;

            _serverPathProvider.PathChanged += OnServerPathChanged;

            if (!string.IsNullOrEmpty(_serverPathProvider.ServerPath))
                Initialize(_serverPathProvider.ServerPath);
        }

        private void OnServerPathChanged()
        {
            var path = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(path)) return;
            Initialize(path);
        }

        internal void Initialize(string serverDir)
        {
            SetupWatchers(serverDir);
            LoadSettings(serverDir);
        }

        // ── Partial callbacks ────────────────────────────────────────────────────

        partial void OnMatchSpawnsChanged(string value)
        {
            UpdateSpawnCount(value);
        }

        partial void OnUseCustomSpawnChanged(bool value)
        {
            SpawnInfo = value
                ? "ValidSpawnPoints will be set to 6 (custom)."
                : "Custom spawn disabled. ValidSpawnPoints will revert to default.";
        }

        partial void OnSelectedSpawnLocationChanged(SpawnLocation? value)
        {
            if (value == null) return;
            LocationInfo = $"Ring Size: {value.RingSize}  |  Lobby: {value.LobbySpawn}  |  Spawns: {value.MatchSpawns.Split(';').Length} points";
        }

        // ── Commands ─────────────────────────────────────────────────────────────

        [RelayCommand]
        private void Save()
        {
            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(serverDir))
            {
                _toast.Warning(Messages.ServerPathNotSet);
                return;
            }

            try
            {
                if (_settings == null)
                    _settings = StarterPackConfigService.Read(serverDir);

                _settings.CustomSpawnPoint = SelectedLocation.Trim();
                if (UseCustomSpawn)
                    _settings.ValidSpawnPoints = "6";
                else if (_settings.ValidSpawnPoints == "6")
                    _settings.ValidSpawnPoints = "2";

                StarterPackConfigService.Write(serverDir, _settings);

                // Save match spawn points
                var spawnPointsStr = MultilineToSpawnPoints(MatchSpawns);
                ModConfigService.WriteSpawnPoints(serverDir, spawnPointsStr);

                // Save ring sizes/speeds to game_settings.txt
                var gsPath = Path.Combine(serverDir, "game_settings.txt");
                var gs = File.Exists(gsPath)
                    ? ConfigIO.ReadGameSettings(gsPath)
                    : new GameSettingsData();

                gs.RingSizes = RingSizes.Trim();
                gs.RingSpeeds = RingSpeeds.Trim();
                _saving = true;
                ConfigIO.WriteGameSettings(gs, gsPath);
                _saving = false;

                StatusText = Messages.SettingsSaved;
            }
            catch (Exception ex)
            {
                _saving = false;
                StatusText = string.Format(Messages.ErrorSaving, ex.Message);
            }
        }

        [RelayCommand]
        private void ApplyLocation()
        {
            var loc = SelectedSpawnLocation;
            if (loc == null) return;

            RingSizes = $"{loc.RingSize}, {loc.RingSize}, {loc.RingSize}";
            SelectedLocation = loc.LobbySpawn;
            UseCustomSpawn = true;
            MatchSpawns = SpawnPointsToMultiline(loc.MatchSpawns);
            StatusText = string.Format(Messages.AppliedLocation, loc.Name);
        }

        [RelayCommand]
        private void PresetDeathmatch()
        {
            if (SelectedSpawnLocation != null)
                RingSizes = $"{SelectedSpawnLocation.RingSize}, {SelectedSpawnLocation.RingSize}, {SelectedSpawnLocation.RingSize}";
            RingSpeeds = "0.001, 50, 0.001";
            StatusText = Messages.AppliedDeathmatch;
        }

        [RelayCommand]
        private void PresetStandardBR()
        {
            RingSizes = "4240.0, 3450.0, 1710.0";
            RingSpeeds = "120, 120, 150";
            StatusText = Messages.AppliedStandardBR;
        }

        // ── Static helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Converts semicolon-separated "x,z;x,z;..." to multiline "x,z\nx,z\n..."
        /// </summary>
        public static string SpawnPointsToMultiline(string spawnPoints)
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
        public static string MultilineToSpawnPoints(string multiline)
        {
            if (string.IsNullOrWhiteSpace(multiline)) return "";
            var points = multiline.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p));
            return string.Join(";", points);
        }

        // ── Internal helpers ─────────────────────────────────────────────────────

        private void LoadSettings(string serverDir)
        {
            try
            {
                // Load StarterPack settings
                _settings = StarterPackConfigService.Read(serverDir);
                SelectedLocation = _settings.CustomSpawnPoint;
                UseCustomSpawn = _settings.ValidSpawnPoints.Contains("6");

                // Load game_settings.txt for ring sizes/speeds
                var gsPath = Path.Combine(serverDir, "game_settings.txt");
                if (File.Exists(gsPath))
                {
                    var gs = ConfigIO.ReadGameSettings(gsPath);
                    RingSizes = gs.RingSizes;
                    RingSpeeds = gs.RingSpeeds;
                }

                // Load match spawn points
                var spawnPoints = ModConfigService.ReadSpawnPoints(serverDir);
                MatchSpawns = SpawnPointsToMultiline(spawnPoints);

                StatusText = Messages.SettingsLoaded;
            }
            catch (Exception ex)
            {
                StatusText = string.Format(Messages.ErrorLoading, ex.Message);
            }
        }

        private void SetupWatchers(string serverDir)
        {
            _watcherRoot?.Dispose();
            _watcherCfg?.Dispose();
            _watcherRoot = null;
            _watcherCfg = null;

            void OnChange(object? s, FileSystemEventArgs e)
            {
                if (_saving) return;
                _debounce?.Dispose();
                _debounce = new Timer(_ =>
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        try { LoadSettings(serverDir); }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[RingSpawnsVM] Debounce reload failed: {ex.Message}");
                        }
                    });
                }, null, 500, Timeout.Infinite);
            }

            if (Directory.Exists(serverDir))
            {
                _watcherRoot = new FileSystemWatcher(serverDir)
                {
                    Filter = "*.txt",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                _watcherRoot.Changed += OnChange;
            }

            var cfgDir = Path.Combine(serverDir, "BepInEx", "config");
            if (Directory.Exists(cfgDir))
            {
                _watcherCfg = new FileSystemWatcher(cfgDir)
                {
                    Filter = "TabgInstaller.MatchCore.cfg",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                _watcherCfg.Changed += OnChange;
            }
        }

        private void UpdateSpawnCount(string matchSpawns)
        {
            var lines = matchSpawns
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();
            SpawnCountText = string.Format(Messages.SpawnPointCount, lines.Length);
        }

        // ── IDisposable ──────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _serverPathProvider.PathChanged -= OnServerPathChanged;
            _debounce?.Dispose();
            _watcherRoot?.Dispose();
            _watcherCfg?.Dispose();
        }
    }
}
