using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class MatchSettingsViewModel : ObservableObject, IDisposable
    {
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IToastService _toast;

        private StarterPackTextSettings? _settings;
        private FileSystemWatcher? _watcher;
        private Timer? _debounce;
        private bool _saving;
        private bool _disposed;

        // ── Match mode ───────────────────────────────────────────────────────────
        [ObservableProperty] private string _loadoutMode = "Normal";
        [ObservableProperty] private string _winCondition = "Default";
        [ObservableProperty] private string _killsToWin = "";
        [ObservableProperty] private Visibility _showKillsToWin = Visibility.Collapsed;

        // ── Match flags ──────────────────────────────────────────────────────────
        [ObservableProperty] private bool _forceKillAtStart;
        [ObservableProperty] private bool _healOnKill;
        [ObservableProperty] private string _healOnKillAmount = "20";
        [ObservableProperty] private bool _dropItemsOnDeath;
        [ObservableProperty] private bool _canGoDown;
        [ObservableProperty] private bool _canLockOut;

        // ── Vote ─────────────────────────────────────────────────────────────────
        [ObservableProperty] private string _percentOfVotes = "50";
        [ObservableProperty] private string _minPlayers = "2";
        [ObservableProperty] private string _timeToStart = "30";

        // ── Spell drops ──────────────────────────────────────────────────────────
        [ObservableProperty] private bool _spelldropEnabled = true;
        [ObservableProperty] private string _minSpellDropDelay = "180";
        [ObservableProperty] private string _maxSpellDropDelay = "420";
        [ObservableProperty] private string _spellDropOffset = "30";

        // ── Timeouts ─────────────────────────────────────────────────────────────
        [ObservableProperty] private string _preMatchTimeout = "15";
        [ObservableProperty] private string _periMatchTimeout = "15";

        // ── Status ───────────────────────────────────────────────────────────────
        [ObservableProperty] private string _statusText = "";

        public MatchSettingsViewModel(
            IServerPathProvider serverPathProvider,
            IToastService toast)
        {
            _serverPathProvider = serverPathProvider;
            _toast = toast;

            _serverPathProvider.PathChanged += OnServerPathChanged;

            // If a path is already set (e.g. late-bind), load now.
            if (!string.IsNullOrEmpty(_serverPathProvider.ServerPath))
                Initialize(_serverPathProvider.ServerPath);
        }

        // Called by PathChanged event or directly by the panel when embedded.
        private void OnServerPathChanged()
        {
            var path = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(path)) return;
            Initialize(path);
        }

        // Internal – sets up watcher and loads settings for a given server directory.
        internal void Initialize(string serverDir)
        {
            SetupWatcher(serverDir);
            LoadSettings(serverDir);
        }

        // ── Partial callbacks ────────────────────────────────────────────────────

        partial void OnWinConditionChanged(string value)
        {
            ShowKillsToWin = value == "KillsToWin"
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ── Commands ─────────────────────────────────────────────────────────────

        [RelayCommand]
        private void Save()
        {
            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(serverDir))
            {
                _toast.Warning("Server path is not set.");
                return;
            }

            try
            {
                if (_settings == null)
                    _settings = new StarterPackTextSettings();

                // Loadout mode — preserve loadout content, change prefix
                var loadoutsWithoutPrefix = _settings.GetLoadoutsWithoutPrefix();
                _settings.SetLoadoutsWithPrefix(LoadoutMode, loadoutsWithoutPrefix);

                // Win condition
                _settings.WinCondition = WinCondition;
                if (int.TryParse(KillsToWin.Trim(), out var ktw))
                    _settings.KillsToWin = ktw;
                else
                    _settings.KillsToWin = null;

                // Match flags
                _settings.ForceKillAtStart = ForceKillAtStart;
                _settings.HealOnKill = HealOnKill;
                if (float.TryParse(HealOnKillAmount.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var healAmt))
                    _settings.HealOnKillAmount = healAmt;
                _settings.DropItemsOnDeath = DropItemsOnDeath;
                _settings.CanGoDown = CanGoDown;
                _settings.CanLockOut = CanLockOut;

                // Vote
                if (int.TryParse(PercentOfVotes.Trim(), out var pov))
                    _settings.PercentOfVotes = pov;
                if (int.TryParse(MinPlayers.Trim(), out var mnp))
                    _settings.MinNumberOfPlayers = mnp;
                if (int.TryParse(TimeToStart.Trim(), out var tts))
                    _settings.TimeToStart = tts;

                // Spell drops
                _settings.SpelldropEnabled = SpelldropEnabled;
                if (int.TryParse(MinSpellDropDelay.Trim(), out var minsd))
                    _settings.MinSpellDropDelay = minsd;
                if (int.TryParse(MaxSpellDropDelay.Trim(), out var maxsd))
                    _settings.MaxSpellDropDelay = maxsd;
                if (int.TryParse(SpellDropOffset.Trim(), out var sdo))
                    _settings.SpellDropOffset = sdo;

                // Timeouts
                if (float.TryParse(PreMatchTimeout.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var pmt))
                    _settings.PreMatchTimeout = pmt;
                if (float.TryParse(PeriMatchTimeout.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var pmtt))
                    _settings.PeriMatchTimeout = pmtt;

                _saving = true;
                StarterPackConfigService.Write(serverDir, _settings);
                _saving = false;
                StatusText = "Settings saved.";
            }
            catch (Exception ex)
            {
                _saving = false;
                StatusText = $"Error saving: {ex.Message}";
            }
        }

        // ── Internal helpers ─────────────────────────────────────────────────────

        private void LoadSettings(string serverDir)
        {
            try
            {
                _settings = StarterPackConfigService.Read(serverDir);

                LoadoutMode = _settings.GetLoadoutMode();
                WinCondition = _settings.WinCondition;
                KillsToWin = _settings.KillsToWin?.ToString() ?? "";
                // ShowKillsToWin is updated via OnWinConditionChanged

                ForceKillAtStart = _settings.ForceKillAtStart;
                HealOnKill = _settings.HealOnKill;
                HealOnKillAmount = _settings.HealOnKillAmount.ToString(CultureInfo.InvariantCulture);
                DropItemsOnDeath = _settings.DropItemsOnDeath;
                CanGoDown = _settings.CanGoDown;
                CanLockOut = _settings.CanLockOut;

                PercentOfVotes = _settings.PercentOfVotes.ToString();
                MinPlayers = _settings.MinNumberOfPlayers.ToString();
                TimeToStart = _settings.TimeToStart.ToString();

                SpelldropEnabled = _settings.SpelldropEnabled;
                MinSpellDropDelay = _settings.MinSpellDropDelay.ToString();
                MaxSpellDropDelay = _settings.MaxSpellDropDelay.ToString();
                SpellDropOffset = _settings.SpellDropOffset.ToString();

                PreMatchTimeout = _settings.PreMatchTimeout.ToString(CultureInfo.InvariantCulture);
                PeriMatchTimeout = _settings.PeriMatchTimeout.ToString(CultureInfo.InvariantCulture);

                StatusText = "Settings loaded.";
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading: {ex.Message}";
            }
        }

        private void SetupWatcher(string serverDir)
        {
            _watcher?.Dispose();
            _watcher = null;

            var dir = Path.GetDirectoryName(Path.Combine(serverDir, "TheStarterPack.txt"));
            if (dir == null || !Directory.Exists(dir)) return;

            _watcher = new FileSystemWatcher(dir)
            {
                Filter = "TheStarterPack.txt",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += (_, _) =>
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
                                $"[MatchSettingsVM] Debounce reload failed: {ex.Message}");
                        }
                    });
                }, null, 500, Timeout.Infinite);
            };
        }

        // ── IDisposable ──────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _serverPathProvider.PathChanged -= OnServerPathChanged;
            _debounce?.Dispose();
            _watcher?.Dispose();
        }
    }
}
