using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Gui.Resources;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class ClientModEntry : ObservableObject
    {
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private bool _isEnabled;
    }

    public partial class ClientPanelViewModel : ObservableObject
    {
        private readonly IAppSettingsService _appSettings;
        private readonly IToastService _toast;

        [ObservableProperty] private string _clientPath = "";
        [ObservableProperty] private string _moddedPath = "";
        [ObservableProperty] private ObservableCollection<ClientModEntry> _mods = new();
        [ObservableProperty] private ObservableCollection<BundledEntry> _availableClientMods = new();
        [ObservableProperty] private ObservableCollection<PluginCatalogEntry> _clientModCatalog = new();
        [ObservableProperty] private PluginCatalogEntry? _selectedClientCatalogMod;
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private bool _isSettingUp;
        [ObservableProperty] private bool _allClientModsInstalled;

        public bool CanInstallBundled => !AllClientModsInstalled;

        partial void OnAllClientModsInstalledChanged(bool value) =>
            OnPropertyChanged(nameof(CanInstallBundled));

        private string PluginsDir =>
            string.IsNullOrEmpty(ModdedPath) ? "" : Path.Combine(ModdedPath, "BepInEx", "plugins");

        // All known bundled client mod DLLs
        public static readonly string[] KnownClientMods = new[]
        {
            "TabgInstaller.FlyingControls.dll", "TabgInstaller.CustomGrenades.dll",
            "TabgInstaller.CoordsDisplay.dll", "TabgInstaller.ModSettings.dll",
            "TabgInstaller.EnhancedClient.dll", "TabgInstaller.PopupBlocker.dll",
            "TabgInstaller.ProximityChat.Client.dll", "TabgInstaller.HuntMode.Client.dll",
            "TabgInstaller.HuntMode.Shared.dll", "JuggernautMode.Client.dll",
            "TabgInstaller.AdminRadar.Client.dll",
        };

        public ClientPanelViewModel(IAppSettingsService appSettings, IToastService toast)
        {
            _appSettings = appSettings;
            _toast = toast;
        }

        /// <summary>Load persisted paths and refresh lists. Called once from code-behind Loaded.</summary>
        public void Initialize()
        {
            var settings = _appSettings.Load();

            if (!string.IsNullOrEmpty(settings.ClientPath))
                ClientPath = settings.ClientPath;
            else
            {
                var detected = Installer.TryFindTabgClientPath();
                if (!string.IsNullOrEmpty(detected))
                    ClientPath = detected;
            }

            if (!string.IsNullOrEmpty(settings.ClientModdedPath))
                ModdedPath = settings.ClientModdedPath;
            else if (!string.IsNullOrEmpty(ClientPath))
            {
                var parent = Path.GetDirectoryName(ClientPath);
                if (parent != null)
                    ModdedPath = Path.Combine(parent, "TABG_Modded");
            }

            RefreshAll();
        }

        /// <summary>Called from code-behind BrowseClient_Click after folder dialog succeeds.</summary>
        public void SetClientPath(string folderPath)
        {
            ClientPath = folderPath;
            var parent = Path.GetDirectoryName(folderPath);
            if (parent != null)
                ModdedPath = Path.Combine(parent, "TABG_Modded");

            var settings = _appSettings.Load();
            settings.ClientPath = ClientPath;
            settings.ClientModdedPath = ModdedPath;
            _appSettings.Save(settings);

            RefreshAll();
        }

        /// <summary>Called from code-behind AddDll_Click after file dialog succeeds.</summary>
        public void AddDll(string filePath)
        {
            var pluginsDir = PluginsDir;
            if (string.IsNullOrEmpty(pluginsDir) || !Directory.Exists(pluginsDir))
            {
                _toast.Warning(Messages.RunInitialSetupFirst);
                return;
            }

            try
            {
                File.Copy(filePath, Path.Combine(pluginsDir, Path.GetFileName(filePath)), true);
                RefreshAll();
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.ErrorPrefix, ex.Message));
            }
        }

        // ── Commands ─────────────────────────────────────────────────────────────

        [RelayCommand]
        private void ToggleMod(ClientModEntry? mod)
        {
            if (mod == null) return;

            var pluginsDir = PluginsDir;
            var src = mod.IsEnabled
                ? Path.Combine(pluginsDir, mod.Name)
                : Path.Combine(pluginsDir, "disabled", mod.Name);
            var dst = mod.IsEnabled
                ? Path.Combine(pluginsDir, "disabled", mod.Name)
                : Path.Combine(pluginsDir, mod.Name);

            try
            {
                if (mod.IsEnabled)
                {
                    var disDir = Path.Combine(pluginsDir, "disabled");
                    if (!Directory.Exists(disDir)) Directory.CreateDirectory(disDir);
                }

                if (File.Exists(src))
                {
                    if (File.Exists(dst)) File.Delete(dst);
                    File.Move(src, dst);
                }

                mod.IsEnabled = !mod.IsEnabled;
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.FailedToToggleMod, ex.Message));
            }
        }

        [RelayCommand]
        private async Task Setup()
        {
            var clientDir = ClientPath.Trim();
            var moddedDir = ModdedPath.Trim();

            if (string.IsNullOrWhiteSpace(clientDir) || !Directory.Exists(clientDir))
            {
                _toast.Warning(Messages.EnterValidTabgPath);
                return;
            }
            if (string.IsNullOrWhiteSpace(moddedDir))
            {
                _toast.Warning(Messages.NoModdedFolderPath);
                return;
            }

            bool alreadyExists = Directory.Exists(moddedDir) &&
                File.Exists(Path.Combine(moddedDir, "TotallyAccurateBattlegrounds.exe"));

            var msg = alreadyExists
                ? string.Format(Messages.InitialClientSetupExisting, moddedDir)
                : string.Format(Messages.InitialClientSetupNew, moddedDir);

            if (MessageBox.Show(msg, Messages.InitialClientSetupTitle,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            IsSettingUp = true;

            try
            {
                bool success = await Task.Run(() =>
                    ClientModInstaller.InstallAsync(
                        clientDir, moddedDir,
                        new List<string>(),
                        new Progress<string>(_ => { })));

                if (success)
                {
                    var settings = _appSettings.Load();
                    settings.ClientPath = clientDir;
                    settings.ClientModdedPath = moddedDir;
                    _appSettings.Save(settings);

                    RefreshAll();
                }
                else
                {
                    _toast.Error(Messages.ClientSetupFailed);
                }
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.ErrorPrefix, ex.Message));
            }
            finally
            {
                IsSettingUp = false;
            }
        }

        [RelayCommand]
        private void InstallBundled()
        {
            var pluginsDir = PluginsDir;
            if (string.IsNullOrEmpty(pluginsDir) || !Directory.Exists(pluginsDir))
            {
                _toast.Warning(Messages.RunInitialSetupCopy);
                return;
            }

            int count = 0;
            foreach (var entry in AvailableClientMods.Where(x => x.IsSelected))
            {
                var srcPath = FindDllPath(entry.Name);
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
                StatusText = string.Format(Messages.InstalledModCount, count);

            RefreshAll();
        }

        [RelayCommand]
        private void ToggleClientCatalogMod(PluginCatalogEntry? entry)
        {
            if (entry == null) return;

            if (entry.IsEnabled)
            {
                MoveClientCatalogMod(entry, enable: false);
                return;
            }

            if (entry.IsInstalled)
                MoveClientCatalogMod(entry, enable: true);
            else
                InstallClientCatalogMod(entry);
        }

        private void InstallClientCatalogMod(PluginCatalogEntry entry)
        {
            var pluginsDir = PluginsDir;
            if (string.IsNullOrEmpty(pluginsDir) || !Directory.Exists(pluginsDir))
            {
                _toast.Warning(Messages.RunInitialSetupCopy);
                RefreshAll();
                return;
            }

            var count = 0;
            foreach (var dll in entry.DllNames)
            {
                var srcPath = FindDllPath(dll);
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

        private void MoveClientCatalogMod(PluginCatalogEntry entry, bool enable)
        {
            var pluginsDir = PluginsDir;
            if (string.IsNullOrEmpty(pluginsDir) || !Directory.Exists(pluginsDir))
            {
                _toast.Warning(Messages.RunInitialSetupCopy);
                RefreshAll();
                return;
            }

            var disabledDir = Path.Combine(pluginsDir, "disabled");
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
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.FailedToToggleMod, ex.Message));
            }

            RefreshAll();
        }

        [RelayCommand]
        private void RemoveMod(ClientModEntry? mod)
        {
            if (mod == null) return;

            if (MessageBox.Show(string.Format(Messages.ConfirmRemove, mod.Name), Messages.ConfirmTitle,
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            try
            {
                var path = mod.IsEnabled
                    ? Path.Combine(PluginsDir, mod.Name)
                    : Path.Combine(PluginsDir, "disabled", mod.Name);

                if (File.Exists(path))
                {
                    File.Delete(path);
                    RefreshAll();
                }
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.ErrorPrefix, ex.Message));
            }
        }

        [RelayCommand]
        private void Refresh() => RefreshAll();

        [RelayCommand]
        private void Launch()
        {
            var moddedDir = ModdedPath;
            var exe = Path.Combine(moddedDir, "TotallyAccurateBattlegrounds.exe");
            if (!File.Exists(exe))
            {
                _toast.Warning(Messages.ModdedTabgNotFound);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = moddedDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.FailedToLaunch, ex.Message));
            }
        }

        [RelayCommand]
        private void OpenModdedFolder()
        {
            var moddedDir = ModdedPath;
            if (Directory.Exists(moddedDir))
                Process.Start("explorer", moddedDir);
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private void RefreshAll()
        {
            LoadModsList();
            LoadAvailableList();
            LoadClientModCatalog();
        }

        private void LoadModsList()
        {
            var pluginsDir = PluginsDir;
            if (string.IsNullOrEmpty(pluginsDir) || !Directory.Exists(pluginsDir))
            {
                Mods = new ObservableCollection<ClientModEntry>();
                return;
            }

            var mods = new ObservableCollection<ClientModEntry>();
            var disabledDir = Path.Combine(pluginsDir, "disabled");

            foreach (var dll in Directory.GetFiles(pluginsDir, "*.dll"))
                mods.Add(new ClientModEntry { Name = Path.GetFileName(dll), IsEnabled = true });

            if (Directory.Exists(disabledDir))
            {
                foreach (var dll in Directory.GetFiles(disabledDir, "*.dll"))
                    mods.Add(new ClientModEntry { Name = Path.GetFileName(dll), IsEnabled = false });
            }

            Mods = mods;
        }

        private void LoadAvailableList()
        {
            var pluginsDir = PluginsDir;
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(pluginsDir) && Directory.Exists(pluginsDir))
                foreach (var f in Directory.GetFiles(pluginsDir, "*.dll"))
                    installed.Add(Path.GetFileName(f));

            var disabledDir = string.IsNullOrEmpty(pluginsDir) ? "" : Path.Combine(pluginsDir, "disabled");
            if (!string.IsNullOrEmpty(disabledDir) && Directory.Exists(disabledDir))
                foreach (var f in Directory.GetFiles(disabledDir, "*.dll"))
                    installed.Add(Path.GetFileName(f));

            var available = new ObservableCollection<BundledEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dll in KnownClientMods)
            {
                if (!installed.Contains(dll) && FindDllPath(dll) != null)
                {
                    available.Add(new BundledEntry { Name = dll });
                    seen.Add(dll);
                }
            }

            var bundledDir = FindClientPluginsDir();
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

            AvailableClientMods = available;
            AllClientModsInstalled = available.Count == 0;
        }

        private void LoadClientModCatalog()
        {
            var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pluginsDir = PluginsDir;
            var disabledDir = string.IsNullOrEmpty(pluginsDir) ? "" : Path.Combine(pluginsDir, "disabled");

            if (!string.IsNullOrEmpty(pluginsDir) && Directory.Exists(pluginsDir))
            {
                foreach (var file in Directory.GetFiles(pluginsDir, "*.dll"))
                    enabled.Add(Path.GetFileName(file));
            }

            if (!string.IsNullOrEmpty(disabledDir) && Directory.Exists(disabledDir))
            {
                foreach (var file in Directory.GetFiles(disabledDir, "*.dll"))
                    disabled.Add(Path.GetFileName(file));
            }

            var catalog = new ObservableCollection<PluginCatalogEntry>();
            foreach (var definitions in ServerModsViewModel.CollapseDuplicateDefinitions(PluginRegistry.ClientMods))
            {
                var definition = definitions[0];
                var dlls = ServerModsViewModel.GetCatalogDllNames(definitions);
                catalog.Add(new PluginCatalogEntry
                {
                    Definition = definition,
                    Definitions = definitions,
                    DllNames = dlls,
                    IsInstalled = dlls.Length > 0 && dlls.All(dll => enabled.Contains(dll) || disabled.Contains(dll)),
                    IsEnabled = dlls.Length > 0 && dlls.All(dll => enabled.Contains(dll)),
                    IsAvailable = dlls.Length > 0 && dlls.All(dll => FindDllPath(dll) != null)
                });
            }

            ClientModCatalog = catalog;
            if (SelectedClientCatalogMod != null)
            {
                SelectedClientCatalogMod = ClientModCatalog.FirstOrDefault(
                    mod => mod.Id.Equals(SelectedClientCatalogMod.Id, StringComparison.OrdinalIgnoreCase));
            }

            SelectedClientCatalogMod ??= ClientModCatalog.FirstOrDefault();
        }

        public string? FindClientPluginsDir()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "client-plugins"),
                Path.Combine(baseDir, "..", "client-plugins"),
                Path.Combine(baseDir, "..", "..", "client-plugins"),
                Path.Combine(baseDir, "..", "..", "..", "client-plugins"),
            };
            return candidates.FirstOrDefault(d => Directory.Exists(d) && Directory.GetFiles(d, "*.dll").Length > 0);
        }

        public string? FindDllPath(string dllName)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "client-plugins", dllName),
                Path.Combine(baseDir, "..", "client-plugins", dllName),
                Path.Combine(baseDir, "..", "..", "client-plugins", dllName),
                Path.Combine(baseDir, "..", "..", "..", "client-plugins", dllName),
            };
            return candidates.FirstOrDefault(File.Exists);
        }
    }
}
