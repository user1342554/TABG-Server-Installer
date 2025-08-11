using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
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

            try
            {
                Installer.EnsureWordListLoadedAsync(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading word list:\n{ex.Message}",
                    "Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            // Populate auto-complete sources
            if (Installer.AllowedWords.Count > 0)
            {
                var words = Installer.AllowedWords.ToList();
                TxtServerName.ItemsSource = words;
                TxtServerPassword.ItemsSource = words;
                TxtServerDescription.ItemsSource = words;
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Please paste the path to your server directory into the text box manually.", "Manual Path Entry");
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            string serverName = TxtServerName.Text.Trim();
            string serverPassword = TxtServerPassword.Text.Trim();
            string serverDesc = TxtServerDescription.Text.Trim();
            string citrusTag = TxtCitrusTag.Text.Trim();
            bool skipCitrus = ChkSkipCitruslib.IsChecked == true;
            bool installCommunityServer = ChkInstallCommunityServer.IsChecked == true;

            string serverDir = PathBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(serverDir))
            {
                MessageBox.Show(
                    "Please select a valid TABG server folder.",
                    "Folder Not Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }
            if (!Directory.Exists(serverDir))
            {
                MessageBox.Show(
                    $"The path '{serverDir}' does not exist.\nPlease select a valid TABG server folder.",
                    "Folder Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            if (serverName.Length == 0 ||
                serverDesc.Length == 0 ||
                (citrusTag.Length == 0 && !skipCitrus))
            {
                MessageBox.Show(
                    "Please fill in Server Name/Description. Release tags are only required if not skipping the plugin.",
                    "Invalid Input",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            try
            {
                Installer.ValidateOneWord("Server Name", serverName);
                if (!string.IsNullOrEmpty(serverPassword))
                    Installer.ValidateOneWord("Server Password", serverPassword);
                Installer.ValidateOneWord("Server Description", serverDesc);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error validating input: {ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SetUiEnabled(false);
            TxtLog.Clear();

            var progress = new Progress<string>(line =>
            {
                Dispatcher.Invoke(() =>
                {
                    TxtLog.AppendText(line + Environment.NewLine);
                    LogScrollViewer.ScrollToEnd();
                });
            });

            var cts = new CancellationTokenSource();

            try
            {
                var installer = new TabgInstaller.Core.Installer(
                    gameDir: serverDir,
                    log: progress
                );

                int exitCode = await installer.RunAsync(
                    serverDir: serverDir,
                    serverName: serverName,
                    serverPassword: serverPassword,
                    serverDescription: serverDesc,
                    starterPackTag: "",
                    citrusLibTag: citrusTag,
                    skipStarterPack: false,
                    skipCitruslib: skipCitrus,
                    installCommunityServer: installCommunityServer,
                    ct: cts.Token
                );

                if (!cts.IsCancellationRequested)
                {
                    if (exitCode == 0)
                    {
                        try
                        {
                            string winMedia = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", "notify.wav");
                            if (File.Exists(winMedia))
                            {
                                var player = new SoundPlayer(winMedia);
                                player.Play();
                            }
                            else
                            {
                                SystemSounds.Asterisk.Play();
                            }
                        }
                        catch { }

                        ((IProgress<string>)progress).Report("Installation completed successfully!");
                        
                        // Enable Config tab only after successful install
                        if (Window.GetWindow(this) is MainWindow mainWindow)
                        {
                            mainWindow.ConfigTab.Initialize(serverDir);
                            if (mainWindow.FindName("ConfigTabItem") is TabItem cfgItem)
                                cfgItem.IsEnabled = true;
                            if (mainWindow.FindName("MainTabs") is TabControl tabs)
                                tabs.SelectedIndex = 1; // switch to Config tab by index
                        }
                        
                        MessageBox.Show("Installation completed successfully! Switching to Config tab...", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"Installation ended with code {exitCode}. See log output.",
                            "Installation Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
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
                MessageBox.Show(
                    $"Unknown error during installation:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
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
                if (mainWindow.FindName("MainTabs") is TabControl tabs)
                    tabs.SelectedIndex = 1; // switch to Config tab
            }
        }

        private void SetUiEnabled(bool isEnabled)
        {
            BtnInstall.IsEnabled = isEnabled;
            PathBox.IsEnabled = isEnabled;
            TxtServerName.IsEnabled = isEnabled;
            TxtServerPassword.IsEnabled = isEnabled;
            TxtServerDescription.IsEnabled = isEnabled;
            TxtCitrusTag.IsEnabled = isEnabled;
            ChkSkipCitruslib.IsEnabled = isEnabled;
            ChkPublicServer.IsEnabled = isEnabled;
            ChkInstallCommunityServer.IsEnabled = isEnabled;
        }
    }
}
