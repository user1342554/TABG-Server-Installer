using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class ModSettingsViewModel : ObservableObject, IDisposable
    {
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IToastService _toast;

        private FileSystemWatcher? _watcher;
        private Timer? _debounce;
        private bool _saving;
        private bool _disposed;

        // ── StarterPack Fixes ────────────────────────────────────────────────────
        [ObservableProperty] private bool _enableLootDrops;

        // ── Grenades on Death - Attacker ─────────────────────────────────────────
        [ObservableProperty] private bool _attackerEnabled;
        [ObservableProperty] private string _attackerChance = "1";
        [ObservableProperty] private int _attackerGrenadeIndex = -1;

        // ── Grenades on Death - Corpse ───────────────────────────────────────────
        [ObservableProperty] private bool _corpseEnabled;
        [ObservableProperty] private string _corpseChance = "0.2";
        [ObservableProperty] private int _corpseGrenadeIndex = -1;

        // ── Lives & Advanced ─────────────────────────────────────────────────────
        [ObservableProperty] private string _lives = "256";
        [ObservableProperty] private string _streamingDistance = "-1";

        // ── Ban List ─────────────────────────────────────────────────────────────
        [ObservableProperty] private string _banList = "";

        // ── Proximity Chat ───────────────────────────────────────────────────────
        [ObservableProperty] private string _proxChatMaxRange = "50";
        [ObservableProperty] private string _proxChatMinRange = "5";
        [ObservableProperty] private int _proxChatFalloffIndex = 0; // 0=Linear, 1=Logarithmic

        // ── Juggernaut Mode ──────────────────────────────────────────────────────
        [ObservableProperty] private string _juggPointsToWin = "100";
        [ObservableProperty] private string _juggHP = "1000";
        [ObservableProperty] private string _juggKillBonus = "5";
        [ObservableProperty] private string _juggKillPoints = "2";
        [ObservableProperty] private string _juggRegularKillPoints = "1";
        [ObservableProperty] private string _juggDamagePerPoint = "10";
        [ObservableProperty] private string _juggLoadoutChoices = "3";
        [ObservableProperty] private string _juggLoadoutTimeout = "10";
        [ObservableProperty] private string _juggMinSpawnDist = "50";
        [ObservableProperty] private string _juggMinPlayers = "3";

        // ── Status ───────────────────────────────────────────────────────────────
        [ObservableProperty] private string _statusText = "";

        // ── Grenade list (for ComboBox binding) ──────────────────────────────────
        /// <summary>Flat display strings ("Name (Id)") for the two grenade combo boxes.</summary>
        public IReadOnlyList<string> GrenadeDisplayItems { get; }

        // Internal list kept in sync with GrenadeDisplayItems for index→id translation.
        private readonly List<GameItem> _grenades;

        // ── Constructor ──────────────────────────────────────────────────────────

        public ModSettingsViewModel(
            IServerPathProvider serverPathProvider,
            IToastService toast)
        {
            _serverPathProvider = serverPathProvider;
            _toast = toast;

            _grenades = ItemDatabase.ByCategory("Grenades").OrderBy(g => g.Name).ToList();
            GrenadeDisplayItems = _grenades.Select(g => $"{g.Name} ({g.Id})").ToList();

            _serverPathProvider.PathChanged += OnServerPathChanged;

            if (!string.IsNullOrEmpty(_serverPathProvider.ServerPath))
                Initialize(_serverPathProvider.ServerPath);
        }

        // ── Path change ──────────────────────────────────────────────────────────

        private void OnServerPathChanged()
        {
            var path = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(path)) return;
            Initialize(path);
        }

        internal void Initialize(string serverDir)
        {
            SetupWatcher(serverDir);
            LoadSettings(serverDir);
        }

        // ── Commands ─────────────────────────────────────────────────────────────

        [RelayCommand]
        private void Save()
        {
            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(serverDir))
            {
                _toast.Warning("Server path is not set.");
                return;
            }

            try
            {
                // Read existing commission to preserve LoadoutCurses and LoadoutBlessings
                var commission = ModConfigService.ReadCommission(serverDir);

                // StarterPack Fixes
                var fixes = new StarterPackFixesSettings
                {
                    EnableLootDrops = EnableLootDrops
                };

                // Grenades - Attacker
                commission.GrenadeAttackerEnabled = AttackerEnabled;
                if (float.TryParse(AttackerChance, NumberStyles.Float, CultureInfo.InvariantCulture, out var attackerChance))
                    commission.GrenadeAttackerChance = attackerChance;
                var attackerId = GetGrenadeIdByIndex(AttackerGrenadeIndex);
                if (attackerId.HasValue) commission.GrenadeAttackerId = attackerId.Value;

                // Grenades - Corpse
                commission.GrenadeCorpseEnabled = CorpseEnabled;
                if (float.TryParse(CorpseChance, NumberStyles.Float, CultureInfo.InvariantCulture, out var corpseChance))
                    commission.GrenadeCorpseChance = corpseChance;
                var corpseId = GetGrenadeIdByIndex(CorpseGrenadeIndex);
                if (corpseId.HasValue) commission.GrenadeCorpseId = corpseId.Value;

                // Lives & Advanced
                if (int.TryParse(Lives, out var lives))
                    commission.Lives = lives;
                if (float.TryParse(StreamingDistance, NumberStyles.Float, CultureInfo.InvariantCulture, out var sd))
                    commission.StreamingDistance = sd;

                // Ban List: one per line -> comma-separated
                var banLines = BanList
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0);
                commission.BanList = string.Join(",", banLines);

                // Write commission + fixes
                _saving = true;
                ModConfigService.WriteCommission(serverDir, commission);
                ModConfigService.WriteFixes(serverDir, fixes);
                _saving = false;

                StatusText = "Settings saved";

                // Proximity Chat cfg
                try
                {
                    string proxCfgDir = Path.Combine(serverDir, "BepInEx", "config");
                    Directory.CreateDirectory(proxCfgDir);
                    string proxCfgPath = Path.Combine(proxCfgDir, "tabginstaller.proximitychat.server.cfg");

                    string falloff = ProxChatFalloffIndex == 1 ? "Logarithmic" : "Linear";
                    string content = $@"[ProximityChat]

## Distance beyond which audio is not relayed
# Setting type: Single
# Default value: 50
MaxRange = {ProxChatMaxRange.Trim()}

## Distance within which audio is full volume
# Setting type: Single
# Default value: 5
MinRange = {ProxChatMinRange.Trim()}

## Volume falloff: Linear or Logarithmic
# Setting type: String
# Default value: Linear
FalloffCurve = {falloff}
";
                    File.WriteAllText(proxCfgPath, content);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ModSettingsVM] ProxChat save failed: {ex.Message}");
                }

                // Juggernaut Mode cfg
                try
                {
                    string juggCfgDir = Path.Combine(serverDir, "BepInEx", "config");
                    Directory.CreateDirectory(juggCfgDir);
                    string juggCfgPath = Path.Combine(juggCfgDir, "com.gigaschmiga.juggernautmode.cfg");

                    string juggContent = $@"[Scoring]

## Points to win the match
# Setting type: Int32
# Default value: 100
PointsToWin = {JuggPointsToWin.Trim()}

## Points awarded per damage chunk
# Setting type: Int32
# Default value: 1
DamagePointsPerChunk = 1

## Damage required per point chunk
# Setting type: Int32
# Default value: 10
DamagePerPoint = {JuggDamagePerPoint.Trim()}

## Bonus points for killing the Juggernaut
# Setting type: Int32
# Default value: 5
JuggernautKillBonus = {JuggKillBonus.Trim()}

## Points Juggernaut earns per kill
# Setting type: Int32
# Default value: 2
JuggernautKillPoints = {JuggKillPoints.Trim()}

## Points for killing a regular player
# Setting type: Int32
# Default value: 1
RegularKillPoints = {JuggRegularKillPoints.Trim()}

[Juggernaut]

## Juggernaut health points
# Setting type: Single
# Default value: 1000
HP = {JuggHP.Trim()}

## Number of loadout choices on respawn
# Setting type: Int32
# Default value: 3
LoadoutChoices = {JuggLoadoutChoices.Trim()}

## Seconds before auto-picking a loadout
# Setting type: Single
# Default value: 10
LoadoutTimeout = {JuggLoadoutTimeout.Trim()}

## Minimum spawn distance from Juggernaut
# Setting type: Single
# Default value: 50
MinSpawnDistance = {JuggMinSpawnDist.Trim()}

[General]

## Minimum players to start
# Setting type: Int32
# Default value: 3
MinPlayers = {JuggMinPlayers.Trim()}
";
                    File.WriteAllText(juggCfgPath, juggContent);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ModSettingsVM] Juggernaut save failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _saving = false;
                StatusText = $"Error saving: {ex.Message}";
            }
        }

        // ── Internal helpers ─────────────────────────────────────────────────────

        private void LoadSettings(string serverDir)
        {
            try
            {
                var commission = ModConfigService.ReadCommission(serverDir);
                var fixes = ModConfigService.ReadFixes(serverDir);

                // StarterPack Fixes
                EnableLootDrops = fixes.EnableLootDrops;

                // Grenades - Attacker
                AttackerEnabled = commission.GrenadeAttackerEnabled;
                AttackerChance = commission.GrenadeAttackerChance.ToString(CultureInfo.InvariantCulture);
                AttackerGrenadeIndex = IndexOfGrenadeById(commission.GrenadeAttackerId);

                // Grenades - Corpse
                CorpseEnabled = commission.GrenadeCorpseEnabled;
                CorpseChance = commission.GrenadeCorpseChance.ToString(CultureInfo.InvariantCulture);
                CorpseGrenadeIndex = IndexOfGrenadeById(commission.GrenadeCorpseId);

                // Lives & Advanced
                Lives = commission.Lives.ToString();
                StreamingDistance = commission.StreamingDistance.ToString(CultureInfo.InvariantCulture);

                // Ban List: comma-separated -> one per line
                if (!string.IsNullOrWhiteSpace(commission.BanList))
                {
                    var ids = commission.BanList
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 0);
                    BanList = string.Join(Environment.NewLine, ids);
                }
                else
                {
                    BanList = "";
                }

                StatusText = "Settings loaded";
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading: {ex.Message}";
            }

            // Juggernaut Mode
            try
            {
                string juggCfgPath = Path.Combine(serverDir, "BepInEx", "config", "com.gigaschmiga.juggernautmode.cfg");
                if (File.Exists(juggCfgPath))
                {
                    var lines = File.ReadAllLines(juggCfgPath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("#") || trimmed.StartsWith("[") || trimmed.Length == 0) continue;
                        if (trimmed.StartsWith("PointsToWin")) JuggPointsToWin = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("HP")) JuggHP = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("JuggernautKillBonus")) JuggKillBonus = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("JuggernautKillPoints")) JuggKillPoints = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("RegularKillPoints")) JuggRegularKillPoints = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("DamagePerPoint")) JuggDamagePerPoint = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("LoadoutChoices")) JuggLoadoutChoices = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("LoadoutTimeout")) JuggLoadoutTimeout = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("MinSpawnDistance")) JuggMinSpawnDist = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("MinPlayers")) JuggMinPlayers = ExtractCfgValue(trimmed);
                    }
                }
                // else: defaults are already set as field initializers
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModSettingsVM] Juggernaut load failed: {ex.Message}");
            }

            // Proximity Chat
            try
            {
                string proxCfgPath = Path.Combine(serverDir, "BepInEx", "config", "tabginstaller.proximitychat.server.cfg");
                if (File.Exists(proxCfgPath))
                {
                    var lines = File.ReadAllLines(proxCfgPath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("#") || trimmed.StartsWith("[") || trimmed.Length == 0) continue;
                        if (trimmed.StartsWith("MaxRange")) ProxChatMaxRange = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("MinRange")) ProxChatMinRange = ExtractCfgValue(trimmed);
                        else if (trimmed.StartsWith("FalloffCurve"))
                            ProxChatFalloffIndex = ExtractCfgValue(trimmed) == "Logarithmic" ? 1 : 0;
                    }
                }
                // else: defaults already set
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModSettingsVM] ProxChat load failed: {ex.Message}");
            }
        }

        private void SetupWatcher(string serverDir)
        {
            _watcher?.Dispose();
            _watcher = null;

            var cfgDir = Path.Combine(serverDir, "BepInEx", "config");
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
                _debounce = new Timer(_ =>
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        try { LoadSettings(serverDir); }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[ModSettingsVM] Debounce reload failed: {ex.Message}");
                        }
                    });
                }, null, 500, Timeout.Infinite);
            };
        }

        // ── Static helpers ───────────────────────────────────────────────────────

        public static string ExtractCfgValue(string line)
        {
            int eq = line.IndexOf('=');
            return eq >= 0 ? line.Substring(eq + 1).Trim() : "";
        }

        private int IndexOfGrenadeById(int id)
        {
            var index = _grenades.FindIndex(g => g.Id == id);
            return index >= 0 ? index : -1;
        }

        private int? GetGrenadeIdByIndex(int index)
        {
            if (index >= 0 && index < _grenades.Count)
                return _grenades[index].Id;
            return null;
        }

        // ── IDisposable ──────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _serverPathProvider.PathChanged -= OnServerPathChanged;
            _debounce?.Dispose();
            _watcher?.Dispose();
        }
    }
}
