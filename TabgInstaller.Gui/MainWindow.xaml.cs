using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
// using System.Threading.Tasks; // Not explicitly used by user's new BtnInstall_Click, but Installer.RunAsync is async
using System.Windows;
using TabgInstaller.Core; // For Installer class
// using TabgInstaller.Core.Services; // Will use Installer's static methods instead of GitHubService directly
using System.Linq;
using System.Windows.Controls;
using System.Media;

namespace TabgInstaller.Gui
{
    public partial class MainWindow : Window
    {
        // private List<string>? _validWords; // CS0169: Unused field, removing.

        public MainWindow()
        {
            InitializeComponent();
            
            var detectedPath = Installer.TryFindTabgServerPath();
            if (!string.IsNullOrEmpty(detectedPath))
            {
                PathBox.Text = detectedPath;
            }

            try
            {
                // Use Installer's static method, handle async in constructor
                Installer.EnsureWordListLoadedAsync(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
                // The original example used a synchronous EnsureWordListLoaded on GitHubService and stored result.
                // Since Installer.EnsureWordListLoadedAsync stores it in a static field in Installer, 
                // we don't strictly need to store it in _validWords here if ValidateOneWord also uses Installer's static list.
                // However, user's ValidateOneWord example takes _validWords. Let's stick to their pattern for now
                // by fetching it once if possible, or adapting ValidateOneWord call.

                // For consistency with user's new code wanting _validWords, let's assume EnsureWordListLoadedAsync
                // could be modified or a new synchronous version created that returns the list.
                // For now, will rely on Installer's internal static list and adjust ValidateOneWord call.
                // OR, if Installer._allowedWords can be exposed (e.g. via a getter) after loading.

                // Simplest path: Installer.ValidateOneWord uses its own static list, so _validWords field here might not be needed.
                // User's example: if (!GitHubService.ValidateOneWord(serverName, _validWords))
                // If Installer.ValidateOneWord doesn't take the list, this needs to change.
                // Current Installer.ValidateOneWord(string fieldName, string candidate) does NOT take a list.
                // It uses its internal static _allowedWords.
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Fehler beim Laden der Wörterliste:\n{ex.Message}",
                    "Initialisierungsfehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Application.Current.Shutdown();
                return;
            }

            TxtLog.TextChanged += (s, e) =>
            {
                TxtLog.ScrollToEnd();
            };

            // build suggestions source
            // _suggestions = Installer.AllowedWords.ToList(); // Obsolete with new UI

            // Populate auto-complete sources once the word list is loaded
            if (Installer.AllowedWords.Count > 0)
            {
                var words = Installer.AllowedWords.ToList();
                TxtServerName.ItemsSource        = words;
                TxtServerPassword.ItemsSource    = words;
                TxtServerDescription.ItemsSource = words;
            }
        }

        // All word-suggestion logic is obsolete with the new UI design.
        // private readonly List<string> _suggestions;
        // private void ShowSuggestions(TextBox source, ListBox list) { ... }
        // private void ServerName_TextChanged(...) { ... }
        // private void SuggestionSelected(...) { ... }
        // private void ServerPassword_TextChanged(...) { ... }
        // private void PassSuggestionSelected(...) { ... }
        // private void ServerDesc_TextChanged(...) { ... }
        // private void DescSuggestionSelected(...) { ... }

        // Retaining Browse_Click for the PathBox
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Please paste the path to your server directory into the text box manually.", "Manual Path Entry");
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            string serverName     = TxtServerName.Text.Trim();
            string serverPassword = TxtServerPassword.Text.Trim();
            string serverDesc     = TxtServerDescription.Text.Trim();
            string citrusTag      = TxtCitrusTag.Text.Trim();
            bool   skipCitrus     = ChkSkipCitruslib.IsChecked == true;
            bool   installCommunityServer = ChkInstallCommunityServer.IsChecked == true;
            bool   installAntiCheatRemover = ChkInstallAntiCheatRemover.IsChecked == true;
            // bool isPublic = ChkPublicServer.IsChecked == true; // value read but not used yet

            // Path comes from PathBox now
            string serverDir = PathBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(serverDir))
            {
                MessageBox.Show(
                    "Bitte wählen Sie einen gültigen TABG-Serverordner aus.",
                    "Ordner nicht ausgewählt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }
            if (!Directory.Exists(serverDir))
            {
                MessageBox.Show(
                    "Der Pfad '" + serverDir + "' existiert nicht.\nBitte wählen Sie einen gültigen TABG-Serverordner aus.",
                    "Ordner nicht gefunden",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            if (serverName.Length == 0 ||
                // Password can be empty, so no check here
                serverDesc.Length == 0 ||
                (citrusTag.Length == 0 && !skipCitrus) )        // Tag only required if not skipping
            {
                MessageBox.Show(
                    "Bitte füllen Sie Server Name/Beschreibung aus. Release-Tags sind nur nötig, wenn das Plugin nicht übersprungen wird.",
                    "Ungültige Eingabe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            try
            {
                // Use Installer's static ValidateOneWord, which uses its internal static list
                Installer.ValidateOneWord("Server-Name", serverName);
                // Only validate password if it's not empty
                if (!string.IsNullOrEmpty(serverPassword))
                Installer.ValidateOneWord("Server-Passwort", serverPassword);
                Installer.ValidateOneWord("Server-Beschreibung", serverDesc);
            }
            catch (InvalidOperationException ex)
            {
                 MessageBox.Show(ex.Message, "Eingabe ungültig", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
            }
            catch (Exception ex) // Should not happen if ValidateOneWord only throws InvalidOperationException
            {
                 MessageBox.Show($"Fehler bei der Eingabevalidierung: {ex.Message}", "Validierungsfehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SetUiEnabled(false);
            TxtLog.Text = ""; 

            var progress = new Progress<string>(line =>
            {
                TxtLog.AppendText(line + Environment.NewLine);
            });

            // CancellationToken for potential future use, not implemented in user's new snippet
            var cts = new CancellationTokenSource(); 

            try
            {
                var installer = new TabgInstaller.Core.Installer(
                    gameDir: serverDir, 
                    log: progress
                );

                int exitCode = await installer.RunAsync(
                    serverDir:          serverDir,
                    serverName:         serverName,
                    serverPassword:     serverPassword,
                    serverDescription:  serverDesc,
                    starterPackTag:     "",
                    citrusLibTag:       citrusTag,
                    skipStarterPack:    false,  // This is the fix: ensure StarterPack is NEVER skipped.
                    skipCitruslib:      skipCitrus,
                    installCommunityServer: installCommunityServer,
                    installAntiCheatRemover: installAntiCheatRemover,
                    ct: cts.Token 
                );

                if (!cts.IsCancellationRequested) // Check if cancelled
                {
                    if (exitCode == 0)
                    {
                        // Play a pleasant sound instead of a popup. Try a Windows chime first, fallback to SystemSounds.
                        try
                        {
                            string winMedia = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", "notify.wav");
                            if (File.Exists(winMedia))
                            {
                                var player = new SoundPlayer(winMedia);
                                player.Play();
                            }
                            else
                            {
                                SystemSounds.Asterisk.Play();
                            }
                        }
                        catch { /* ignore sound errors */ }

                        var cfgWin = new ConfigWindow(serverDir) { Owner = this };
                        cfgWin.Show();
                    }
                    else
                    {
                        MessageBox.Show(
                            $"Installation beendet mit Code {exitCode}. Siehe Protokollfenster.",
                            "Installation fehlgeschlagen",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    }
                }
            }
            catch (OperationCanceledException) // Catch cancellation
            {
                ((System.IProgress<string>)progress).Report("Installation vom Benutzer abgebrochen.");
                MessageBox.Show("Installation abgebrochen.", "Abgebrochen", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                progress.LogException("Unbekannter Fehler während der Installation", ex);
                MessageBox.Show(
                    $"Unbekannter Fehler während der Installation:\n{ex.Message}",
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                SetUiEnabled(true);
                cts.Dispose();
            }
        }

        private void SetUiEnabled(bool isEnabled)
        {
            BtnInstall.IsEnabled           = isEnabled;
            PathBox.IsEnabled              = isEnabled; // Added PathBox
            TxtServerName.IsEnabled        = isEnabled;
            TxtServerPassword.IsEnabled    = isEnabled;
            TxtServerDescription.IsEnabled = isEnabled;
            TxtCitrusTag.IsEnabled         = isEnabled;
            ChkSkipCitruslib.IsEnabled     = isEnabled;
            ChkPublicServer.IsEnabled      = isEnabled; // Added public server checkbox
            ChkInstallCommunityServer.IsEnabled = isEnabled;
            ChkInstallAntiCheatRemover.IsEnabled = isEnabled;
            // Cancel button is not in the new XAML, so no need to manage it here.
        }

        /* Obsolete with new UI design.
        private void NoPwd_Checked(object sender, RoutedEventArgs e)
        {
            bool noPwd = ChkNoPassword.IsChecked == true;
            TxtServerPassword.IsEnabled = !noPwd;
            if (noPwd)
            {
                TxtServerPassword.Text = string.Empty;
                LstPassSuggestions.Visibility = Visibility.Collapsed;
            }
        }
        */
    }
}
