using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ModSettingsPanel : UserControl
    {
        private string _serverDir = "";
        private List<GameItem> _grenades = new();
        private FileSystemWatcher? _watcher;
        private Timer? _debounce;
        private bool _saving;

        public ModSettingsPanel()
        {
            InitializeComponent();
        }

        public void Initialize(string serverDir)
        {
            _serverDir = serverDir;

            // Populate grenade combo boxes
            _grenades = ItemDatabase.ByCategory("Grenades").OrderBy(g => g.Name).ToList();
            var grenadeDisplayItems = _grenades.Select(g => $"{g.Name} ({g.Id})").ToList();

            CmbAttackerGrenade.ItemsSource = grenadeDisplayItems;
            CmbCorpseGrenade.ItemsSource = grenadeDisplayItems;

            // Load configs
            LoadSettings();
            SetupWatcher();
        }

        private void SetupWatcher()
        {
            _watcher?.Dispose();
            var cfgDir = Path.Combine(_serverDir, "BepInEx", "config");
            if (!Directory.Exists(cfgDir)) return;

            _watcher = new FileSystemWatcher(cfgDir)
            {
                Filter = "*.cfg",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _watcher.Changed += (_, _) =>
            {
                if (_saving) return;
                _debounce?.Dispose();
                _debounce = new Timer(_ => Dispatcher.Invoke(() => { try { LoadSettings(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ModSettings] Operation failed: {ex.Message}"); } }), null, 500, Timeout.Infinite);
            };
        }

        private void LoadSettings()
        {
            try
            {
                var commission = ModConfigService.ReadCommission(_serverDir);
                var fixes = ModConfigService.ReadFixes(_serverDir);

                // StarterPack Fixes
                ChkEnableLootDrops.IsChecked = fixes.EnableLootDrops;

                // Grenades - Attacker
                ChkAttackerEnabled.IsChecked = commission.GrenadeAttackerEnabled;
                TxtAttackerChance.Text = commission.GrenadeAttackerChance.ToString(CultureInfo.InvariantCulture);
                SelectGrenadeById(CmbAttackerGrenade, commission.GrenadeAttackerId);

                // Grenades - Corpse
                ChkCorpseEnabled.IsChecked = commission.GrenadeCorpseEnabled;
                TxtCorpseChance.Text = commission.GrenadeCorpseChance.ToString(CultureInfo.InvariantCulture);
                SelectGrenadeById(CmbCorpseGrenade, commission.GrenadeCorpseId);

                // Lives & Advanced
                TxtLives.Text = commission.Lives.ToString();
                TxtStreamingDistance.Text = commission.StreamingDistance.ToString(CultureInfo.InvariantCulture);

                // Ban List: comma-separated -> one per line
                if (!string.IsNullOrWhiteSpace(commission.BanList))
                {
                    var ids = commission.BanList
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 0);
                    TxtBanList.Text = string.Join(Environment.NewLine, ids);
                }
                else
                {
                    TxtBanList.Text = "";
                }

                StatusText.Text = "Settings loaded";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading: {ex.Message}";
            }

            // Juggernaut Mode
            try
            {
                string juggCfgPath = Path.Combine(_serverDir, "BepInEx", "config", "com.gigaschmiga.juggernautmode.cfg");
                if (File.Exists(juggCfgPath))
                {
                    var lines = File.ReadAllLines(juggCfgPath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("#") || trimmed.StartsWith("[") || trimmed.Length == 0) continue;
                        if (trimmed.StartsWith("PointsToWin")) TxtJuggPointsToWin.Text = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("HP")) TxtJuggHP.Text = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("JuggernautKillBonus")) TxtJuggKillBonus.Text = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("JuggernautKillPoints")) TxtJuggKillPoints.Text = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("RegularKillPoints")) TxtJuggRegularKillPoints.Text = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("DamagePerPoint")) TxtJuggDamagePerPoint.Text = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("LoadoutChoices")) TxtJuggLoadoutChoices.Text = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("LoadoutTimeout")) TxtJuggLoadoutTimeout.Text = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("MinSpawnDistance")) TxtJuggMinSpawnDist.Text = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("MinPlayers")) TxtJuggMinPlayers.Text = ExtractCfgValue(trimmed);
                    }
                }
                else
                {
                    // Defaults
                    TxtJuggPointsToWin.Text = "100";
                    TxtJuggHP.Text = "1000";
                    TxtJuggKillBonus.Text = "5";
                    TxtJuggKillPoints.Text = "2";
                    TxtJuggRegularKillPoints.Text = "1";
                    TxtJuggDamagePerPoint.Text = "10";
                    TxtJuggLoadoutChoices.Text = "3";
                    TxtJuggLoadoutTimeout.Text = "10";
                    TxtJuggMinSpawnDist.Text = "50";
                    TxtJuggMinPlayers.Text = "3";
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ModSettings] Operation failed: {ex.Message}"); }

            try
            {
                string proxCfgPath = Path.Combine(_serverDir, "BepInEx", "config", "tabginstaller.proximitychat.server.cfg");
                if (File.Exists(proxCfgPath))
                {
                    var lines = File.ReadAllLines(proxCfgPath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("#") || trimmed.StartsWith("[") || trimmed.Length == 0) continue;
                        if (trimmed.StartsWith("MaxRange")) TxtProxChatMaxRange.Text = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("MinRange")) TxtProxChatMinRange.Text = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("FalloffCurve"))
                        {
                            string val = ExtractCfgValue(trimmed);
                            CmbProxChatFalloff.SelectedIndex = val == "Logarithmic" ? 1 : 0;
                        }
                        // VoicePort removed — voice now uses game network
                    }
                }
                else
                {
                    TxtProxChatMaxRange.Text = "50";
                    TxtProxChatMinRange.Text = "5";
                    CmbProxChatFalloff.SelectedIndex = 0;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ModSettings] Operation failed: {ex.Message}"); }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Read existing commission to preserve LoadoutCurses and LoadoutBlessings
                var commission = ModConfigService.ReadCommission(_serverDir);

                // StarterPack Fixes
                var fixes = new StarterPackFixesSettings
                {
                    EnableLootDrops = ChkEnableLootDrops.IsChecked == true
                };

                // Grenades - Attacker
                commission.GrenadeAttackerEnabled = ChkAttackerEnabled.IsChecked == true;
                if (float.TryParse(TxtAttackerChance.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var attackerChance))
                    commission.GrenadeAttackerChance = attackerChance;
                commission.GrenadeAttackerId = GetSelectedGrenadeId(CmbAttackerGrenade) ?? commission.GrenadeAttackerId;

                // Grenades - Corpse
                commission.GrenadeCorpseEnabled = ChkCorpseEnabled.IsChecked == true;
                if (float.TryParse(TxtCorpseChance.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var corpseChance))
                    commission.GrenadeCorpseChance = corpseChance;
                commission.GrenadeCorpseId = GetSelectedGrenadeId(CmbCorpseGrenade) ?? commission.GrenadeCorpseId;

                // Lives & Advanced
                if (int.TryParse(TxtLives.Text, out var lives))
                    commission.Lives = lives;
                if (float.TryParse(TxtStreamingDistance.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var sd))
                    commission.StreamingDistance = sd;

                // Ban List: one per line -> comma-separated
                var banLines = TxtBanList.Text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0);
                commission.BanList = string.Join(",", banLines);

                // Write both configs
                _saving = true;
                ModConfigService.WriteCommission(_serverDir, commission);
                ModConfigService.WriteFixes(_serverDir, fixes);
                _saving = false;

                StatusText.Text = "Settings saved";

                try
                {
                    string proxCfgDir = Path.Combine(_serverDir, "BepInEx", "config");
                    Directory.CreateDirectory(proxCfgDir);
                    string proxCfgPath = Path.Combine(proxCfgDir, "tabginstaller.proximitychat.server.cfg");

                    string falloff = CmbProxChatFalloff.SelectedIndex == 1 ? "Logarithmic" : "Linear";
                    string content = $@"[ProximityChat]

## Distance beyond which audio is not relayed
# Setting type: Single
# Default value: 50
MaxRange = {TxtProxChatMaxRange.Text.Trim()}

## Distance within which audio is full volume
# Setting type: Single
# Default value: 5
MinRange = {TxtProxChatMinRange.Text.Trim()}

## Volume falloff: Linear or Logarithmic
# Setting type: String
# Default value: Linear
FalloffCurve = {falloff}
";
                    File.WriteAllText(proxCfgPath, content);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ModSettings] Operation failed: {ex.Message}"); }

                // Juggernaut Mode
                try
                {
                    string juggCfgDir = Path.Combine(_serverDir, "BepInEx", "config");
                    Directory.CreateDirectory(juggCfgDir);
                    string juggCfgPath = Path.Combine(juggCfgDir, "com.gigaschmiga.juggernautmode.cfg");

                    string juggContent = $@"[Scoring]

## Points to win the match
# Setting type: Int32
# Default value: 100
PointsToWin = {TxtJuggPointsToWin.Text.Trim()}

## Points awarded per damage chunk
# Setting type: Int32
# Default value: 1
DamagePointsPerChunk = 1

## Damage required per point chunk
# Setting type: Int32
# Default value: 10
DamagePerPoint = {TxtJuggDamagePerPoint.Text.Trim()}

## Bonus points for killing the Juggernaut
# Setting type: Int32
# Default value: 5
JuggernautKillBonus = {TxtJuggKillBonus.Text.Trim()}

## Points Juggernaut earns per kill
# Setting type: Int32
# Default value: 2
JuggernautKillPoints = {TxtJuggKillPoints.Text.Trim()}

## Points for killing a regular player
# Setting type: Int32
# Default value: 1
RegularKillPoints = {TxtJuggRegularKillPoints.Text.Trim()}

[Juggernaut]

## Juggernaut health points
# Setting type: Single
# Default value: 1000
HP = {TxtJuggHP.Text.Trim()}

## Number of loadout choices on respawn
# Setting type: Int32
# Default value: 3
LoadoutChoices = {TxtJuggLoadoutChoices.Text.Trim()}

## Seconds before auto-picking a loadout
# Setting type: Single
# Default value: 10
LoadoutTimeout = {TxtJuggLoadoutTimeout.Text.Trim()}

## Minimum spawn distance from Juggernaut
# Setting type: Single
# Default value: 50
MinSpawnDistance = {TxtJuggMinSpawnDist.Text.Trim()}

[General]

## Minimum players to start
# Setting type: Int32
# Default value: 3
MinPlayers = {TxtJuggMinPlayers.Text.Trim()}
";
                    File.WriteAllText(juggCfgPath, juggContent);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ModSettings] Operation failed: {ex.Message}"); }
            }
            catch (Exception ex)
            {
                _saving = false;
                StatusText.Text = $"Error saving: {ex.Message}";
            }
        }

        private static string ExtractCfgValue(string line)
        {
            int eq = line.IndexOf('=');
            return eq >= 0 ? line.Substring(eq + 1).Trim() : "";
        }

        private void SelectGrenadeById(ComboBox combo, int id)
        {
            var index = _grenades.FindIndex(g => g.Id == id);
            combo.SelectedIndex = index >= 0 ? index : -1;
        }

        private int? GetSelectedGrenadeId(ComboBox combo)
        {
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < _grenades.Count)
                return _grenades[combo.SelectedIndex].Id;
            return null;
        }
    }
}
