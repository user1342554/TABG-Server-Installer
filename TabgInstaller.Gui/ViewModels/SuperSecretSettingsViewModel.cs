using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using System.Windows;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class SuperSecretSettingsViewModel : ObservableObject
    {
        private const string Password = "123";

        private SigmaModeApp? _sigmaMode;

        [ObservableProperty] private string _passwordInput = "";
        [ObservableProperty] private bool _isRunning = false;
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private bool _isUnlocked = false;
        [ObservableProperty] private string _enterButtonContent = "Enter";
        [ObservableProperty] private bool _enterButtonEnabled = true;
        [ObservableProperty] private bool _stopButtonVisible = false;
        [ObservableProperty] private string _infoText = "";
        [ObservableProperty] private string _debugText = "";
        [ObservableProperty] private string _errorText = "";

        private readonly IToastService _toast;

        public SuperSecretSettingsViewModel(IToastService toast)
        {
            _toast = toast;
        }

        [RelayCommand]
        private async Task EnterAsync()
        {
            ErrorText = string.Empty;
            var entered = PasswordInput ?? string.Empty;

            if (entered != Password)
            {
                ErrorText = "Incorrect password.";
                return;
            }

            var result = MessageBox.Show(
                "Password correct. Enter Sigma mode?",
                "Super Secret",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            IsUnlocked = true;

            try
            {
                EnterButtonEnabled = false;
                EnterButtonContent = "Starting Sigma Mode...";
                StopButtonVisible = false;
                InfoText = "";
                DebugText = "";
                IsRunning = true;

                _sigmaMode = new SigmaModeApp(logger: msg =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Sigma] {msg}");

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        if (msg.Contains("Music"))
                            EnterButtonContent = "Playing Music...";
                        else if (msg.Contains("TABG") && msg.Contains("Launch"))
                            EnterButtonContent = "Launching TABG...";
                        else if (msg.Contains("fans"))
                            EnterButtonContent = "Setting Fans...";
                        else if (msg.Contains("overlay"))
                            EnterButtonContent = "Creating Overlays...";
                        else if (msg.Contains("engaged"))
                        {
                            EnterButtonContent = "Scanning for TABG...";
                            StopButtonVisible = true;
                            InfoText = "Music and black screen active. Waiting for TABG to reach main menu...";
                        }
                        else if (msg.Contains("process found"))
                        {
                            EnterButtonContent = "TABG Loading...";
                            InfoText = "TABG process started! Waiting for main menu to load...";
                        }
                        else if (msg.Contains("window detected"))
                        {
                            EnterButtonContent = "TABG Window Found...";
                            InfoText = "TABG window visible! Waiting for main menu...";
                        }
                        else if (msg.Contains("main menu should be loaded"))
                        {
                            EnterButtonContent = "Main Menu Ready!";
                            InfoText = "TABG main menu loaded! Stopping Sigma Mode...";
                            StopButtonVisible = false;
                        }

                        if (msg.Contains("elapsed") || msg.Contains("Found") || msg.Contains("process") ||
                            msg.Contains("window") || msg.Contains("waiting") || msg.Contains("visible"))
                        {
                            DebugText = msg;
                        }
                    });
                });

                var success = await _sigmaMode.StartSigmaModeAsync();

                if (success)
                    _toast.Success("Sigma Mode completed successfully!");
                else
                    _toast.Warning("Sigma Mode completed with issues. Check the log for details.");
            }
            catch (Exception ex)
            {
                _toast.Error($"Sigma Mode error: {ex.Message}");
            }
            finally
            {
                _sigmaMode?.Dispose();
                _sigmaMode = null;
                IsRunning = false;
                EnterButtonEnabled = true;
                EnterButtonContent = "Enter";
                StopButtonVisible = false;
                InfoText = "";
                DebugText = "";
            }
        }

        [RelayCommand]
        private void Stop()
        {
            if (_sigmaMode == null)
                return;

            IsRunning = false;
            EnterButtonContent = "Force Stopping...";
            StopButtonVisible = false;
            InfoText = "Force stopping Sigma Mode...";
            DebugText = "Manual stop requested";

            _sigmaMode.RequestEmergencyExit();
        }

        public void Cleanup()
        {
            _sigmaMode?.RequestEmergencyExit();
            _sigmaMode?.Dispose();
        }
    }
}
