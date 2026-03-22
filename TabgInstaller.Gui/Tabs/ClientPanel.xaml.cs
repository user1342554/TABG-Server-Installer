using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ClientPanel : UserControl
    {
        public ClientPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var clientPath = Installer.TryFindTabgClientPath();
            if (!string.IsNullOrEmpty(clientPath))
            {
                ClientPathBox.Text = clientPath;
                var parent = Path.GetDirectoryName(clientPath);
                if (parent != null)
                    ClientModdedPathBox.Text = Path.Combine(parent, "TABG_Modded");
            }
        }

        private void BtnLaunchModdedTabg_Click(object sender, RoutedEventArgs e)
        {
            string moddedDir = ClientModdedPathBox.Text.Trim();
            string exe = Path.Combine(moddedDir, "TotallyAccurateBattlegrounds.exe");
            if (!File.Exists(exe))
            {
                MessageBox.Show("Modded TABG not found. Install client mods first.", "Not Installed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = moddedDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnInstallClientMods_Click(object sender, RoutedEventArgs e)
        {
            string clientDir = ClientPathBox.Text.Trim();
            string moddedDir = ClientModdedPathBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(clientDir) || !Directory.Exists(clientDir))
            {
                MessageBox.Show("Please enter a valid TABG Steam folder path.",
                    "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(moddedDir))
            {
                MessageBox.Show("Please enter a folder path for the modded TABG copy.",
                    "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedClientPlugins = new List<string>();
            if (ChkClientFlyingControls.IsChecked == true) selectedClientPlugins.Add("TabgInstaller.FlyingControls.dll");
            if (ChkClientEnhancedTabg.IsChecked == true) selectedClientPlugins.Add("Enhanced TABG.dll");
            if (ChkClientCoordsDisplay.IsChecked == true) selectedClientPlugins.Add("TabgInstaller.CoordsDisplay.dll");
            if (ChkClientBigSmoke.IsChecked == true || ChkClientMGLFlashbang.IsChecked == true) selectedClientPlugins.Add("TabgInstaller.CustomGrenades.dll");
            if (ChkClientModSettings.IsChecked == true) selectedClientPlugins.Add("TabgInstaller.ModSettings.dll");
            if (ChkClientPopupBlocker.IsChecked == true) selectedClientPlugins.Add("Pop-up Blocker.dll");

            if (selectedClientPlugins.Count == 0)
            {
                MessageBox.Show("Please select at least one client mod.", "No Mods Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool alreadyExists = Directory.Exists(moddedDir) && File.Exists(Path.Combine(moddedDir, "TotallyAccurateBattlegrounds.exe"));

            var result = MessageBox.Show(
                alreadyExists
                    ? $"Modded copy already exists at:\n{moddedDir}\n\nThis will update the mods. Continue?"
                    : $"This will copy TABG to:\n{moddedDir}\n\nThen install BepInEx + {selectedClientPlugins.Count} mod(s).\n\nTo play: open Steam, then run the exe from the modded folder.\n\nContinue?",
                "Install Client Mods", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            SetUiEnabled(false);

            var progress = new Progress<string>(line =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    TxtLog.AppendText(line + Environment.NewLine);
                    LogScrollViewer.ScrollToEnd();
                });
            });

            try
            {
                TxtLog.AppendText("=== Installing Client Mods ===" + Environment.NewLine);
                bool success = await Task.Run(() => ClientModInstaller.InstallAsync(clientDir, moddedDir, selectedClientPlugins, progress));

                if (success)
                    MessageBox.Show($"Client mods installed!\n\nModded TABG is at:\n{moddedDir}\n\nTo play:\n1. Make sure Steam is open\n2. Run TotallyAccurateBattlegrounds.exe from the modded folder\n3. Do NOT launch through Steam",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show("Client mod installation had errors. Check the log.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                TxtLog.AppendText($"ERROR: {ex.Message}" + Environment.NewLine);
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetUiEnabled(true);
            }
        }

        private void SetUiEnabled(bool isEnabled)
        {
            BtnLaunchModdedTabg.IsEnabled = isEnabled;
            ClientPathBox.IsEnabled = isEnabled;
            ClientModdedPathBox.IsEnabled = isEnabled;
        }
    }
}
