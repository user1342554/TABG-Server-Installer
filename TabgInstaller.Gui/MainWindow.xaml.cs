using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.Windows;

namespace TabgInstaller.Gui
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initialize toast system
            ToastService.Instance.Initialize((msg, type, dur) =>
                Dispatcher.Invoke(() => ToastControl.Show(msg, type, dur)));

            // Run update check
            try
            {
                var updater = new UpdateService();
                var updateInfo = await updater.CheckForUpdateAsync();
                if (updateInfo != null)
                {
                    // Check if user previously skipped this version
                    var updateSettings = AppSettingsService.Load();
                    if (updateInfo.TagName == updateSettings.SkippedUpdateVersion)
                    {
                        // Skipped — don't prompt
                    }
                    else
                    {
                        var current = UpdateService.GetCurrentVersion();
                        var dialog = new ChangelogWindow(current, updateInfo.Version, updateInfo.ReleaseNotes, updateInfo.TagName);
                        dialog.Owner = this;

                        if (dialog.ShowDialog() == true)
                        {
                            Title = "TABG Manager — Updating...";
                            bool ok = await updater.ApplyUpdateAsync(updateInfo.DownloadUrl);
                            if (ok)
                            {
                                Application.Current.Shutdown();
                                return;
                            }
                            else
                            {
                                ToastService.Instance.Error("Update failed. You can download manually from GitHub.");
                                Title = "TABG Manager";
                            }
                        }
                        else if (dialog.SkippedVersion != null)
                        {
                            updateSettings.SkippedUpdateVersion = dialog.SkippedVersion;
                            AppSettingsService.Save(updateSettings);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] Failed to check for updates: {ex.Message}");
            }

            // Check if setup is needed
            var settings = AppSettingsService.Load();
            if (!settings.SetupCompleted || string.IsNullOrEmpty(settings.ServerPath) || !Directory.Exists(settings.ServerPath))
            {
                RunSetupWizard();
            }
            else
            {
                InitializeAllPanels(settings.ServerPath);
            }
        }

        private void RunSetupWizard()
        {
            this.Visibility = Visibility.Hidden;

            var wizard = new SetupWizardWindow();
            var result = wizard.ShowDialog();

            this.Visibility = Visibility.Visible;
            this.Activate();

            if (result == true && wizard.SetupCompleted)
            {
                var settings = AppSettingsService.Load();
                InitializeAllPanels(settings.ServerPath);
            }
            else
            {
                var settings = AppSettingsService.Load();
                if (!string.IsNullOrEmpty(settings.ServerPath) && Directory.Exists(settings.ServerPath))
                {
                    InitializeAllPanels(settings.ServerPath);
                }
                else
                {
                    ToastService.Instance.Error("Setup was not completed. The app needs a server path to function.");
                    Application.Current.Shutdown();
                }
            }
        }

        private void InitializeAllPanels(string serverDir)
        {
            // Initialize Console first (Dashboard depends on it)
            ConsoleTab.Initialize(serverDir);

            // Initialize Dashboard with reference to Console
            DashboardTab.Initialize(serverDir, ConsoleTab);
            DashboardTab.RequestOpenConsole += () =>
            {
                MainTabs.SelectedIndex = 4; // Console tab
            };

            // Initialize Config
            ConfigTab.Initialize(serverDir);

            // Initialize Server Mods
            ServerModsTab.Initialize(serverDir);

            // Initialize Backups
            BackupsTab.Initialize(serverDir);

            // Initialize Settings
            SettingsTab.RequestHardReset += () =>
            {
                if (ConsoleTab.IsServerRunning)
                    ConsoleTab.StopButton_Click(this, new RoutedEventArgs());

                RunSetupWizard();
            };

            // Select Dashboard
            MainTabs.SelectedIndex = 0;
        }
    }
}
