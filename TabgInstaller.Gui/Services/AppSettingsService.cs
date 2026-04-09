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
    public string? SkippedUpdateVersion { get; set; }
    public string Language { get; set; } = "en";
    public bool HighContrastEnabled { get; set; } = false;
    public bool? CrashReportingEnabled { get; set; } = null;
}

public class AppSettingsService : IAppSettingsService
{
    private readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TabgInstaller");

    private readonly string SettingsPath;

    private AppSettings? _cached;

    public AppSettingsService()
    {
        SettingsPath = Path.Combine(SettingsDir, "settings.json");
    }

    public AppSettings Load()
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
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AppSettings] Failed to load settings, using defaults: {ex.Message}"); }

        _cached = new AppSettings();
        return _cached;
    }

    public void Save(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(SettingsDir))
                Directory.CreateDirectory(SettingsDir);

            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(SettingsPath, json);
            _cached = settings;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AppSettings] Failed to save settings: {ex.Message}"); }
    }

    public void MarkSetupComplete(string serverPath, string clientPath, string clientModdedPath)
    {
        var settings = Load();
        settings.SetupCompleted = true;
        settings.ServerPath = serverPath;
        settings.ClientPath = clientPath;
        settings.ClientModdedPath = clientModdedPath;
        Save(settings);
    }

    public void Reset()
    {
        _cached = null;
        try { if (File.Exists(SettingsPath)) File.Delete(SettingsPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AppSettings] Failed to delete settings file: {ex.Message}"); }
    }
}
