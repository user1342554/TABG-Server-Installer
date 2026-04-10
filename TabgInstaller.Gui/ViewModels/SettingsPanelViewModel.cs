using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Resources;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels;

public partial class SettingsPanelViewModel : ObservableObject
{
    private readonly IAppSettingsService _appSettings;
    private readonly INavigationService _navigation;
    private readonly IServerPathProvider _serverPathProvider;
    private readonly IThemeService _themeService;

    [ObservableProperty] private string _serverPath = "";
    [ObservableProperty] private string _clientPath = "";
    [ObservableProperty] private string _moddedPath = "";
    [ObservableProperty] private string _appVersion = "";
    [ObservableProperty] private List<CultureInfo> _availableLanguages = new();
    [ObservableProperty] private CultureInfo? _selectedLanguage;
    [ObservableProperty] private bool _highContrastEnabled;
    [ObservableProperty] private bool _crashReportingEnabled;
    [ObservableProperty] private string _restartMessage = "";

    public SettingsPanelViewModel(
        IAppSettingsService appSettings,
        INavigationService navigation,
        IServerPathProvider serverPathProvider,
        IThemeService themeService)
    {
        _appSettings = appSettings;
        _navigation = navigation;
        _serverPathProvider = serverPathProvider;
        _themeService = themeService;
        _serverPathProvider.PathChanged += OnServerPathChanged;

        AppVersion = $"v{UpdateService.GetCurrentVersion()}";

        var settings = _appSettings.Load();
        AvailableLanguages = LocalizationHelper.GetAvailableLanguages();
        SelectedLanguage = AvailableLanguages.FirstOrDefault(c => c.Name == settings.Language)
                           ?? AvailableLanguages.First();
        HighContrastEnabled = settings.HighContrastEnabled;
        CrashReportingEnabled = settings.CrashReportingEnabled == true;
    }

    private void OnServerPathChanged()
    {
        var settings = _appSettings.Load();
        ServerPath = settings.ServerPath;
        ClientPath = settings.ClientPath;
        ModdedPath = settings.ClientModdedPath;
    }

    partial void OnSelectedLanguageChanged(CultureInfo? value)
    {
        if (value == null) return;
        var settings = _appSettings.Load();
        if (settings.Language != value.Name)
        {
            settings.Language = value.Name;
            _appSettings.Save(settings);
            RestartMessage = Strings.RestartRequired;
        }
    }

    partial void OnHighContrastEnabledChanged(bool value)
    {
        _themeService.SetHighContrast(value);
    }

    partial void OnCrashReportingEnabledChanged(bool value)
    {
        var settings = _appSettings.Load();
        settings.CrashReportingEnabled = value;
        _appSettings.Save(settings);
    }

    [RelayCommand]
    private void HardReset()
    {
        _appSettings.Reset();
        _navigation.RequestHardReset();
    }
}
