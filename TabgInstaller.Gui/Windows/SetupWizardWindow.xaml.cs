using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Windows
{
    public partial class SetupWizardWindow : Window
    {
        public string ServerPath => TxtServerPath.Text.Trim();
        public string ClientPath => TxtClientPath.Text.Trim();
        public string ClientModdedPath { get; private set; } = "";
        public bool SetupCompleted { get; private set; }

        private readonly List<CheckBox> _pluginCheckboxes = new();

        private static readonly (string Label, string DllName, bool DefaultChecked)[] PluginDefs = new[]
        {
            ("Citruslib — Core server library (admin, permissions, commands)", "Citruslib", true),
            ("StarterPack — Match mechanics, loadouts, win conditions", "StarterPack", true),
            ("StarterPackFixes — Loot drop control", "StarterPackFixes.dll", true),
            ("CustomSpawnpoints — Custom spawn points", "CustomSpawnpoints.dll", true),
            ("FreddoTABGCommission — Curses, grenades on kill, bans", "FreddoTABGCommission.dll", true),
            ("MatchAndPreMatchTimeout — Match timing", "MatchAndPreMatchTimeout.dll", true),
            ("ServerLogger — Player logging", "ServerLogger.dll", true),
            ("VoteToStart — Vote-to-start", "VoteToStart.dll", true),
            ("UnusedVehicles — Spawn cut vehicles (Heli, UFO, Mustang, VW, HoverBike)", "TabgInstaller.UnusedVehicles.dll", true),
            ("BigSmokeGrenade — Giant purple smoke grenades", "TabgInstaller.CustomGrenades.dll", true),
            ("SoloTesting — Prevents 'You Win' when testing alone", "TabgInstaller.SoloTesting.dll", false),
            ("TABGCommunityServer — Community server", "TABGCommunityServer", false),
            ("ProximityChat — Proximity voice chat [Client mod required]", "TabgInstaller.ProximityChat.Server.dll", true),
            ("Hunt Mode (4v1) — 1 Killer vs 4 Survivors [Client mod required]", "TabgInstaller.HuntMode.dll", false),
            ("Juggernaut Mode — One massive player vs everyone", "JuggernautMode.Server.dll", false),
            ("TABGVR Server — VR hand sync [Client mod required]", "TABGVR.Server.CitrusLib.dll", false),
            ("FakePlayers — Spawn dummy players for solo testing", "TabgInstaller.FakePlayers.dll", false),
        };

        public SetupWizardWindow()
        {
            InitializeComponent();
            BuildPluginCheckboxes();

            var serverPath = Installer.TryFindTabgServerPath();
            if (!string.IsNullOrEmpty(serverPath))
                TxtServerPath.Text = serverPath;

            var clientPath = Installer.TryFindTabgClientPath();
            if (!string.IsNullOrEmpty(clientPath))
            {
                TxtClientPath.Text = clientPath;
                UpdateModdedPathPreview();
            }
        }

        private void BuildPluginCheckboxes()
        {
            foreach (var (label, _, defaultChecked) in PluginDefs)
            {
                var cb = new CheckBox
                {
                    Content = label,
                    IsChecked = defaultChecked,
                    Margin = new Thickness(5, 3, 5, 3)
                };
                _pluginCheckboxes.Add(cb);
                PluginCheckboxes.Children.Add(cb);
            }
        }

        private void BrowseServer_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select TABG Server Folder" };
            var detected = Installer.TryFindTabgServerPath();
            if (!string.IsNullOrEmpty(detected) && Directory.Exists(detected))
                dialog.InitialDirectory = Path.GetDirectoryName(detected) ?? detected;

            if (dialog.ShowDialog() == true)
                TxtServerPath.Text = dialog.FolderName;
        }

        private void BrowseClient_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select TABG Steam Folder" };
            if (dialog.ShowDialog() == true)
            {
                TxtClientPath.Text = dialog.FolderName;
                UpdateModdedPathPreview();
            }
        }

        private void UpdateModdedPathPreview()
        {
            var client = TxtClientPath.Text.Trim();
            if (!string.IsNullOrEmpty(client))
            {
                var parent = Path.GetDirectoryName(client);
                if (parent != null)
                {
                    ClientModdedPath = Path.Combine(parent, "TABG_Modded");
                    TxtModdedPathPreview.Text = $"Modded copy will be at: {ClientModdedPath}";
                }
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (WizardSteps.SelectedIndex > 0)
            {
                WizardSteps.SelectedIndex--;
                UpdateNavButtons();
            }
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            WizardSteps.SelectedIndex++;
            UpdateNavButtons();
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            int step = WizardSteps.SelectedIndex;

            if (step == 0)
            {
                var path = TxtServerPath.Text.Trim();
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    MessageBox.Show("Please select a valid TABG server folder.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                WizardSteps.SelectedIndex = 1;
            }
            else if (step == 1)
            {
                UpdateModdedPathPreview();
                WizardSteps.SelectedIndex = 2;
            }
            else if (step == 2)
            {
                WizardSteps.SelectedIndex = 3;
                UpdateNavButtons();
                await RunInstallation();
                return;
            }

            UpdateNavButtons();
        }

        private void UpdateNavButtons()
        {
            int step = WizardSteps.SelectedIndex;
            BtnBack.Visibility = step > 0 && step < 3 ? Visibility.Visible : Visibility.Collapsed;
            BtnSkip.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            BtnNext.Content = step == 2 ? "Install" : "Next";
            BtnNext.Visibility = step == 3 ? Visibility.Collapsed : Visibility.Visible;
        }

        private async Task RunInstallation()
        {
            string serverDir = TxtServerPath.Text.Trim();

            BtnBack.IsEnabled = false;
            BtnNext.IsEnabled = false;

            var progress = new Progress<string>(line =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    TxtLog.AppendText(line + Environment.NewLine);
                    LogScrollViewer.ScrollToEnd();

                    var pct = EstimateProgress(line);
                    if (pct >= 0)
                    {
                        InstallProgress.Value = pct;
                        TxtInstallStage.Text = GetStageName(pct);
                    }
                });
            });

            var cts = new CancellationTokenSource();

            try
            {
                var bundled = new List<string>();
                for (int i = 0; i < _pluginCheckboxes.Count; i++)
                {
                    if (_pluginCheckboxes[i].IsChecked == true)
                    {
                        var dllName = PluginDefs[i].DllName;
                        if (dllName == "Citruslib" || dllName == "StarterPack" || dllName == "TABGCommunityServer")
                            continue;
                        if (dllName == "TabgInstaller.HuntMode.dll")
                            bundled.Add("TabgInstaller.HuntMode.Shared.dll");
                        bundled.Add(dllName);
                    }
                }

                bool skipCitrus = !(_pluginCheckboxes[0].IsChecked == true);
                bool skipStarterPack = !(_pluginCheckboxes[1].IsChecked == true);
                bool installCommunityServer = _pluginCheckboxes[11].IsChecked == true;

                int exitCode = await Task.Run(async () =>
                {
                    var backupService = new TabgInstaller.Core.Services.BackupService(progress);
                    if (Directory.Exists(serverDir) && Directory.GetFileSystemEntries(serverDir).Length > 0)
                    {
                        ((IProgress<string>)progress).Report("Creating backup...");
                        await backupService.CreateBackupAsync(serverDir);
                    }

                    var installer = new TabgInstaller.Core.Installer(gameDir: serverDir, log: progress);
                    return await installer.RunAsync(
                        serverDir: serverDir,
                        serverName: "enormous",
                        serverPassword: "enormous",
                        serverDescription: "enormous",
                        starterPackTag: "",
                        citrusLibTag: "v0.7",
                        skipStarterPack: skipStarterPack,
                        skipCitruslib: skipCitrus,
                        installCommunityServer: installCommunityServer,
                        bundledPlugins: bundled,
                        ct: cts.Token);
                });

                if (exitCode == 0)
                {
                    InstallProgress.Value = 100;
                    TxtInstallStage.Text = "Complete!";

                    AppSettingsService.MarkSetupComplete(serverDir, ClientPath, ClientModdedPath);
                    SetupCompleted = true;

                    MessageBox.Show(
                        "Setup complete! You can now configure your server.\n\nChange the Server Name, Password and Description in the Config tab.",
                        "Setup Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    TxtInstallStage.Text = "Failed";
                    MessageBox.Show("Installation failed. Check the log for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    BtnBack.IsEnabled = true;
                    BtnBack.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                TxtInstallStage.Text = "Failed";
                MessageBox.Show($"Installation error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnBack.IsEnabled = true;
                BtnBack.Visibility = Visibility.Visible;
            }
            finally
            {
                cts.Dispose();
            }
        }

        private int EstimateProgress(string msg)
        {
            if (msg.Contains("Killing running server")) return 2;
            if (msg.Contains("game_settings.txt")) return 10;
            if (msg.Contains("BepInEx")) return 30;
            if (msg.Contains("doorstop")) return 35;
            if (msg.Contains("Citrus")) return 50;
            if (msg.Contains("bundled plugin")) return 60;
            if (msg.Contains("StarterPack")) return 75;
            if (msg.Contains("TheStarterPack.txt")) return 85;
            if (msg.Contains("complete")) return 100;
            return -1;
        }

        private string GetStageName(int pct) => pct switch
        {
            <= 5 => "Preparing...",
            <= 30 => "Installing BepInEx...",
            <= 55 => "Installing plugins...",
            <= 75 => "Configuring StarterPack...",
            <= 95 => "Finalizing...",
            _ => "Complete!"
        };
    }
}
