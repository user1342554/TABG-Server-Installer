using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Resources;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class ConfigViewModel : ObservableObject, IDisposable
    {
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IToastService _toast;
        private readonly ConfigValidationService _validationService;

        private FileSystemWatcher? _gameSettingsWatcher;
        private DateTime _lastWriteTime = DateTime.MinValue;
        private Timer? _autoSaveDebounce;
        private bool _saving;
        private bool _disposed;

        [ObservableProperty] private GameSettingsDynamicViewModel? _gameSettingsVm;
        [ObservableProperty] private string _statusText = "";

        public ConfigViewModel(
            IServerPathProvider serverPathProvider,
            IToastService toast,
            ConfigValidationService validationService)
        {
            _serverPathProvider = serverPathProvider;
            _toast = toast;
            _validationService = validationService;

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

        private void Initialize(string serverDir)
        {
            // Dispose previous watcher / debounce
            _gameSettingsWatcher?.Dispose();
            _gameSettingsWatcher = null;
            _autoSaveDebounce?.Dispose();
            _autoSaveDebounce = null;

            var gsPath = Path.Combine(serverDir, "game_settings.txt");
            if (File.Exists(gsPath))
            {
                var gs = ConfigIO.ReadGameSettings(gsPath);
                var newVm = new GameSettingsDynamicViewModel(gs, _validationService);
                WireAutoSave(newVm);
                GameSettingsVm = newVm;
            }
            else
            {
                GameSettingsVm = null;
            }

            SetupFileWatcher(serverDir);
        }

        // ── Auto-save ─────────────────────────────────────────────────────────────

        private void WireAutoSave(GameSettingsDynamicViewModel vm)
        {
            vm.PropertyChanged += (_, _) => ScheduleAutoSave();
        }

        private void ScheduleAutoSave()
        {
            Application.Current?.Dispatcher.Invoke(() => StatusText = "Unsaved changes...");
            _autoSaveDebounce?.Dispose();
            _autoSaveDebounce = new Timer(_ =>
            {
                Application.Current?.Dispatcher.Invoke(PerformAutoSave);
            }, null, 1500, Timeout.Infinite);
        }

        private void PerformAutoSave()
        {
            if (GameSettingsVm == null) return;
            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(serverDir)) return;

            try
            {
                _saving = true;
                if (_gameSettingsWatcher != null)
                    _gameSettingsWatcher.EnableRaisingEvents = false;

                var path = Path.Combine(serverDir, "game_settings.txt");
                ConfigIO.WriteGameSettings(GameSettingsVm.ToModel(), path);
                StatusText = Messages.AllChangesSaved;

                // Re-enable watcher after a short delay so the write event is ignored
                var reenableTimer = new Timer(_ =>
                {
                    _saving = false;
                    if (_gameSettingsWatcher != null)
                        _gameSettingsWatcher.EnableRaisingEvents = true;
                }, null, 1000, Timeout.Infinite);

                // Fade the status text after 2s
                var fadeTimer = new Timer(_ =>
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        if (StatusText == Messages.AllChangesSaved)
                            StatusText = "";
                    });
                }, null, 3000, Timeout.Infinite);

                _ = reenableTimer; // captured to prevent early GC
                _ = fadeTimer;
            }
            catch (Exception ex)
            {
                _saving = false;
                StatusText = string.Format(Messages.AutoSaveFailed, ex.Message);
                if (_gameSettingsWatcher != null)
                    _gameSettingsWatcher.EnableRaisingEvents = true;
            }
        }

        // ── File watcher ──────────────────────────────────────────────────────────

        private void SetupFileWatcher(string serverDir)
        {
            try
            {
                var gsPath = Path.Combine(serverDir, "game_settings.txt");
                if (!File.Exists(gsPath)) return;

                _gameSettingsWatcher = new FileSystemWatcher(Path.GetDirectoryName(gsPath)!)
                {
                    Filter = Path.GetFileName(gsPath),
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _gameSettingsWatcher.Changed += (_, e) =>
                {
                    if (_saving) return;
                    var currentWriteTime = File.GetLastWriteTime(e.FullPath);
                    if (currentWriteTime == _lastWriteTime) return;
                    _lastWriteTime = currentWriteTime;

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var gs = ConfigIO.ReadGameSettings(e.FullPath);
                            var updatedVm = new GameSettingsDynamicViewModel(gs, _validationService);
                            WireAutoSave(updatedVm);
                            GameSettingsVm = updatedVm;
                            StatusText = Messages.SettingsReloaded;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ConfigVM] Reload failed: {ex.Message}");
                        }
                    });
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigVM] SetupFileWatcher failed: {ex.Message}");
            }
        }

        // ── Commands ──────────────────────────────────────────────────────────────

        [RelayCommand]
        private void Save()
        {
            if (GameSettingsVm == null) return;
            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(serverDir)) return;

            try
            {
                _saving = true;
                if (_gameSettingsWatcher != null)
                    _gameSettingsWatcher.EnableRaisingEvents = false;

                var path = Path.Combine(serverDir, "game_settings.txt");
                ConfigIO.WriteGameSettings(GameSettingsVm.ToModel(), path);
                StatusText = Messages.SettingsSavedToFile;

                var reenableTimer = new Timer(_ =>
                {
                    _saving = false;
                    if (_gameSettingsWatcher != null)
                        _gameSettingsWatcher.EnableRaisingEvents = true;
                }, null, 1000, Timeout.Infinite);

                _ = reenableTimer;
            }
            catch (Exception ex)
            {
                _saving = false;
                _toast.Error(string.Format(Messages.SaveFailed, ex.Message));
            }
        }

        [RelayCommand]
        private void OpenGameSettings()
        {
            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(serverDir)) return;
            try
            {
                var path = Path.Combine(serverDir, "game_settings.txt");
                if (!File.Exists(path))
                {
                    _toast.Warning(Messages.GameSettingsNotFound);
                    return;
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.CouldNotOpenFile, ex.Message));
            }
        }

        [RelayCommand]
        private void OpenServerFolder()
        {
            var serverDir = _serverPathProvider.ServerPath;
            if (!string.IsNullOrEmpty(serverDir))
                Process.Start("explorer", serverDir);
        }

        [RelayCommand]
        private void OpenLogs()
        {
            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(serverDir)) return;
            var logDir = Path.Combine(serverDir, "BepInEx");
            if (!Directory.Exists(logDir)) logDir = serverDir;
            Process.Start("explorer", logDir);
        }

        [RelayCommand]
        private void OpenConfigs()
        {
            var serverDir = _serverPathProvider.ServerPath;
            if (!string.IsNullOrEmpty(serverDir))
                Process.Start("explorer", serverDir);
        }

        // ── IDisposable ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _serverPathProvider.PathChanged -= OnServerPathChanged;
            _gameSettingsWatcher?.Dispose();
            _autoSaveDebounce?.Dispose();
        }
    }
}
