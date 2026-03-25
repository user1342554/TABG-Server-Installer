using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class LoadoutEditorPanel : UserControl
    {
        private string _serverDir = "";
        private readonly StarterPackLoadoutService _loadoutSvc = new();
        private readonly ObservableCollection<LoadoutViewModel> _loadouts = new();
        private bool _suppressSelectionChange;
        private bool _suppressDirty;
        private FreddoCommissionSettings? _commissionSettings;
        private FileSystemWatcher? _watcherRoot;
        private FileSystemWatcher? _watcherCfg;
        private Timer? _debounce;
        private bool _saving;
        private bool _dirty;

        public LoadoutEditorPanel()
        {
            InitializeComponent();
            LstLoadouts.ItemsSource = _loadouts;

            // Populate category combo
            foreach (var cat in ItemDatabase.Categories)
                CmbCategory.Items.Add(cat);
            if (CmbCategory.Items.Count > 0)
                CmbCategory.SelectedIndex = 0;
        }

        public void Initialize(string serverDir)
        {
            _serverDir = serverDir;
            LoadAll();
            SetupWatchers();
        }

        private void MarkDirty()
        {
            if (_suppressDirty) return;
            if (!_dirty)
            {
                _dirty = true;
                TxtStatus.Text = "Unsaved changes";
            }
        }

        private void ClearDirty()
        {
            _dirty = false;
        }

        private void SetupWatchers()
        {
            _watcherRoot?.Dispose();
            _watcherCfg?.Dispose();

            void OnChange(object? s, FileSystemEventArgs e)
            {
                if (_saving) return;
                _debounce?.Dispose();
                _debounce = new Timer(_ => Dispatcher.Invoke(() => { try { LoadAll(); } catch { } }), null, 500, Timeout.Infinite);
            }

            if (Directory.Exists(_serverDir))
            {
                _watcherRoot = new FileSystemWatcher(_serverDir)
                {
                    Filter = "TheStarterPack.txt",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                _watcherRoot.Changed += OnChange;
            }

            var cfgDir = Path.Combine(_serverDir, "BepInEx", "config");
            if (Directory.Exists(cfgDir))
            {
                _watcherCfg = new FileSystemWatcher(cfgDir)
                {
                    Filter = "FreddoTABGCommission.cfg",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                _watcherCfg.Changed += OnChange;
            }
        }

        // ── Loading ──

        private void LoadAll()
        {
            try
            {
                _suppressDirty = true;
                var settings = StarterPackConfigService.Read(_serverDir);
                var mode = settings.GetLoadoutMode();
                SetModeCombo(mode);

                var rawLoadouts = settings.GetLoadoutsWithoutPrefix();
                var parsed = _loadoutSvc.ParseLoadoutsValue(rawLoadouts);

                // Read curses
                _commissionSettings = ModConfigService.ReadCommission(_serverDir);
                var perLoadoutCurses = ParseLoadoutCurses(_commissionSettings.LoadoutCurses);

                _loadouts.Clear();
                for (int i = 0; i < parsed.Count; i++)
                {
                    var lo = parsed[i];
                    var curses = i < perLoadoutCurses.Count ? perLoadoutCurses[i] : new HashSet<int>();
                    _loadouts.Add(new LoadoutViewModel(lo.Name, lo.Percent, lo.Items, curses));
                }

                if (_loadouts.Count > 0)
                    LstLoadouts.SelectedIndex = 0;

                ClearDirty();
                _suppressDirty = false;
                TxtStatus.Text = $"Loaded {_loadouts.Count} loadouts.";
            }
            catch (Exception ex)
            {
                _suppressDirty = false;
                TxtStatus.Text = $"Load error: {ex.Message}";
            }
        }

        private void SetModeCombo(string mode)
        {
            var idx = mode switch
            {
                "GunGame" => 1,
                "ReverseGunGame" => 2,
                "KeepInventory" => 3,
                _ => 0
            };
            CmbMode.SelectedIndex = idx;
        }

        private string GetSelectedMode()
        {
            return CmbMode.SelectedIndex switch
            {
                1 => "GunGame",
                2 => "ReverseGunGame",
                3 => "KeepInventory",
                _ => "Normal"
            };
        }

        // ── Mode Changed ──

        private void CmbMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MarkDirty();
        }

        // ── Loadout Curse Parsing ──

        private static List<HashSet<int>> ParseLoadoutCurses(string raw)
        {
            var result = new List<HashSet<int>>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            var groups = raw.Split('/', StringSplitOptions.None);
            foreach (var group in groups)
            {
                var set = new HashSet<int>();
                if (!string.IsNullOrWhiteSpace(group))
                {
                    foreach (var tok in group.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (int.TryParse(tok, out var id))
                            set.Add(id);
                    }
                }
                result.Add(set);
            }
            return result;
        }

        private string BuildLoadoutCurses()
        {
            var parts = new List<string>();
            foreach (var lo in _loadouts)
            {
                if (lo.Curses.Count > 0)
                    parts.Add(string.Join(",", lo.Curses.OrderBy(c => c)));
                else
                    parts.Add("");
            }

            // Trim trailing empty groups
            while (parts.Count > 0 && string.IsNullOrEmpty(parts[^1]))
                parts.RemoveAt(parts.Count - 1);

            return string.Join("/", parts);
        }

        // ── Saving ──

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = StarterPackConfigService.Read(_serverDir);
                var mode = GetSelectedMode();

                // Build loadout records
                var records = _loadouts.Select(lo =>
                    new StarterPackLoadoutService.Loadout(
                        lo.Name,
                        lo.Percent,
                        lo.Items.Select(it => new StarterPackLoadoutService.Item(it.Id, it.Quantity)).ToList()
                    )).ToList();

                var loadoutsStr = _loadoutSvc.BuildLoadoutsValue(records);
                settings.SetLoadoutsWithPrefix(mode, loadoutsStr);
                _saving = true;
                StarterPackConfigService.Write(_serverDir, settings);

                // Save curses
                var commission = _commissionSettings ?? ModConfigService.ReadCommission(_serverDir);
                commission.LoadoutCurses = BuildLoadoutCurses();
                ModConfigService.WriteCommission(_serverDir, commission);
                _saving = false;

                ClearDirty();
                TxtStatus.Text = "Saved successfully.";
            }
            catch (Exception ex)
            {
                _saving = false;
                TxtStatus.Text = $"Save error: {ex.Message}";
            }
        }

        // ── Import / Export ──

        private void ImportRaw_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Window
            {
                Title = "Import Raw Loadouts",
                Width = 600,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            var sp = new StackPanel { Margin = new Thickness(8) };
            sp.Children.Add(new TextBlock
            {
                Text = "Paste the raw loadout string (including optional GunGame/ prefix):",
                Margin = new Thickness(0, 0, 0, 4)
            });
            var txt = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 180
            };
            sp.Children.Add(txt);
            var btn = new Button { Content = "Import", Width = 80, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            btn.Click += (_, _) =>
            {
                var raw = txt.Text.Trim();
                if (string.IsNullOrEmpty(raw))
                {
                    ToastService.Instance.Warning("No text entered.");
                    return;
                }

                // Detect mode prefix
                string mode = "Normal";
                string body = raw;
                if (raw.StartsWith("GunGame/")) { mode = "GunGame"; body = raw.Substring("GunGame/".Length); }
                else if (raw.StartsWith("ReverseGunGame/")) { mode = "ReverseGunGame"; body = raw.Substring("ReverseGunGame/".Length); }
                else if (raw.StartsWith("KeepInventory/")) { mode = "KeepInventory"; body = raw.Substring("KeepInventory/".Length); }

                SetModeCombo(mode);
                var parsed = _loadoutSvc.ParseLoadoutsValue(body);
                _loadouts.Clear();
                foreach (var lo in parsed)
                    _loadouts.Add(new LoadoutViewModel(lo.Name, lo.Percent, lo.Items, new HashSet<int>()));

                if (_loadouts.Count > 0)
                    LstLoadouts.SelectedIndex = 0;

                MarkDirty();
                TxtStatus.Text = $"Imported {_loadouts.Count} loadouts.";
                dlg.Close();
            };
            sp.Children.Add(btn);
            dlg.Content = sp;
            dlg.ShowDialog();
        }

        private void ExportRaw_Click(object sender, RoutedEventArgs e)
        {
            var records = _loadouts.Select(lo =>
                new StarterPackLoadoutService.Loadout(
                    lo.Name,
                    lo.Percent,
                    lo.Items.Select(it => new StarterPackLoadoutService.Item(it.Id, it.Quantity)).ToList()
                )).ToList();

            var body = _loadoutSvc.BuildLoadoutsValue(records);
            var mode = GetSelectedMode();
            var full = mode == "Normal" ? body : mode + "/" + body;

            var dlg = new Window
            {
                Title = "Export Raw Loadouts",
                Width = 600,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            var sp = new StackPanel { Margin = new Thickness(8) };
            sp.Children.Add(new TextBlock
            {
                Text = "Raw loadout string (copy this):",
                Margin = new Thickness(0, 0, 0, 4)
            });
            var txt = new TextBox
            {
                Text = full,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 180,
                IsReadOnly = true
            };
            sp.Children.Add(txt);
            var btnCopy = new Button { Content = "Copy to Clipboard", Width = 130, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            btnCopy.Click += (_, _) =>
            {
                Clipboard.SetText(full);
                TxtStatus.Text = "Copied to clipboard.";
            };
            sp.Children.Add(btnCopy);
            dlg.Content = sp;
            dlg.ShowDialog();
        }

        // ── Loadout List Management ──

        private void AddLoadout_Click(object sender, RoutedEventArgs e)
        {
            var lo = new LoadoutViewModel("New Loadout", 100, new List<StarterPackLoadoutService.Item>(), new HashSet<int>());
            _loadouts.Add(lo);
            LstLoadouts.SelectedItem = lo;
            MarkDirty();
        }

        private void DuplicateLoadout_Click(object sender, RoutedEventArgs e)
        {
            if (LstLoadouts.SelectedItem is not LoadoutViewModel source) return;

            // Deep copy items and curses
            var clonedItems = source.Items.Select(it =>
                new StarterPackLoadoutService.Item(it.Id, it.Quantity)).ToList();
            var clonedCurses = new HashSet<int>(source.Curses);

            var clone = new LoadoutViewModel(
                source.Name + " (Copy)",
                source.Percent,
                clonedItems,
                clonedCurses);

            var idx = _loadouts.IndexOf(source);
            _loadouts.Insert(idx + 1, clone);
            LstLoadouts.SelectedItem = clone;
            MarkDirty();
        }

        private void RemoveLoadout_Click(object sender, RoutedEventArgs e)
        {
            if (LstLoadouts.SelectedItem is LoadoutViewModel lo)
            {
                var result = MessageBox.Show(
                    $"Remove loadout \"{lo.Name}\"?",
                    "Confirm Remove",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                var idx = _loadouts.IndexOf(lo);
                _loadouts.Remove(lo);
                if (_loadouts.Count > 0)
                    LstLoadouts.SelectedIndex = Math.Min(idx, _loadouts.Count - 1);
                MarkDirty();
            }
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            var idx = LstLoadouts.SelectedIndex;
            if (idx > 0)
            {
                _loadouts.Move(idx, idx - 1);
                LstLoadouts.SelectedIndex = idx - 1;
                MarkDirty();
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            var idx = LstLoadouts.SelectedIndex;
            if (idx >= 0 && idx < _loadouts.Count - 1)
            {
                _loadouts.Move(idx, idx + 1);
                LstLoadouts.SelectedIndex = idx + 1;
                MarkDirty();
            }
        }

        // ── Selection Changed ──

        private void LstLoadouts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChange) return;

            var lo = LstLoadouts.SelectedItem as LoadoutViewModel;
            PnlEditor.IsEnabled = lo != null;
            if (lo == null)
            {
                TxtName.Text = "";
                TxtPercent.Text = "";
                DgItems.ItemsSource = null;
                IcCurses.ItemsSource = null;
                IcBlessings.ItemsSource = null;
                return;
            }

            _suppressSelectionChange = true;
            TxtName.Text = lo.Name;
            TxtPercent.Text = lo.Percent.ToString();
            RefreshItemsGrid(lo);
            RefreshCurseCheckboxes(lo);
            _suppressSelectionChange = false;
        }

        private void RefreshItemsGrid(LoadoutViewModel lo)
        {
            var display = lo.Items.Select(it => new ItemDisplayRow
            {
                Id = it.Id,
                Quantity = it.Quantity,
                ItemName = ResolveItemName(it.Id)
            }).ToList();
            DgItems.ItemsSource = display;
        }

        private void RefreshCurseCheckboxes(LoadoutViewModel lo)
        {
            var entries = ItemDatabase.Curses.Select(c => new CurseCheckEntry
            {
                CurseId = c.Id,
                DisplayText = $"{c.Name} ({c.Id})",
                IsChecked = lo.Curses.Contains(c.Id)
            }).ToList();

            foreach (var entry in entries)
            {
                entry.PropertyChanged += (s, _) =>
                {
                    if (s is CurseCheckEntry ce)
                    {
                        if (ce.IsChecked)
                            lo.Curses.Add(ce.CurseId);
                        else
                            lo.Curses.Remove(ce.CurseId);
                        MarkDirty();
                    }
                };
            }

            IcCurses.ItemsSource = entries;

            // Blessing checkboxes — blessings are items in the loadout
            var blessingItems = ItemDatabase.ByCategory("Blessings").ToList();
            var existingItemIds = lo.Items.Select(it => it.Id).ToHashSet();

            var blessingEntries = blessingItems.Select(b => new BlessingCheckEntry
            {
                ItemId = b.Id,
                DisplayText = $"{b.Name} ({b.Id})",
                IsChecked = existingItemIds.Contains(b.Id.ToString())
            }).ToList();

            foreach (var entry in blessingEntries)
            {
                entry.PropertyChanged += (s, _) =>
                {
                    if (s is BlessingCheckEntry be)
                    {
                        var idStr = be.ItemId.ToString();
                        if (be.IsChecked)
                        {
                            // Add blessing as item if not already present
                            if (!lo.Items.Any(it => it.Id == idStr))
                            {
                                lo.Items.Add(new ItemViewModel(idStr, 1));
                                RefreshItemsGrid(lo);
                            }
                        }
                        else
                        {
                            // Remove blessing item
                            var match = lo.Items.FirstOrDefault(it => it.Id == idStr);
                            if (match != null)
                            {
                                lo.Items.Remove(match);
                                RefreshItemsGrid(lo);
                            }
                        }
                        MarkDirty();
                    }
                };
            }

            IcBlessings.ItemsSource = blessingEntries;
        }

        private static string ResolveItemName(string id)
        {
            if (int.TryParse(id, out var numId))
            {
                var name = ItemDatabase.GetNameById(numId);
                if (name != null) return name;
            }
            return $"(#{id})";
        }

        // ── Name / Percent Changed ──

        private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressSelectionChange) return;
            if (LstLoadouts.SelectedItem is LoadoutViewModel lo)
            {
                lo.Name = TxtName.Text;
                lo.NotifyDisplayNameChanged();

                // Force ListBox to refresh
                _suppressSelectionChange = true;
                var idx = LstLoadouts.SelectedIndex;
                LstLoadouts.Items.Refresh();
                LstLoadouts.SelectedIndex = idx;
                _suppressSelectionChange = false;

                MarkDirty();
            }
        }

        private void TxtPercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressSelectionChange) return;
            if (LstLoadouts.SelectedItem is LoadoutViewModel lo)
            {
                if (int.TryParse(TxtPercent.Text, out var pct))
                {
                    lo.Percent = pct;
                    lo.NotifyDisplayNameChanged();

                    _suppressSelectionChange = true;
                    var idx = LstLoadouts.SelectedIndex;
                    LstLoadouts.Items.Refresh();
                    LstLoadouts.SelectedIndex = idx;
                    _suppressSelectionChange = false;

                    MarkDirty();
                }
            }
        }

        // ── Item Search ──

        private void TxtItemSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = TxtItemSearch.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                LstSearchResults.Visibility = Visibility.Collapsed;
                PnlCategorySelect.Visibility = Visibility.Visible;
                LstSearchResults.ItemsSource = null;
                return;
            }

            // Search across all items
            var matches = ItemDatabase.AllItems
                .Where(item => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || item.Id.ToString() == query)
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Name)
                .Take(30)
                .Select(item => $"{item.Name} ({item.Id}) [{item.Category}]")
                .ToList();

            if (matches.Count > 0)
            {
                LstSearchResults.ItemsSource = matches;
                LstSearchResults.Visibility = Visibility.Visible;
                PnlCategorySelect.Visibility = Visibility.Collapsed;
            }
            else
            {
                LstSearchResults.ItemsSource = new[] { "(no matches)" };
                LstSearchResults.Visibility = Visibility.Visible;
                PnlCategorySelect.Visibility = Visibility.Collapsed;
            }
        }

        private void LstSearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LstLoadouts.SelectedItem is not LoadoutViewModel lo) return;
            if (LstSearchResults.SelectedItem is not string selected) return;
            if (selected == "(no matches)") return;

            // Extract ID from "Name (ID) [Category]" format
            var openParen = selected.LastIndexOf('(');
            var closeParen = selected.LastIndexOf(')');
            if (openParen < 0 || closeParen < 0) return;

            var idStr = selected.Substring(openParen + 1, closeParen - openParen - 1);

            if (!int.TryParse(TxtQuantity.Text, out var qty) || qty < 1)
                qty = 1;
            if (qty > 255) qty = 255;

            lo.Items.Add(new ItemViewModel(idStr, qty));
            RefreshItemsGrid(lo);
            MarkDirty();

            // Clear search
            TxtItemSearch.Text = "";
        }

        // ── Item Management ──

        private void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CmbItem.Items.Clear();
            if (CmbCategory.SelectedItem is string cat)
            {
                foreach (var item in ItemDatabase.ByCategory(cat))
                    CmbItem.Items.Add($"{item.Name} ({item.Id})");
                if (CmbItem.Items.Count > 0)
                    CmbItem.SelectedIndex = 0;
            }
        }

        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            if (LstLoadouts.SelectedItem is not LoadoutViewModel lo) return;

            // If search results are visible and something is selected, add from search
            if (LstSearchResults.Visibility == Visibility.Visible
                && LstSearchResults.SelectedItem is string searchSelected
                && searchSelected != "(no matches)")
            {
                var op = searchSelected.LastIndexOf('(');
                var cp = searchSelected.LastIndexOf(')');
                if (op >= 0 && cp >= 0)
                {
                    var idStr = searchSelected.Substring(op + 1, cp - op - 1);
                    if (!int.TryParse(TxtQuantity.Text, out var q) || q < 1) q = 1;
                    if (q > 255) q = 255;
                    lo.Items.Add(new ItemViewModel(idStr, q));
                    RefreshItemsGrid(lo);
                    MarkDirty();
                    TxtItemSearch.Text = "";
                    return;
                }
            }

            // Otherwise add from category dropdown
            if (CmbItem.SelectedItem is not string selected) return;

            var openParen = selected.LastIndexOf('(');
            var closeParen = selected.LastIndexOf(')');
            if (openParen < 0 || closeParen < 0) return;

            var id = selected.Substring(openParen + 1, closeParen - openParen - 1);

            if (!int.TryParse(TxtQuantity.Text, out var qty) || qty < 1)
                qty = 1;
            if (qty > 255) qty = 255;

            lo.Items.Add(new ItemViewModel(id, qty));
            RefreshItemsGrid(lo);
            MarkDirty();
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (LstLoadouts.SelectedItem is not LoadoutViewModel lo) return;
            if (sender is Button btn && btn.Tag is ItemDisplayRow row)
            {
                // Find and remove the matching item
                var match = lo.Items.FirstOrDefault(it => it.Id == row.Id && it.Quantity == row.Quantity);
                if (match != null)
                {
                    lo.Items.Remove(match);
                    RefreshItemsGrid(lo);
                    MarkDirty();
                }
            }
        }

        // ── Inline Quantity Editing ──

        private void DgItems_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (LstLoadouts.SelectedItem is not LoadoutViewModel lo) return;
            if (e.EditAction == DataGridEditAction.Cancel) return;
            if (e.Column.Header?.ToString() != "Qty") return;

            if (e.EditingElement is TextBox tb && e.Row.Item is ItemDisplayRow row)
            {
                if (int.TryParse(tb.Text, out var newQty) && newQty >= 1 && newQty <= 255)
                {
                    // Update the underlying ItemViewModel
                    var match = lo.Items.FirstOrDefault(it => it.Id == row.Id && it.Quantity == row.Quantity);
                    if (match != null)
                    {
                        match.Quantity = newQty;
                        MarkDirty();
                    }
                }
            }
        }

        // ── View Models ──

        internal class LoadoutViewModel : INotifyPropertyChanged
        {
            public string Name { get; set; }
            public int Percent { get; set; }
            public ObservableCollection<ItemViewModel> Items { get; }
            public HashSet<int> Curses { get; }

            public string DisplayName
            {
                get
                {
                    var itemCount = Items.Count;
                    return $"{Name} ({Percent}%) - {itemCount} item{(itemCount != 1 ? "s" : "")}";
                }
            }

            public LoadoutViewModel(string name, int percent, List<StarterPackLoadoutService.Item> items, HashSet<int> curses)
            {
                Name = name;
                Percent = percent;
                Items = new ObservableCollection<ItemViewModel>(
                    items.Select(it => new ItemViewModel(it.Id, it.Quantity)));
                Curses = curses;
            }

            public void NotifyDisplayNameChanged() => OnPropertyChanged(nameof(DisplayName));

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string? name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal class ItemViewModel
        {
            public string Id { get; set; }
            public int Quantity { get; set; }

            public ItemViewModel(string id, int quantity)
            {
                Id = id;
                Quantity = quantity;
            }
        }

        internal class ItemDisplayRow
        {
            public string ItemName { get; set; } = "";
            public string Id { get; set; } = "";
            public int Quantity { get; set; }
        }

        internal class CurseCheckEntry : INotifyPropertyChanged
        {
            public int CurseId { get; set; }
            public string DisplayText { get; set; } = "";

            private bool _isChecked;
            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked != value)
                    {
                        _isChecked = value;
                        OnPropertyChanged();
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string? name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal class BlessingCheckEntry : INotifyPropertyChanged
        {
            public int ItemId { get; set; }
            public string DisplayText { get; set; } = "";

            private bool _isChecked;
            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked != value)
                    {
                        _isChecked = value;
                        OnPropertyChanged();
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string? name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
