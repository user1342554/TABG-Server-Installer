using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;

namespace TabgInstaller.Gui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        //private readonly ILogger<App> _logger;
        //private readonly IHost _host;

        public App()
        {
            DispatcherUnhandledException += (s, args) =>
            {
                try
                {
                    var logDir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "logs");
                    Directory.CreateDirectory(logDir);
                    File.AppendAllText(Path.Combine(logDir, "startup.log"), "UNHANDLED: " + args.Exception.ToString() + "\n");
                }
                catch (Exception logEx) { System.Diagnostics.Trace.TraceError($"[App] Failed to write crash log: {logEx}"); }
                MessageBox.Show("Error: " + args.Exception.ToString(), "TABG Manager Error");
                args.Handled = true;
            };
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                var logDir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, "startup.log");
                File.AppendAllText(logFile, $"Starting {System.DateTime.Now}\n");

                var mw = new MainWindow();
                mw.Show();

                File.AppendAllText(logFile, "MainWindow shown\n");
            }
            catch (System.Exception ex)
            {
                try
                {
                    var logDir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "logs");
                    Directory.CreateDirectory(logDir);
                    File.AppendAllText(Path.Combine(logDir, "startup.log"), "ERROR: " + ex.ToString() + "\n");
                }
                catch (Exception logEx) { System.Diagnostics.Trace.TraceError($"[App] Failed to write startup log: {logEx}"); }
                MessageBox.Show("Startup error: " + ex.Message, "TABG Manager", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }
    }
}
