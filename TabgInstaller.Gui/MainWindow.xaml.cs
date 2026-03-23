using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Components.Layout;

namespace TabgInstaller.Gui;

public partial class MainWindow : Window
{
    // Stub so old WPF panels (InstallerPanel, PresetsGrid) still compile.
    // These will be removed when the old WPF tabs are deleted (Task 19).
    public ConfigTabStub ConfigTab { get; } = new();
    public class ConfigTabStub { public void Initialize(string serverDir) { } }

    public MainWindow()
    {
        InitializeComponent();
        RootComponent.ComponentType = typeof(MainLayout);
        WebView.Services = App.Services;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var updater = new UpdateService();
            var update = await updater.CheckForUpdateAsync();
            if (update == null) return;

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

            if (result != MessageBoxResult.Yes) return;

            Title = "TABG Manager — Updating...";
            bool ok = await updater.ApplyUpdateAsync(url);
            if (ok)
            {
                Application.Current.Shutdown();
            }
            else
            {
                MessageBox.Show("Update failed. You can download manually from GitHub.",
                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Title = "TABG Manager";
            }
        }
        catch
        {
            // Never block startup for update check failures
        }
    }
}
