using System.ComponentModel;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Gui.ViewModels
{
    public class ServerSettingsViewModel : INotifyPropertyChanged
    {
        private readonly GameSettingsData _data;
        public ServerSettingsViewModel(GameSettingsData data)
        {
            _data = data;
        }

        public string ServerName
        {
            get => _data.ServerName;
            set { if (value != _data.ServerName) { _data.ServerName = value; OnPropertyChanged(nameof(ServerName)); } }
        }
        public int Port
        {
            get => _data.Port;
            set { if (value != _data.Port) { _data.Port = value; OnPropertyChanged(nameof(Port)); } }
        }
        public int MaxPlayers
        {
            get => _data.MaxPlayers;
            set { if (value != _data.MaxPlayers) { _data.MaxPlayers = value; OnPropertyChanged(nameof(MaxPlayers)); } }
        }

        public GameSettingsData ToModel() => _data;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 