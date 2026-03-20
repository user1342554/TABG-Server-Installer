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
                _debounce = new Timer(_ => Dispatcher.Invoke(() => { try { LoadSettings(); } catch { } }), null, 500, Timeout.Infinite);
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
            }
            catch (Exception ex)
            {
                _saving = false;
                StatusText.Text = $"Error saving: {ex.Message}";
            }
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
