using System;
using System.Windows;

namespace TabgInstaller.Gui.Services;

public class ThemeService : IThemeService
{
    private readonly IAppSettingsService _appSettings;
    private bool _isHighContrast;

    public bool IsHighContrast => _isHighContrast;

    public ThemeService(IAppSettingsService appSettings)
    {
        _appSettings = appSettings;
        _isHighContrast = appSettings.Load().HighContrastEnabled;
    }

    public void SetHighContrast(bool enabled)
    {
        _isHighContrast = enabled;

        var app = Application.Current;
        if (app == null) return;

        var mergedDicts = app.Resources.MergedDictionaries;

        for (int i = mergedDicts.Count - 1; i >= 0; i--)
        {
            if (mergedDicts[i].Source?.OriginalString?.Contains("HighContrast") == true)
                mergedDicts.RemoveAt(i);
        }

        if (enabled)
        {
            mergedDicts.Add(new ResourceDictionary
            {
                Source = new Uri("Themes/HighContrast.xaml", UriKind.Relative)
            });
        }

        var settings = _appSettings.Load();
        settings.HighContrastEnabled = enabled;
        _appSettings.Save(settings);
    }
}
