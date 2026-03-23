using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace TabgInstaller.Gui;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public App() { }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        try
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, "startup.log");
            File.AppendAllText(logFile, $"Starting {DateTime.Now}\n");

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddWpfBlazorWebView();
            serviceCollection.AddSingleton<Services.AppState>();

            Services = serviceCollection.BuildServiceProvider();

            var mw = new MainWindow();
            mw.Show();

            File.AppendAllText(logFile, "MainWindow shown\n");
        }
        catch (Exception ex)
        {
            try
            {
                var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "startup.log"), "ERROR: " + ex.ToString() + "\n");
            }
            catch { }
            MessageBox.Show("Startup error: " + ex.Message, "TABG Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
}
