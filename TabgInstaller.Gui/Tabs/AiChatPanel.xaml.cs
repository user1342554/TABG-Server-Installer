using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Core.Services.AI;

namespace TabgInstaller.Gui.Tabs
{
    public partial class AiChatPanel : UserControl
    {
        private readonly ISecureKeyStore _keyStore = new SecureKeyStore();
        private readonly Dictionary<string, IAiProvider> _providers;
        private const string ServerPathKey = "TABG_SERVER_PATH";
        private DispatcherTimer? _thinkingTimer;
        private int _dotCount = 0;

        public AiChatPanel()
        {
            InitializeComponent();
            InitializeThinkingAnimation();

            _providers = new Dictionary<string, IAiProvider>
            {
                { "OpenAI (GPT-5 - Thinking)", new OpenAIProvider() },
                { "OpenAI (GPT-5 - Chat)", new OpenAIProvider() },
                { "Anthropic (Claude Opus 4.1)", new AnthropicProvider() },
                { "Anthropic (Claude Sonnet 4)", new AnthropicProvider() },
                { "xAI (Grok-4)", new XaiProvider() },
                { "Gemini (2.5 Pro - Vertex)", new GeminiVertexProvider(
                    projectId: System.Environment.GetEnvironmentVariable("GCP_PROJECT") ?? string.Empty,
                    location: System.Environment.GetEnvironmentVariable("GCP_LOCATION") ?? "global") }
            };

            ProviderCombo.SelectedIndex = 0;
            LoadKeyIntoBox();
        }

        private void InitializeThinkingAnimation()
        {
            _thinkingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _thinkingTimer.Tick += (s, e) =>
            {
                _dotCount = (_dotCount + 1) % 4;
                if (ThinkingDots != null)
                {
                    ThinkingDots.Text = _dotCount == 0 ? "   " : new string('.', _dotCount);
                }
            };
        }

        private void LoadKeyIntoBox()
        {
            try
            {
                var providerName = ((ComboBoxItem)ProviderCombo.SelectedItem)?.Content?.ToString();
                if (!string.IsNullOrWhiteSpace(providerName))
                {
                    var savedKey = _keyStore.GetKey(providerName);
                    if (!string.IsNullOrWhiteSpace(savedKey))
                    {
                        ApiKeyBox.Password = savedKey;
                    }
                    else
                    {
                        ApiKeyBox.Password = "";
                    }
                }
            }
            catch (Exception ex)
            {
                AppendToLog($"[error] Failed to load API key: {ex.Message}\n");
                ApiKeyBox.Password = "";
            }
        }

        private void SaveKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var providerName = ((ComboBoxItem)ProviderCombo.SelectedItem)?.Content?.ToString();
                if (string.IsNullOrWhiteSpace(providerName))
                {
                    AppendToLog("[error] No provider selected.\n");
                    return;
                }

                var key = ApiKeyBox.Password?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    AppendToLog("[error] API key is empty.\n");
                    return;
                }
                
