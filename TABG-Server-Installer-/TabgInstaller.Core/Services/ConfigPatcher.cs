using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace TabgInstaller.Core.Services
{
    public interface IConfigPatcher
    {
        string ApplyGameSettingsChange(string configPath, string settingName, string newValue);
        string ApplyDatapackChange(string datapackPath, string section, JObject changes);
        string GetGameSettingValue(string configPath, string settingName);
    }

    public class ConfigPatcher : IConfigPatcher
    {
        public string ApplyGameSettingsChange(string configPath, string settingName, string newValue)
        {
            if (!File.Exists(configPath))
                return $"Configuration file not found: {configPath}";

            try
            {
                var lines = File.ReadAllLines(configPath).ToList();
                var pattern = $@"^{Regex.Escape(settingName)}=.*$";
                var found = false;

                for (int i = 0; i < lines.Count; i++)
                {
                    if (Regex.IsMatch(lines[i], pattern))
                    {
                        lines[i] = $"{settingName}={newValue}";
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // Add the setting if it doesn't exist
                    lines.Add($"{settingName}={newValue}");
                }

                File.WriteAllLines(configPath, lines);
                return $"Successfully updated {settingName} to {newValue}";
            }
            catch (Exception ex)
            {
                return $"Error updating configuration: {ex.Message}";
            }
        }

        public string ApplyDatapackChange(string datapackPath, string section, JObject changes)
        {
            if (!File.Exists(datapackPath))
                return $"Datapack file not found: {datapackPath}";

            try
            {
                // TheStarterPack.txt uses key=value format, not JSON
                // For now, we'll treat the section parameter as ignored and apply changes directly
                var lines = File.ReadAllLines(datapackPath).ToList();
                
                foreach (var change in changes.Properties())
                {
                    var key = change.Name;
                    var value = change.Value.ToString();
                    var pattern = $@"^{Regex.Escape(key)}=.*$";
                    var found = false;

                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (Regex.IsMatch(lines[i], pattern))
                        {
                            lines[i] = $"{key}={value}";
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        // Add the setting if it doesn't exist
                        lines.Add($"{key}={value}");
                    }
                }

                File.WriteAllLines(datapackPath, lines);
                return $"Successfully updated starter pack settings";
            }
            catch (Exception ex)
            {
                return $"Error updating datapack: {ex.Message}";
            }
        }

        public string GetGameSettingValue(string configPath, string settingName)
        {
            if (!File.Exists(configPath))
                return "";

            try
            {
                var lines = File.ReadAllLines(configPath);
                var pattern = $@"^{Regex.Escape(settingName)}=(.*)$";

                foreach (var line in lines)
                {
                    var match = Regex.Match(line, pattern);
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }
            catch { }

            return "";
        }
    }
} 