using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace TabgInstaller.Core.Services
{
    public class PromptBuilder
    {
        private readonly string _knowledgePath;

        public PromptBuilder()
        {
            // Get the executable's directory and go up to find Knowledge folder
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var baseInfo = new DirectoryInfo(baseDir);
            var candidates = new List<string>
            {
                Path.Combine(baseInfo.FullName, "Knowledge")
            };
            if (baseInfo.Parent != null)
                candidates.Add(Path.Combine(baseInfo.Parent.FullName, "Knowledge"));
            if (baseInfo.Parent?.Parent != null)
                candidates.Add(Path.Combine(baseInfo.Parent.Parent.FullName, "Knowledge"));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "Knowledge"));

            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c) && Directory.Exists(c))
                {
                    _knowledgePath = c;
                    break;
                }
            }
            if (string.IsNullOrEmpty(_knowledgePath))
            {
                _knowledgePath = Path.Combine(baseDir, "Knowledge");
            }
        }

        public string BuildSystemPrompt(string serverDirectory)
        {
            var prompt = new StringBuilder();
            
            prompt.AppendLine("You are an AI assistant for configuring TABG (Totally Accurate Battlegrounds) servers.");
            prompt.AppendLine("You help users modify server settings and understand game configuration options.");
            prompt.AppendLine();
            prompt.AppendLine("CRITICAL TOOL USAGE RULES:");
            prompt.AppendLine("1. ONLY use tools when the user EXPLICITLY asks you to change or modify settings");
            prompt.AppendLine("2. NEVER use tools just to check or read values - describe settings verbally instead");
            prompt.AppendLine("3. ALWAYS confirm with the user before making changes");
            prompt.AppendLine("4. If unsure about a request, ASK for clarification instead of guessing");
            prompt.AppendLine("5. Only use ONE tool per response unless explicitly asked to do multiple changes");
            prompt.AppendLine();
            prompt.AppendLine($"IMPORTANT: The user's TABG server is installed at: {serverDirectory}");
            prompt.AppendLine($"Configuration files are located at:");
            prompt.AppendLine($"- Game settings: {Path.Combine(serverDirectory, "game_settings.txt")}");
            prompt.AppendLine($"- Starter pack settings: {Path.Combine(serverDirectory, "TheStarterPack.txt")}");
            prompt.AppendLine();
            prompt.AppendLine("Available functions:");
            prompt.AppendLine("- modify_game_settings: Modify game_settings.txt file (key=value format)");
            prompt.AppendLine("- modify_starter_pack: Modify TheStarterPack.txt file (key=value format)");
            prompt.AppendLine("  Important: For Loadouts use format: \"Name:100% itemID:qty,itemID:qty/Name2:100% itemID:qty/\"");
            prompt.AppendLine("  Each loadout ends with /, multiple items separated by commas");
            prompt.AppendLine("  Example Loadouts:");
            prompt.AppendLine("    \"Sniper:100% 328:1,131:5,132:2/\" - VSS rifle with bandages and medkits");
            prompt.AppendLine("    \"Overpowered:50% 152:1,131:10,132:5/Healer:50% 187:3,132:10/\" - 50% chance for each");
            prompt.AppendLine("    \"Full Blessing:100% 42:1,43:1,44:1/\" - Multiple blessing items");
            // Local AI removed; no local model management functions
            prompt.AppendLine();

            // Load and include Knowledge files - explicitly validate all three files
            if (Directory.Exists(_knowledgePath))
            {
                prompt.AppendLine("=== GAME KNOWLEDGE DATABASE ===");
                prompt.AppendLine($"Knowledge files are located at: {_knowledgePath}");
                prompt.AppendLine();
                
                // Explicitly load each required file
                var requiredFiles = new[]
                {
                    "Game settings explanation.json",
                    "The starter pack explained.json", 
                    "Weaponlist.json"
                };
                
                foreach (var fileName in requiredFiles)
                {
                    var filePath = Path.Combine(_knowledgePath, fileName);
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            var content = File.ReadAllText(filePath);
                            
                            prompt.AppendLine($"=== {fileName} ===");
                            prompt.AppendLine($"Full path: {filePath}");
                            prompt.AppendLine("Content:");
                            prompt.AppendLine(content);
                            prompt.AppendLine();
                        }
                        else
                        {
                            prompt.AppendLine($"WARNING: Required knowledge file missing: {fileName}");
                            prompt.AppendLine($"Expected at: {filePath}");
                            prompt.AppendLine();
                        }
                    }
                    catch (Exception ex)
                    {
                        prompt.AppendLine($"Error loading {fileName}: {ex.Message}");
                        prompt.AppendLine();
                    }
                }
            }
            else
            {
                prompt.AppendLine($"WARNING: Knowledge directory not found at {_knowledgePath}");
            }

            return prompt.ToString();
        }
    }
} 