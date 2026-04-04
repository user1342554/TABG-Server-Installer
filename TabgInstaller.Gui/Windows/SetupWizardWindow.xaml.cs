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
        private readonly List<CheckBox> _clientModCheckboxes = new();

        // Server plugin definitions — matches the old InstallerPanel exactly
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
            ("BigSmokeGrenade — Giant purple smoke grenades (/give 208)", "BigSmoke", true),
            ("MGLFlashbang — MGL shoots flashbang rounds", "MGLFlashbang", true),
            ("SoloTesting — Prevents 'You Win' when testing alone", "TabgInstaller.SoloTesting.dll", false),
            ("TABGCommunityServer — Community server", "TABGCommunityServer", false),
            ("ProximityChat — Proximity voice chat [Client mod required]", "TabgInstaller.ProximityChat.Server.dll", true),
            ("Hunt Mode (4v1) — 1 Killer vs 4 Survivors survival [Client mod required]", "TabgInstaller.HuntMode.dll", false),
            ("Juggernaut Mode — One massive player vs everyone, score-based", "JuggernautMode.Server.dll", false),
            ("TABGVR Server — VR hand sync for VR players [Client mod required]", "TABGVR.Server.CitrusLib.dll", false),
            ("FakePlayers — Spawn dummy players for solo testing (/spawndummy, /removedummy)", "TabgInstaller.FakePlayers.dll", false),
        };

        // Client mod definitions ��� matches the old ClientPanel exactly
        private static readonly (string Label, string DllName, bool DefaultChecked)[] ClientModDefs = new[]
        {
            ("FlyingControls — Steer Helicopters, UFOs, Hover vehicles (W/S/A/D + Space)", "TabgInstaller.FlyingControls.dll", true),
            ("Enhanced TABG — Infinite LOD (F1), HUD toggle (F2), fog removal (F3)", "Enhanced TABG.dll", true),
            ("BigSmokeGrenade + MGLFlashbang — Custom grenade effects", "TabgInstaller.CustomGrenades.dll", true),
            ("Coords Display — Press F5 to show your X/Y/Z position", "TabgInstaller.CoordsDisplay.dll", true),
            ("Mod Settings — In-game settings menu for all mods (press #)", "TabgInstaller.ModSettings.dll", true),
            ("Pop-up Blocker — Disable anti-cheat popups for custom servers", "Pop-up Blocker.dll", true),
            ("Proximity Chat — Proximity voice chat for custom servers", "TabgInstaller.ProximityChat.Client.dll", true),
            ("Hunt Mode Client — HUD and perk selection UI", "TabgInstaller.HuntMode.Client.dll", false),
            ("Juggernaut Mode Client — Boss bar, loadout picker, scoreboard", "JuggernautMode.Client.dll", false),
            ("TABGVR — Play TABG in Virtual Reality (requires VR headset)", "TABGVR.dll", false),
        };

        public SetupWizardWindow()
        {
            InitializeComponent();
            BuildPluginCheckboxes();
            BuildClientModCheckboxes();

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

        private void BuildClientModCheckboxes()
        {
            foreach (var (label, _, defaultChecked) in ClientModDefs)
            {
                var cb = new CheckBox
                {
                    Content = label,
                    IsChecked = defaultChecked,
                    Margin = new Thickness(5, 3, 5, 3)
                };
                _clientModCheckboxes.Add(cb);
                ClientModCheckboxes.Children.Add(cb);
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

            if (step == 0) // Server path
            {
                var path = TxtServerPath.Text.Trim();
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    ToastService.Instance.Warning("Please select a valid TABG server folder.");
                    return;
                }
                WizardSteps.SelectedIndex = 1;
            }
            else if (step == 1) // Client path
            {
                UpdateModdedPathPreview();
                WizardSteps.SelectedIndex = 2;
            }
            else if (step == 2) // Server plugins → client mods
            {
                WizardSteps.SelectedIndex = 3;
            }
            else if (step == 3) // Client mods → install
            {
                WizardSteps.SelectedIndex = 4;
                UpdateNavButtons();
                await RunInstallation();
                return;
            }

            UpdateNavButtons();
        }

        private void UpdateNavButtons()
        {
            int step = WizardSteps.SelectedIndex;
            BtnBack.Visibility = step > 0 && step < 4 ? Visibility.Visible : Visibility.Collapsed;
            BtnSkip.Visibility = step == 1 || step == 3 ? Visibility.Visible : Visibility.Collapsed;
            BtnNext.Content = step == 3 ? "Install" : "Next";
            BtnNext.Visibility = step == 4 ? Visibility.Collapsed : Visibility.Visible;
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
                    if (pct >= 0 && pct > InstallProgress.Value)
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
                bool needsCustomGrenades = false;
                for (int i = 0; i < _pluginCheckboxes.Count; i++)
                {
                    if (_pluginCheckboxes[i].IsChecked == true)
                    {
                        var dllName = PluginDefs[i].DllName;
                        // These are handled separately by the installer, not as bundled DLLs
                        if (dllName == "Citruslib" || dllName == "StarterPack" || dllName == "TABGCommunityServer")
                            continue;
                        // BigSmoke and MGLFlashbang both share TabgInstaller.CustomGrenades.dll
                        if (dllName == "BigSmoke" || dllName == "MGLFlashbang")
                        {
                            needsCustomGrenades = true;
                            continue;
                        }
                        if (dllName == "TabgInstaller.HuntMode.dll")
                            bundled.Add("TabgInstaller.HuntMode.Shared.dll");
                        bundled.Add(dllName);
                    }
                }
                if (needsCustomGrenades)
                    bundled.Add("TabgInstaller.CustomGrenades.dll");

                bool skipCitrus = !(_pluginCheckboxes[0].IsChecked == true);
                bool skipStarterPack = !(_pluginCheckboxes[1].IsChecked == true);
                // TABGCommunityServer is at index 12 (after adding MGLFlashbang)
                bool installCommunityServer = _pluginCheckboxes[12].IsChecked == true;

                // Collect client mod selections NOW on the UI thread (before any await)
                var selectedClientMods = new List<string>();
                string clientPath = ClientPath;
                string clientModdedPath = ClientModdedPath;
                for (int i = 0; i < _clientModCheckboxes.Count; i++)
                {
                    if (_clientModCheckboxes[i].IsChecked == true)
                    {
                        var dllName = ClientModDefs[i].DllName;
                        if (dllName == "TabgInstaller.HuntMode.Client.dll")
                            selectedClientMods.Add("TabgInstaller.HuntMode.Shared.dll");
                        selectedClientMods.Add(dllName);
                    }
                }

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
                    InstallProgress.Value = 95;
                    TxtInstallStage.Text = "Server done, installing client mods...";

                    // Install client mods if client path was set
                    if (!string.IsNullOrEmpty(clientPath) && Directory.Exists(clientPath)
                        && !string.IsNullOrEmpty(clientModdedPath) && selectedClientMods.Count > 0)
                    {
                        ((IProgress<string>)progress).Report("Installing client mods...");
                        await Task.Run(() =>
                            ClientModInstaller.InstallAsync(clientPath, clientModdedPath, selectedClientMods, progress));
                    }

                    InstallProgress.Value = 100;
                    TxtInstallStage.Text = "Complete!";

                    AppSettingsService.MarkSetupComplete(serverDir, clientPath, clientModdedPath);
                    SetupCompleted = true;

                    // Brief pause so user sees "Complete!", then close
                    await Task.Delay(800);
                    DialogResult = true;
                }
                else
                {
                    TxtInstallStage.Text = "Failed";
                    ToastService.Instance.Error("Installation failed. Check the log for details.");
                    BtnBack.IsEnabled = true;
                    BtnBack.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                TxtInstallStage.Text = "Failed";
                ToastService.Instance.Error($"Installation error: {ex.Message}");
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
            // Order matters: check most specific first, most generic last.
            // "Installation completed" must be checked before generic "complete".
            if (msg.Contains("Installation complete")) return 100;
            if (msg.Contains("PlayerPerms.json")) return 95;
            if (msg.Contains("ExtraSettings.json")) return 90;
            if (msg.Contains("TheStarterPack.txt")) return 85;
            if (msg.Contains("StarterPackSetup")) return 80;
            if (msg.Contains("StarterPack.dll")) return 75;
            if (msg.Contains("bundled plugins copied")) return 65;
            if (msg.Contains("Copying bundled plugin") || msg.Contains("bundled plugin")) return 60;
            if (msg.Contains("Citruslib.dll")) return 55;
            if (msg.Contains("Downloading Citruslib") || msg.Contains("Installing Citruslib")) return 45;
            if (msg.Contains("doorstop")) return 38;
            if (msg.Contains("BepInEx installed") || msg.Contains("Extracting BepInEx")) return 32;
            if (msg.Contains("Downloading BepInEx") || msg.Contains("Installing BepInEx")) return 20;
            if (msg.Contains("game_settings.txt")) return 12;
            if (msg.Contains("VanillaFiles.txt")) return 8;
            if (msg.Contains("Killing running server")) return 3;
            if (msg.Contains("Creating backup")) return 1;
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
