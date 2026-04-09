using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    // ── PluginSelection ─────────────────────────────────────────────────────────

    public partial class PluginSelection : ObservableObject
    {
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string _pluginId = "";
        [ObservableProperty] private bool _isSelected = true;
        [ObservableProperty] private bool _isEnabled = true;

        /// <summary>True when selecting this plugin should show the client-mod dependency hint.</summary>
        public bool RequiresClientMod { get; init; }

        partial void OnIsSelectedChanged(bool value)
        {
            DependencyChanged?.Invoke();
        }

        /// <summary>Raised when IsSelected changes, so the parent VM can recompute DependencyHintVisible.</summary>
        internal Action? DependencyChanged;
    }

    // ── InstallerPanelViewModel ─────────────────────────────────────────────────

    public partial class InstallerPanelViewModel : ObservableObject
    {
        private readonly IServerPathProvider _serverPathProvider;
        private readonly INavigationService _navigation;
        private readonly IToastService _toast;
        private CancellationTokenSource? _cts;

        [ObservableProperty] private string _serverPath = "";
        [ObservableProperty] private string _citrusTag = "v0.7";
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private double _progress;
        [ObservableProperty] private bool _isInstalling;
        [ObservableProperty] private bool _isProgressVisible;
        [ObservableProperty] private bool _uiEnabled = true;
        [ObservableProperty] private bool _isCancelVisible;
        [ObservableProperty] private string _cancelButtonText = "CANCEL";
        [ObservableProperty] private string _logText = "";
        [ObservableProperty] private bool _dependencyHintVisible;
        [ObservableProperty] private ObservableCollection<PluginSelection> _pluginSelections = new();

        public InstallerPanelViewModel(
            IServerPathProvider serverPathProvider,
            INavigationService navigation,
            IToastService toast)
        {
            _serverPathProvider = serverPathProvider;
            _navigation = navigation;
            _toast = toast;

            BuildPluginSelections();
            TryAutoDetectPath();
        }

        // ── Path auto-detection ─────────────────────────────────────────────────

        private void TryAutoDetectPath()
        {
            var detectedPath = Installer.TryFindTabgServerPath();
            if (!string.IsNullOrEmpty(detectedPath))
                ServerPath = detectedPath;
        }

        /// <summary>Called from code-behind Browse_Click after dialog completes.</summary>
        public void SetServerPath(string path) => ServerPath = path;

        // ── Plugin selections ────────────────────────────────────────────────────

        private void BuildPluginSelections()
        {
            var selections = new ObservableCollection<PluginSelection>();

            foreach (var def in PluginRegistry.ServerPlugins)
            {
                var sel = new PluginSelection
                {
                    Name = def.Label,
                    PluginId = def.Id,
                    IsSelected = def.DefaultChecked,
                    RequiresClientMod = def.RequiresClientMod,
                };
                sel.DependencyChanged = RefreshDependencyHint;
                selections.Add(sel);
            }

            PluginSelections = selections;
            RefreshDependencyHint();
        }

        private void RefreshDependencyHint()
        {
            DependencyHintVisible = PluginSelections.Any(p => p.RequiresClientMod && p.IsSelected);
        }

        // ── Plugin preset commands ───────────────────────────────────────────────

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var p in PluginSelections)
                p.IsSelected = true;
        }

        [RelayCommand]
        private void SelectNone()
        {
            foreach (var p in PluginSelections)
                p.IsSelected = false;
        }

        [RelayCommand]
        private void SelectSigma()
        {
            foreach (var p in PluginSelections)
                p.IsSelected = PluginRegistry.SigmaPresetIds.Contains(p.PluginId);
        }

        // ── Install ──────────────────────────────────────────────────────────────

        [RelayCommand]
        private async Task Install()
        {
            const string serverName = "";
            const string serverPassword = "";
            const string serverDesc = "";

            string citrusTag = CitrusTag.Trim();
            bool skipCitrus = !GetSelectionById("Citruslib");
            bool installCommunityServer = GetSelectionById("CommunityServer");
            bool skipStarterPack = !GetSelectionById("StarterPack");
            string serverDir = ServerPath.Trim();

            var result = MessageBox.Show(
                "WARNING: Installation will modify server files!\n\nA backup will be created in the 'backup' folder before installation.\nDo you want to continue?",
                "Installation Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            if (string.IsNullOrWhiteSpace(serverDir))
            {
                _toast.Warning("Please select a valid TABG server folder.");
                return;
            }
            if (!Directory.Exists(serverDir))
            {
                _toast.Error($"The path '{serverDir}' does not exist.");
                return;
            }
            if (citrusTag.Length == 0 && !skipCitrus)
            {
                _toast.Warning("CitrusLib tag is required if not skipping the plugin.");
                return;
            }

            // Collect bundled plugin DLLs from non-core selections
            var selectedBundledPlugins = new List<string>();
            foreach (var sel in PluginSelections)
            {
                if (!sel.IsSelected) continue;
                var def = PluginRegistry.FindById(sel.PluginId);
                if (def != null && def.Kind == PluginKind.Bundled)
                {
                    foreach (var dll in def.DllNames)
                        if (!selectedBundledPlugins.Contains(dll))
                            selectedBundledPlugins.Add(dll);
                }
            }

            UiEnabled = false;
            LogText = "";
            IsProgressVisible = true;
            Progress = 0;
            StatusText = "Preparing...";
            IsCancelVisible = true;
            CancelButtonText = "CANCEL";

            var logLines = new System.Text.StringBuilder();

            var progress = new Progress<string>(line =>
            {
                logLines.AppendLine(line);
                LogText = logLines.ToString();

                var pct = ProgressEstimator.Estimate(line);
                if (pct >= 0)
                {
                    Progress = pct;
                    StatusText = ProgressEstimator.GetStageName(pct);
                }
            });

            _cts = new CancellationTokenSource();

            try
            {
                int exitCode = await Task.Run(async () =>
                {
                    var backupService = new TabgInstaller.Core.Services.BackupService(progress);
                    if (Directory.Exists(serverDir) && Directory.GetFileSystemEntries(serverDir).Length > 0)
                    {
                        ((IProgress<string>)progress).Report("Creating backup...");
                        bool backupSuccess = await backupService.CreateBackupAsync(serverDir);
                        if (!backupSuccess)
                            ((IProgress<string>)progress).Report("Warning: Backup failed — continuing anyway.");
                    }

                    var installer = new Installer(gameDir: serverDir, log: progress);

                    return await installer.RunAsync(
                        serverDir: serverDir,
                        serverName: serverName,
                        serverPassword: serverPassword,
                        serverDescription: serverDesc,
                        starterPackTag: "",
                        citrusLibTag: citrusTag,
                        skipStarterPack: skipStarterPack,
                        skipCitruslib: skipCitrus,
                        installCommunityServer: installCommunityServer,
                        bundledPlugins: selectedBundledPlugins,
                        ct: _cts.Token
                    );
                });

                if (!_cts.IsCancellationRequested)
                {
                    if (exitCode == 0)
                    {
                        ((IProgress<string>)progress).Report("Installation completed successfully!");
                        Progress = 100;
                        StatusText = "Complete";
                        _toast.Success("Installation completed! Change Server Name, Password and Description in Server Settings.");
                        ActivateServerTabs(serverDir);
                    }
                    else
                    {
                        StatusText = "Failed";
                        _toast.Error($"Installation ended with code {exitCode}. See log output.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ((IProgress<string>)progress).Report("Installation cancelled by user.");
                StatusText = "Cancelled";
                _toast.Warning("Installation cancelled.");
            }
            catch (Exception ex)
            {
                progress.LogException("Unknown error during installation", ex);
                StatusText = "Failed";
                _toast.Error($"Installation error: {ex.Message}");
            }
            finally
            {
                UiEnabled = true;
                IsCancelVisible = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        // ── Cancel ───────────────────────────────────────────────────────────────

        [RelayCommand]
        private void Cancel()
        {
            _cts?.Cancel();
            CancelButtonText = "Cancelling...";
        }

        // ── Continue ─────────────────────────────────────────────────────────────

        [RelayCommand]
        private void Continue()
        {
            string serverDir = ServerPath.Trim();
            if (string.IsNullOrWhiteSpace(serverDir) || !Directory.Exists(serverDir))
            {
                _toast.Warning("Please select a valid TABG server folder.");
                return;
            }

            ActivateServerTabs(serverDir);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private bool GetSelectionById(string pluginId)
        {
            var sel = PluginSelections.FirstOrDefault(p =>
                string.Equals(p.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
            return sel?.IsSelected ?? false;
        }

        /// <summary>
        /// Updates the server path provider (triggers all MVVM VMs) and navigates to the Config tab (index 3).
        /// </summary>
        private void ActivateServerTabs(string serverDir)
        {
            _serverPathProvider.SetPath(serverDir);
            _navigation.NavigateToTab(3);
        }
    }
}
