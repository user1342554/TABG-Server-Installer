using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    // ── Inner model types ──────────────────────────────────────────────────────

    public partial class LoadoutVm : ObservableObject
    {
        [ObservableProperty] private string _name = "New Loadout";
        [ObservableProperty] private int _percent = 100;
        [ObservableProperty] private ObservableCollection<ItemVm> _items = new();
        [ObservableProperty] private HashSet<int> _curses = new();

        public string DisplayName =>
            $"{Name} ({Percent}%) - {Items.Count} item{(Items.Count != 1 ? "s" : "")}";

        partial void OnNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));
        partial void OnPercentChanged(int value) => OnPropertyChanged(nameof(DisplayName));
        partial void OnItemsChanged(ObservableCollection<ItemVm> value) => OnPropertyChanged(nameof(DisplayName));

        public void NotifyDisplayNameChanged() => OnPropertyChanged(nameof(DisplayName));
    }

    public class ItemVm
    {
        public string Id { get; set; } = "";
        public int Quantity { get; set; } = 1;

        public ItemVm() { }
        public ItemVm(string id, int quantity) { Id = id; Quantity = quantity; }
    }

    public class ItemDisplayRow
    {
        public string ItemName { get; set; } = "";
        public string Id { get; set; } = "";
        public int Quantity { get; set; } = 1;
    }

    public partial class CurseCheckEntry : ObservableObject
    {
        public int CurseId { get; set; }
        public string DisplayText { get; set; } = "";

        [ObservableProperty] private bool _isChecked;
    }

    public partial class BlessingCheckEntry : ObservableObject
    {
        public int ItemId { get; set; }
        public string DisplayText { get; set; } = "";

        [ObservableProperty] private bool _isChecked;
    }

    public class ItemSearchResult
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";

        /// <summary>Formatted for display: "Name (ID) [Category]"</summary>
        public string DisplayText => $"{Name} ({Id}) [{Category}]";
    }

    // ── Main ViewModel ─────────────────────────────────────────────────────────

    public partial class LoadoutEditorViewModel : ObservableObject, IDisposable
    {
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IToastService _toast;

        private readonly StarterPackLoadoutService _loadoutSvc = new();
        private FileSystemWatcher? _watcherRoot;
        private FileSystemWatcher? _watcherCfg;
        private Timer? _debounce;
        private bool _saving;
        private bool _suppressDirty;
        private bool _disposed;
        private FreddoCommissionSettings? _commissionSettings;

        // ── Observable properties ──────────────────────────────────────────────

        [ObservableProperty] private ObservableCollection<LoadoutVm> _loadouts = new();
        [ObservableProperty] private LoadoutVm? _selectedLoadout;
        [ObservableProperty] private bool _isDirty;
        [ObservableProperty] private string _selectedMode = "Normal";
        [ObservableProperty] private string _searchText = "";
        [ObservableProperty] private ObservableCollection<ItemSearchResult> _searchResults = new();
        [ObservableProperty] private string _selectedCategory = "";
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private ObservableCollection<ItemDisplayRow> _currentItems = new();
        [ObservableProperty] private ObservableCollection<CurseCheckEntry> _currentCurses = new();
        [ObservableProperty] private ObservableCollection<BlessingCheckEntry> _currentBlessings = new();
        [ObservableProperty] private ObservableCollection<string> _categoryItems = new();
        [ObservableProperty] private string? _selectedCategoryItem;
        [ObservableProperty] private bool _searchResultsVisible;
        [ObservableProperty] private bool _categorySelectVisible = true;
        [ObservableProperty] private string _quantityText = "1";
        [ObservableProperty] private string? _selectedSearchResult;

        // ── Constructor ────────────────────────────────────────────────────────

        public LoadoutEditorViewModel(
            IServerPathProvider serverPathProvider,
            IToastService toast)
        {
            _serverPathProvider = serverPathProvider;
            _toast = toast;

            _serverPathProvider.PathChanged += OnServerPathChanged;

            if (!string.IsNullOrEmpty(_serverPathProvider.ServerPath))
                Initialize(_serverPathProvider.ServerPath);
        }

        private void OnServerPathChanged()
        {
            var path = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(path)) return;
            Initialize(path);
        }

        public void Initialize(string serverDir)
        {
            SetupWatchers(serverDir);
            LoadAll(serverDir);
        }

        // ── Partial change handlers ────────────────────────────────────────────

        partial void OnSelectedLoadoutChanged(LoadoutVm? value)
        {
            if (value == null)
            {
                CurrentItems.Clear();
                CurrentCurses.Clear();
                CurrentBlessings.Clear();
                return;
            }

            RefreshItemsGrid(value);
            RefreshCurseCheckboxes(value);
        }

        partial void OnSearchTextChanged(string value)
        {
            var query = value?.Trim() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                SearchResults.Clear();
                SearchResultsVisible = false;
                CategorySelectVisible = true;
                return;
            }

            var matches = ItemDatabase.AllItems
                .Where(item => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || item.Id.ToString() == query)
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Name)
                .Take(30)
                .Select(item => new ItemSearchResult
                {
                    Id = item.Id.ToString(),
                    Name = item.Name,
                    Category = item.Category
                })
                .ToList();

            SearchResults = new ObservableCollection<ItemSearchResult>(matches);
            SearchResultsVisible = matches.Count > 0;
            CategorySelectVisible = false;
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            RefreshCategoryItems(value);
        }

        // ── Loading ────────────────────────────────────────────────────────────

        public void LoadAll(string serverDir)
        {
            try
            {
                _suppressDirty = true;
                var settings = StarterPackConfigService.Read(serverDir);
                var mode = settings.GetLoadoutMode();
                SelectedMode = mode;

                var rawLoadouts = settings.GetLoadoutsWithoutPrefix();
                var parsed = _loadoutSvc.ParseLoadoutsValue(rawLoadouts);

                _commissionSettings = ModConfigService.ReadCommission(serverDir);
                var perLoadoutCurses = ParseLoadoutCurses(_commissionSettings.LoadoutCurses);

                var newLoadouts = new ObservableCollection<LoadoutVm>();
                for (int i = 0; i < parsed.Count; i++)
                {
                    var lo = parsed[i];
                    var curses = i < perLoadoutCurses.Count ? perLoadoutCurses[i] : new HashSet<int>();
                    var vm = new LoadoutVm { Name = lo.Name, Percent = lo.Percent, Curses = curses };
                    foreach (var it in lo.Items)
                        vm.Items.Add(new ItemVm(it.Id, it.Quantity));
                    newLoadouts.Add(vm);
                }

                Loadouts = newLoadouts;

                if (Loadouts.Count > 0)
                    SelectedLoadout = Loadouts[0];
                else
                    SelectedLoadout = null;

                ClearDirty();
                _suppressDirty = false;
                StatusText = $"Loaded {Loadouts.Count} loadouts.";
            }
            catch (Exception ex)
            {
                _suppressDirty = false;
                StatusText = $"Load error: {ex.Message}";
            }
        }

        // ── Dirty tracking ─────────────────────────────────────────────────────

        public void MarkDirty()
        {
            if (_suppressDirty) return;
            if (!IsDirty)
            {
                IsDirty = true;
                StatusText = "Unsaved changes";
            }
        }

        public void ClearDirty()
        {
            IsDirty = false;
        }

        // ── Grid / Checkbox refresh helpers ───────────────────────────────────

        public void RefreshItemsGrid(LoadoutVm lo)
        {
            var display = lo.Items.Select(it => new ItemDisplayRow
            {
                Id = it.Id,
                Quantity = it.Quantity,
                ItemName = ResolveItemName(it.Id)
            });
            CurrentItems = new ObservableCollection<ItemDisplayRow>(display);
        }

        public void RefreshCurseCheckboxes(LoadoutVm lo)
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
                        if (ce.IsChecked) lo.Curses.Add(ce.CurseId);
                        else lo.Curses.Remove(ce.CurseId);
                        MarkDirty();
                    }
                };
            }

            CurrentCurses = new ObservableCollection<CurseCheckEntry>(entries);

            // Blessings — items in the Blessings category stored as loadout items
            var blessingItems = ItemDatabase.ByCategory("Blessings").ToList();
            var existingIds = lo.Items.Select(it => it.Id).ToHashSet();

            var blessingEntries = blessingItems.Select(b => new BlessingCheckEntry
            {
                ItemId = b.Id,
                DisplayText = $"{b.Name} ({b.Id})",
                IsChecked = existingIds.Contains(b.Id.ToString())
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
                            if (!lo.Items.Any(it => it.Id == idStr))
                            {
                                lo.Items.Add(new ItemVm(idStr, 1));
                                RefreshItemsGrid(lo);
                            }
                        }
                        else
                        {
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

            CurrentBlessings = new ObservableCollection<BlessingCheckEntry>(blessingEntries);
        }

        private void RefreshCategoryItems(string category)
        {
            if (string.IsNullOrEmpty(category)) return;
            var items = ItemDatabase.ByCategory(category)
                .Select(item => $"{item.Name} ({item.Id})")
                .ToList();
            CategoryItems = new ObservableCollection<string>(items);
            SelectedCategoryItem = items.Count > 0 ? items[0] : null;
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

        // ── Curse parsing helpers ──────────────────────────────────────────────

        public static List<HashSet<int>> ParseLoadoutCurses(string raw)
        {
            var result = new List<HashSet<int>>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            var groups = raw.Split('/', StringSplitOptions.None);
            foreach (var group in groups)
            {
                var set = new HashSet<int>();
                if (!string.IsNullOrWhiteSpace(group))
                {
                    foreach (var tok in group.Split(',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (int.TryParse(tok, out var id))
                            set.Add(id);
                    }
                }
                result.Add(set);
            }
            return result;
        }

        public string BuildLoadoutCurses()
        {
            var parts = new List<string>();
            foreach (var lo in Loadouts)
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

        // ── File watchers ──────────────────────────────────────────────────────

        private void SetupWatchers(string serverDir)
        {
            _watcherRoot?.Dispose();
            _watcherCfg?.Dispose();

            void OnChange(object? s, FileSystemEventArgs e)
            {
                if (_saving) return;
                _debounce?.Dispose();
                _debounce = new Timer(_ =>
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        try { LoadAll(serverDir); }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[LoadoutEditorVM] Debounce reload failed: {ex.Message}");
                        }
                    });
                }, null, 500, Timeout.Infinite);
            }

            if (Directory.Exists(serverDir))
            {
                _watcherRoot = new FileSystemWatcher(serverDir)
                {
                    Filter = "TheStarterPack.txt",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                _watcherRoot.Changed += OnChange;
            }

            var cfgDir = Path.Combine(serverDir, "BepInEx", "config");
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

        // ── Commands ───────────────────────────────────────────────────────────

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
                var settings = StarterPackConfigService.Read(serverDir);

                var records = Loadouts.Select(lo =>
                    new StarterPackLoadoutService.Loadout(
                        lo.Name,
                        lo.Percent,
                        lo.Items.Select(it => new StarterPackLoadoutService.Item(it.Id, it.Quantity)).ToList()
                    )).ToList();

                var loadoutsStr = _loadoutSvc.BuildLoadoutsValue(records);
                settings.SetLoadoutsWithPrefix(SelectedMode, loadoutsStr);
                _saving = true;
                StarterPackConfigService.Write(serverDir, settings);

                var commission = _commissionSettings ?? ModConfigService.ReadCommission(serverDir);
                commission.LoadoutCurses = BuildLoadoutCurses();
                ModConfigService.WriteCommission(serverDir, commission);
                _saving = false;

                ClearDirty();
                StatusText = "Saved successfully.";
            }
            catch (Exception ex)
            {
                _saving = false;
                StatusText = $"Save error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ImportRaw(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                _toast.Warning("No text entered.");
                return;
            }

            // Detect mode prefix
            string mode = "Normal";
            string body = raw;
            if (raw.StartsWith("GunGame/")) { mode = "GunGame"; body = raw.Substring("GunGame/".Length); }
            else if (raw.StartsWith("ReverseGunGame/")) { mode = "ReverseGunGame"; body = raw.Substring("ReverseGunGame/".Length); }
            else if (raw.StartsWith("KeepInventory/")) { mode = "KeepInventory"; body = raw.Substring("KeepInventory/".Length); }

            SelectedMode = mode;
            var parsed = _loadoutSvc.ParseLoadoutsValue(body);

            var newLoadouts = new ObservableCollection<LoadoutVm>();
            foreach (var lo in parsed)
            {
                var vm = new LoadoutVm { Name = lo.Name, Percent = lo.Percent };
                foreach (var it in lo.Items)
                    vm.Items.Add(new ItemVm(it.Id, it.Quantity));
                newLoadouts.Add(vm);
            }

            Loadouts = newLoadouts;
            SelectedLoadout = Loadouts.Count > 0 ? Loadouts[0] : null;
            MarkDirty();
            StatusText = $"Imported {Loadouts.Count} loadouts.";
        }

        /// <summary>Builds and returns the raw export string. Dialog is shown by the View.</summary>
        public string BuildExportRaw()
        {
            var records = Loadouts.Select(lo =>
                new StarterPackLoadoutService.Loadout(
                    lo.Name,
                    lo.Percent,
                    lo.Items.Select(it => new StarterPackLoadoutService.Item(it.Id, it.Quantity)).ToList()
                )).ToList();

            var body = _loadoutSvc.BuildLoadoutsValue(records);
            return SelectedMode == "Normal" ? body : SelectedMode + "/" + body;
        }

        [RelayCommand]
        private void AddLoadout()
        {
            var lo = new LoadoutVm { Name = "New Loadout", Percent = 100 };
            Loadouts.Add(lo);
            SelectedLoadout = lo;
            MarkDirty();
        }

        [RelayCommand]
        private void DuplicateLoadout()
        {
            if (SelectedLoadout == null) return;
            var source = SelectedLoadout;

            var clone = new LoadoutVm
            {
                Name = source.Name + " (Copy)",
                Percent = source.Percent,
                Curses = new HashSet<int>(source.Curses)
            };
            foreach (var it in source.Items)
                clone.Items.Add(new ItemVm(it.Id, it.Quantity));

            var idx = Loadouts.IndexOf(source);
            Loadouts.Insert(idx + 1, clone);
            SelectedLoadout = clone;
            MarkDirty();
        }

        [RelayCommand]
        private void RemoveLoadout()
        {
            if (SelectedLoadout == null) return;
            var lo = SelectedLoadout;

            var result = MessageBox.Show(
                $"Remove loadout \"{lo.Name}\"?",
                "Confirm Remove",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            var idx = Loadouts.IndexOf(lo);
            Loadouts.Remove(lo);
            if (Loadouts.Count > 0)
                SelectedLoadout = Loadouts[Math.Min(idx, Loadouts.Count - 1)];
            else
                SelectedLoadout = null;
            MarkDirty();
        }

        [RelayCommand]
        private void MoveUp()
        {
            if (SelectedLoadout == null) return;
            var idx = Loadouts.IndexOf(SelectedLoadout);
            if (idx > 0)
            {
                Loadouts.Move(idx, idx - 1);
                MarkDirty();
            }
        }

        [RelayCommand]
        private void MoveDown()
        {
            if (SelectedLoadout == null) return;
            var idx = Loadouts.IndexOf(SelectedLoadout);
            if (idx >= 0 && idx < Loadouts.Count - 1)
            {
                Loadouts.Move(idx, idx + 1);
                MarkDirty();
            }
        }

        [RelayCommand]
        private void AddItemFromSearch(ItemSearchResult item)
        {
            if (SelectedLoadout == null || item == null) return;

            if (!int.TryParse(QuantityText, out var qty) || qty < 1) qty = 1;
            if (qty > 255) qty = 255;

            SelectedLoadout.Items.Add(new ItemVm(item.Id, qty));
            RefreshItemsGrid(SelectedLoadout);
            MarkDirty();

            // Clear search
            SearchText = "";
        }

        [RelayCommand]
        private void AddItemFromCategory()
        {
            if (SelectedLoadout == null) return;

            string? selected = SelectedCategoryItem;
            if (string.IsNullOrEmpty(selected)) return;

            var openParen = selected.LastIndexOf('(');
            var closeParen = selected.LastIndexOf(')');
            if (openParen < 0 || closeParen < 0) return;

            var id = selected.Substring(openParen + 1, closeParen - openParen - 1);

            if (!int.TryParse(QuantityText, out var qty) || qty < 1) qty = 1;
            if (qty > 255) qty = 255;

            SelectedLoadout.Items.Add(new ItemVm(id, qty));
            RefreshItemsGrid(SelectedLoadout);
            MarkDirty();
        }

        [RelayCommand]
        private void RemoveItem(ItemDisplayRow item)
        {
            if (SelectedLoadout == null || item == null) return;

            var match = SelectedLoadout.Items.FirstOrDefault(
                it => it.Id == item.Id && it.Quantity == item.Quantity);
            if (match != null)
            {
                SelectedLoadout.Items.Remove(match);
                RefreshItemsGrid(SelectedLoadout);
                MarkDirty();
            }
        }

        /// <summary>
        /// Called from code-behind after DataGrid CellEditEnding to update quantity.
        /// </summary>
        public void UpdateItemQuantity(ItemDisplayRow row, int newQty)
        {
            if (SelectedLoadout == null) return;
            if (newQty < 1 || newQty > 255) return;

            var match = SelectedLoadout.Items.FirstOrDefault(
                it => it.Id == row.Id && it.Quantity == row.Quantity);
            if (match != null)
            {
                match.Quantity = newQty;
                MarkDirty();
            }
        }

        /// <summary>
        /// Called from code-behind when loadout name text changes.
        /// </summary>
        public void UpdateSelectedLoadoutName(string name)
        {
            if (SelectedLoadout == null) return;
            SelectedLoadout.Name = name;
            SelectedLoadout.NotifyDisplayNameChanged();
            MarkDirty();
        }

        /// <summary>
        /// Called from code-behind when loadout percent text changes.
        /// </summary>
        public void UpdateSelectedLoadoutPercent(string percentText)
        {
            if (SelectedLoadout == null) return;
            if (int.TryParse(percentText, out var pct))
            {
                SelectedLoadout.Percent = pct;
                SelectedLoadout.NotifyDisplayNameChanged();
                MarkDirty();
            }
        }

        // ── IDisposable ────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _serverPathProvider.PathChanged -= OnServerPathChanged;
            _watcherRoot?.Dispose();
            _watcherCfg?.Dispose();
            _debounce?.Dispose();
        }
    }
}
