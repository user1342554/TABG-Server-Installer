using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core;

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
            MessageBox.Show("Please paste the path to your server directory into the text box manually.", "Manual Path Entry");
        }

        // ── Plugin selection helpers ──

        private CheckBox[] GetAllPluginCheckBoxes() => new[]
        {
            ChkCitruslib, ChkInstallStarterPack, ChkStarterPackFixes,
            ChkCustomSpawnpoints, ChkFreddoCommission, ChkMatchTimeout,
            ChkServerLogger, ChkVoteToStart, ChkUnusedVehicles, ChkBigSmoke, ChkMGLFlashbang, ChkSoloTesting, ChkInstallCommunityServer
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
                MessageBox.Show("Please select a valid TABG server folder.", "Folder Not Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!Directory.Exists(serverDir))
            {
                MessageBox.Show($"The path '{serverDir}' does not exist.\nPlease select a valid TABG server folder.", "Folder Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (citrusTag.Length == 0 && !skipCitrus)
            {
                MessageBox.Show("CitrusLib tag is required if not skipping the plugin.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetUiEnabled(false);
            TxtLog.Clear();

            var progress = new Progress<string>(line =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    TxtLog.AppendText(line + Environment.NewLine);
                    LogScrollViewer.ScrollToEnd();
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

                        MessageBox.Show("Installation completed successfully! Switching to Config tab.\n\nChange Server Name, Password and Description in Server Settings.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Installation ended with code {exitCode}. See log output.", "Installation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ((IProgress<string>)progress).Report("Installation cancelled by user.");
                MessageBox.Show("Installation cancelled.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                progress.LogException("Unknown error during installation", ex);
                MessageBox.Show($"Unknown error during installation:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Please select a valid TABG server folder.", "Folder Not Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
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
