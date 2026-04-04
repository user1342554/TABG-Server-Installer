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

        public ClientPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var settings = AppSettingsService.Load();
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

            LoadModsList();
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

                var settings = AppSettingsService.Load();
                settings.ClientPath = _clientDir;
                settings.ClientModdedPath = _moddedDir;
                AppSettingsService.Save(settings);

                LoadModsList();
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
                    ToastService.Instance.Error($"Failed to toggle mod: {ex.Message}");
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
                ToastService.Instance.Warning("Please enter a valid TABG Steam folder path.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_moddedDir))
            {
                ToastService.Instance.Warning("No modded folder path set.");
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
                    var settings = AppSettingsService.Load();
                    settings.ClientPath = _clientDir;
                    settings.ClientModdedPath = _moddedDir;
                    AppSettingsService.Save(settings);

                    ToastService.Instance.Success("Client setup complete! Now add mods using the buttons below.");
                    LoadModsList();
                }
                else
                {
                    ToastService.Instance.Error("Client setup failed.");
                }
            }
            catch (Exception ex)
            {
                ToastService.Instance.Error($"Error: {ex.Message}");
            }
            finally
            {
                BtnSetup.IsEnabled = true;
            }
        }

        private void AddBundled_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pluginsDir) || !Directory.Exists(_pluginsDir))
            {
                ToastService.Instance.Warning("Run Initial Setup first to create the modded TABG copy.");
                return;
            }

            var bundledDir = FindClientPluginsDir();
            if (bundledDir == null)
            {
                ToastService.Instance.Warning("Bundled client plugins directory not found.");
                return;
            }

            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in Directory.GetFiles(_pluginsDir, "*.dll")) installed.Add(Path.GetFileName(f));
            var disDir = Path.Combine(_pluginsDir, "disabled");
            if (Directory.Exists(disDir))
                foreach (var f in Directory.GetFiles(disDir, "*.dll")) installed.Add(Path.GetFileName(f));

            var available = Directory.GetFiles(bundledDir, "*.dll")
                .Select(Path.GetFileName)
                .Where(n => !installed.Contains(n!))
                .ToList();

            if (available.Count == 0)
            {
                ToastService.Instance.Info("All bundled client mods are already installed.");
                return;
            }

            var win = new Window
            {
                Title = "Add Client Mods",
                Width = 400, Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            var sp = new StackPanel { Margin = new Thickness(10) };
            sp.Children.Add(new TextBlock { Text = "Select mods to install:", Margin = new Thickness(0, 0, 0, 8) });
            var lb = new ListBox { SelectionMode = SelectionMode.Extended, Height = 220, ItemsSource = available };
            sp.Children.Add(lb);
            var btn = new Button { Content = "Install Selected", Width = 120, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            btn.Click += (_, _) =>
            {
                int count = 0;
                foreach (string name in lb.SelectedItems)
                {
                    var src = Path.Combine(bundledDir, name);
                    var dst = Path.Combine(_pluginsDir, name);
                    try { File.Copy(src, dst, true); count++; } catch { }
                }
                if (count > 0) ToastService.Instance.Success($"Installed {count} client mod(s).");
                win.Close();
                LoadModsList();
            };
            sp.Children.Add(btn);
            win.Content = sp;
            win.ShowDialog();
        }

        private void AddDll_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pluginsDir) || !Directory.Exists(_pluginsDir))
            {
                ToastService.Instance.Warning("Run Initial Setup first.");
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "DLL files (*.dll)|*.dll", Title = "Select client mod DLL" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(dialog.FileName, Path.Combine(_pluginsDir, Path.GetFileName(dialog.FileName)), true);
                    LoadModsList();
                    ToastService.Instance.Success($"Added {Path.GetFileName(dialog.FileName)}");
                }
                catch (Exception ex)
                {
                    ToastService.Instance.Error($"Failed: {ex.Message}");
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
                    try { if (File.Exists(path)) { File.Delete(path); LoadModsList(); } }
                    catch (Exception ex) { ToastService.Instance.Error($"Failed: {ex.Message}"); }
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadModsList();

        private void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            var exe = Path.Combine(_moddedDir, "TotallyAccurateBattlegrounds.exe");
            if (!File.Exists(exe)) { ToastService.Instance.Warning("Modded TABG not found. Run Initial Setup first."); return; }
            try { Process.Start(new ProcessStartInfo { FileName = exe, WorkingDirectory = _moddedDir, UseShellExecute = true }); }
            catch (Exception ex) { ToastService.Instance.Error($"Failed to launch: {ex.Message}"); }
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
    }
}
