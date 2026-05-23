using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Windows
{
    public partial class SetupWizardWindow : Window
    {
        public string ServerPath => TxtServerPath.Text.Trim();
        public string ClientPath => TxtClientPath.Text.Trim();
        public string ClientModdedPath { get; private set; } = "";
        public bool SetupCompleted { get; private set; }

        private readonly List<CheckBox> _pluginCheckboxes = new();
        private readonly List<CheckBox> _clientModCheckboxes = new();
        private CancellationTokenSource? _cts;
        private readonly IToastService _toast;
        private readonly IAppSettingsService _appSettings;

        private const string DefaultCitrusTag = "v0.7";

        public SetupWizardWindow(IToastService? toast = null, IAppSettingsService? appSettings = null)
        {
            _toast = toast ?? ToastService.Instance;
            _appSettings = appSettings ?? new AppSettingsService();
            InitializeComponent();
            BuildPluginCheckboxes();
            BuildClientModCheckboxes();

            var serverPath = Installer.TryFindTabgServerPath();
            if (!string.IsNullOrEmpty(serverPath))
                TxtServerPath.Text = serverPath;

            var clientPath = Installer.TryFindTabgClientPath();
            if (!string.IsNullOrEmpty(clientPath))
            {
                TxtClientPath.Text = clientPath;
                UpdateModdedPathPreview();
            }
        }

        private void BuildPluginCheckboxes()
        {
            foreach (var plugin in PluginRegistry.ServerPlugins)
            {
                var cb = new CheckBox
                {
                    Content = plugin.Label,
                    IsChecked = plugin.DefaultChecked,
                    Margin = new Thickness(5, 3, 5, 3),
                    Tag = plugin.Id
                };
                _pluginCheckboxes.Add(cb);
                PluginCheckboxes.Children.Add(cb);
            }
        }

        private void BuildClientModCheckboxes()
        {
            foreach (var mod in PluginRegistry.ClientMods)
            {
                var cb = new CheckBox
                {
                    Content = mod.Label,
                    IsChecked = mod.DefaultChecked,
                    Margin = new Thickness(5, 3, 5, 3),
                    Tag = mod.Id
                };
                _clientModCheckboxes.Add(cb);
                ClientModCheckboxes.Children.Add(cb);
            }
        }

        private void BrowseServer_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select TABG Server Folder" };
            var detected = Installer.TryFindTabgServerPath();
            if (!string.IsNullOrEmpty(detected) && Directory.Exists(detected))
                dialog.InitialDirectory = Path.GetDirectoryName(detected) ?? detected;

            if (dialog.ShowDialog() == true)
                TxtServerPath.Text = dialog.FolderName;
        }

        private void BrowseClient_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select TABG Steam Folder" };
            if (dialog.ShowDialog() == true)
            {
                TxtClientPath.Text = dialog.FolderName;
                UpdateModdedPathPreview();
            }
        }

        private void UpdateModdedPathPreview()
        {
            var client = TxtClientPath.Text.Trim();
            if (!string.IsNullOrEmpty(client))
            {
                var parent = Path.GetDirectoryName(client);
                if (parent != null)
                {
                    ClientModdedPath = Path.Combine(parent, "TABG_Modded");
                    TxtModdedPathPreview.Text = $"Modded copy will be at: {ClientModdedPath}";
                }
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (WizardSteps.SelectedIndex > 0)
            {
                WizardSteps.SelectedIndex--;
                UpdateNavButtons();
            }
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            WizardSteps.SelectedIndex++;
            UpdateNavButtons();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            BtnCancel.IsEnabled = false;
            BtnCancel.Content = "Cancelling...";
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            int step = WizardSteps.SelectedIndex;

            if (step == 0) // Server path
            {
                var path = TxtServerPath.Text.Trim();
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    _toast.Warning("Please select a valid TABG server folder.");
                    return;
                }
                WizardSteps.SelectedIndex = 1;
            }
            else if (step == 1) // Client path
            {
                UpdateModdedPathPreview();
                WizardSteps.SelectedIndex = 2;
            }
            else if (step == 2) // Server plugins → client mods
            {
                WizardSteps.SelectedIndex = 3;
            }
            else if (step == 3) // Client mods → install
            {
                WizardSteps.SelectedIndex = 4;
                UpdateNavButtons();
                await RunInstallation();
                return;
            }

            UpdateNavButtons();
        }

        private void UpdateNavButtons()
        {
            int step = WizardSteps.SelectedIndex;
            BtnBack.Visibility = step > 0 && step < 4 ? Visibility.Visible : Visibility.Collapsed;
            BtnSkip.Visibility = step == 1 || step == 3 ? Visibility.Visible : Visibility.Collapsed;
            BtnNext.Content = step == 3 ? "Install" : "Next";
            BtnNext.Visibility = step == 4 ? Visibility.Collapsed : Visibility.Visible;
            BtnCancel.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task RunInstallation()
        {
            string serverDir = TxtServerPath.Text.Trim();

            BtnBack.IsEnabled = false;
            BtnNext.IsEnabled = false;

            _cts = new CancellationTokenSource();

            var progress = new Progress<string>(line =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    TxtLog.AppendText(line + Environment.NewLine);
                    LogScrollViewer.ScrollToEnd();

                    var pct = ProgressEstimator.Estimate(line);
                    if (pct >= 0 && pct > InstallProgress.Value)
                    {
                        InstallProgress.Value = pct;
                        TxtInstallStage.Text = ProgressEstimator.GetStageName(pct);
                    }
                });
            });

            try
            {
                var bundled = new List<string>();
                bool skipCitrus = true;
                bool installCommunityServer = false;

                for (int i = 0; i < _pluginCheckboxes.Count && i < PluginRegistry.ServerPlugins.Length; i++)
                {
                    if (_pluginCheckboxes[i].IsChecked != true) continue;
                    var plugin = PluginRegistry.ServerPlugins[i];

                    switch (plugin.Kind)
                    {
                        case PluginKind.CoreDependency:
                            if (plugin.Id == "Citruslib") skipCitrus = false;
                            break;
                        case PluginKind.CommunityServer:
                            installCommunityServer = true;
                            break;
                        case PluginKind.Bundled:
                            foreach (var dll in plugin.DllNames)
                                if (!bundled.Contains(dll))
                                    bundled.Add(dll);
                            break;
                    }
                }

                // Collect client mod selections NOW on the UI thread (before any await)
                var selectedClientMods = new List<string>();
                string clientPath = ClientPath;
                string clientModdedPath = ClientModdedPath;
                for (int i = 0; i < _clientModCheckboxes.Count && i < PluginRegistry.ClientMods.Length; i++)
                {
                    if (_clientModCheckboxes[i].IsChecked == true)
                    {
                        foreach (var dll in PluginRegistry.ClientMods[i].DllNames)
                            if (!selectedClientMods.Contains(dll))
                                selectedClientMods.Add(dll);
                    }
                }

                int exitCode = await Task.Run(async () =>
                {
                    var backupService = new TabgInstaller.Core.Services.BackupService(progress);
                    if (Directory.Exists(serverDir) && Directory.GetFileSystemEntries(serverDir).Length > 0)
                    {
                        ((IProgress<string>)progress).Report("Creating backup...");
                        await backupService.CreateBackupAsync(serverDir);
                    }

                    var installer = new TabgInstaller.Core.Installer(gameDir: serverDir, log: progress);
                    return await installer.RunAsync(
                        serverDir: serverDir,
                        serverName: "enormous",
                        serverPassword: "enormous",
                        serverDescription: "enormous",
                        starterPackTag: "",
                        citrusLibTag: DefaultCitrusTag,
                        skipStarterPack: true,
                        skipCitruslib: skipCitrus,
                        installCommunityServer: installCommunityServer,
                        bundledPlugins: bundled,
                        ct: _cts.Token);
                });

                if (exitCode == 0)
                {
                    InstallProgress.Value = 95;
                    TxtInstallStage.Text = "Server done, installing client mods...";

                    // Install client mods if client path was set
                    if (!string.IsNullOrEmpty(clientPath) && Directory.Exists(clientPath)
                        && !string.IsNullOrEmpty(clientModdedPath) && selectedClientMods.Count > 0)
                    {
                        ((IProgress<string>)progress).Report("Installing client mods...");
                        await Task.Run(() =>
                            ClientModInstaller.InstallAsync(clientPath, clientModdedPath, selectedClientMods, progress));
                    }

                    InstallProgress.Value = 100;
                    TxtInstallStage.Text = "Complete!";

                    _appSettings.MarkSetupComplete(serverDir, clientPath, clientModdedPath);
                    SetupCompleted = true;

                    // Brief pause so user sees "Complete!", then close
                    await Task.Delay(800);
                    DialogResult = true;
                }
                else
                {
                    TxtInstallStage.Text = "Failed";
                    _toast.Error("Installation failed. Check the log for details.");
                    BtnBack.IsEnabled = true;
                    BtnBack.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                TxtInstallStage.Text = "Failed";
                _toast.Error($"Installation error: {ex.Message}");
                BtnBack.IsEnabled = true;
                BtnBack.Visibility = Visibility.Visible;
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                BtnCancel.Visibility = Visibility.Collapsed;
            }
        }
    }
}
