using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ClientPanel : UserControl
    {
        public ClientPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var clientPath = Installer.TryFindTabgClientPath();
            if (!string.IsNullOrEmpty(clientPath))
            {
                ClientPathBox.Text = clientPath;
                var parent = Path.GetDirectoryName(clientPath);
                if (parent != null)
                    ClientModdedPathBox.Text = Path.Combine(parent, "TABG_Modded");
            }
        }

        private void BtnLaunchModdedTabg_Click(object sender, RoutedEventArgs e)
        {
            string moddedDir = ClientModdedPathBox.Text.Trim();
            string exe = Path.Combine(moddedDir, "TotallyAccurateBattlegrounds.exe");
            if (!File.Exists(exe))
            {
                ToastService.Instance.Warning("Modded TABG not found. Install client mods first.");
                return;
            }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = moddedDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ToastService.Instance.Error($"Failed to launch: {ex.Message}");
            }
        }

        private async void BtnInstallClientMods_Click(object sender, RoutedEventArgs e)
        {
            string clientDir = ClientPathBox.Text.Trim();
            string moddedDir = ClientModdedPathBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(clientDir) || !Directory.Exists(clientDir))
            {
                ToastService.Instance.Warning("Please enter a valid TABG Steam folder path.");
                return;
            }
            if (string.IsNullOrWhiteSpace(moddedDir))
            {
                ToastService.Instance.Warning("Please enter a folder path for the modded TABG copy.");
                return;
            }

            var selectedClientPlugins = new List<string>();
            if (ChkClientFlyingControls.IsChecked == true) selectedClientPlugins.Add("TabgInstaller.FlyingControls.dll");
            if (ChkClientEnhancedTabg.IsChecked == true) selectedClientPlugins.Add("Enhanced TABG.dll");
            if (ChkClientCoordsDisplay.IsChecked == true) selectedClientPlugins.Add("TabgInstaller.CoordsDisplay.dll");
            if (ChkClientBigSmoke.IsChecked == true || ChkClientMGLFlashbang.IsChecked == true) selectedClientPlugins.Add("TabgInstaller.CustomGrenades.dll");
            if (ChkClientModSettings.IsChecked == true) selectedClientPlugins.Add("TabgInstaller.ModSettings.dll");
            if (ChkClientPopupBlocker.IsChecked == true) selectedClientPlugins.Add("Pop-up Blocker.dll");
            if (ChkClientProximityChat.IsChecked == true)
            {
                selectedClientPlugins.Add("TabgInstaller.ProximityChat.Client.dll");
            }
            if (ChkClientHuntMode.IsChecked == true)
            {
                selectedClientPlugins.Add("TabgInstaller.HuntMode.Client.dll");
                selectedClientPlugins.Add("TabgInstaller.HuntMode.Shared.dll");
            }
            if (ChkClientJuggernautMode.IsChecked == true) selectedClientPlugins.Add("JuggernautMode.Client.dll");
            bool installVR = ChkClientTabgVR.IsChecked == true;
            if (installVR) selectedClientPlugins.Add("TABGVR.dll");

            if (selectedClientPlugins.Count == 0)
            {
                ToastService.Instance.Warning("Please select at least one client mod.");
                return;
            }

            bool alreadyExists = Directory.Exists(moddedDir) && File.Exists(Path.Combine(moddedDir, "TotallyAccurateBattlegrounds.exe"));

            var result = MessageBox.Show(
                alreadyExists
                    ? $"Modded copy already exists at:\n{moddedDir}\n\nThis will update the mods. Continue?"
                    : $"This will copy TABG to:\n{moddedDir}\n\nThen install BepInEx + {selectedClientPlugins.Count} mod(s).\n\nTo play: open Steam, then run the exe from the modded folder.\n\nContinue?",
                "Install Client Mods", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            SetUiEnabled(false);

            var progress = new Progress<string>(line =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    TxtLog.AppendText(line + Environment.NewLine);
                    LogScrollViewer.ScrollToEnd();
                });
            });

            try
            {
                TxtLog.AppendText("=== Installing Client Mods ===" + Environment.NewLine);
                bool success = await Task.Run(() => ClientModInstaller.InstallAsync(clientDir, moddedDir, selectedClientPlugins, progress));

                // Install TABGVR extras (XR runtime, OpenXR loader) if selected
                if (success && installVR)
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            var vrZipCandidates = new[]
                            {
                                Path.Combine(baseDir, "tabgvr", "TABGVR.zip"),
                                Path.Combine(baseDir, "..", "tabgvr", "TABGVR.zip"),
                                Path.Combine(baseDir, "..", "..", "tabgvr", "TABGVR.zip"),
                                Path.Combine(baseDir, "..", "..", "..", "tabgvr", "TABGVR.zip"),
                            };

                            string vrZip = null;
                            foreach (var c in vrZipCandidates)
                                if (File.Exists(c)) { vrZip = Path.GetFullPath(c); break; }

                            if (vrZip != null)
                            {
                                ((IProgress<string>)progress).Report("Installing TABGVR extras (XR runtime, OpenXR)...");
                                ZipFile.ExtractToDirectory(vrZip, moddedDir, overwriteFiles: true);
                                ((IProgress<string>)progress).Report("TABGVR VR runtime installed.");
                            }
                            else
                            {
                                ((IProgress<string>)progress).Report("WARNING: TABGVR.zip not found — VR runtime files not installed. TABGVR.dll was installed but VR may not work without the runtime.");
                            }
                        }
                        catch (Exception vrEx)
                        {
                            ((IProgress<string>)progress).Report($"WARNING: Failed to extract TABGVR extras: {vrEx.Message}");
                        }
                    });
                }

                if (success)
                    ToastService.Instance.Success($"Client mods installed! Modded TABG is at: {moddedDir}");
                else
                    ToastService.Instance.Warning("Client mod installation had errors. Check the log.");
            }
            catch (Exception ex)
            {
                TxtLog.AppendText($"ERROR: {ex.Message}" + Environment.NewLine);
                ToastService.Instance.Error($"Error: {ex.Message}");
            }
            finally
            {
                SetUiEnabled(true);
            }
        }

        private void SetUiEnabled(bool isEnabled)
        {
            BtnLaunchModdedTabg.IsEnabled = isEnabled;
            ClientPathBox.IsEnabled = isEnabled;
            ClientModdedPathBox.IsEnabled = isEnabled;
        }
    }
}
