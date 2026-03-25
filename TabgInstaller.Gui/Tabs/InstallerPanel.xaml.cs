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

namespace TabgInstaller.Gui.Tabs
{
    public partial class InstallerPanel : UserControl
    {
        public InstallerPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var detectedPath = Installer.TryFindTabgServerPath();
            if (!string.IsNullOrEmpty(detectedPath))
            {
                PathBox.Text = detectedPath;
            }

        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select TABG Server Folder"
            };

            var detectedPath = Installer.TryFindTabgServerPath();
            if (!string.IsNullOrEmpty(detectedPath) && Directory.Exists(detectedPath))
                dialog.InitialDirectory = Path.GetDirectoryName(detectedPath) ?? detectedPath;

            if (dialog.ShowDialog() == true)
            {
                var selectedPath = dialog.FolderName;
                if (!File.Exists(Path.Combine(selectedPath, "TABG.exe")))
                    ToastService.Instance.Warning("Selected folder may not be a TABG server directory (no TABG.exe found).");
                PathBox.Text = selectedPath;
            }
        }

        private void DependencyPlugin_Changed(object sender, RoutedEventArgs e)
        {
            if (TxtDependencyHint == null) return; // Not yet initialized during XAML load
            bool anyDep = ChkProximityChat.IsChecked == true ||
                          ChkHuntMode.IsChecked == true ||
                          ChkTabgVR.IsChecked == true;
            TxtDependencyHint.Visibility = anyDep ? Visibility.Visible : Visibility.Collapsed;
        }

        private int EstimateProgress(string msg)
        {
            if (msg.Contains("Killing running server")) return 2;
            if (msg.Contains("VanillaFiles.txt")) return 5;
            if (msg.Contains("game_settings.txt")) return 10;
            if (msg.Contains("Downloading BepInEx") || msg.Contains("Installing BepInEx")) return 15;
            if (msg.Contains("Extracting BepInEx") || msg.Contains("BepInEx installed") || msg.Contains("BepInEx")) return 30;
            if (msg.Contains("doorstop")) return 35;
            if (msg.Contains("Downloading Citruslib") || msg.Contains("Installing Citruslib") || msg.Contains("Citrus")) return 40;
            if (msg.Contains("Citruslib.dll")) return 50;
            if (msg.Contains("Copying bundled plugin") || msg.Contains("bundled plugin")) return 55;
            if (msg.Contains("bundled plugins copied")) return 60;
            if (msg.Contains("StarterPack")) return 70;
            if (msg.Contains("StarterPackSetup")) return 80;
            if (msg.Contains("TheStarterPack.txt")) return 85;
            if (msg.Contains("ExtraSettings.json")) return 90;
            if (msg.Contains("PlayerPerms.json")) return 95;
            if (msg.Contains("Installation complete") || msg.Contains("complete")) return 100;
            return -1;
        }

        private string GetStageName(int percent) => percent switch
        {
            <= 5 => "Preparing...",
            <= 30 => "Installing BepInEx...",
            <= 55 => "Installing plugins...",
            <= 60 => "Copying plugins...",
            <= 85 => "Configuring StarterPack...",
            <= 95 => "Finalizing...",
            _ => "Complete"
        };

        // ── Plugin selection helpers ──

        private CheckBox[] GetAllPluginCheckBoxes() => new[]
        {
            ChkCitruslib, ChkInstallStarterPack, ChkStarterPackFixes,
            ChkCustomSpawnpoints, ChkFreddoCommission, ChkMatchTimeout,
            ChkServerLogger, ChkVoteToStart, ChkUnusedVehicles, ChkBigSmoke, ChkMGLFlashbang, ChkSoloTesting, ChkInstallCommunityServer, ChkProximityChat, ChkHuntMode, ChkJuggernautMode, ChkTabgVR, ChkFakePlayers
        };

        private void SelectAllPlugins_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in GetAllPluginCheckBoxes())
                cb.IsChecked = true;
        }

        private void SelectNonePlugins_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in GetAllPluginCheckBoxes())
                cb.IsChecked = false;
        }

        private void SelectSigmaPlugins_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in GetAllPluginCheckBoxes())
                cb.IsChecked = false;

            ChkCitruslib.IsChecked = true;
            ChkInstallStarterPack.IsChecked = true;
            ChkStarterPackFixes.IsChecked = true;
            ChkCustomSpawnpoints.IsChecked = true;
            ChkFreddoCommission.IsChecked = true;
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            // Use "enormous" as default for name/password/description.
            // User can change these later in the Config tab.
            const string serverName = "enormous";
            const string serverPassword = "enormous";
            const string serverDesc = "enormous";

            string citrusTag = TxtCitrusTag.Text.Trim();
            bool skipCitrus = !(ChkCitruslib.IsChecked == true);
            bool installCommunityServer = ChkInstallCommunityServer.IsChecked == true;

            string serverDir = PathBox.Text.Trim();

            var result = MessageBox.Show(
                "WARNING: Installation will modify server files!\n\nA backup will be created in the 'backup' folder before installation.\nDo you want to continue?",
                "Installation Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result != MessageBoxResult.Yes)
                return;

            if (string.IsNullOrWhiteSpace(serverDir))
            {
                ToastService.Instance.Warning("Please select a valid TABG server folder.");
                return;
            }
            if (!Directory.Exists(serverDir))
            {
                ToastService.Instance.Error($"The path '{serverDir}' does not exist.");
                return;
            }

            if (citrusTag.Length == 0 && !skipCitrus)
            {
                ToastService.Instance.Warning("CitrusLib tag is required if not skipping the plugin.");
                return;
            }

            SetUiEnabled(false);
            TxtLog.Clear();
            ProgressPanel.Visibility = Visibility.Visible;
            InstallProgress.Value = 0;
            TxtStage.Text = "Preparing...";

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
                        TxtStage.Text = GetStageName(pct);
                    }
                });
            });

            var cts = new CancellationTokenSource();

            try
            {
                bool skipStarterPack = !(ChkInstallStarterPack.IsChecked ?? true);

                var selectedBundledPlugins = new List<string>();
                if (ChkStarterPackFixes.IsChecked == true) selectedBundledPlugins.Add("StarterPackFixes.dll");
                if (ChkCustomSpawnpoints.IsChecked == true) selectedBundledPlugins.Add("CustomSpawnpoints.dll");
                if (ChkFreddoCommission.IsChecked == true) selectedBundledPlugins.Add("FreddoTABGCommission.dll");
                if (ChkMatchTimeout.IsChecked == true) selectedBundledPlugins.Add("MatchAndPreMatchTimeout.dll");
                if (ChkServerLogger.IsChecked == true) selectedBundledPlugins.Add("ServerLogger.dll");
                if (ChkVoteToStart.IsChecked == true) selectedBundledPlugins.Add("VoteToStart.dll");
                if (ChkUnusedVehicles.IsChecked == true) selectedBundledPlugins.Add("TabgInstaller.UnusedVehicles.dll");
                if (ChkBigSmoke.IsChecked == true || ChkMGLFlashbang.IsChecked == true) selectedBundledPlugins.Add("TabgInstaller.CustomGrenades.dll");
                if (ChkSoloTesting.IsChecked == true) selectedBundledPlugins.Add("TabgInstaller.SoloTesting.dll");
                if (ChkProximityChat.IsChecked == true) selectedBundledPlugins.Add("TabgInstaller.ProximityChat.Server.dll");
                if (ChkHuntMode.IsChecked == true)
                {
                    selectedBundledPlugins.Add("TabgInstaller.HuntMode.dll");
                    selectedBundledPlugins.Add("TabgInstaller.HuntMode.Shared.dll");
                }
                if (ChkJuggernautMode.IsChecked == true) selectedBundledPlugins.Add("JuggernautMode.Server.dll");
                if (ChkTabgVR.IsChecked == true) selectedBundledPlugins.Add("TABGVR.Server.CitrusLib.dll");
                if (ChkFakePlayers.IsChecked == true) selectedBundledPlugins.Add("TabgInstaller.FakePlayers.dll");

                int exitCode = await Task.Run(async () =>
                {
                    var backupService = new TabgInstaller.Core.Services.BackupService(progress);
                    if (Directory.Exists(serverDir) && Directory.GetFileSystemEntries(serverDir).Length > 0)
                    {
                        ((IProgress<string>)progress).Report("Creating backup...");
                        bool backupSuccess = await backupService.CreateBackupAsync(serverDir);
                        if (!backupSuccess)
                            ((IProgress<string>)progress).Report("⚠️ Backup failed — continuing anyway.");
                    }

                    var installer = new TabgInstaller.Core.Installer(gameDir: serverDir, log: progress);

                    return await installer.RunAsync(
                        serverDir: serverDir,
                        serverName: serverName,
                        serverPassword: serverPassword,
                        serverDescription: serverDesc,
                        starterPackTag: "",
                        citrusLibTag: citrusTag,
                        skipStarterPack: skipStarterPack,
                        skipCitruslib: skipCitrus,
                        installCommunityServer: installCommunityServer,
                        bundledPlugins: selectedBundledPlugins,
                        ct: cts.Token
                    );
                });

                if (!cts.IsCancellationRequested)
                {
                    if (exitCode == 0)
                    {
                        ((IProgress<string>)progress).Report("Installation completed successfully!");

                        if (Window.GetWindow(this) is MainWindow mainWindow)
                        {
                            mainWindow.ConfigTab.Initialize(serverDir);
                            if (mainWindow.FindName("ConfigTabItem") is TabItem cfgItem)
                                cfgItem.IsEnabled = true;
                            if (mainWindow.FindName("BackupsTab") is TabItem backupsItem)
                            {
                                backupsItem.IsEnabled = true;
                                if (backupsItem.Content is BackupsPanel backupsPanel)
                                    backupsPanel.Initialize(serverDir);
                            }
                            if (mainWindow.FindName("SuperSecretTab") is TabItem secretTab)
                                secretTab.IsEnabled = true;
                            if (mainWindow.FindName("MainTabs") is TabControl tabs)
                                tabs.SelectedIndex = 2;
                        }

                        InstallProgress.Value = 100;
                        TxtStage.Text = "Complete";
                        ToastService.Instance.Success("Installation completed! Change Server Name, Password and Description in Server Settings.");
                    }
                    else
                    {
                        TxtStage.Text = "Failed";
                        ToastService.Instance.Error($"Installation ended with code {exitCode}. See log output.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ((IProgress<string>)progress).Report("Installation cancelled by user.");
                TxtStage.Text = "Cancelled";
                ToastService.Instance.Warning("Installation cancelled.");
            }
            catch (Exception ex)
            {
                progress.LogException("Unknown error during installation", ex);
                TxtStage.Text = "Failed";
                ToastService.Instance.Error($"Installation error: {ex.Message}");
            }
            finally
            {
                SetUiEnabled(true);
                cts.Dispose();
            }
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            string serverDir = PathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(serverDir) || !Directory.Exists(serverDir))
            {
                ToastService.Instance.Warning("Please select a valid TABG server folder.");
                return;
            }

            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.ConfigTab.Initialize(serverDir);
                if (mainWindow.FindName("ConfigTabItem") is TabItem cfgItem)
                    cfgItem.IsEnabled = true;
                if (mainWindow.FindName("BackupsTab") is TabItem backupsItem)
                {
                    backupsItem.IsEnabled = true;
                    if (backupsItem.Content is BackupsPanel backupsPanel)
                        backupsPanel.Initialize(serverDir);
                }
                if (mainWindow.FindName("SuperSecretTab") is TabItem secretTab)
                    secretTab.IsEnabled = true;
                if (mainWindow.FindName("MainTabs") is TabControl tabs)
                    tabs.SelectedIndex = 2;
            }
        }

        private void SetUiEnabled(bool isEnabled)
        {
            BtnInstall.IsEnabled = isEnabled;
            PathBox.IsEnabled = isEnabled;
            TxtCitrusTag.IsEnabled = isEnabled;
            foreach (var cb in GetAllPluginCheckBoxes())
                cb.IsEnabled = isEnabled;
        }
    }
}
