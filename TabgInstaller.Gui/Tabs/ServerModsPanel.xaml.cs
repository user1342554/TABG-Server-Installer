using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ServerModsPanel : UserControl
    {
        private string _serverDir = "";
        private string _pluginsDir = "";

        private class PluginEntry { public string Name { get; set; } = ""; public bool IsEnabled { get; set; } }

        public ServerModsPanel()
        {
            InitializeComponent();
        }

        public void Initialize(string serverDir)
        {
            _serverDir = serverDir;
            _pluginsDir = Path.Combine(serverDir, "BepInEx", "plugins");
            RefreshAll();
        }

        private void RefreshAll()
        {
            LoadPluginsList();
            LoadAvailableList();
        }

        private void LoadPluginsList()
        {
            try
            {
                if (!Directory.Exists(_pluginsDir))
                    Directory.CreateDirectory(_pluginsDir);

                var plugins = new List<PluginEntry>();
                var disabledDir = Path.Combine(_pluginsDir, "disabled");

                foreach (var dll in Directory.GetFiles(_pluginsDir, "*.dll"))
                    plugins.Add(new PluginEntry { Name = Path.GetFileName(dll), IsEnabled = true });

                if (Directory.Exists(disabledDir))
                {
                    foreach (var dll in Directory.GetFiles(disabledDir, "*.dll"))
                        plugins.Add(new PluginEntry { Name = Path.GetFileName(dll), IsEnabled = false });
                }

                LstPlugins.ItemsSource = plugins;
            }
            catch (Exception ex)
            {
                ToastService.Instance.Error($"Error loading plugins: {ex.Message}");
            }
        }

        private void LoadAvailableList()
        {
            var bundledDir = FindBundledPluginsDir();
            if (bundledDir == null)
            {
                TxtAllInstalled.Text = "Bundled plugins directory not found.";
                TxtAllInstalled.Visibility = Visibility.Visible;
                LstBundled.Visibility = Visibility.Collapsed;
                BtnInstallBundled.IsEnabled = false;
                return;
            }

            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(_pluginsDir))
                foreach (var f in Directory.GetFiles(_pluginsDir, "*.dll"))
                    installed.Add(Path.GetFileName(f));
            var disabledDir = Path.Combine(_pluginsDir, "disabled");
            if (Directory.Exists(disabledDir))
                foreach (var f in Directory.GetFiles(disabledDir, "*.dll"))
                    installed.Add(Path.GetFileName(f));

            var available = Directory.GetFiles(bundledDir, "*.dll")
                .Select(Path.GetFileName)
                .Where(n => !installed.Contains(n!))
                .ToList();

            if (available.Count == 0)
            {
                TxtAllInstalled.Text = "All bundled plugins are already installed.";
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
                    ToastService.Instance.Error($"Failed to toggle plugin: {ex.Message}");
                    pe.IsEnabled = !pe.IsEnabled;
                    cb.IsChecked = pe.IsEnabled;
                }
            }
        }

        private void InstallBundled_Click(object sender, RoutedEventArgs e)
        {
            var bundledDir = FindBundledPluginsDir();
            if (bundledDir == null) return;

            int count = 0;
            foreach (string name in LstBundled.SelectedItems)
            {
                var src = Path.Combine(bundledDir, name);
                var dst = Path.Combine(_pluginsDir, name);
                try
                {
                    if (File.Exists(src))
                    {
                        File.Copy(src, dst, true);
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    ToastService.Instance.Error($"Failed to install {name}: {ex.Message}");
                }
            }

            if (count > 0)
                ToastService.Instance.Success($"Installed {count} plugin(s).");

            RefreshAll();
        }

        private void AddDll_Click(object sender, RoutedEventArgs e)
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
                    RefreshAll();
                    ToastService.Instance.Success($"Added {Path.GetFileName(dialog.FileName)}");
                }
                catch (Exception ex)
                {
                    ToastService.Instance.Error($"Failed to add plugin: {ex.Message}");
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
                            RefreshAll();
                            ToastService.Instance.Success($"Removed {pe.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        ToastService.Instance.Error($"Failed to remove plugin: {ex.Message}");
                    }
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshAll();

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(_pluginsDir))
                Process.Start("explorer", _pluginsDir);
        }

        private string? FindBundledPluginsDir()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "plugins"),
                Path.Combine(baseDir, "..", "plugins"),
                Path.Combine(baseDir, "..", "..", "plugins"),
                Path.Combine(baseDir, "..", "..", "..", "plugins"),
            };
            return candidates.FirstOrDefault(d => Directory.Exists(d) && Directory.GetFiles(d, "*.dll").Length > 0);
        }
    }
}