                _keyStore.SaveKey(providerName, key);
                AppendToLog($"[info] API key saved for {providerName}.\n");
                ApiKeyBox.Password = ""; // Clear the password box after saving
            }
            catch (Exception ex)
            {
                AppendToLog($"[error] Failed to save API key: {ex.Message}\n");
            }
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var providerName = ((ComboBoxItem)ProviderCombo.SelectedItem).Content.ToString();
                if (providerName == null || !_providers.TryGetValue(providerName, out var provider))
                    return;

                var apiKey = GetOrStoreKey(providerName);
                var model = GetModelForProvider(providerName);
                var prompt = PromptBox.Text;
                if (string.IsNullOrWhiteSpace(prompt))
                    return;

                // Show thinking indicator and disable send button
                ThinkingIndicator.Visibility = Visibility.Visible;
                _thinkingTimer?.Start();
                SendButton.IsEnabled = false;

                AppendToLog($"> {prompt}\n");
                PromptBox.Text = string.Empty;

                var messages = new List<AiMessage>();
                // Prepend system prompts from knowledge files
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var kdir = System.IO.Path.Combine(baseDir, "Knowledge");
                    var files = new[]
                    {
                        System.IO.Path.Combine(kdir, "Game settings explanation.json"),
                        System.IO.Path.Combine(kdir, "The starter pack explained.json"),
                        System.IO.Path.Combine(kdir, "Weaponlist.json"),
                    };
                    var intro = ToolInstruction + "\nThese files are provided as system context. Keep responses concise unless asked.\n";
                    messages.Add(new AiMessage("system", intro));
                    foreach (var f in files)
                    {
                        if (System.IO.File.Exists(f))
                        {
                            var content = System.IO.File.ReadAllText(f);
                            var tag = "FILE: " + System.IO.Path.GetFileName(f) + "\n" + content;
                            messages.Add(new AiMessage("system", tag));
                        }
                    }
                }
                catch { }

                messages.Add(new AiMessage("user", prompt));

                // Special-case Vertex: use environment variables for project/location
                if (provider is GeminiVertexProvider)
                {
                    provider = new GeminiVertexProvider(
                        projectId: System.Environment.GetEnvironmentVariable("GCP_PROJECT") ?? string.Empty,
                        location: System.Environment.GetEnvironmentVariable("GCP_LOCATION") ?? "global"
                    );
                }

                // Non-streaming response
                AppendToLog("[ai] ");
                var reply = await provider.SendAsync(apiKey, model, messages);
                AppendToLog(reply);
                AppendToLog("\n\n");

                // Process tool calls from the response
                try
                {
                    HandleToolCalls(reply);
                }
                catch (Exception toolEx)
                {
                    AppendToLog("[tool_error] " + toolEx.Message + "\n\n");
                }
            }
            catch (Exception ex)
            {
                AppendToLog("[error] " + ex.Message + "\n");
            }
            finally
            {
                // Hide thinking indicator and re-enable send button
                _thinkingTimer?.Stop();
                ThinkingIndicator.Visibility = Visibility.Collapsed;
                SendButton.IsEnabled = true;
            }
        }

        private string GetOrStoreKey(string provider)
        {
            var existing = _keyStore.GetKey(provider);
            if (!string.IsNullOrWhiteSpace(existing))
                return existing!;

            var entered = ApiKeyBox.Password;
            if (string.IsNullOrWhiteSpace(entered))
            {
                AppendToLog("[info] Enter your API key in the box before sending.\n");
                return string.Empty;
            }

            _keyStore.SaveKey(provider, entered);
            ApiKeyBox.Password = string.Empty; // clear from UI immediately
            return entered;
        }

        private static string GetModelForProvider(string providerName)
        {
            return providerName switch
            {
                "OpenAI (GPT-5 - Thinking)" => "gpt-5",
                "OpenAI (GPT-5 - Chat)" => "gpt-5-chat-latest",
                "Anthropic (Claude Opus 4.1)" => "claude-opus-4-1-20250805",
                "Anthropic (Claude Sonnet 4)" => "claude-sonnet-4-20250514",
                "xAI (Grok-4)" => "grok-4-0709",
                "Gemini (2.5 Pro - Vertex)" => "gemini-2.5-pro",
                _ => "gpt-5"
            };
        }

        private void AppendToLog(string text)
        {
            ChatLog.AppendText(text);
            ChatLog.ScrollToEnd();
        }



        private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadKeyIntoBox();
        }

        private static string ToolInstruction =>
            "You can request edits to server config files via a tool call. " +
            "Respond with a single line starting with 'TOOL_CALL ' followed by a JSON object. " +
            "Schema: {\"tool\":\"edit_tabg_config\",\"target\":\"game_settings|starter_pack\",\"ops\":[{\"type\":\"set\",\"key\":\"KeyName\",\"value\":\"NewValue\"}]} . " +
            "Targets map to files: game_settings => TotallyAccurateBattlegroundsDedicatedServer/game_settings.txt, starter_pack => TotallyAccurateBattlegroundsDedicatedServer/TheStarterPack.txt. " +
            "Keys are lines like 'Key=Value'. The tool will replace the value after '='. " +
            "Always show the TOOL_CALL you emit.\n";

        private string CleanReplyForDisplay(string reply)
        {
            try
            {
                // Remove TOOL_CALL lines
                var lineRegex = new Regex(@"^\s*TOOL_CALL\s+\{[\s\S]*?\}\s*$", RegexOptions.Multiline);
                reply = lineRegex.Replace(reply, "");
                
                // Remove tool call code fences
                var fenceRegex = new Regex(@"```(?:tool|json)?\s*TOOL_CALL[\s\S]*?```", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                reply = fenceRegex.Replace(reply, "");
                
                // Remove empty JSON code fences that might contain tool calls
                var jsonRegex = new Regex(@"```(?:json)?\s*\{[\s\S]*?""tool""[\s\S]*?\}\s*```", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                reply = jsonRegex.Replace(reply, "");
                
                // Clean up extra whitespace
                reply = Regex.Replace(reply, @"\n\s*\n\s*\n", "\n\n", RegexOptions.Multiline);
                reply = reply.Trim();
                
                return reply;
            }
            catch
            {
                return reply; // Fallback to original
            }
        }

        private void HandleToolCalls(string assistantReply)
        {
            // Find TOOL_CALL JSON; accept either a line starting with TOOL_CALL {json} or a fenced block
            var toolJsons = new List<string>();

            // Pattern 1: single-line TOOL_CALL {...}
            var lineRegex = new Regex(@"^\s*TOOL_CALL\s+([\s\S]*?)$", RegexOptions.Multiline);
            foreach (Match m in lineRegex.Matches(assistantReply))
            {
                var afterToken = m.Groups[1].Value;
                var json = TryExtractJsonObject(afterToken);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    toolJsons.Add(json!);
                }
            }

            // Pattern 2: code fence ```tool {json} ``` or ```json with TOOL_CALL
            var fenceRegex = new Regex(@"```(?:tool|json)?\s*([\s\S]*?)```", RegexOptions.Multiline);
            foreach (Match m in fenceRegex.Matches(assistantReply))
            {
                var block = m.Groups[1].Value.Trim();
                if (block.StartsWith("TOOL_CALL"))
                {
                    var idx = block.IndexOf('{');
                    if (idx >= 0)
                    {
                        var json = TryExtractJsonObject(block.Substring(idx));
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            toolJsons.Add(json!);
                        }
                    }
                }
                else
                {
                    var json = TryExtractJsonObject(block);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        toolJsons.Add(json!);
                    }
                }
            }

            foreach (var json in toolJsons)
            {
                ShowToolCallSummary(json);
                ExecuteToolCall(json);
            }
        }

        private string? TryExtractJsonObject(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text)) return null;
                var start = text.IndexOf('{');
                if (start < 0) return null;

                var inString = false;
                var escape = false;
                var depth = 0;
                for (int i = start; i < text.Length; i++)
                {
                    var c = text[i];
                    if (escape)
                    {
                        escape = false;
                        continue;
                    }
                    if (inString)
                    {
                        if (c == '\\')
                        {
                            escape = true;
                        }
                        else if (c == '"')
                        {
                            inString = false;
                        }
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = true;
                        continue;
                    }
                    if (c == '{')
                    {
                        depth++;
                    }
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return text.Substring(start, i - start + 1);
                        }
                    }
                }

                // Fallback: trim to last closing brace if any
                var last = text.LastIndexOf('}');
                if (last > start)
                {
                    return text.Substring(start, last - start + 1);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void ShowToolCallSummary(string json)
        {
            try
            {
                var call = JsonSerializer.Deserialize<ToolCall>(json);
                if (call?.tool == "edit_tabg_config" && call.ops != null)
                {
                    var targetName = call.target == "starter_pack" ? "Starter Pack" : "Game Settings";
                    AppendToLog($"🔧 Editing {targetName}:\n");
                    
                    foreach (var op in call.ops)
                    {
                        if (op?.type == "set" && !string.IsNullOrWhiteSpace(op.key))
                        {
                            if (op.key == "Loadouts" && !string.IsNullOrWhiteSpace(op.value))
                            {
                                var loadouts = ParseLoadouts(op.value);
                                AppendToLog($"   📦 Updated {loadouts.Count} loadout(s):\n");
                                foreach (var loadout in loadouts.Take(5)) // Show first 5
                                {
                                    AppendToLog($"      • {loadout.Name} ({loadout.Chance}) - {loadout.Weapons}\n");
                                }
                                if (loadouts.Count > 5)
                                {
                                    AppendToLog($"      ... and {loadouts.Count - 5} more loadouts\n");
                                }
                            }
                            else
                            {
                                var displayValue = op.value?.Length > 50 ? op.value.Substring(0, 50) + "..." : op.value;
                                AppendToLog($"   ⚙️ {op.key} = {displayValue}\n");
                            }
                        }
                    }
                    AppendToLog("\n");
                }
                else
                {
                    AppendToLog($"🔧 Tool call: {call?.tool ?? "unknown"}\n");
                }
            }
            catch
            {
                AppendToLog("🔧 Processing tool call...\n");
            }
        }

        private class LoadoutInfo
        {
            public string Name { get; set; } = "";
            public string Chance { get; set; } = "";
            public string Weapons { get; set; } = "";
        }

        private List<LoadoutInfo> ParseLoadouts(string loadoutsValue)
        {
            var loadouts = new List<LoadoutInfo>();
            try
            {
                // Load weapon database once
                var weaponMap = LoadWeaponMap();
                
                // Parse loadouts separated by '/'
                var loadoutStrings = loadoutsValue.Split('/', StringSplitOptions.RemoveEmptyEntries);
                foreach (var loadoutString in loadoutStrings)
                {
                    var loadout = ParseSingleLoadout(loadoutString.Trim(), weaponMap);
                    if (loadout != null)
                    {
                        loadouts.Add(loadout);
                    }
                }
            }
            catch { }
            return loadouts;
        }

        private LoadoutInfo? ParseSingleLoadout(string loadoutString, Dictionary<int, (string, string)> weaponMap)
        {
            try
            {
                // Format: "Name:Chance% ItemId:Quantity,ItemId:Quantity,..." or "Name:Chance%ItemId:Quantity,..."
                var colonIndex = loadoutString.IndexOf(':');
                if (colonIndex == -1) return null;
                
                var nameAndChancePart = loadoutString.Substring(0, colonIndex);
                var afterColon = loadoutString.Substring(colonIndex + 1);
                
                // Find the percent sign
                var percentIndex = afterColon.IndexOf('%');
                if (percentIndex == -1) return null;
                
                var chance = afterColon.Substring(0, percentIndex);
                var itemsString = afterColon.Substring(percentIndex + 1).Trim(); // Trim to handle spaces
                
                var name = nameAndChancePart;
                
                // Parse items to find weapons, spells, blessings, and grenades
                var weapons = new List<string>();
                var spells = new List<string>();  
                var blessings = new List<string>();
                var grenades = new List<string>();
                var items = itemsString.Split(',', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var item in items)
                {
                    var parts = item.Trim().Split(':');
                    if (parts.Length >= 2 && int.TryParse(parts[0].Trim(), out int itemId))
                    {
                        if (weaponMap.TryGetValue(itemId, out var itemInfo))
                        {
                            var (itemName, category) = itemInfo;
                            switch (category)
                            {
                                case "Weapon":
                                case "Melee":
                                    weapons.Add(itemName);
                                    break;
                                case "Spell":
                                    spells.Add(itemName);
                                    break;
                                case "Blessing":
                                    blessings.Add(itemName);
                                    break;
                                case "Grenade":
                                    grenades.Add(itemName);
                                    break;
                            }
                        }
                    }
                }
                
                // Build display string prioritizing most important items
                var displayParts = new List<string>();
                if (weapons.Count > 0) displayParts.Add(string.Join(", ", weapons.Take(2)));
                if (spells.Count > 0) displayParts.Add(string.Join(", ", spells.Take(2)));
                if (grenades.Count > 0) displayParts.Add(string.Join(", ", grenades.Take(2)));
                if (blessings.Count > 0) displayParts.Add(string.Join(", ", blessings.Take(1))); // Less space for blessings
                
                var displayText = displayParts.Count > 0 ? string.Join(" + ", displayParts) : "Mixed items";
                
                return new LoadoutInfo
                {
                    Name = name,
                    Chance = chance + "%", 
                    Weapons = displayText
                };
            }
            catch
            {
                return null;
            }
        }

        private Dictionary<int, (string, string)> LoadWeaponMap()
        {
            var weaponMap = new Dictionary<int, (string, string)>();
            try
            {
                // Try multiple possible paths for the Knowledge folder
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                               var possiblePaths = new[]
               {
                   Path.Combine(baseDir, "Knowledge", "Weaponlist.json"),
                   Path.Combine(Directory.GetCurrentDirectory(), "Knowledge", "Weaponlist.json"),
                   Path.Combine(AppContext.BaseDirectory, "Knowledge", "Weaponlist.json")
               };
                
                string? weaponFile = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        weaponFile = path;
                        break;
                    }
                }
                
                if (weaponFile != null && File.Exists(weaponFile))
                {
                    var json = File.ReadAllText(weaponFile);
                    using var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.TryGetProperty("items", out var items))
                    {
                        foreach (var item in items.EnumerateObject())
                        {
                            if (int.TryParse(item.Name, out int id) &&
                                item.Value.TryGetProperty("name", out var nameProperty) &&
                                item.Value.TryGetProperty("category", out var categoryProperty))
                            {
                                var category = categoryProperty.GetString() ?? "";
                                var name = nameProperty.GetString() ?? $"Item{id}";
                                
                                if (category == "Weapon" || category == "Spell" || category == "Melee" || category == "Blessing" || category == "Grenade")
                                {
                                    weaponMap[id] = (name, category);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return weaponMap;
        }

        private sealed class ToolOp
        {
            public string? type { get; set; }
            public string? key { get; set; }
            public string? value { get; set; }
        }
        private sealed class ToolCall
        {
            public string? tool { get; set; }
            public string? target { get; set; }
            public List<ToolOp>? ops { get; set; }
        }

        private void ExecuteToolCall(string json)
        {
            var call = JsonSerializer.Deserialize<ToolCall>(json);
            if (call == null || call.tool != "edit_tabg_config" || string.IsNullOrWhiteSpace(call.target))
            {
                AppendToLog("TOOL_RESULT {\"status\":\"ignored\",\"reason\":\"invalid tool call\"}\n\n");
                return;
            }

            var serverDir = GetServerPath();
            if (string.IsNullOrWhiteSpace(serverDir))
            {
                AppendToLog("TOOL_RESULT {\"status\":\"error\",\"message\":\"server path not set. Click Save Path.\"}\n\n");
                return;
            }
            var filePath = call.target == "starter_pack"
                ? Path.Combine(serverDir, "TheStarterPack.txt")
                : Path.Combine(serverDir, "game_settings.txt");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            }
            catch { }

            // Backup
            var backup = filePath + ".bak";
            try { File.Copy(filePath, backup, true); } catch { }

            var lines = File.Exists(filePath)
                ? new List<string>(File.ReadAllLines(filePath))
                : new List<string>();
            int changes = 0;
            if (call.ops != null)
            {
                foreach (var op in call.ops)
                {
                    if (op?.type == "set" && !string.IsNullOrWhiteSpace(op.key))
                    {
                        var prefix = op.key + "=";
                        var idx = lines.FindIndex(l => l.StartsWith(prefix));
                        if (idx >= 0)
                        {
                            lines[idx] = prefix + (op.value ?? "");
                            changes++;
                        }
                        else
                        {
                            // append if not found
                            lines.Add(prefix + (op.value ?? ""));
                            changes++;
                        }
                    }
                }
            }

            try
            {
                File.WriteAllLines(filePath, lines);
                // Force flush to disk
                using (var fs = File.OpenRead(filePath))
                {
                    // Just opening for read ensures write completed
                }
                AppendToLog($"TOOL_RESULT {{\"status\":\"ok\",\"file\":\"{filePath.Replace("\\", "\\\\")}\",\"changes\":{changes}}}\n");
            }
            catch (Exception writeEx)
            {
                AppendToLog($"TOOL_RESULT {{\"status\":\"error\",\"message\":\"Failed to write file: {writeEx.Message}\"}}\n\n");
                return;
            }

            // Post-write verification preview
            string[] after;
            try { after = File.ReadAllLines(filePath); } catch { after = Array.Empty<string>(); }
            if (call.ops != null)
            {
                foreach (var op in call.ops)
                {
                    if (op?.type == "set" && !string.IsNullOrWhiteSpace(op.key))
                    {
                        var pref = op.key + "=";
                        var match = Array.Find(after, l => l.StartsWith(pref)) ?? string.Empty;
                        if (match.Length > 220) match = match.Substring(0, 220) + "...";
                        AppendToLog($"TOOL_VERIFY {op.key} -> {match}\n");
                    }
                }
            }
            AppendToLog("\n");
        }

        private string GetServerPath()
        {
            // Use global server path from installer
            var global = GlobalServerPath.Current;
            if (!string.IsNullOrWhiteSpace(global)) return global;

            // Fallback to auto-detected path
            var detected = Installer.TryFindTabgServerPath();
            if (!string.IsNullOrWhiteSpace(detected)) return detected;

            // Last resort: default next to exe
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, "TotallyAccurateBattlegroundsDedicatedServer");
        }


    }
}


