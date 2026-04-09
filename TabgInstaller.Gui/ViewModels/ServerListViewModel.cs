using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class ServerListViewModel : ObservableObject
    {
        private readonly IServerInstanceManager _manager;
        private readonly IToastService _toast;
        private readonly ICredentialStorageService _credentials;

        public ObservableCollection<ServerInstanceData> Instances => _manager.InstanceDataList;

        [ObservableProperty] private ServerInstanceData? _selectedInstance;

        public ServerListViewModel(
            IServerInstanceManager manager,
            IToastService toast,
            ICredentialStorageService credentials)
        {
            _manager = manager;
            _toast = toast;
            _credentials = credentials;
            _selectedInstance = _manager.ActiveInstanceData;
        }

        partial void OnSelectedInstanceChanged(ServerInstanceData? value)
        {
            if (value != null)
                _manager.SetActiveInstance(value.Id);
        }

        [RelayCommand]
        private void AddServer()
        {
            // Will be wired to AddServerDialog in Task 13
        }

        [RelayCommand]
        private void RemoveServer()
        {
            if (SelectedInstance == null) return;
            try
            {
                _manager.RemoveInstance(SelectedInstance.Id);
            }
            catch (InvalidOperationException)
            {
                _toast.Warning("Cannot remove the last server instance.");
            }
        }

        [RelayCommand]
        private void RenameServer(string newName)
        {
            if (SelectedInstance == null || string.IsNullOrWhiteSpace(newName)) return;
            _manager.RenameInstance(SelectedInstance.Id, newName.Trim());
        }
    }
}
