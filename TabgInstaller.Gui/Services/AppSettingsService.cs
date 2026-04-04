using System;
using System.IO;
using Newtonsoft.Json;

namespace TabgInstaller.Gui.Services;

public class AppSettings
{
    public bool SetupCompleted { get; set; }
    public string ServerPath { get; set; } = "";
    public string ClientPath { get; set; } = "";
    public string ClientModdedPath { get; set; } = "";
}

public static class AppSettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TabgInstaller");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static AppSettings? _cached;

    public static AppSettings Load()
    {
        if (_cached != null) return _cached;

        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _cached = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                return _cached;
            }
        }
        catch { }

        _cached = new AppSettings();
        return _cached;
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(SettingsDir))
                Directory.CreateDirectory(SettingsDir);

            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(SettingsPath, json);
            _cached = settings;
        }
        catch { }
    }

    public static void MarkSetupComplete(string serverPath, string clientPath, string clientModdedPath)
    {
        var settings = Load();
        settings.SetupCompleted = true;
        settings.ServerPath = serverPath;
        settings.ClientPath = clientPath;
        settings.ClientModdedPath = clientModdedPath;
        Save(settings);
    }

    public static void Reset()
    {
        _cached = null;
        try { if (File.Exists(SettingsPath)) File.Delete(SettingsPath); } catch { }
    }
}
