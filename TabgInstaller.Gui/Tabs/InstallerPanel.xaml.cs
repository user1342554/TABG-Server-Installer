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
        private CancellationTokenSource? _cts;

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
                    ToastServiceStatic.Instance.Warning("Selected folder may not be a TABG server directory (no TABG.exe found).");
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

        // Progress estimation is now in TabgInstaller.Core.ProgressEstimator

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
            // Map checkbox names to plugin IDs for lookup
            var cbMap = new (CheckBox cb, string id)[]
            {
                (ChkCitruslib, "Citruslib"), (ChkInstallStarterPack, "StarterPack"),
                (ChkStarterPackFixes, "StarterPackFixes"), (ChkCustomSpawnpoints, "CustomSpawnpoints"),
                (ChkFreddoCommission, "FreddoCommission"), (ChkMatchTimeout, "MatchTimeout"),
                (ChkServerLogger, "ServerLogger"), (ChkVoteToStart, "VoteToStart"),
                (ChkUnusedVehicles, "UnusedVehicles"), (ChkBigSmoke, "BigSmoke"),
                (ChkMGLFlashbang, "MGLFlashbang"), (ChkSoloTesting, "SoloTesting"),
                (ChkInstallCommunityServer, "CommunityServer"), (ChkProximityChat, "ProximityChat"),
                (ChkHuntMode, "HuntMode"), (ChkJuggernautMode, "JuggernautMode"),
                (ChkTabgVR, "TabgVR"), (ChkFakePlayers, "FakePlayers"),
            };
            foreach (var (cb, id) in cbMap)
                cb.IsChecked = PluginRegistry.SigmaPresetIds.Contains(id);
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            // User sets these in Config tab after install
            const string serverName = "";
            const string serverPassword = "";
            const string serverDesc = "";

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
                ToastServiceStatic.Instance.Warning("Please select a valid TABG server folder.");
                return;
            }
            if (!Directory.Exists(serverDir))
            {
                ToastServiceStatic.Instance.Error($"The path '{serverDir}' does not exist.");
                return;
            }

            if (citrusTag.Length == 0 && !skipCitrus)
            {
                ToastServiceStatic.Instance.Warning("CitrusLib tag is required if not skipping the plugin.");
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

                    var pct = ProgressEstimator.Estimate(line);
                    if (pct >= 0)
                    {
                        InstallProgress.Value = pct;
                        TxtStage.Text = ProgressEstimator.GetStageName(pct);
                    }
                });
            });

            _cts = new CancellationTokenSource();
            BtnCancel.Visibility = Visibility.Visible;
            BtnCancel.IsEnabled = true;
            BtnCancel.Content = "CANCEL";

            try
            {
                bool skipStarterPack = !(ChkInstallStarterPack.IsChecked ?? true);

                // Map each checkbox to its PluginRegistry ID
                var checkboxPluginMap = new (CheckBox cb, string pluginId)[]
                {
                    (ChkStarterPackFixes, "StarterPackFixes"),
                    (ChkCustomSpawnpoints, "CustomSpawnpoints"),
                    (ChkFreddoCommission, "FreddoCommission"),
                    (ChkMatchTimeout, "MatchTimeout"),
                    (ChkServerLogger, "ServerLogger"),
                    (ChkVoteToStart, "VoteToStart"),
                    (ChkUnusedVehicles, "UnusedVehicles"),
                    (ChkBigSmoke, "BigSmoke"),
                    (ChkMGLFlashbang, "MGLFlashbang"),
                    (ChkSoloTesting, "SoloTesting"),
                    (ChkProximityChat, "ProximityChat"),
                    (ChkHuntMode, "HuntMode"),
                    (ChkJuggernautMode, "JuggernautMode"),
                    (ChkTabgVR, "TabgVR"),
                    (ChkFakePlayers, "FakePlayers"),
                };

                var selectedBundledPlugins = new List<string>();
                foreach (var (cb, pluginId) in checkboxPluginMap)
                {
                    if (cb.IsChecked == true)
                    {
                        var def = PluginRegistry.FindById(pluginId);
                        if (def != null)
                            foreach (var dll in def.DllNames)
                                if (!selectedBundledPlugins.Contains(dll))
                                    selectedBundledPlugins.Add(dll);
                    }
                }

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
                        ct: _cts.Token
                    );
                });

                if (!_cts.IsCancellationRequested)
                {
                    if (exitCode == 0)
                    {
                        ((IProgress<string>)progress).Report("Installation completed successfully!");

                        if (Window.GetWindow(this) is MainWindow mainWindow)
                        {
                            mainWindow.ReloadFromPath(serverDir);
                            if (mainWindow.FindName("ConfigTabItem") is TabItem cfgItem)
                                cfgItem.IsEnabled = true;
                            if (mainWindow.FindName("BackupsTab") is TabItem backupsItem)
                                backupsItem.IsEnabled = true;
                            // BackupsPanel is MVVM — it loads via PathChanged subscription on its ViewModel
                            if (mainWindow.FindName("SuperSecretTab") is TabItem secretTab)
                                secretTab.IsEnabled = true;
                            if (mainWindow.FindName("MainTabs") is TabControl tabs)
                                tabs.SelectedIndex = 2;
                        }

                        InstallProgress.Value = 100;
                        TxtStage.Text = "Complete";
                        ToastServiceStatic.Instance.Success("Installation completed! Change Server Name, Password and Description in Server Settings.");
                    }
                    else
                    {
                        TxtStage.Text = "Failed";
                        ToastServiceStatic.Instance.Error($"Installation ended with code {exitCode}. See log output.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ((IProgress<string>)progress).Report("Installation cancelled by user.");
                TxtStage.Text = "Cancelled";
                ToastServiceStatic.Instance.Warning("Installation cancelled.");
            }
            catch (Exception ex)
            {
                progress.LogException("Unknown error during installation", ex);
                TxtStage.Text = "Failed";
                ToastServiceStatic.Instance.Error($"Installation error: {ex.Message}");
            }
            finally
            {
                SetUiEnabled(true);
                BtnCancel.Visibility = Visibility.Collapsed;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            BtnCancel.IsEnabled = false;
            BtnCancel.Content = "Cancelling...";
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            string serverDir = PathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(serverDir) || !Directory.Exists(serverDir))
            {
                ToastServiceStatic.Instance.Warning("Please select a valid TABG server folder.");
                return;
            }

            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.ReloadFromPath(serverDir);
                if (mainWindow.FindName("ConfigTabItem") is TabItem cfgItem)
                    cfgItem.IsEnabled = true;
                if (mainWindow.FindName("BackupsTab") is TabItem backupsItem)
                    backupsItem.IsEnabled = true;
                // BackupsPanel is MVVM — it loads via PathChanged subscription on its ViewModel
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
