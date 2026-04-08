using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using TabgInstaller.Gui.Windows;

namespace TabgInstaller.Gui
{
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _services;
        private readonly IAppSettingsService _appSettings;
        private readonly IServerPathProvider _serverPath;

        public MainWindow(
            IServiceProvider services,
            IAppSettingsService appSettings,
            IServerPathProvider serverPath)
        {
            _services = services;
            _appSettings = appSettings;
            _serverPath = serverPath;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initialize toast system
            var toast = _services.GetRequiredService<ToastService>();
            toast.Initialize((msg, type, dur) =>
                Dispatcher.Invoke(() => ToastControl.Show(msg, type, dur)));

            // Initialize navigation
            var nav = _services.GetRequiredService<INavigationService>() as NavigationService;
            nav?.Initialize(index => MainTabs.SelectedIndex = index);

            // Wire hard reset
            var navService = _services.GetRequiredService<INavigationService>();
            navService.HardResetRequested += () =>
            {
                // Stop server if running
                var procSvc = _services.GetRequiredService<ServerProcessService>();
                if (procSvc.IsRunning) procSvc.Stop();
                RunSetupWizard();
            };

            // Run update check
            try
            {
                var updater = _services.GetRequiredService<IUpdateService>();
                var updateInfo = await updater.CheckForUpdateAsync();
                if (updateInfo != null)
                {
                    var updateSettings = _appSettings.Load();
                    if (updateInfo.TagName == updateSettings.SkippedUpdateVersion)
                    {
                        // Skipped — don't prompt
                    }
                    else
                    {
                        if (updateSettings.SkippedUpdateVersion != null)
                        {
                            updateSettings.SkippedUpdateVersion = null;
                            _appSettings.Save(updateSettings);
                        }

                        var current = UpdateService.GetCurrentVersion();
                        var dialog = new ChangelogWindow(current, updateInfo.Version,
                            updateInfo.ReleaseNotes, updateInfo.TagName);
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
                                var toastSvc = _services.GetRequiredService<IToastService>();
                                toastSvc.Error("Update failed. You can download manually from GitHub.");
                                Title = "TABG Manager";
                            }
                        }
                        else if (dialog.SkippedVersion != null)
                        {
                            updateSettings.SkippedUpdateVersion = dialog.SkippedVersion;
                            _appSettings.Save(updateSettings);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] Failed to check for updates: {ex.Message}");
            }

            // Check if setup is needed
            var settings = _appSettings.Load();
            if (!settings.SetupCompleted || string.IsNullOrEmpty(settings.ServerPath)
                || !Directory.Exists(settings.ServerPath))
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
                var settings = _appSettings.Load();
                InitializeAllPanels(settings.ServerPath);
            }
            else
            {
                var settings = _appSettings.Load();
                if (!string.IsNullOrEmpty(settings.ServerPath) && Directory.Exists(settings.ServerPath))
                {
                    InitializeAllPanels(settings.ServerPath);
                }
                else
                {
                    var toast = _services.GetRequiredService<IToastService>();
                    toast.Error("Setup was not completed. The app needs a server path to function.");
                    Application.Current.Shutdown();
                }
            }
        }

        private void InitializeAllPanels(string serverDir)
        {
            // Set the server path — triggers all ViewModel initialization via PathChanged
            (_serverPath as ServerPathProvider)?.SetPath(serverDir);

            // Initialize panels that haven't been migrated to MVVM yet
            // (these calls are removed one by one as panels are migrated)
            ConsoleTab.Initialize(serverDir);
            DashboardTab.DataContext = _services.GetRequiredService<DashboardViewModel>();
            ConfigTab.Initialize(serverDir);
            ConfigTab.AdminPanelControl.DataContext = _services.GetRequiredService<AdminPanelViewModel>();
            ServerModsTab.Initialize(serverDir);
            BackupsTab.Initialize(serverDir);
            SettingsTab.DataContext = _services.GetRequiredService<SettingsPanelViewModel>();
            SettingsTab.SuperSecretControl.DataContext = _services.GetRequiredService<SuperSecretSettingsViewModel>();

            MainTabs.SelectedIndex = 0;
        }
    }
}
