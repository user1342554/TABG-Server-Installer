using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class AddServerDialogViewModel : ObservableObject
    {
        [ObservableProperty] private string _displayName = "";
        [ObservableProperty] private string _serverPath = "";
        [ObservableProperty] private bool _isLocal = true;
        [ObservableProperty] private bool _isRemote;

        // Remote fields
        [ObservableProperty] private string _host = "";
        [ObservableProperty] private int _port = 22;
        [ObservableProperty] private string _username = "";
        [ObservableProperty] private bool _usePassword = true;
        [ObservableProperty] private bool _usePrivateKey;
        [ObservableProperty] private string _password = "";
        [ObservableProperty] private string _privateKeyPath = "";
        [ObservableProperty] private string _remoteServerPath = "";
        [ObservableProperty] private bool _useScreen = true;
        [ObservableProperty] private bool _useSystemd;

        [ObservableProperty] private string _validationError = "";

        public bool DialogResult { get; private set; }
        public Action? CloseAction { get; set; }

        [RelayCommand]
        private void BrowseServerPath()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "TABG Server|TABG.exe",
                Title = "Select TABG Server"
            };
            if (dialog.ShowDialog() == true)
            {
                ServerPath = System.IO.Path.GetDirectoryName(dialog.FileName) ?? "";
                if (string.IsNullOrEmpty(DisplayName))
                    DisplayName = "New Server";
            }
        }

        [RelayCommand]
        private void BrowsePrivateKey()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Private Key|*.pem;*.ppk;*id_rsa;*id_ed25519|All Files|*.*",
                Title = "Select SSH Private Key"
            };
            if (dialog.ShowDialog() == true)
                PrivateKeyPath = dialog.FileName;
        }

        [RelayCommand]
        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                ValidationError = "Display name is required.";
                return;
            }

            if (IsLocal)
            {
                if (string.IsNullOrWhiteSpace(ServerPath))
                {
                    ValidationError = "Server path is required.";
                    return;
                }
                if (!System.IO.File.Exists(System.IO.Path.Combine(ServerPath, "TABG.exe")))
                {
                    ValidationError = "TABG.exe not found in the selected directory.";
                    return;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Host))
                {
                    ValidationError = "Hostname is required.";
                    return;
                }
                if (string.IsNullOrWhiteSpace(Username))
                {
                    ValidationError = "Username is required.";
                    return;
                }
                if (string.IsNullOrWhiteSpace(RemoteServerPath))
                {
                    ValidationError = "Remote server path is required.";
                    return;
                }
            }

            DialogResult = true;
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
            CloseAction?.Invoke();
        }

        public RemoteConnectionConfig BuildRemoteConfig() => new()
        {
            Host = Host,
            Port = Port,
            Username = Username,
            AuthMethod = UsePassword ? SshAuthMethod.Password : SshAuthMethod.PrivateKey,
            PrivateKeyPath = PrivateKeyPath,
            RemoteServerPath = RemoteServerPath,
            ProcessMode = UseScreen ? RemoteProcessMode.Screen : RemoteProcessMode.Systemd
        };
    }
}
