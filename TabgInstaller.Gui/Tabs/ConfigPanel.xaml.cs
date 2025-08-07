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
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ConfigPanel : UserControl
    {
        private string _serverDir = "";
        private string _pluginsDir = "";
        private GameSettingsDynamicViewModel? _vm;
        private ServerProcessService? _procSvc;
        private FileSystemWatcher? _gameSettingsWatcher;
        private FileSystemWatcher? _datapackWatcher;
        private DateTime _lastWriteTime = DateTime.MinValue;

        private class PluginEntry { public string Name { get; set; } = ""; public bool IsEnabled { get; set; } }

        public ConfigPanel()
        {
            InitializeComponent();
        }

        public void Initialize(string serverDir)
        {
            _serverDir = serverDir;
            _pluginsDir = Path.Combine(_serverDir, "BepInEx", "plugins");
            
            var gsPath = Path.Combine(serverDir, "game_settings.txt");
            if (File.Exists(gsPath))
            {
                var gs = ConfigIO.ReadGameSettings(gsPath);
                _vm = new GameSettingsDynamicViewModel(gs);
                DataContext = _vm;
            }

            PresetsGridControl.SetServerPath(_serverDir);
            LoadPluginsList();
            SetupFileWatchers();
            
            // Initialize server process service
            _procSvc = new ServerProcessService(_serverDir);
            _procSvc.OutputReceived += line => Dispatcher.Invoke(() =>
            {
                ConsoleTextBox.AppendText(line + Environment.NewLine);
                ConsoleScrollViewer.ScrollToEnd();
            });
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

        private void OpenGameSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = Path.Combine(_serverDir, "game_settings.txt");
                if (!File.Exists(path))
                {
                    MessageBox.Show("game_settings.txt not found. Save settings first to generate the file.");
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
                MessageBox.Show($"Could not open file: {ex.Message}");
            }
        }

        private void HardReset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("This will reset all settings to defaults. Continue?", "Confirm Hard Reset", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (_vm != null)
                {
                    var defaults = new Core.Model.GameSettingsData(); // Uses default values
                    var newVm = new GameSettingsDynamicViewModel(defaults);
                    _vm = newVm;
                    DataContext = _vm;
                    StatusTextBlock.Text = "Settings reset to defaults (not saved yet)";
                }
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

        private void QuickSaveRestart_Click(object sender, RoutedEventArgs e)
        {
            SaveButton_Click(sender, e);
            if (_procSvc?.IsRunning == true)
            {
                _procSvc.Stop();
                Task.Delay(1000).ContinueWith(_ => Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _procSvc.Start();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to restart: {ex.Message}");
                    }
                }));
            }
        }

        private void TxtCommand_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendCommand_Click(sender, e);
            }
        }

        private void SendCommand_Click(object sender, RoutedEventArgs e)
        {
            if (_procSvc?.IsRunning == true && !string.IsNullOrWhiteSpace(TxtCommand.Text))
            {
                // ServerProcessService doesn't have SendCommand, just log the command
                ConsoleTextBox.AppendText($"> {TxtCommand.Text}{Environment.NewLine}");
                ConsoleTextBox.AppendText($"Command sending not implemented yet{Environment.NewLine}");
                TxtCommand.Clear();
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_procSvc?.IsRunning == true) return;
            
            try
            {
                _procSvc.Start();
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Failed to start", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_procSvc == null || !_procSvc.IsRunning) return;
            
            _procSvc.Stop();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private void ClearConsole_Click(object sender, RoutedEventArgs e)
        {
            ConsoleTextBox.Clear();
        }

        private void LoadPluginsList()
        {
            try
            {
                if (!Directory.Exists(_pluginsDir))
                {
                    Directory.CreateDirectory(_pluginsDir);
                }

                var plugins = new List<PluginEntry>();
                var disabledDir = Path.Combine(_pluginsDir, "disabled");
                
                foreach (var dll in Directory.GetFiles(_pluginsDir, "*.dll"))
                {
                    plugins.Add(new PluginEntry { Name = Path.GetFileName(dll), IsEnabled = true });
                }

                if (Directory.Exists(disabledDir))
                {
                    foreach (var dll in Directory.GetFiles(disabledDir, "*.dll"))
                    {
                        plugins.Add(new PluginEntry { Name = Path.GetFileName(dll), IsEnabled = false });
                    }
                }

                LstPlugins.ItemsSource = plugins;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading plugins: {ex.Message}");
            }
        }

        private void PluginToggle(object sender, RoutedEventArgs e)
        {
            var cb = sender as CheckBox;
            if (cb?.DataContext is PluginEntry pe)
            {
                var src = pe.IsEnabled
                    ? Path.Combine(_pluginsDir, pe.Name)
                    : Path.Combine(_pluginsDir, "disabled", pe.Name);
                var dst = pe.IsEnabled
                    ? Path.Combine(_pluginsDir, "disabled", pe.Name)
                    : Path.Combine(_pluginsDir, pe.Name);

                try
                {
                    if (!pe.IsEnabled)
                    {
                        var disDir = Path.Combine(_pluginsDir, "disabled");
                        if (!Directory.Exists(disDir)) Directory.CreateDirectory(disDir);
                    }

                    if (File.Exists(src))
                    {
                        if (File.Exists(dst)) File.Delete(dst);
                        File.Move(src, dst);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to toggle plugin: {ex.Message}");
                    pe.IsEnabled = !pe.IsEnabled;
                    cb.IsChecked = pe.IsEnabled;
                }
            }
        }

        private void AddPlugin_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "DLL files (*.dll)|*.dll",
                Title = "Select plugin DLL"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var dst = Path.Combine(_pluginsDir, Path.GetFileName(dialog.FileName));
                    File.Copy(dialog.FileName, dst, true);
                    LoadPluginsList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to add plugin: {ex.Message}");
                }
            }
        }

        private void RemovePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (LstPlugins.SelectedItem is PluginEntry pe)
            {
                if (MessageBox.Show($"Remove {pe.Name}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        var path = pe.IsEnabled
                            ? Path.Combine(_pluginsDir, pe.Name)
                            : Path.Combine(_pluginsDir, "disabled", pe.Name);

                        if (File.Exists(path))
                        {
                            File.Delete(path);
                            LoadPluginsList();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to remove plugin: {ex.Message}");
                    }
                }
            }
        }

        private void OpenPluginsFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer", _pluginsDir);
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
                                catch { }
                            });
                        }
                    };

                    _gameSettingsWatcher.EnableRaisingEvents = true;
                }
            }
            catch { }
        }
    }
}
