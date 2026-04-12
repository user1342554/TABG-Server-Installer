using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class BrowsePluginsViewModel : ObservableObject
    {
        private readonly IRegistryService _registry;
        private readonly IMarketplaceInstallService _installer;
        private readonly IInstalledPluginTracker _tracker;
        private readonly IActiveInstanceService _activeInstance;
        private readonly IAppSettingsService _appSettings;
        private readonly IToastService _toast;

        private static readonly string CurrentInstallerVersion = "4.0.0";

        [ObservableProperty] private string _searchText = "";
        [ObservableProperty] private string _selectedCategory = "All";
        [ObservableProperty] private string _selectedSort = "A-Z";
        [ObservableProperty] private int _updateCount;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private PluginCardViewModel? _selectedPlugin;

        public List<PluginCardViewModel> AllPluginCards { get; private set; } = new();
        public ObservableCollection<PluginCardViewModel> FilteredPlugins { get; } = new();

        public string[] Categories { get; } = { "All", "Server", "Client" };
        public string[] SortOptions { get; } = { "A-Z", "Recently Updated", "Newest" };

        public bool HasUpdates => UpdateCount > 0;
        public bool HasSelectedPlugin => SelectedPlugin != null;

        public BrowsePluginsViewModel(
            IRegistryService registry,
            IMarketplaceInstallService installer,
            IInstalledPluginTracker tracker,
            IActiveInstanceService activeInstance,
            IAppSettingsService appSettings,
            IToastService toast)
        {
            _registry = registry;
            _installer = installer;
            _tracker = tracker;
            _activeInstance = activeInstance;
            _appSettings = appSettings;
            _toast = toast;
        }

        partial void OnSearchTextChanged(string value) => ApplyFilters();
        partial void OnSelectedCategoryChanged(string value) => ApplyFilters();
        partial void OnSelectedSortChanged(string value) => ApplyFilters();
        partial void OnUpdateCountChanged(int value) => OnPropertyChanged(nameof(HasUpdates));
        partial void OnSelectedPluginChanged(PluginCardViewModel? value) => OnPropertyChanged(nameof(HasSelectedPlugin));

        public async Task LoadPluginsAsync()
        {
            IsLoading = true;
            StatusText = "Fetching plugin registry...";

            var response = await _registry.FetchRegistryAsync();
            if (response == null)
            {
                StatusText = "Plugin marketplace unavailable.";
                IsLoading = false;
                return;
            }

            AllPluginCards = response.Plugins.Select(manifest =>
            {
                var installed = _tracker.FindById(manifest.Id);
                return new PluginCardViewModel(manifest, installed, CurrentInstallerVersion);
            }).ToList();

            UpdateCount = AllPluginCards.Count(c => c.InstallStatus == PluginInstallStatus.UpdateAvailable);
            StatusText = "";
            IsLoading = false;

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var filtered = AllPluginCards.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
                filtered = filtered.Where(c => c.MatchesSearch(SearchText));

            if (SelectedCategory != "All")
            {
                var type = SelectedCategory.ToLowerInvariant();
                filtered = filtered.Where(c =>
                    c.Manifest.Type == type || c.Manifest.Type == "both");
            }

            filtered = SelectedSort switch
            {
                "A-Z" => filtered.OrderBy(c => c.Manifest.Name, StringComparer.OrdinalIgnoreCase),
                "Recently Updated" => filtered.OrderByDescending(c => c.Manifest.Version),
                "Newest" => filtered.OrderByDescending(c => c.Manifest.Version),
                _ => filtered.OrderBy(c => c.Manifest.Name, StringComparer.OrdinalIgnoreCase)
            };

            FilteredPlugins.Clear();
            foreach (var card in filtered)
                FilteredPlugins.Add(card);
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadPluginsAsync();
            _toast.Info("Plugin registry refreshed.");
        }

        [RelayCommand]
        private async Task InstallPluginAsync(PluginCardViewModel card)
        {
            if (card.InstallStatus != PluginInstallStatus.Available) return;

            var serverRoot = _activeInstance.ServerPath;
            var clientPath = _appSettings.Load().ClientModdedPath;
            var registry = _registry.GetCachedRegistry();
            if (registry == null) return;

            var success = await _installer.InstallPluginAsync(
                card.Manifest, registry.Plugins, serverRoot, clientPath);

            if (success)
            {
                _toast.Success($"{card.Manifest.Name} installed successfully.");
                await LoadPluginsAsync();
            }
            else
            {
                _toast.Error($"Failed to install {card.Manifest.Name}.");
            }
        }

        [RelayCommand]
        private async Task UpdatePluginAsync(PluginCardViewModel card)
        {
            if (card.InstallStatus != PluginInstallStatus.UpdateAvailable) return;

            var serverRoot = _activeInstance.ServerPath;
            var clientPath = _appSettings.Load().ClientModdedPath;

            var success = await _installer.UpdatePluginAsync(
                card.Manifest, serverRoot, clientPath);

            if (success)
            {
                _toast.Success($"{card.Manifest.Name} updated to {card.Manifest.Version}.");
                await LoadPluginsAsync();
            }
            else
            {
                _toast.Error($"Failed to update {card.Manifest.Name}.");
            }
        }

        [RelayCommand]
        private async Task UninstallPluginAsync(PluginCardViewModel card)
        {
            if (!card.IsInstalled) return;

            var serverRoot = _activeInstance.ServerPath;
            var clientPath = _appSettings.Load().ClientModdedPath;

            var success = _installer.UninstallPlugin(card.Manifest.Id, serverRoot, clientPath);

            if (success)
            {
                _toast.Success($"{card.Manifest.Name} uninstalled.");
                await LoadPluginsAsync();
            }
            else
            {
                _toast.Error($"Failed to uninstall {card.Manifest.Name}.");
            }
        }

        [RelayCommand]
        private async Task UpdateAllAsync()
        {
            var serverRoot = _activeInstance.ServerPath;
            var clientPath = _appSettings.Load().ClientModdedPath;

            var updatable = AllPluginCards
                .Where(c => c.InstallStatus == PluginInstallStatus.UpdateAvailable)
                .ToList();

            int updated = 0;
            foreach (var card in updatable)
            {
                var success = await _installer.UpdatePluginAsync(card.Manifest, serverRoot, clientPath);
                if (success) updated++;
            }

            _toast.Success($"Updated {updated} of {updatable.Count} plugins.");
            await LoadPluginsAsync();
        }

        [RelayCommand]
        private void TogglePin(PluginCardViewModel card)
        {
            var newPinned = !card.IsPinned;
            _tracker.SetPinned(card.Manifest.Id, newPinned);
            card.IsPinned = newPinned;

            var installed = _tracker.FindById(card.Manifest.Id);
            var refreshed = new PluginCardViewModel(card.Manifest, installed, CurrentInstallerVersion);
            var idx = AllPluginCards.FindIndex(c => c.Manifest.Id == card.Manifest.Id);
            if (idx >= 0) AllPluginCards[idx] = refreshed;
            ApplyFilters();
        }

        [RelayCommand]
        private async Task InstallOrUpdateAsync(PluginCardViewModel card)
        {
            if (card.InstallStatus == PluginInstallStatus.Available)
                await InstallPluginAsync(card);
            else if (card.InstallStatus == PluginInstallStatus.UpdateAvailable)
                await UpdatePluginAsync(card);
        }
    }
}
