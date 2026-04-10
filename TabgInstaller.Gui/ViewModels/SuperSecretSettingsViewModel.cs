using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using System.Windows;
using TabgInstaller.Gui.Resources;
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
                ErrorText = Messages.IncorrectPassword;
                return;
            }

            var result = MessageBox.Show(
                Messages.PasswordCorrectPrompt,
                Messages.SuperSecretTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            IsUnlocked = true;

            try
            {
                EnterButtonEnabled = false;
                EnterButtonContent = Messages.StartingSigma;
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
                            EnterButtonContent = Messages.PlayingMusic;
                        else if (msg.Contains("TABG") && msg.Contains("Launch"))
                            EnterButtonContent = Messages.LaunchingTabg;
                        else if (msg.Contains("fans"))
                            EnterButtonContent = Messages.SettingFans;
                        else if (msg.Contains("overlay"))
                            EnterButtonContent = Messages.CreatingOverlays;
                        else if (msg.Contains("engaged"))
                        {
                            EnterButtonContent = Messages.ScanningForTabg;
                            StopButtonVisible = true;
                            InfoText = Messages.WaitingForMainMenu;
                        }
                        else if (msg.Contains("process found"))
                        {
                            EnterButtonContent = Messages.TabgLoading;
                            InfoText = Messages.TabgProcessStarted;
                        }
                        else if (msg.Contains("window detected"))
                        {
                            EnterButtonContent = Messages.TabgWindowFound;
                            InfoText = Messages.TabgWindowVisible;
                        }
                        else if (msg.Contains("main menu should be loaded"))
                        {
                            EnterButtonContent = Messages.MainMenuReady;
                            InfoText = Messages.TabgMainMenuLoaded;
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
                    _toast.Success(Messages.SigmaModeCompleted);
                else
                    _toast.Warning(Messages.SigmaModeIssues);
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.SigmaModeError, ex.Message));
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
            EnterButtonContent = Messages.ForceStoppingLabel;
            StopButtonVisible = false;
            InfoText = Messages.ForceStopping;
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
