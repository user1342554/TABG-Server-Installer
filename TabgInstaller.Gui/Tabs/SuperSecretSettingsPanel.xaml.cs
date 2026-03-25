using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class SuperSecretSettingsPanel : UserControl
    {
        private const string Password = "123";
        private SigmaModeApp _sigmaMode;
        
        public SuperSecretSettingsPanel()
        {
            InitializeComponent();
        }

        private async void EnterButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;
            var entered = PwdBox.Password ?? string.Empty;
            if (entered == Password)
            {
                var result = MessageBox.Show("Password correct. Enter Sigma mode?", "Super Secret", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        EnterButton.IsEnabled = false;
                        EnterButton.Content = "Starting Sigma Mode...";
                        StopButton.Visibility = Visibility.Collapsed;
                        InfoText.Text = "";
                        DebugText.Text = "";
                        
                        var logMessages = new System.Collections.Generic.List<string>();
                        
                        _sigmaMode = new SigmaModeApp(logger: msg => 
                        {
                            System.Diagnostics.Debug.WriteLine($"[Sigma] {msg}");
                            logMessages.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
                            
                            // Update UI with current status
                            Dispatcher.Invoke(() =>
                            {
                                if (msg.Contains("Music"))
                                    EnterButton.Content = "♪ Playing Music...";
                                else if (msg.Contains("TABG") && msg.Contains("Launch"))
                                    EnterButton.Content = "🎮 Launching TABG...";
                                else if (msg.Contains("fans"))
                                    EnterButton.Content = "🌪️ Setting Fans...";
                                else if (msg.Contains("overlay"))
                                    EnterButton.Content = "🖥️ Creating Overlays...";
                                else if (msg.Contains("engaged"))
                                {
                                    EnterButton.Content = "🔍 Scanning for TABG...";
                                    StopButton.Visibility = Visibility.Visible;
                                    InfoText.Text = "Music and black screen active. Waiting for TABG to reach main menu...";
                                }
                                else if (msg.Contains("process found"))
                                {
                                    EnterButton.Content = "⏳ TABG Loading...";
                                    InfoText.Text = "TABG process started! Waiting for main menu to load...";
                                }
                                else if (msg.Contains("window detected"))
                                {
                                    EnterButton.Content = "📺 TABG Window Found...";
                                    InfoText.Text = "TABG window visible! Waiting for main menu...";
                                }
                                else if (msg.Contains("main menu should be loaded"))
                                {
                                    EnterButton.Content = "🎮 Main Menu Ready!";
                                    InfoText.Text = "TABG main menu loaded! Stopping Sigma Mode...";
                                    StopButton.Visibility = Visibility.Collapsed;
                                }
                                
                                // Show debug info
                                if (msg.Contains("elapsed") || msg.Contains("Found") || msg.Contains("process") || 
                                    msg.Contains("window") || msg.Contains("waiting") || msg.Contains("visible"))
                                {
                                    DebugText.Text = msg;
                                }
                            });
                        });
                        
                        var success = await _sigmaMode.StartSigmaModeAsync();
                        
                        if (success)
                        {
                            ToastService.Instance.Success("Sigma Mode completed successfully!");
                        }
                        else
                        {
                            ToastService.Instance.Warning("Sigma Mode completed with issues. Check the log for details.");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ToastService.Instance.Error($"Sigma Mode error: {ex.Message}");
                    }
                    finally
                    {
                        _sigmaMode?.Dispose();
                        _sigmaMode = null;
                        EnterButton.IsEnabled = true;
                        EnterButton.Content = "Enter";
                        StopButton.Visibility = Visibility.Collapsed;
                        InfoText.Text = "";
                        DebugText.Text = "";
                    }
                }
            }
            else
            {
                ErrorText.Text = "Incorrect password.";
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sigmaMode != null)
            {
                EnterButton.Content = "Force Stopping...";
                StopButton.IsEnabled = false;
                InfoText.Text = "Force stopping Sigma Mode...";
                DebugText.Text = "Manual stop requested";
                
                _sigmaMode.RequestEmergencyExit();
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _sigmaMode?.RequestEmergencyExit();
            _sigmaMode?.Dispose();
        }
    }
}


