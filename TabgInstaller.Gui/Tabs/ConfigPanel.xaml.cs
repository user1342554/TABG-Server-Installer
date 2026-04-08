using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ConfigPanel : UserControl
    {
        private string _serverDir = "";
        private GameSettingsDynamicViewModel? _vm;
        private FileSystemWatcher? _gameSettingsWatcher;
        private DateTime _lastWriteTime = DateTime.MinValue;
        private System.Windows.Threading.DispatcherTimer? _autoSaveTimer;

        public ConfigPanel()
        {
            InitializeComponent();
        }

        public void Initialize(string serverDir)
        {
            _serverDir = serverDir;

            // Set global server path for other components to use
            GlobalServerPath.Set(serverDir);

            var gsPath = Path.Combine(serverDir, "game_settings.txt");
            if (File.Exists(gsPath))
            {
                var gs = ConfigIO.ReadGameSettings(gsPath);
                _vm = new GameSettingsDynamicViewModel(gs);
                DataContext = _vm;
            }

            PresetsGridControl.SetServerPath(_serverDir);
            SetupFileWatchers();

            // Initialize config sub-panels
            MatchSettingsControl.Initialize(_serverDir);
            RingSpawnsControl.Initialize(_serverDir);
            LoadoutEditorControl.Initialize(_serverDir);
            ModSettingsControl.Initialize(_serverDir);
            // AdminPanelControl DataContext is set in MainWindow; ViewModel subscribes to PathChanged

            // Setup auto-save
            SetupAutoSave();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;

            if (_gameSettingsWatcher != null)
                _gameSettingsWatcher.EnableRaisingEvents = false;

            var path = Path.Combine(_serverDir, "game_settings.txt");
            ConfigIO.WriteGameSettings(_vm.ToModel(), path);

            StatusTextBlock.Text = "Settings saved to file";

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1000);
            timer.Tick += (s, args) =>
            {
                if (_gameSettingsWatcher != null)
                    _gameSettingsWatcher.EnableRaisingEvents = true;
                timer.Stop();
            };
            timer.Start();
        }

        private void SetupAutoSave()
        {
            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            _autoSaveTimer.Tick += (s, e) =>
            {
                _autoSaveTimer.Stop();
                PerformAutoSave();
            };

            if (_vm != null)
            {
                _vm.PropertyChanged += (s, e) => ResetAutoSaveTimer();
            }
        }

        private void ResetAutoSaveTimer()
        {
            _autoSaveTimer?.Stop();
            StatusTextBlock.Text = "Unsaved changes...";
            StatusTextBlock.Opacity = 1;
            _autoSaveTimer?.Start();
        }

        private void PerformAutoSave()
        {
            if (_vm == null) return;
            try
            {
                if (_gameSettingsWatcher != null)
                    _gameSettingsWatcher.EnableRaisingEvents = false;

                var path = Path.Combine(_serverDir, "game_settings.txt");
                ConfigIO.WriteGameSettings(_vm.ToModel(), path);
                StatusTextBlock.Text = "All changes saved";

                var fadeTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                fadeTimer.Tick += (s, e) =>
                {
                    fadeTimer.Stop();
                    var animation = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
                    animation.Completed += (_, _) =>
                    {
                        StatusTextBlock.Opacity = 1;
                        if (StatusTextBlock.Text == "All changes saved")
                            StatusTextBlock.Text = "";
                    };
                    StatusTextBlock.BeginAnimation(System.Windows.UIElement.OpacityProperty, animation);
                };
                fadeTimer.Start();

                var reenableTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(1000)
                };
                reenableTimer.Tick += (s, args) =>
                {
                    if (_gameSettingsWatcher != null)
                        _gameSettingsWatcher.EnableRaisingEvents = true;
                    reenableTimer.Stop();
                };
                reenableTimer.Start();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Auto-save failed: {ex.Message}";
            }
        }

        private void OpenGameSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = Path.Combine(_serverDir, "game_settings.txt");
                if (!File.Exists(path))
                {
                    ToastServiceStatic.Instance.Warning("game_settings.txt not found. Save settings first to generate the file.");
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
                ToastServiceStatic.Instance.Error($"Could not open file: {ex.Message}");
            }
        }

        private void OpenServerFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer", _serverDir);
        }

        private void OpenLogs_Click(object sender, RoutedEventArgs e)
        {
            var logDir = Path.Combine(_serverDir, "BepInEx");
            if (!Directory.Exists(logDir)) logDir = _serverDir;
            Process.Start("explorer", logDir);
        }

        private void OpenConfigs_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer", _serverDir);
        }

        private void SetupFileWatchers()
        {
            try
            {
                var gsPath = Path.Combine(_serverDir, "game_settings.txt");
                if (File.Exists(gsPath))
                {
                    _gameSettingsWatcher = new FileSystemWatcher(Path.GetDirectoryName(gsPath)!)
                    {
                        Filter = Path.GetFileName(gsPath),
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                    };

                    _gameSettingsWatcher.Changed += (s, e) =>
                    {
                        var currentWriteTime = File.GetLastWriteTime(e.FullPath);
                        if (currentWriteTime != _lastWriteTime)
                        {
                            _lastWriteTime = currentWriteTime;
                            Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    var gs = ConfigIO.ReadGameSettings(e.FullPath);
                                    _vm = new GameSettingsDynamicViewModel(gs);
                                    DataContext = _vm;
                                    StatusTextBlock.Text = "Settings reloaded from file";
                                }
                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ConfigPanel] Operation failed: {ex.Message}"); }
                            });
                        }
                    };

                    _gameSettingsWatcher.EnableRaisingEvents = true;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ConfigPanel] Operation failed: {ex.Message}"); }
        }
    }
}
