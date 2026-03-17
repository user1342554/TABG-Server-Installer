using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class MatchSettingsPanel : UserControl
    {
        private string _serverDir = "";
        private StarterPackTextSettings? _settings;
        private FileSystemWatcher? _watcher;
        private Timer? _debounce;
        private bool _saving;

        public MatchSettingsPanel()
        {
            InitializeComponent();
        }

        public void Initialize(string serverDir)
        {
            _serverDir = serverDir;
            LoadSettings();
            SetupWatcher();
        }

        private void SetupWatcher()
        {
            _watcher?.Dispose();
            var path = Path.Combine(_serverDir, "TheStarterPack.txt");
            var dir = Path.GetDirectoryName(path);
            if (dir == null || !Directory.Exists(dir)) return;

            _watcher = new FileSystemWatcher(dir)
            {
                Filter = "TheStarterPack.txt",
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
                _settings = StarterPackConfigService.Read(_serverDir);

                // Loadout mode
                var mode = _settings.GetLoadoutMode();
                SelectComboItem(CmbLoadoutMode, mode);

                // Win condition
                SelectComboItem(CmbWinCondition, _settings.WinCondition);
                TxtKillsToWin.Text = _settings.KillsToWin?.ToString() ?? "";
                UpdateKillsToWinVisibility();

                // Match mode checkboxes
                ChkForceKillAtStart.IsChecked = _settings.ForceKillAtStart;
                ChkHealOnKill.IsChecked = _settings.HealOnKill;
                TxtHealOnKillAmount.Text = _settings.HealOnKillAmount.ToString(CultureInfo.InvariantCulture);
                ChkDropItemsOnDeath.IsChecked = _settings.DropItemsOnDeath;
                ChkCanGoDown.IsChecked = _settings.CanGoDown;
                ChkCanLockOut.IsChecked = _settings.CanLockOut;

                // Vote
                TxtPercentOfVotes.Text = _settings.PercentOfVotes.ToString();
                TxtMinPlayers.Text = _settings.MinNumberOfPlayers.ToString();
                TxtTimeToStart.Text = _settings.TimeToStart.ToString();

                // Spell drops
                ChkSpelldropEnabled.IsChecked = _settings.SpelldropEnabled;
                TxtMinSpellDropDelay.Text = _settings.MinSpellDropDelay.ToString();
                TxtMaxSpellDropDelay.Text = _settings.MaxSpellDropDelay.ToString();
                TxtSpellDropOffset.Text = _settings.SpellDropOffset.ToString();

                // Timeouts
                TxtPreMatchTimeout.Text = _settings.PreMatchTimeout.ToString(CultureInfo.InvariantCulture);
                TxtPeriMatchTimeout.Text = _settings.PeriMatchTimeout.ToString(CultureInfo.InvariantCulture);

                StatusText.Text = "Settings loaded.";
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
                if (_settings == null)
                    _settings = new StarterPackTextSettings();

                // Loadout mode - preserve loadout content, change prefix
                var selectedMode = GetComboText(CmbLoadoutMode);
                var loadoutsWithoutPrefix = _settings.GetLoadoutsWithoutPrefix();
                _settings.SetLoadoutsWithPrefix(selectedMode, loadoutsWithoutPrefix);

                // Win condition
                _settings.WinCondition = GetComboText(CmbWinCondition);
                if (int.TryParse(TxtKillsToWin.Text.Trim(), out var ktw))
                    _settings.KillsToWin = ktw;
                else
                    _settings.KillsToWin = null;

                // Match mode
                _settings.ForceKillAtStart = ChkForceKillAtStart.IsChecked == true;
                _settings.HealOnKill = ChkHealOnKill.IsChecked == true;
                if (float.TryParse(TxtHealOnKillAmount.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var healAmt))
                    _settings.HealOnKillAmount = healAmt;
                _settings.DropItemsOnDeath = ChkDropItemsOnDeath.IsChecked == true;
                _settings.CanGoDown = ChkCanGoDown.IsChecked == true;
                _settings.CanLockOut = ChkCanLockOut.IsChecked == true;

                // Vote
                if (int.TryParse(TxtPercentOfVotes.Text.Trim(), out var pov))
                    _settings.PercentOfVotes = pov;
                if (int.TryParse(TxtMinPlayers.Text.Trim(), out var mnp))
                    _settings.MinNumberOfPlayers = mnp;
                if (int.TryParse(TxtTimeToStart.Text.Trim(), out var tts))
                    _settings.TimeToStart = tts;

                // Spell drops
                _settings.SpelldropEnabled = ChkSpelldropEnabled.IsChecked == true;
                if (int.TryParse(TxtMinSpellDropDelay.Text.Trim(), out var minsd))
                    _settings.MinSpellDropDelay = minsd;
                if (int.TryParse(TxtMaxSpellDropDelay.Text.Trim(), out var maxsd))
                    _settings.MaxSpellDropDelay = maxsd;
                if (int.TryParse(TxtSpellDropOffset.Text.Trim(), out var sdo))
                    _settings.SpellDropOffset = sdo;

                // Timeouts
                if (float.TryParse(TxtPreMatchTimeout.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var pmt))
                    _settings.PreMatchTimeout = pmt;
                if (float.TryParse(TxtPeriMatchTimeout.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var pmtt))
                    _settings.PeriMatchTimeout = pmtt;

                _saving = true;
                StarterPackConfigService.Write(_serverDir, _settings);
                _saving = false;
                StatusText.Text = "Settings saved.";
            }
            catch (Exception ex)
            {
                _saving = false;
                StatusText.Text = $"Error saving: {ex.Message}";
            }
        }

        private void CmbWinCondition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateKillsToWinVisibility();
        }

        private void UpdateKillsToWinVisibility()
        {
            if (KillsToWinRow == null) return;
            var selected = GetComboText(CmbWinCondition);
            KillsToWinRow.Visibility = selected == "KillsToWin"
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private static void SelectComboItem(ComboBox combo, string value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            // If not found, select first item
            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private static string GetComboText(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem item)
                return item.Content?.ToString() ?? "";
            return "";
        }
    }
}
