using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public interface IToolExecutor
    {
        string ExecuteToolCall(ToolCall toolCall, string serverPath);
        FunctionSpec[] GetAvailableFunctions();
    }

    public class ToolExecutor : IToolExecutor
    {
        private readonly string _serverDirectory;
        private readonly ConfigPatcher _configPatcher;
        private readonly Func<Task> _reloadOllamaFunc;

        public ToolExecutor(string serverDirectory, Func<Task> reloadOllamaFunc = null)
        {
            _serverDirectory = serverDirectory;
            _configPatcher = new ConfigPatcher();
            _reloadOllamaFunc = reloadOllamaFunc;
        }

        public string ExecuteToolCall(ToolCall toolCall, string serverPath)
        {
            try
            {
                // Validate tool call
                if (string.IsNullOrWhiteSpace(toolCall.Function?.Name))
                {
                    return "Error: Invalid tool call - missing function name";
                }
                
                if (string.IsNullOrWhiteSpace(toolCall.Function.Arguments))
                {
                    return "Error: Invalid tool call - missing arguments";
                }
                
                JObject args;
                try
                {
                    args = JObject.Parse(toolCall.Function.Arguments);
                }
                catch (JsonException)
                {
                    return "Error: Invalid tool call - malformed JSON arguments";
                }

                switch (toolCall.Function.Name)
                {
                    case "modify_game_settings":
                        return ModifyGameSettings(args);

                    case "modify_datapack":
                        return ModifyDatapack(args);

                    case "get_game_setting":
                        return GetGameSetting(args);

                    case "modify_starter_pack":
                        return ModifyStarterPack(args);

                    case "manage_ollama_model":
                        return ManageOllamaModel(args);

                    default:
                        return $"Unknown function: {toolCall.Function.Name}";
                }
            }
            catch (Exception ex)
            {
                return $"Error executing tool call: {ex.Message}";
            }
        }

        private string ModifyGameSettings(JObject args)
        {
            var settingName = args["setting_name"]?.ToString();
            var newValue = args["new_value"]?.ToString();

            if (string.IsNullOrEmpty(settingName) || newValue == null)
                return "Missing required parameters: setting_name and new_value";

            var configPath = Path.Combine(_serverDirectory, "game_settings.txt");
            
            if (!File.Exists(configPath))
                return $"Configuration file not found at: {configPath}. Server directory: {_serverDirectory}";
                
            return _configPatcher.ApplyGameSettingsChange(configPath, settingName, newValue);
        }

        private string ModifyDatapack(JObject args)
        {
            var section = args["section"]?.ToString();
            var changes = args["changes"] as JObject;

            if (string.IsNullOrEmpty(section) || changes == null)
                return "Missing required parameters: section and changes";

            // Try both possible filenames
            var datapackPath = Path.Combine(_serverDirectory, "TheStarterPack.txt");
            if (!File.Exists(datapackPath))
            {
                datapackPath = Path.Combine(_serverDirectory, "datapack.txt");
                if (!File.Exists(datapackPath))
                    return $"Datapack file not found. Looked for TheStarterPack.txt and datapack.txt in: {_serverDirectory}";
            }
            
            return _configPatcher.ApplyDatapackChange(datapackPath, section, changes);
        }

        private string GetGameSetting(JObject args)
        {
            var settingName = args["setting_name"]?.ToString();

            if (string.IsNullOrEmpty(settingName))
                return "Missing required parameter: setting_name";

            var configPath = Path.Combine(_serverDirectory, "game_settings.txt");
            
            if (!File.Exists(configPath))
                return $"Configuration file not found at: {configPath}";
                
            var value = _configPatcher.GetGameSettingValue(configPath, settingName);
            
            return string.IsNullOrEmpty(value) 
                ? $"Setting '{settingName}' not found in {configPath}" 
                : $"{settingName}={value}";
        }

        private string ModifyStarterPack(JObject args)
        {
            var settingName = args["setting_name"]?.ToString();
            var newValue = args["new_value"]?.ToString();

            if (string.IsNullOrEmpty(settingName) || newValue == null)
                return "Missing required parameters: setting_name and new_value";

            var starterPackPath = Path.Combine(_serverDirectory, "TheStarterPack.txt");
            
            if (!File.Exists(starterPackPath))
                return $"TheStarterPack.txt not found at: {starterPackPath}";
                
            return _configPatcher.ApplyGameSettingsChange(starterPackPath, settingName, newValue);
        }

        private string ManageOllamaModel(JObject args)
        {
            var action = args["action"]?.ToString();
                                var modelName = args["model_name"]?.ToString() ?? "llama3.2:latest";

            if (string.IsNullOrEmpty(action))
                return "Missing required parameter: action (delete/reinstall)";

            try
            {
                switch (action.ToLower())
                {
                    case "delete":
                        var deleteProcess = Process.Start(new ProcessStartInfo
                        {
                            FileName = "ollama",
                            Arguments = $"rm {modelName}",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        deleteProcess?.WaitForExit();
                        return $"Deleted model: {modelName}";

                    case "reinstall":
                        var deleteProc = Process.Start(new ProcessStartInfo
                        {
                            FileName = "ollama",
                            Arguments = $"rm {modelName}",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        deleteProc?.WaitForExit();

                        var pullProcess = Process.Start(new ProcessStartInfo
                        {
                            FileName = "ollama",
                            Arguments = $"pull {modelName}",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        pullProcess?.WaitForExit();
                        return $"Reinstalled model: {modelName}";

                    default:
                        return "Invalid action. Use 'delete' or 'reinstall'";
                }
            }
            catch (Exception ex)
            {
                return $"Error managing Ollama model: {ex.Message}";
            }
        }

        public FunctionSpec[] GetAvailableFunctions()
        {
            return new[]
            {
                new FunctionSpec
                {
                    Name = "modify_game_settings",
                    Description = "Modify a game setting in the game_settings.txt file",
                    Parameters = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["setting_name"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "The name of the setting to modify (e.g., 'ServerName', 'MaxPlayers')"
                            },
                            ["new_value"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "The new value for the setting"
                            }
                        },
                        ["required"] = new[] { "setting_name", "new_value" }
                    }
                },
                new FunctionSpec
                {
                    Name = "modify_datapack",
                    Description = "Modify settings in the datapack.txt file",
                    Parameters = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["section"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "The section in the datapack to modify"
                            },
                            ["changes"] = new Dictionary<string, object>
                            {
                                ["type"] = "object",
                                ["description"] = "Object containing the key-value pairs to update"
                            }
                        },
                        ["required"] = new[] { "section", "changes" }
                    }
                },
                new FunctionSpec
                {
                    Name = "get_game_setting",
                    Description = "Get the current value of a game setting",
                    Parameters = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["setting_name"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "The name of the setting to retrieve"
                            }
                        },
                        ["required"] = new[] { "setting_name" }
                    }
                },
                new FunctionSpec
                {
                    Name = "modify_starter_pack",
                    Description = "Modify settings in TheStarterPack.txt file (key=value format). Common settings: Loadouts, ItemsGiven, HealOnKill, HealOnKillAmount, WinCondition, ForceKillAtStart, ValidSpawnPoints, RingSettings, SpelldropEnabled, etc.",
                    Parameters = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["setting_name"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "The name of the setting to modify (e.g., 'Loadouts', 'HealOnKillAmount', 'WinCondition')"
                            },
                            ["new_value"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "The new value for the setting. For Loadouts use format: \"Name:100% itemID:qty,itemID:qty/Name2:100% itemID:qty/\""
                            }
                        },
                        ["required"] = new[] { "setting_name", "new_value" }
                    }
                },
                new FunctionSpec
                {
                    Name = "manage_ollama_model",
                    Description = "Delete or reinstall Ollama AI models",
                    Parameters = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["action"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["enum"] = new[] { "delete", "reinstall" },
                                ["description"] = "Action to perform on the model"
                            },
                            ["model_name"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Name of the model (default: llama3.2:latest)"
                            }
                        },
                        ["required"] = new[] { "action" }
                    }
                }
            };
        }
    }
} 