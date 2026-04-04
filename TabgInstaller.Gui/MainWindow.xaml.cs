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
                var update = await updater.CheckForUpdateAsync();
                if (update != null)
                {
                    var (tag, version, url) = update.Value;
                    var current = UpdateService.GetCurrentVersion();

                    var result = MessageBox.Show(
                        $"A new version is available!\n\n" +
                        $"Current: {current.Major}.{current.Minor}.{current.Build}\n" +
                        $"New: {version.Major}.{version.Minor}.{version.Build} ({tag})\n\n" +
                        "Download and install now?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        Title = "TABG Manager — Updating...";
                        bool ok = await updater.ApplyUpdateAsync(url);
                        if (ok)
                        {
                            Application.Current.Shutdown();
                            return;
                        }
                        else
                        {
                            MessageBox.Show("Update failed. You can download manually from GitHub.",
                                "Update Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                            Title = "TABG Manager";
                        }
                    }
                }
            }
            catch { }

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
                    MessageBox.Show("Setup was not completed. The app needs a server path to function.\n\nThe app will close.",
                        "Setup Required", MessageBoxButton.OK, MessageBoxImage.Warning);
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
