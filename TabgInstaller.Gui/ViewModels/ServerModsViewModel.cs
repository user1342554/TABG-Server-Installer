using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TabgInstaller.Core;
using TabgInstaller.Gui.Resources;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class PluginEntry : ObservableObject
    {
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private bool _isEnabled;
    }

    public partial class BundledEntry : ObservableObject
    {
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private bool _isSelected;
    }

    public partial class ServerModsViewModel : ObservableObject
    {
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IToastService _toast;

        [ObservableProperty] private ObservableCollection<PluginEntry> _plugins = new();
        [ObservableProperty] private ObservableCollection<BundledEntry> _availableMods = new();
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private bool _allPluginsInstalled;

        public bool CanInstallBundled => !AllPluginsInstalled;

        partial void OnAllPluginsInstalledChanged(bool value) =>
            OnPropertyChanged(nameof(CanInstallBundled));

        // All known bundled server plugin DLLs
        public static readonly string[] KnownServerPlugins = new[]
        {
            "Citruslib.dll", "TabgInstaller.MatchCore.dll", "TabgInstaller.ServerLogger.dll",
            "TabgInstaller.UnusedVehicles.dll", "TabgInstaller.CustomGrenades.dll",
            "TabgInstaller.SoloTesting.dll", "TabgInstaller.ProximityChat.Server.dll",
            "TabgInstaller.HuntMode.dll", "TabgInstaller.HuntMode.Shared.dll",
            "JuggernautMode.Server.dll", "TabgInstaller.FakePlayers.dll",
            "TabgInstaller.AdminRadar.Server.dll",
        };

        public ServerModsViewModel(IServerPathProvider serverPathProvider, IToastService toast)
        {
            _serverPathProvider = serverPathProvider;
            _toast = toast;

            _serverPathProvider.PathChanged += OnServerPathChanged;
        }

        private string PluginsDir =>
            Path.Combine(_serverPathProvider.ServerPath, "BepInEx", "plugins");

        private void OnServerPathChanged() => RefreshAll();

        private void RefreshAll()
        {
            LoadPluginsList();
            LoadAvailableList();
        }

        private void LoadPluginsList()
        {
            var serverPath = _serverPathProvider.ServerPath;
            if (string.IsNullOrWhiteSpace(serverPath)) return;

            try
            {
                var pluginsDir = PluginsDir;
                if (!Directory.Exists(pluginsDir))
                    Directory.CreateDirectory(pluginsDir);

                var plugins = new ObservableCollection<PluginEntry>();
                var disabledDir = Path.Combine(pluginsDir, "disabled");

                foreach (var dll in Directory.GetFiles(pluginsDir, "*.dll"))
                    plugins.Add(new PluginEntry { Name = Path.GetFileName(dll), IsEnabled = true });

                if (Directory.Exists(disabledDir))
                {
                    foreach (var dll in Directory.GetFiles(disabledDir, "*.dll"))
                        plugins.Add(new PluginEntry { Name = Path.GetFileName(dll), IsEnabled = false });
                }

                Plugins = plugins;
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.ErrorLoadingPlugins, ex.Message));
            }
        }

        private void LoadAvailableList()
        {
            var serverPath = _serverPathProvider.ServerPath;
            if (string.IsNullOrWhiteSpace(serverPath)) return;

            var pluginsDir = PluginsDir;
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(pluginsDir))
                foreach (var f in Directory.GetFiles(pluginsDir, "*.dll"))
                    installed.Add(Path.GetFileName(f));

            var disabledDir = Path.Combine(pluginsDir, "disabled");
            if (Directory.Exists(disabledDir))
                foreach (var f in Directory.GetFiles(disabledDir, "*.dll"))
                    installed.Add(Path.GetFileName(f));

            var available = new ObservableCollection<BundledEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dll in KnownServerPlugins)
            {
                if (!installed.Contains(dll) && FindDllPath(dll, "plugins") != null)
                {
                    available.Add(new BundledEntry { Name = dll });
                    seen.Add(dll);
                }
            }

            var bundledDir = FindBundledPluginsDir("plugins");
            if (bundledDir != null)
            {
                foreach (var f in Directory.GetFiles(bundledDir, "*.dll"))
                {
                    var name = Path.GetFileName(f);
                    if (!installed.Contains(name) && !seen.Contains(name))
                    {
                        available.Add(new BundledEntry { Name = name });
                        seen.Add(name);
                    }
                }
            }

            AvailableMods = available;
            AllPluginsInstalled = available.Count == 0;
        }

        [RelayCommand]
        private void TogglePlugin(PluginEntry? entry)
        {
            if (entry == null) return;

            var pluginsDir = PluginsDir;
            var src = entry.IsEnabled
                ? Path.Combine(pluginsDir, entry.Name)
                : Path.Combine(pluginsDir, "disabled", entry.Name);
            var dst = entry.IsEnabled
                ? Path.Combine(pluginsDir, "disabled", entry.Name)
                : Path.Combine(pluginsDir, entry.Name);

            try
            {
                if (entry.IsEnabled)
                {
                    // Moving to disabled dir — ensure it exists
                    var disDir = Path.Combine(pluginsDir, "disabled");
                    if (!Directory.Exists(disDir)) Directory.CreateDirectory(disDir);
                }

                if (File.Exists(src))
                {
                    if (File.Exists(dst)) File.Delete(dst);
                    File.Move(src, dst);
                }

                // Flip the flag to reflect the new state
                entry.IsEnabled = !entry.IsEnabled;
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.FailedToTogglePlugin, ex.Message));
            }
        }

        [RelayCommand]
        private void InstallBundled()
        {
            int count = 0;
            var pluginsDir = PluginsDir;

            foreach (var entry in AvailableMods.Where(x => x.IsSelected))
            {
                var srcPath = FindDllPath(entry.Name, "plugins");
                if (srcPath == null) continue;
                var dst = Path.Combine(pluginsDir, entry.Name);
                try
                {
                    File.Copy(srcPath, dst, true);
                    count++;
                }
                catch (Exception ex)
                {
                    _toast.Error(string.Format(Messages.FailedToInstallPlugin, entry.Name, ex.Message));
                }
            }

            if (count > 0)
                StatusText = string.Format(Messages.InstalledPluginCount, count);

            RefreshAll();
        }

        [RelayCommand]
        private void RemovePlugin(PluginEntry? entry)
        {
            if (entry == null) return;

            var result = System.Windows.MessageBox.Show(
                string.Format(Messages.ConfirmRemove, entry.Name), Messages.ConfirmTitle,
                System.Windows.MessageBoxButton.YesNo);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                var path = entry.IsEnabled
                    ? Path.Combine(PluginsDir, entry.Name)
                    : Path.Combine(PluginsDir, "disabled", entry.Name);

                if (File.Exists(path))
                {
                    File.Delete(path);
                    RefreshAll();
                }
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.FailedToRemovePlugin, ex.Message));
            }
        }

        [RelayCommand]
        private void Refresh() => RefreshAll();

        [RelayCommand]
        private void OpenFolder()
        {
            var dir = PluginsDir;
            if (Directory.Exists(dir))
                Process.Start("explorer", dir);
        }

        /// <summary>
        /// Called from code-behind after OpenFileDialog succeeds.
        /// </summary>
        public void AddDll(string filePath)
        {
            try
            {
                var dst = Path.Combine(PluginsDir, Path.GetFileName(filePath));
                File.Copy(filePath, dst, true);
                RefreshAll();
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.FailedToAddPlugin, ex.Message));
            }
        }

        public string? FindBundledPluginsDir(string folderName)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, folderName),
                Path.Combine(baseDir, "..", folderName),
                Path.Combine(baseDir, "..", "..", folderName),
                Path.Combine(baseDir, "..", "..", "..", folderName),
            };
            return candidates.FirstOrDefault(d => Directory.Exists(d) && Directory.GetFiles(d, "*.dll").Length > 0);
        }

        public string? FindDllPath(string dllName, string folderName)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, folderName, dllName),
                Path.Combine(baseDir, "..", folderName, dllName),
                Path.Combine(baseDir, "..", "..", folderName, dllName),
                Path.Combine(baseDir, "..", "..", "..", folderName, dllName),
            };
            return candidates.FirstOrDefault(File.Exists);
        }
    }
}
