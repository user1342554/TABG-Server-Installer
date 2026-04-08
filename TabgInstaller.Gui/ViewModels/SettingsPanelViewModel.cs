using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class SettingsPanelViewModel : ObservableObject
    {
        private readonly IAppSettingsService _appSettings;
        private readonly INavigationService _navigation;
        private readonly IServerPathProvider _serverPathProvider;

        [ObservableProperty] private string _serverPath = "";
        [ObservableProperty] private string _clientPath = "";
        [ObservableProperty] private string _moddedPath = "";
        [ObservableProperty] private string _appVersion = "";

        public SettingsPanelViewModel(
            IAppSettingsService appSettings,
            INavigationService navigation,
            IServerPathProvider serverPathProvider)
        {
            _appSettings = appSettings;
            _navigation = navigation;
            _serverPathProvider = serverPathProvider;
            _serverPathProvider.PathChanged += OnServerPathChanged;

            AppVersion = $"v{UpdateService.GetCurrentVersion()}";
        }

        private void OnServerPathChanged()
        {
            var settings = _appSettings.Load();
            ServerPath = settings.ServerPath;
            ClientPath = settings.ClientPath;
            ModdedPath = settings.ClientModdedPath;
        }

        [RelayCommand]
        private void HardReset()
        {
            _appSettings.Reset();
            _navigation.RequestHardReset();
        }
    }
}
