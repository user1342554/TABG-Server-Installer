using System;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

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

            // Run update check first
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
            catch
            {
                // Never block startup for update check failures
            }

        }
    }
}
