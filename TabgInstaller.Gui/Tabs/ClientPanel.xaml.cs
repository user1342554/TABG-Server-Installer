using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ClientPanel : UserControl
    {
        private string _clientDir = "";
        private string _moddedDir = "";
        private string _pluginsDir = "";

        private class ModEntry { public string Name { get; set; } = ""; public bool IsEnabled { get; set; } }
        private class BundledEntry { public string Name { get; set; } = ""; public bool IsSelected { get; set; } }

        public ClientPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var settings = AppSettingsServiceStatic.Load();
            if (!string.IsNullOrEmpty(settings.ClientPath))
                ClientPathBox.Text = settings.ClientPath;
            else
            {
                var detected = Installer.TryFindTabgClientPath();
                if (!string.IsNullOrEmpty(detected))
                    ClientPathBox.Text = detected;
            }

            if (!string.IsNullOrEmpty(settings.ClientModdedPath))
                ClientModdedPathBox.Text = settings.ClientModdedPath;
            else if (!string.IsNullOrEmpty(ClientPathBox.Text))
            {
                var parent = Path.GetDirectoryName(ClientPathBox.Text);
                if (parent != null)
                    ClientModdedPathBox.Text = Path.Combine(parent, "TABG_Modded");
            }

            _clientDir = ClientPathBox.Text;
            _moddedDir = ClientModdedPathBox.Text;
            _pluginsDir = string.IsNullOrEmpty(_moddedDir) ? "" : Path.Combine(_moddedDir, "BepInEx", "plugins");

            RefreshAll();
        }

        private void RefreshAll()
        {
            LoadModsList();
            LoadAvailableList();
        }

        private void BrowseClient_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select TABG Steam Folder" };
            if (dialog.ShowDialog() == true)
            {
                ClientPathBox.Text = dialog.FolderName;
                _clientDir = dialog.FolderName;
                var parent = Path.GetDirectoryName(dialog.FolderName);
                if (parent != null)
                {
                    _moddedDir = Path.Combine(parent, "TABG_Modded");
                    ClientModdedPathBox.Text = _moddedDir;
                    _pluginsDir = Path.Combine(_moddedDir, "BepInEx", "plugins");
                }

                var settings = AppSettingsServiceStatic.Load();
                settings.ClientPath = _clientDir;
                settings.ClientModdedPath = _moddedDir;
                AppSettingsServiceStatic.Save(settings);

                RefreshAll();
            }
        }

        private void LoadModsList()
        {
            if (string.IsNullOrEmpty(_pluginsDir) || !Directory.Exists(_pluginsDir))
            {
                LstClientMods.ItemsSource = null;
                TxtNoMods.Visibility = Visibility.Visible;
                return;
            }

            TxtNoMods.Visibility = Visibility.Collapsed;

            var mods = new List<ModEntry>();
            var disabledDir = Path.Combine(_pluginsDir, "disabled");

            foreach (var dll in Directory.GetFiles(_pluginsDir, "*.dll"))
                mods.Add(new ModEntry { Name = Path.GetFileName(dll), IsEnabled = true });

            if (Directory.Exists(disabledDir))
            {
                foreach (var dll in Directory.GetFiles(disabledDir, "*.dll"))
                    mods.Add(new ModEntry { Name = Path.GetFileName(dll), IsEnabled = false });
            }

            LstClientMods.ItemsSource = mods;
        }

        // All known client mod DLLs
        private static readonly string[] KnownClientMods = new[]
        {
            "TabgInstaller.FlyingControls.dll", "Enhanced TABG.dll", "TabgInstaller.CustomGrenades.dll",
            "TabgInstaller.CoordsDisplay.dll", "TabgInstaller.ModSettings.dll", "Pop-up Blocker.dll",
            "TabgInstaller.ProximityChat.Client.dll", "TabgInstaller.HuntMode.Client.dll",
            "TabgInstaller.HuntMode.Shared.dll", "JuggernautMode.Client.dll", "TABGVR.dll",
        };

        private void LoadAvailableList()
        {
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(_pluginsDir) && Directory.Exists(_pluginsDir))
                foreach (var f in Directory.GetFiles(_pluginsDir, "*.dll"))
                    installed.Add(Path.GetFileName(f));
            var disabledDir = string.IsNullOrEmpty(_pluginsDir) ? "" : Path.Combine(_pluginsDir, "disabled");
            if (!string.IsNullOrEmpty(disabledDir) && Directory.Exists(disabledDir))
                foreach (var f in Directory.GetFiles(disabledDir, "*.dll"))
                    installed.Add(Path.GetFileName(f));

            // Show known mods that aren't installed yet and can be found
            var available = new List<BundledEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dll in KnownClientMods)
            {
                if (!installed.Contains(dll) && FindDllPath(dll) != null)
                {
                    available.Add(new BundledEntry { Name = dll });
                    seen.Add(dll);
                }
            }

            // Also add any DLLs from bundled dir not in the known list
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

            if (available.Count == 0)
            {
                TxtAllInstalled.Text = "All client mods are already installed.";
                TxtAllInstalled.Visibility = Visibility.Visible;
                LstBundled.Visibility = Visibility.Collapsed;
                BtnInstallBundled.IsEnabled = false;
            }
            else
            {
                TxtAllInstalled.Visibility = Visibility.Collapsed;
                LstBundled.Visibility = Visibility.Visible;
                LstBundled.ItemsSource = available;
                BtnInstallBundled.IsEnabled = true;
            }
        }

        private void ModToggle(object sender, RoutedEventArgs e)
        {
            var cb = sender as CheckBox;
            if (cb?.DataContext is ModEntry me)
            {
                var src = me.IsEnabled
                    ? Path.Combine(_pluginsDir, me.Name)
                    : Path.Combine(_pluginsDir, "disabled", me.Name);
                var dst = me.IsEnabled
                    ? Path.Combine(_pluginsDir, "disabled", me.Name)
                    : Path.Combine(_pluginsDir, me.Name);

                try
                {
                    if (!me.IsEnabled)
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
                    ToastServiceStatic.Instance.Error($"Failed to toggle mod: {ex.Message}");
                    me.IsEnabled = !me.IsEnabled;
                    cb.IsChecked = me.IsEnabled;
                }
            }
        }

        private async void BtnSetup_Click(object sender, RoutedEventArgs e)
        {
            _clientDir = ClientPathBox.Text.Trim();
            _moddedDir = ClientModdedPathBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(_clientDir) || !Directory.Exists(_clientDir))
            {
                ToastServiceStatic.Instance.Warning("Please enter a valid TABG Steam folder path.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_moddedDir))
            {
                ToastServiceStatic.Instance.Warning("No modded folder path set.");
                return;
            }

            bool alreadyExists = Directory.Exists(_moddedDir) && File.Exists(Path.Combine(_moddedDir, "TotallyAccurateBattlegrounds.exe"));
            var msg = alreadyExists
                ? $"Modded copy already exists at:\n{_moddedDir}\n\nThis will re-copy game files and reinstall BepInEx. Your plugins in BepInEx/plugins will be kept.\n\nContinue?"
                : $"This will copy TABG to:\n{_moddedDir}\n\nThen install BepInEx so you can add client mods.\n\nContinue?";

            if (MessageBox.Show(msg, "Initial Client Setup", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            BtnSetup.IsEnabled = false;

            try
            {
                bool success = await Task.Run(() =>
                    ClientModInstaller.InstallAsync(_clientDir, _moddedDir, new List<string>(), new Progress<string>(_ => { })));

                if (success)
                {
                    _pluginsDir = Path.Combine(_moddedDir, "BepInEx", "plugins");
                    var settings = AppSettingsServiceStatic.Load();
                    settings.ClientPath = _clientDir;
                    settings.ClientModdedPath = _moddedDir;
                    AppSettingsServiceStatic.Save(settings);

                    RefreshAll();
                }
                else
                {
                    ToastServiceStatic.Instance.Error("Client setup failed.");
                }
            }
            catch (Exception ex)
            {
                ToastServiceStatic.Instance.Error($"Error: {ex.Message}");
            }
            finally
            {
                BtnSetup.IsEnabled = true;
            }
        }

        private void InstallBundled_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pluginsDir) || !Directory.Exists(_pluginsDir))
            {
                ToastServiceStatic.Instance.Warning("Run Initial Setup first to create the modded TABG copy.");
                return;
            }

            int count = 0;
            if (LstBundled.ItemsSource is List<BundledEntry> entries)
            {
                foreach (var entry in entries.Where(x => x.IsSelected))
                {
                    var srcPath = FindDllPath(entry.Name);
                    if (srcPath == null) continue;
                    var dst = Path.Combine(_pluginsDir, entry.Name);
                    try
                    {
                        if (File.Exists(srcPath))
                        {
                            File.Copy(srcPath, dst, true);
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        ToastServiceStatic.Instance.Error($"Failed to install {entry.Name}: {ex.Message}");
                    }
                }
            }

            RefreshAll();
        }

        private void AddDll_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pluginsDir) || !Directory.Exists(_pluginsDir))
            {
                ToastServiceStatic.Instance.Warning("Run Initial Setup first.");
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "DLL files (*.dll)|*.dll", Title = "Select client mod DLL" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(dialog.FileName, Path.Combine(_pluginsDir, Path.GetFileName(dialog.FileName)), true);
                    RefreshAll();
                }
                catch (Exception ex)
                {
                    ToastServiceStatic.Instance.Error($"Failed: {ex.Message}");
                }
            }
        }

        private void RemoveMod_Click(object sender, RoutedEventArgs e)
        {
            if (LstClientMods.SelectedItem is ModEntry me)
            {
                if (MessageBox.Show($"Remove {me.Name}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    var path = me.IsEnabled
                        ? Path.Combine(_pluginsDir, me.Name)
                        : Path.Combine(_pluginsDir, "disabled", me.Name);
                    try { if (File.Exists(path)) { File.Delete(path); RefreshAll(); } }
                    catch (Exception ex) { ToastServiceStatic.Instance.Error($"Failed: {ex.Message}"); }
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshAll();

        private void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            var exe = Path.Combine(_moddedDir, "TotallyAccurateBattlegrounds.exe");
            if (!File.Exists(exe)) { ToastServiceStatic.Instance.Warning("Modded TABG not found. Run Initial Setup first."); return; }
            try { Process.Start(new ProcessStartInfo { FileName = exe, WorkingDirectory = _moddedDir, UseShellExecute = true }); }
            catch (Exception ex) { ToastServiceStatic.Instance.Error($"Failed to launch: {ex.Message}"); }
        }

        private void OpenModdedFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(_moddedDir)) Process.Start("explorer", _moddedDir);
        }

        private string? FindClientPluginsDir()
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

        /// <summary>Search multiple directories for a specific DLL file.</summary>
        private string? FindDllPath(string dllName)
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
