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

    public partial class PluginCatalogEntry : ObservableObject
    {
        public PluginDefinition Definition { get; init; } = null!;
        public IReadOnlyList<PluginDefinition> Definitions { get; init; } = Array.Empty<PluginDefinition>();
        public string[] DllNames { get; init; } = Array.Empty<string>();
        public string Id => Definition.Id;
        public string DisplayName => string.Join(" / ", EffectiveDefinitions
            .Select(definition => SplitLabel(definition.Label).name)
            .Distinct(StringComparer.OrdinalIgnoreCase));
        public string Description => string.Join("; ", EffectiveDefinitions
            .Select(definition => SplitLabel(definition.Label).description)
            .Where(description => !string.IsNullOrWhiteSpace(description))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        public string Dlls => DllNames.Length == 0 ? "Handled by installer" : string.Join(", ", DllNames);
        public string Kind => Definition.Kind == PluginKind.CoreDependency ? "Dependency" : "Bundled";
        public string ClientRequirement => EffectiveDefinitions.Any(definition => definition.RequiresClientMod) ? "Client mod required" : "";
        public string DefaultText => EffectiveDefinitions.All(definition => definition.DefaultChecked) ? "Default install" : "Optional";
        public bool DefaultChecked => EffectiveDefinitions.Any(definition => definition.DefaultChecked);

        private IReadOnlyList<PluginDefinition> EffectiveDefinitions =>
            Definitions.Count > 0 ? Definitions : new[] { Definition };

        [ObservableProperty] private bool _isInstalled;
        [ObservableProperty] private bool _isEnabled;
        [ObservableProperty] private bool _isAvailable;

        public string StateText =>
            IsEnabled ? "Installed, enabled" :
            IsInstalled ? "Installed, disabled" :
            IsAvailable ? "Ready to install" :
            "Missing bundled DLL";

        public bool CanToggle => IsInstalled || IsEnabled || IsAvailable;

        partial void OnIsInstalledChanged(bool value)
        {
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(CanToggle));
        }

        partial void OnIsEnabledChanged(bool value)
        {
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(CanToggle));
        }

        partial void OnIsAvailableChanged(bool value)
        {
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(CanToggle));
        }

        private static (string name, string description) SplitLabel(string label)
        {
            var marker = " - ";
            var index = label.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
                return (label, "");

            return (label.Substring(0, index), label.Substring(index + marker.Length));
        }
    }

    public partial class ServerModsViewModel : ObservableObject
    {
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IToastService _toast;

        [ObservableProperty] private ObservableCollection<PluginEntry> _plugins = new();
        [ObservableProperty] private ObservableCollection<BundledEntry> _availableMods = new();
        [ObservableProperty] private ObservableCollection<PluginCatalogEntry> _pluginCatalog = new();
        [ObservableProperty] private PluginCatalogEntry? _selectedCatalogPlugin;
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
            LoadPluginCatalog();
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

        private void LoadPluginCatalog()
        {
            var serverPath = _serverPathProvider.ServerPath;
            if (string.IsNullOrWhiteSpace(serverPath)) return;

            var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pluginsDir = PluginsDir;
            var disabledDir = Path.Combine(pluginsDir, "disabled");

            if (Directory.Exists(pluginsDir))
            {
                foreach (var file in Directory.GetFiles(pluginsDir, "*.dll"))
                    enabled.Add(Path.GetFileName(file));
            }

            if (Directory.Exists(disabledDir))
            {
                foreach (var file in Directory.GetFiles(disabledDir, "*.dll"))
                    disabled.Add(Path.GetFileName(file));
            }

            var catalog = new ObservableCollection<PluginCatalogEntry>();
            foreach (var definitions in CollapseDuplicateDefinitions(PluginRegistry.ServerPlugins))
            {
                var definition = definitions[0];
                var dlls = GetCatalogDllNames(definitions);
                var isInstalled = dlls.Length > 0 && dlls.All(dll => enabled.Contains(dll) || disabled.Contains(dll));
                var isEnabled = dlls.Length > 0 && dlls.All(dll => enabled.Contains(dll));
                var isAvailable = dlls.Length > 0 && dlls.All(dll => FindDllPath(dll, "plugins") != null);

                catalog.Add(new PluginCatalogEntry
                {
                    Definition = definition,
                    Definitions = definitions,
                    DllNames = dlls,
                    IsInstalled = isInstalled,
                    IsEnabled = isEnabled,
                    IsAvailable = isAvailable
                });
            }

            PluginCatalog = catalog;
            if (SelectedCatalogPlugin != null)
            {
                SelectedCatalogPlugin = PluginCatalog.FirstOrDefault(p => p.Id.Equals(SelectedCatalogPlugin.Id, StringComparison.OrdinalIgnoreCase));
            }

            SelectedCatalogPlugin ??= PluginCatalog.FirstOrDefault();
        }

        public static List<PluginDefinition[]> CollapseDuplicateDefinitions(IEnumerable<PluginDefinition> definitions)
        {
            return definitions
                .GroupBy(GetPluginDllKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.ToArray())
                .ToList();
        }

        public static string[] GetCatalogDllNames(IReadOnlyList<PluginDefinition> definitions)
        {
            return definitions
                .SelectMany(definition => definition.DllNames ?? Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(dll => dll, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string GetPluginDllKey(PluginDefinition definition)
        {
            var dlls = definition.DllNames ?? Array.Empty<string>();
            return dlls.Length == 0
                ? "id:" + definition.Id
                : "dll:" + string.Join("|", dlls.OrderBy(dll => dll, StringComparer.OrdinalIgnoreCase));
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
        private void ToggleCatalogPlugin(PluginCatalogEntry? entry)
        {
            if (entry == null) return;

            if (entry.IsEnabled)
            {
                MoveCatalogPlugin(entry, enable: false);
                return;
            }

            if (entry.IsInstalled)
                MoveCatalogPlugin(entry, enable: true);
            else
                InstallCatalogPlugin(entry);
        }

        [RelayCommand]
        private void InstallCatalogPlugin(PluginCatalogEntry? entry)
        {
            if (entry == null) return;

            var pluginsDir = PluginsDir;
            Directory.CreateDirectory(pluginsDir);

            var count = 0;
            foreach (var dll in entry.DllNames)
            {
                var srcPath = FindDllPath(dll, "plugins");
                if (srcPath == null)
                {
                    _toast.Error($"Bundled DLL not found: {dll}");
                    continue;
                }

                try
                {
                    File.Copy(srcPath, Path.Combine(pluginsDir, dll), overwrite: true);
                    count++;
                }
                catch (Exception ex)
                {
                    _toast.Error(string.Format(Messages.FailedToInstallPlugin, dll, ex.Message));
                }
            }

            if (count > 0)
                StatusText = $"Installed {entry.DisplayName}.";

            RefreshAll();
        }

        [RelayCommand]
        private void EnableCatalogPlugin(PluginCatalogEntry? entry)
        {
            MoveCatalogPlugin(entry, enable: true);
        }

        [RelayCommand]
        private void DisableCatalogPlugin(PluginCatalogEntry? entry)
        {
            MoveCatalogPlugin(entry, enable: false);
        }

        private void MoveCatalogPlugin(PluginCatalogEntry? entry, bool enable)
        {
            if (entry == null) return;

            var pluginsDir = PluginsDir;
            var disabledDir = Path.Combine(pluginsDir, "disabled");
            Directory.CreateDirectory(pluginsDir);
            Directory.CreateDirectory(disabledDir);

            try
            {
                foreach (var dll in entry.DllNames)
                {
                    var src = enable ? Path.Combine(disabledDir, dll) : Path.Combine(pluginsDir, dll);
                    var dst = enable ? Path.Combine(pluginsDir, dll) : Path.Combine(disabledDir, dll);
                    if (!File.Exists(src)) continue;
                    if (File.Exists(dst)) File.Delete(dst);
                    File.Move(src, dst);
                }

                StatusText = enable
                    ? $"Enabled {entry.DisplayName}."
                    : $"Disabled {entry.DisplayName}.";
                RefreshAll();
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.FailedToTogglePlugin, ex.Message));
            }
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
