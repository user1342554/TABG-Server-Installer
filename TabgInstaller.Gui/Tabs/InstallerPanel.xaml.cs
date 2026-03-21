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

            // Auto-detect TABG client path
            var clientPath = Installer.TryFindTabgClientPath();
            if (!string.IsNullOrEmpty(clientPath))
            {
                ClientPathBox.Text = clientPath;
                // Default modded copy location: next to the Steam folder
                var parent = Path.GetDirectoryName(clientPath);
                if (parent != null)
                    ClientModdedPathBox.Text = Path.Combine(parent, "TABG_Modded");
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
                                tabs.SelectedIndex = 1;
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
                    tabs.SelectedIndex = 1;
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

        private void BrowseClient_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Paste the path to your TABG game folder.\n\nUsually found at:\nC:\\Program Files (x86)\\Steam\\steamapps\\common\\TotallyAccurateBattlegrounds", "TABG Client Path");
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
            BtnInstall.IsEnabled = isEnabled;
            PathBox.IsEnabled = isEnabled;
            TxtCitrusTag.IsEnabled = isEnabled;
            foreach (var cb in GetAllPluginCheckBoxes())
                cb.IsEnabled = isEnabled;
        }
    }
}
