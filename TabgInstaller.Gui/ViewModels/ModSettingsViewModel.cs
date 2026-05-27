using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Resources;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class ModSettingsViewModel : ObservableObject, IDisposable
    {
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IToastService _toast;
        private readonly IAppSettingsService? _appSettings;

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

        // ── Server Logger ───────────────────────────────────────────────────────
        [ObservableProperty] private bool _serverLoggerLogToConsole = true;
        [ObservableProperty] private bool _serverLoggerWriteCsv = true;
        [ObservableProperty] private bool _serverLoggerWriteLegacy = true;
        [ObservableProperty] private bool _serverLoggerFallbackScan = true;
        [ObservableProperty] private string _serverLoggerFallbackInterval = "2";
        [ObservableProperty] private string _serverLoggerLogDirectory = "server-logs";
        [ObservableProperty] private string _serverLoggerCsvFileName = "players.csv";
        [ObservableProperty] private string _serverLoggerLegacyFileName = "ServerLogger.txt";

        // ── Status ───────────────────────────────────────────────────────────────
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private ObservableCollection<PluginSettingsGroupViewModel> _additionalServerPluginSettings = new();
        [ObservableProperty] private ObservableCollection<PluginSettingsGroupViewModel> _clientPluginSettings = new();
        [ObservableProperty] private string _clientPluginSettingsStatus = "Set up a modded client copy to edit client plugin settings here.";

        // ── Grenade list (for ComboBox binding) ──────────────────────────────────
        /// <summary>Flat display strings ("Name (Id)") for the two grenade combo boxes.</summary>
        public IReadOnlyList<string> GrenadeDisplayItems { get; }

        // Internal list kept in sync with GrenadeDisplayItems for index→id translation.
        private readonly List<GameItem> _grenades;

        // ── Constructor ──────────────────────────────────────────────────────────

        public ModSettingsViewModel(
            IServerPathProvider serverPathProvider,
            IToastService toast)
            : this(serverPathProvider, toast, null)
        {
        }

        public ModSettingsViewModel(
            IServerPathProvider serverPathProvider,
            IToastService toast,
            IAppSettingsService? appSettings)
        {
            _serverPathProvider = serverPathProvider;
            _toast = toast;
            _appSettings = appSettings;

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
                _toast.Warning(Messages.ServerPathNotSet);
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

                // Server Logger cfg
                try
                {
                    var loggerSettings = ModConfigService.ReadServerLogger(serverDir);
                    loggerSettings.LogToBepInExConsole = ServerLoggerLogToConsole;
                    loggerSettings.WriteCsv = ServerLoggerWriteCsv;
                    loggerSettings.WriteLegacyServerLoggerTxt = ServerLoggerWriteLegacy;
                    loggerSettings.FallbackPlayerScan = ServerLoggerFallbackScan;
                    if (float.TryParse(ServerLoggerFallbackInterval, NumberStyles.Float, CultureInfo.InvariantCulture, out var scanInterval))
                        loggerSettings.FallbackScanIntervalSeconds = scanInterval;
                    loggerSettings.LogDirectory = ServerLoggerLogDirectory?.Trim() ?? "server-logs";
                    loggerSettings.CsvFileName = ServerLoggerCsvFileName?.Trim() ?? "players.csv";
                    loggerSettings.LegacyFileName = ServerLoggerLegacyFileName?.Trim() ?? "ServerLogger.txt";
                    ModConfigService.WriteServerLogger(serverDir, loggerSettings);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ModSettingsVM] ServerLogger save failed: {ex.Message}");
                }

                SavePluginSettings(AdditionalServerPluginSettings);
                SavePluginSettings(ClientPluginSettings);
                _saving = false;

                StatusText = Messages.SettingsSaved;
            }
            catch (Exception ex)
            {
                _saving = false;
                StatusText = string.Format(Messages.ErrorSaving, ex.Message);
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

                StatusText = Messages.SettingsLoaded;
            }
            catch (Exception ex)
            {
                StatusText = string.Format(Messages.ErrorLoading, ex.Message);
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

            // Server Logger
            try
            {
                var loggerSettings = ModConfigService.ReadServerLogger(serverDir);
                ServerLoggerLogToConsole = loggerSettings.LogToBepInExConsole;
                ServerLoggerWriteCsv = loggerSettings.WriteCsv;
                ServerLoggerWriteLegacy = loggerSettings.WriteLegacyServerLoggerTxt;
                ServerLoggerFallbackScan = loggerSettings.FallbackPlayerScan;
                ServerLoggerFallbackInterval = loggerSettings.FallbackScanIntervalSeconds.ToString(CultureInfo.InvariantCulture);
                ServerLoggerLogDirectory = loggerSettings.LogDirectory;
                ServerLoggerCsvFileName = loggerSettings.CsvFileName;
                ServerLoggerLegacyFileName = loggerSettings.LegacyFileName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModSettingsVM] ServerLogger load failed: {ex.Message}");
            }

            LoadPluginSettings(serverDir);
        }

        [RelayCommand]
        private void OpenServerLoggerConfig()
        {
            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrWhiteSpace(serverDir))
            {
                _toast.Warning(Messages.ServerPathNotSet);
                return;
            }

            OpenPath(ModConfigService.ServerLoggerConfigPath(serverDir), createParent: true);
        }

        [RelayCommand]
        private void OpenServerLoggerCsv()
        {
            if (string.IsNullOrWhiteSpace(_serverPathProvider.ServerPath))
            {
                _toast.Warning(Messages.ServerPathNotSet);
                return;
            }

            var settings = BuildServerLoggerSettingsFromFields();
            OpenPath(ModConfigService.GetServerLoggerCsvPath(_serverPathProvider.ServerPath, settings), createParent: true);
        }

        [RelayCommand]
        private void OpenServerLoggerLegacy()
        {
            if (string.IsNullOrWhiteSpace(_serverPathProvider.ServerPath))
            {
                _toast.Warning(Messages.ServerPathNotSet);
                return;
            }

            var settings = BuildServerLoggerSettingsFromFields();
            OpenPath(ModConfigService.GetServerLoggerLegacyPath(_serverPathProvider.ServerPath, settings), createParent: true);
        }

        private ServerLoggerSettings BuildServerLoggerSettingsFromFields()
        {
            var settings = ModConfigService.ReadServerLogger(_serverPathProvider.ServerPath);
            settings.LogDirectory = ServerLoggerLogDirectory?.Trim() ?? "server-logs";
            settings.CsvFileName = ServerLoggerCsvFileName?.Trim() ?? "players.csv";
            settings.LegacyFileName = ServerLoggerLegacyFileName?.Trim() ?? "ServerLogger.txt";
            return settings;
        }

        private void OpenPath(string path, bool createParent)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                var parent = Path.GetDirectoryName(path);
                if (createParent && !string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);

                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                else if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                {
                    Process.Start("explorer", parent);
                }
                else
                {
                    _toast.Warning(Messages.ServerPathNotSet);
                }
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.CouldNotOpenFile, ex.Message));
            }
        }

        [RelayCommand]
        private void OpenPluginConfig(PluginSettingsGroupViewModel? group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.ConfigPath))
                return;

            OpenPath(group.ConfigPath, createParent: true);
        }

        private void LoadPluginSettings(string serverDir)
        {
            AdditionalServerPluginSettings = BuildPluginSettingsGroups(
                PluginSettingsCatalog.AdditionalServerPlugins,
                serverDir);

            var clientDir = _appSettings?.Load().ClientModdedPath ?? "";
            if (string.IsNullOrWhiteSpace(clientDir))
            {
                ClientPluginSettings = new ObservableCollection<PluginSettingsGroupViewModel>();
                ClientPluginSettingsStatus = "Set up a modded client copy to edit client plugin settings here.";
                return;
            }

            if (!Directory.Exists(clientDir))
            {
                ClientPluginSettings = new ObservableCollection<PluginSettingsGroupViewModel>();
                ClientPluginSettingsStatus = $"Client modded copy not found: {clientDir}";
                return;
            }

            ClientPluginSettings = BuildPluginSettingsGroups(PluginSettingsCatalog.ClientPlugins, clientDir);
            ClientPluginSettingsStatus = "";
        }

        private static ObservableCollection<PluginSettingsGroupViewModel> BuildPluginSettingsGroups(
            IEnumerable<PluginConfigDefinition> definitions,
            string rootPath)
        {
            var groups = new ObservableCollection<PluginSettingsGroupViewModel>();
            foreach (var definition in definitions)
            {
                var values = string.IsNullOrWhiteSpace(rootPath)
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : ModConfigService.ReadPluginConfigValues(rootPath, definition);

                var settings = new ObservableCollection<PluginSettingValueViewModel>(
                    definition.Settings.Select(setting =>
                    {
                        var value = values.TryGetValue(setting.FullKey, out var configured)
                            ? configured
                            : setting.DefaultValue;
                        return new PluginSettingValueViewModel(setting, value);
                    }));

                groups.Add(new PluginSettingsGroupViewModel(definition, rootPath, settings));
            }

            return groups;
        }

        private static void SavePluginSettings(IEnumerable<PluginSettingsGroupViewModel> groups)
        {
            foreach (var group in groups)
            {
                if (!group.HasSettings || string.IsNullOrWhiteSpace(group.RootPath))
                    continue;

                var values = group.Settings.ToDictionary(
                    setting => setting.Definition.FullKey,
                    setting => setting.Value ?? "",
                    StringComparer.OrdinalIgnoreCase);
                ModConfigService.WritePluginConfigValues(group.RootPath, group.Definition, values);
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
