using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ConsolePanel : UserControl
    {
        private string _serverDir = "";
        private ServerProcessService? _procSvc;

        public bool IsServerRunning => _procSvc?.IsRunning == true;

        public ConsolePanel()
        {
            InitializeComponent();
        }

        public void Initialize(string serverDir)
        {
            _serverDir = serverDir;

            _procSvc = new ServerProcessService(_serverDir);
            _procSvc.OutputReceived += line => Dispatcher.Invoke(() =>
            {
                ConsoleTextBox.AppendText(line + Environment.NewLine);
                ConsoleScrollViewer.ScrollToEnd();
            });
        }

        public string GetRecentOutput(int maxLines = 20)
        {
            var text = ConsoleTextBox.Text;
            if (string.IsNullOrEmpty(text)) return "";
            var lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var start = Math.Max(0, lines.Length - maxLines);
            return string.Join(Environment.NewLine, lines[start..]);
        }

        public void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_procSvc?.IsRunning == true) return;

            try
            {
                _procSvc!.Start();
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ToastService.Instance.Error($"Failed to start: {ex.Message}");
            }
        }

        public void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_procSvc == null || !_procSvc.IsRunning) return;

            _procSvc.Stop();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private void ClearConsole_Click(object sender, RoutedEventArgs e)
        {
            ConsoleTextBox.Clear();
        }

        private void QuickSaveRestart_Click(object sender, RoutedEventArgs e)
        {
            var gsPath = Path.Combine(_serverDir, "game_settings.txt");
            if (File.Exists(gsPath))
            {
                try
                {
                    var gs = ConfigIO.ReadGameSettings(gsPath);
                    ConfigIO.WriteGameSettings(gs, gsPath);
                }
                catch { }
            }

            if (_procSvc?.IsRunning == true)
            {
                _procSvc.Stop();
                Task.Delay(1000).ContinueWith(_ => Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _procSvc.Start();
                        StartButton.IsEnabled = false;
                        StopButton.IsEnabled = true;
                    }
                    catch (Exception ex)
                    {
                        ToastService.Instance.Error($"Failed to restart: {ex.Message}");
                    }
                }));
            }
        }

        private void TxtCommand_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SendCommand_Click(sender, e);
        }

        private void SendCommand_Click(object sender, RoutedEventArgs e)
        {
            if (_procSvc?.IsRunning == true && !string.IsNullOrWhiteSpace(TxtCommand.Text))
            {
                ConsoleTextBox.AppendText($"> {TxtCommand.Text}{Environment.NewLine}");
                ConsoleTextBox.AppendText($"Command sending not implemented yet{Environment.NewLine}");
                TxtCommand.Clear();
            }
        }
    }
}
