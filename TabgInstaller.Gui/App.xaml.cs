using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        public App() { }

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
                catch { }
                MessageBox.Show("Startup error: " + ex.Message, "TABG Manager", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }
    }
}
