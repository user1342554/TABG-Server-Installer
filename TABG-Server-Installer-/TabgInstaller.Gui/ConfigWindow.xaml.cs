using System;
using System.Windows;
using System.IO;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Gui.ViewModels;
using TabgInstaller.Core.Services;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using TabgInstaller.Gui.Tabs;

namespace TabgInstaller.Gui
{
    public partial class ConfigWindow : Window
    {
        private readonly string _serverDir;
        private readonly string _pluginsDir;
        private readonly GameSettingsDynamicViewModel _vm;
        private ServerProcessService? _procSvc;
        private FileSystemWatcher? _gameSettingsWatcher;
        private FileSystemWatcher? _datapackWatcher;
        private DateTime _lastWriteTime = DateTime.MinValue;

        private class PluginEntry { public string Name {get;set;} = ""; public bool IsEnabled {get;set;} }

        public ConfigWindow(string serverDir)
        {
            _serverDir = serverDir;
            _pluginsDir = Path.Combine(_serverDir, "BepInEx", "plugins");
            var gsPath = Path.Combine(serverDir, "game_settings.txt");
            var gs = ConfigIO.ReadGameSettings(gsPath);
            _vm = new GameSettingsDynamicViewModel(gs);
            DataContext = _vm;
            InitializeComponent();

            // Initialize Weapon Spawn Config tab
            // WeaponSpawnGrid.DataContext = new WeaponSpawnViewModel(_serverDir);
            
            // Initialize StarterPack tab
            // StarterPackGrid.Initialize(_serverDir);

            // NEW: pass server path to Presets tab
            PresetsGridControl.SetServerPath(_serverDir);

            StartButton.Click += StartButton_Click;
            StopButton.Click  += StopButton_Click;

            LoadPluginsList();
            
            // Set up file watchers for real-time sync
            SetupFileWatchers();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Temporarily disable file watching to avoid triggering on our own save
            if (_gameSettingsWatcher != null)
                _gameSettingsWatcher.EnableRaisingEvents = false;
                
            var path = Path.Combine(_serverDir, "game_settings.txt");
            ConfigIO.WriteGameSettings(_vm.ToModel(), path);
            
            StatusTextBlock.Text = "Settings saved to file";
            
            // Re-enable file watching after a short delay
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

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
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

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_procSvc?.IsRunning == true) return;
            _procSvc ??= new ServerProcessService(_serverDir);
            _procSvc.OutputReceived += line=>Dispatcher.Invoke(()=>{
                ConsoleTextBox.AppendText(line+"\n");
                ConsoleTextBox.ScrollToEnd();
            });
            try
            {
                _procSvc.Start();
                StartButton.IsEnabled=false; StopButton.IsEnabled=true;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message,"Failed to start",MessageBoxButton.OK,MessageBoxImage.Error);
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_procSvc==null|| !_procSvc.IsRunning) return;
            _procSvc.Stop();
            StartButton.IsEnabled=true; StopButton.IsEnabled=false;
        }

        private void OpenServerFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer", _serverDir);
        }

        private void OpenLogs_Click(object sender, RoutedEventArgs e)
        {
            var logDir = Path.Combine(_serverDir, "BepInEx");
            if(!Directory.Exists(logDir)) logDir = _serverDir;
            Process.Start("explorer", logDir);
        }

        private void OpenConfigs_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer", _serverDir);
        }

        private void HardReset_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This feature has been temporarily disabled.");
        }

        public void QuickSaveRestart_Click(object sender, RoutedEventArgs e)
        {
            // Save game_settings
            var gsPath = Path.Combine(_serverDir, "game_settings.txt");
            ConfigIO.WriteGameSettings(_vm.ToModel(), gsPath);
            // restart server
            StopButton_Click(sender, e);
            StartButton_Click(sender, e);
        }

        private void AiChat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var aiChatWindow = new AiChatWindow(_serverDir);
                aiChatWindow.Owner = this;
                aiChatWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening AI Chat: {ex.Message}\n\nDetails: {ex.StackTrace}", 
                    "AI Chat Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void ClearConsole_Click(object sender, RoutedEventArgs e)
        {
            ConsoleTextBox.Clear();
        }

        private void CopyConsole_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(ConsoleTextBox.Text); }
            catch { }
        }



        private void LoadPluginsList()
        {
            LstPlugins.ItemsSource = null;
            var list = new List<PluginEntry>();
            if (Directory.Exists(_pluginsDir))
            {
                foreach (var file in Directory.GetFiles(_pluginsDir, "*", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(file);
                    bool enabled = file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
                    list.Add(new PluginEntry{ Name = name, IsEnabled = enabled});
                }
            }
            LstPlugins.ItemsSource = list;
        }

        private void AddPlugin_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "DLL files (*.dll)|*.dll",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                Directory.CreateDirectory(_pluginsDir);
                foreach (var src in dlg.FileNames)
                {
                    try
                    {
                        var dest = Path.Combine(_pluginsDir, Path.GetFileName(src));
                        File.Copy(src, dest, true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to copy {Path.GetFileName(src)}: {ex.Message}");
                    }
                }
                LoadPluginsList();
            }
        }

        private void RemovePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (LstPlugins.SelectedItem is string file)
            {
                var path = Path.Combine(_pluginsDir, file);
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                    LoadPluginsList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Cannot delete {file}: {ex.Message}");
                }
            }
        }

        private void OpenPluginsFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(_pluginsDir);
            Process.Start("explorer", _pluginsDir);
        }

        private void PluginToggle(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.CheckBox)?.DataContext is PluginEntry entry)
            {
                if (entry.Name.Equals("StarterPack.dll", StringComparison.OrdinalIgnoreCase))
                {
                    // StarterPack is mandatory – ignore any attempt to disable
                    (sender as CheckBox)!.IsChecked = true;
                    return;
                }
                string src = Path.Combine(_pluginsDir, entry.Name);
                if (entry.IsEnabled)
                {
                    // ensure extension .dll
                    if (src.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                    {
                        string dst = src.Substring(0, src.Length - 9); // remove .disabled
                        try { File.Move(src, dst, true); entry.Name = Path.GetFileName(dst);} catch(Exception ex){MessageBox.Show(ex.Message);}                    }
                }
                else
                {
                    // rename to .disabled
                    if (src.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        string dst = src + ".disabled";
                        try { File.Move(src, dst, true); entry.Name = Path.GetFileName(dst);} catch(Exception ex){MessageBox.Show(ex.Message);}                    }
                }
                LoadPluginsList();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _procSvc?.Dispose();
            _gameSettingsWatcher?.Dispose();
            _datapackWatcher?.Dispose();
            base.OnClosed(e);
        }
        
        private void SetupFileWatchers()
        {
            // Watch game_settings.txt
            var gameSettingsPath = Path.Combine(_serverDir, "game_settings.txt");
            if (File.Exists(gameSettingsPath))
            {
                _gameSettingsWatcher = new FileSystemWatcher(Path.GetDirectoryName(gameSettingsPath)!)
                {
                    Filter = Path.GetFileName(gameSettingsPath),
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };
                _gameSettingsWatcher.Changed += OnGameSettingsChanged;
                _gameSettingsWatcher.EnableRaisingEvents = true;
            }
            
            // Watch datapack.txt
            var datapackPath = Path.Combine(_serverDir, "datapack.txt");
            if (File.Exists(datapackPath))
            {
                _datapackWatcher = new FileSystemWatcher(Path.GetDirectoryName(datapackPath)!)
                {
                    Filter = Path.GetFileName(datapackPath),
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };
                _datapackWatcher.Changed += OnDatapackChanged;
                _datapackWatcher.EnableRaisingEvents = true;
            }
        }
        
        private void OnGameSettingsChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce rapid changes
            var lastWrite = File.GetLastWriteTime(e.FullPath);
            if (lastWrite - _lastWriteTime < TimeSpan.FromMilliseconds(500))
                return;
                
            _lastWriteTime = lastWrite;
            
            // Reload settings on UI thread
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var gs = ConfigIO.ReadGameSettings(e.FullPath);
                    _vm.UpdateFromGameSettings(gs);
                    StatusTextBlock.Text = "Configuration reloaded from file";
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = $"Error reloading: {ex.Message}";
                }
            }));
        }
        
        private void OnDatapackChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                var currentWriteTime = File.GetLastWriteTime(e.FullPath);
                if (currentWriteTime != _lastWriteTime)
                {
                    _lastWriteTime = currentWriteTime;
                    Dispatcher.InvokeAsync(() => StatusTextBlock.Text = "External datapack change detected");
                }
            }
        }
    }
} 