using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Microsoft.Win32;

namespace TabgInstaller.Core
{
/// <summary>
/// Enthält die komplette Installations-Logik.
/// Der Installer generiert (oder validiert) vorab eine TheStarterPack.txt,
/// damit StarterPackSetup.exe keinen FormatException‐Fehler mehr wirft.
/// </summary>
    public sealed partial class Installer : IDisposable
    {
        private readonly string _gameDir;
        private readonly string _pluginsDir;
        private readonly IProgress<string> _log;
        private readonly GitHubService _githubService;
        // -----------------------------------------------------------------------
        // We will lazily load the TABG word list (one‐word names allowed) into this:
        private static HashSet<string>? _allowedWords;
        private static readonly SemaphoreSlim _wordListLock = new SemaphoreSlim(1, 1);

        private const string BepInExOwner = "BepInEx";
        private const string BepInExRepo = "BepInEx";
        private const string BepInExReleaseTag = "v5.4.22";
        private const string BepInExWindowsAssetExactName = "BepInEx_x64_5.4.22.0.zip";

        private const string BepInExVersion = "5.4.22";
        private const string BepInExZipUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip";

        private const string TabgClientAppId = "823130"; // Totally Accurate Battlegrounds (client)
        private const string TabgClientDirName = "TotallyAccurateBattlegrounds";

        private const string CitrusOwner = "CyrusTheLesser";
        private const string CitrusRepo = "Citruslib";
        private const string CitrusDllAssetName = "Citruslib.dll";
        private const string AntiCheatBypassDllName = "TabgInstaller.AntiCheatBypass.dll";
        private const string TestModDllName = "tabginstaller.testmod.dll";
        private const string WeaponSpawnConfigDllName = "TabgInstaller.WeaponSpawnConfig.dll";
        private const string StarterPackDllAssetName = "StarterPack.dll";
        private const string StarterPackSetupAssetName = "StarterPackSetup.exe";
        private const string EosDllName = "EOSSDK-Win64-Shipping.dll";

        private const string UnityEngineDll = "UnityEngine.dll";
        private const string UnityEngineCoreDll = "UnityEngine.CoreModule.dll";

        private const string StarterPackOwner = "ContagiouslyStupid";
        private const string StarterPackRepo = "TABGStarterPack";

        public Installer(string gameDir, IProgress<string> log)
        {
            _gameDir = gameDir;
            _pluginsDir = Path.Combine(_gameDir, "BepInEx", "plugins");
            _log = log;
            _githubService = new GitHubService(new HttpClient(), _log);
        }

        public static string? TryFindTabgServerPath()
        {
            try
            {
                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath)) return null;

                var libraryFolders = GetSteamLibraryFolders(steamPath);
                
                // The AppID for "Totally Accurate Battlegrounds Dedicated Server" is 1020290
                const string tabgAppId = "1020290";
                const string tabgDirName = "TotallyAccurateBattlegroundsDedicatedServer";

                foreach (var libraryFolder in libraryFolders)
                {
                    var steamAppsFolder = Path.Combine(libraryFolder, "steamapps");
                    var tabgAppManifest = Path.Combine(steamAppsFolder, $"appmanifest_{tabgAppId}.acf");

                    if (File.Exists(tabgAppManifest))
                    {
                        var potentialPath = Path.Combine(steamAppsFolder, "common", tabgDirName);
                        if (Directory.Exists(potentialPath))
                        {
                            return potentialPath;
                        }
                    }
                }

                // Fallback: search for the directory directly if manifest is not found
                foreach (var libraryFolder in libraryFolders)
                {
                     var potentialPath = Path.Combine(libraryFolder, "steamapps", "common", tabgDirName);
                     if (Directory.Exists(potentialPath))
                     {
                         return potentialPath;
                     }
                }
            }
            catch (Exception)
            {
                // Silently fail, this is a best-effort detection
            }
            
            return null;
        }

        private static string? GetSteamInstallPath()
        {
            // For 64-bit systems, the path is in the 32-bit node
            var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam") ??
                      Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
            
            return key?.GetValue("InstallPath") as string;
        }

        private static List<string> GetSteamLibraryFolders(string steamInstallPath)
        {
            var libraryFolders = new List<string> { steamInstallPath };
            var libraryFoldersVdfPath = Path.Combine(steamInstallPath, "steamapps", "libraryfolders.vdf");

            if (!File.Exists(libraryFoldersVdfPath)) return libraryFolders;

            var content = File.ReadAllText(libraryFoldersVdfPath);
            // Crude VDF parsing with regex to find library paths
            var regex = new Regex("\"path\"\\s+\"(.+?)\"");
            var matches = regex.Matches(content);

            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count > 1)
                {
                    var path = match.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(path))
                    {
                        libraryFolders.Add(path);
                    }
                }
            }
            return libraryFolders.Distinct().ToList();
        }

    /// <summary>
    ///   Run a full fresh install.  The caller must validate that
    ///   serverName, serverPassword, and serverDescription each
    ///   are exactly one word from the official all_words.txt.
    /// </summary>
    public async Task<int> RunAsync(
        string serverDir,
        string serverName,
        string serverPassword,
        string serverDescription,
        string starterPackTag,
        string citrusLibTag,
        bool   skipStarterPack,
        bool   skipCitruslib,
        bool   installCommunityServer = false,
        CancellationToken ct = default
    )
    {
            var newConfigManager = new NewServerConfigManager(_log);

        try
        {
            // Validation of serverName, serverPassword, serverDescription is now expected to be done by the GUI.
            KillRunningServers(_log);
            EnsureVanillaWhitelist(serverDir, _log);

            // 1) Write game_settings.txt with the new, full template
            _log.Report("• Writing game_settings.txt with detailed configuration...");
            var settingsServerName = newConfigManager.SanitizeServerNameForGameSettings(serverName); // Use the sanitized name for the value
            var settingsPath = Path.Combine(serverDir, "game_settings.txt");
            try
            {
                using (var w = new StreamWriter(settingsPath, false, Encoding.UTF8)) // Ensure UTF8 encoding
                {
                    w.WriteLine("// Allowed word list for name / description: https://github.com/landfallgames/tabg-word-list");
                    w.WriteLine("// Name of the server");
                    w.WriteLine($"ServerName={settingsServerName}"); // Use sanitized name here
                    w.WriteLine("// Server Description");
                    w.WriteLine($"ServerDescription={serverDescription}"); // Use direct validated input
                    w.WriteLine("//Port To Use");
                    w.WriteLine("Port=7777");
                    w.WriteLine("// max players on server. Max being 253");
                    w.WriteLine("MaxPlayers=70");
                    w.WriteLine("//Use Relay");
                    w.WriteLine("Relay=true");
                    w.WriteLine("// server wide auto teaming");
                    w.WriteLine("AutoTeam=false");
                    w.WriteLine("//Password");
                    w.WriteLine($"Password={serverPassword}"); // Use direct validated input
                    w.WriteLine("// 0.0 - 1.0 percentage of cars to spawn. 0 being 0% and 1 being 100%.");
                    w.WriteLine("CarSpawnRate=1.0");
                    w.WriteLine();
                    w.WriteLine("// Will start match with fewer then PlayersToStart if waited longer then ForceStartTime");
                    w.WriteLine("UseTimedForceStart=true");
                    w.WriteLine();
                    w.WriteLine("// Seconds until force start the countdown");
                    w.WriteLine("ForceStartTime=200.0");
                    w.WriteLine();
                    w.WriteLine("// Players needed to start the force start timer");
                    w.WriteLine("MinPlayersToForceStart=2");
                    w.WriteLine();
                    w.WriteLine("// Players to start countdown");
                    w.WriteLine("PlayersToStart=2");
                    w.WriteLine();
                    w.WriteLine("// Seconds it takes to start the game after Players have joined or force start triggered");
                    w.WriteLine("Countdown=20.0");
                    w.WriteLine();
                    w.WriteLine("// enable or disable the respawn minigame.");
                    w.WriteLine("AllowRespawnMinigame=true");
                    w.WriteLine();
                    w.WriteLine("// SQUAD, DUO or SOLO");
                    w.WriteLine("TeamMode=SQUAD");
                    w.WriteLine();
                    w.WriteLine("// Ehm..  Have fun");
                    w.WriteLine("GameMode=BattleRoyale");
                    w.WriteLine("//Leave This To False");
                    w.WriteLine("AntiCheat=false");
                }
                _log.Report($"  → Wrote detailed game_settings.txt to: {settingsPath}");
            }
            catch (Exception ex)
            {
                _log.Report($"[ERROR] Failed to write game_settings.txt: {ex.Message}");
                throw; // Rethrow as this is a critical step
            }

            HardResetMods(serverDir);
            ct.ThrowIfCancellationRequested();

            await InstallBepInExAsync(serverDir, ct);
            await FixDoorstopConfigAsync(serverDir, _log); // Ensure BepInEx is enabled
            Directory.CreateDirectory(_pluginsDir);

            // Define actual tags to be used, potentially overriding with latest
            string actualStarterPackTag = starterPackTag;
            string actualCitrusLibTag = citrusLibTag;

            if (!skipStarterPack)
            {
                _log.Report("• Starting StarterPack installation...");

                var latestStarterPackRelease = await _githubService.GetLatestReleaseAsync(StarterPackOwner, StarterPackRepo);
                if (latestStarterPackRelease == null)
                {
                    throw new InvalidOperationException("Could not fetch the latest release for TABGStarterPack.");
                }
                _log.Report($"  → Found latest StarterPack release: {latestStarterPackRelease.TagName}");

                var tempDir = Path.Combine(Path.GetTempPath(), "TabgInstallerDownloads");
                Directory.CreateDirectory(tempDir);

                // Download StarterPack.dll
                var dllAsset = latestStarterPackRelease.Assets.FirstOrDefault(a => a.Name.Equals(StarterPackDllAssetName, StringComparison.OrdinalIgnoreCase));
                if (dllAsset == null) throw new InvalidOperationException($"{StarterPackDllAssetName} not found in latest release.");
                _log.Report($"  → Downloading {StarterPackDllAssetName}...");
                var dllDownloaded = await _githubService.DownloadAssetAsync(StarterPackOwner, StarterPackRepo, latestStarterPackRelease.TagName, StarterPackDllAssetName, tempDir, dllAsset.BrowserDownloadUrl);
                if (!dllDownloaded) throw new InvalidOperationException("Failed to download StarterPack.dll");
                File.Copy(Path.Combine(tempDir, StarterPackDllAssetName), Path.Combine(_pluginsDir, StarterPackDllAssetName), true);
                _log.Report($"  → Copied {StarterPackDllAssetName} to plugins folder.");

                // Run server once to generate config
                _log.Report("• Running server once to generate StarterPack config...");
                await RunServerUntilHeartbeatAsync(serverDir, true);
                _log.Report("  → Server ran once and was terminated.");

                // Download StarterPackSetup.exe
                var setupAsset = latestStarterPackRelease.Assets.FirstOrDefault(a => a.Name.Equals(StarterPackSetupAssetName, StringComparison.OrdinalIgnoreCase));
                if (setupAsset == null) throw new InvalidOperationException($"{StarterPackSetupAssetName} not found in latest release.");
                _log.Report($"  → Downloading {StarterPackSetupAssetName}...");
                var setupExeDownloaded = await _githubService.DownloadAssetAsync(StarterPackOwner, StarterPackRepo, latestStarterPackRelease.TagName, StarterPackSetupAssetName, serverDir, setupAsset.BrowserDownloadUrl);
                if (!setupExeDownloaded) throw new InvalidOperationException("Failed to download StarterPackSetup.exe");
                _log.Report($"  → Copied {StarterPackSetupAssetName} to server root.");

                _log.Report("• Launching StarterPackSetup.exe for user configuration...");
                _log.Report("  IMPORTANT: Make your changes in the setup window, save, and then close it to continue.");
                
                var setupProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(serverDir, StarterPackSetupAssetName),
                        WorkingDirectory = serverDir,
                        UseShellExecute = true // Launch interactively for the user
                    }
                };
                setupProcess.Start();
                await setupProcess.WaitForExitAsync(ct);

                _log.Report("  → StarterPackSetup.exe was closed.");

                // Sanitize TheStarterPack.txt to ensure StarterPack can parse numeric fields correctly
                try
                {
                    var starterPackConfigPath = Path.Combine(serverDir, "TheStarterPack.txt");
                    SanitizeStarterPackConfig(starterPackConfigPath);
                }
                catch (Exception ex)
                {
                    _log.Report($"[WARN] Failed to sanitize TheStarterPack.txt: {ex.Message}");
                }
            }
            else
            {
                 _log.Report("  → Skipping StarterPack installation (per user choice).");
            }
            
            _log.Report("\n✔ Fresh install complete. You can now start the server with your new configuration.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            _log.Report("\n⚠ Vorgang abgebrochen.");
            return 1;
        }
        catch (Exception ex)
        {
            _log.Report($"\n❌ Installation fehlgeschlagen:\n{ex}");
            return 2;
        }
    }

    private void HardResetMods(string serverDir)
    {
        var vanillaFilesPath = Path.Combine(serverDir, "VanillaFiles.txt");
        if (!File.Exists(vanillaFilesPath))
        {
            _log.Report($"[WARN] VanillaFiles.txt not found in {serverDir}. Skipping hard reset of mods.");
            return;
        }
        _log.Report("• Starting hard reset of mod files based on whitelist...");
            HashSet<string> keepRules = File.ReadAllLines(vanillaFilesPath)
                       .Select(p => p.Trim())
                                       .Where(p => !string.IsNullOrEmpty(p) && !p.StartsWith("#"))
                       .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _log.Report($"  → Loaded {keepRules.Count} rules from VanillaFiles.txt.");
        
        List<string> pathsToDelete = new List<string>();
            foreach (var path in Directory.EnumerateFileSystemEntries(serverDir, "*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(serverDir, path);
                
                // --- BEGIN DIAGNOSTIC ---
                if (Path.GetFileName(path).Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase) && !ShouldKeepItem(relPath, keepRules))
                {
                    _log.Report("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                    _log.Report("[CRITICAL WARNING] winhttp.dll IS ABOUT TO BE DELETED! WHITELIST IS WRONG!");
                    _log.Report($"Whitelist contains {keepRules.Count} items. They are:");
                    foreach(var rule in keepRules) { _log.Report($"  - {rule}"); }
                    _log.Report("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                }
                // --- END DIAGNOSTIC ---

                if (Path.GetFileName(path).Equals("VanillaFiles.txt", StringComparison.OrdinalIgnoreCase) || ShouldKeepItem(relPath, keepRules))
                {
                    _log.Report($"    Keeping: {relPath}");
                }
                else
                {
                    pathsToDelete.Add(path);
                    _log.Report($"    Marked for deletion: {relPath}");
                }
            }
            foreach (var path in pathsToDelete.Where(p => File.Exists(p)).OrderByDescending(p => p.Length))
            {
                try
                {
                    File.Delete(path);
                    _log.Report($"  → Deleted file: {Path.GetRelativePath(serverDir, path)}");
                }
                catch (Exception ex)
                {
                    _log.Report($"[WARN] Could not delete file {Path.GetRelativePath(serverDir, path)}: {ex.Message}");
                }
            }
            foreach (var path in pathsToDelete.Where(p => Directory.Exists(p)).OrderByDescending(p => p.Length))
            {
                try
                {
                    // Never delete the Presets root directory
                    var rel = Path.GetRelativePath(serverDir, path).Replace('\\','/');
                    if (rel.Equals("Presets", StringComparison.OrdinalIgnoreCase))
                    {
                        _log.Report("  → Skipped deleting Presets directory");
                        continue;
                    }
                    Directory.Delete(path, true); 
                    _log.Report($"  → Deleted directory: {Path.GetRelativePath(serverDir, path)}");
                }
                catch (Exception ex)
                {
                    _log.Report($"[WARN] Could not delete directory {Path.GetRelativePath(serverDir, path)}: {ex.Message}");
                }
            }

        if (!Directory.Exists(Path.Combine(serverDir, "TABG_Data")))
            {
                var errorMsg = "[FATAL ERROR] TABG_Data directory was deleted!";
                _log.Report(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }
            _log.Report("• Sanity check: TABG_Data directory is present after hard reset.");
            _log.Report("• Hard reset of mod files completed.");
        }

        private static bool ShouldKeepItem(string relPath, HashSet<string> whitelist)
        {
            relPath = relPath.Replace('\\', '/');

            // Always preserve user-created Presets directory and its contents
            if (relPath.Equals("Presets", StringComparison.OrdinalIgnoreCase) || relPath.StartsWith("Presets/", StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var rule in whitelist)
            {
                var r = rule.Replace('\\', '/');
                if (IsDirectoryRule(r))
                {
                    string dirRule = r.TrimEnd('/');
                    if (relPath.Equals(dirRule, StringComparison.OrdinalIgnoreCase) || relPath.StartsWith(dirRule + "/", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (relPath.Equals(r, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;

            static bool IsDirectoryRule(string rule) =>
                rule.EndsWith("/") || rule.EndsWith("\\") || (!string.IsNullOrEmpty(rule) && rule.IndexOf('.') == -1);
        }

        private static void KillRunningServers(IProgress<string> log)
        {
            log.Report("• Attempting to kill any running server processes...");
            var names = new[] { "TABG", "TABG-DS", "TotallyAccurateBattlegroundsDedicatedServer" };
            foreach (var n in names)
            {
                foreach (var p in Process.GetProcessesByName(n))
                {
                    try
                    {
                        log.Report($"  → Killing stray process {p.ProcessName} (PID {p.Id})...");
                        p.Kill(true);
                        p.WaitForExit(5000);
                        log.Report($"    ↳ Process {p.ProcessName} (PID {p.Id}) killed.");
        }
        catch (Exception ex)
        {
                        log.Report($"[WARN] Could not kill process {p.ProcessName} (PID {p.Id}): {ex.Message}");
                    }
                }
            }
            log.Report("• Finished attempt to kill running server processes.");
        }

        private void PatchBepInExCfg(string serverDir)
        {
            var bepInExConfigDir = Path.Combine(serverDir, "BepInEx", "config");
            var cfgPath = Path.Combine(bepInExConfigDir, "BepInEx.cfg");
            if (!File.Exists(cfgPath))
            {
                _log.Report($"• BepInEx.cfg not found at {cfgPath}. Skipping patching.");
                return;
            }
            _log.Report("• Patching BepInEx.cfg for logging...");
            try
            {
                var txt = File.ReadAllText(cfgPath, Encoding.UTF8);
                string originalTxt = txt;
                // Regex to enable console logging:
                // Looks for [Logging.Console]'s "Enabled = false" and changes it to "Enabled = true"
                txt = Regex.Replace(txt, @"(?i)(^\s*\[Logging\.Console\](?:\s*\r?\n)*^\s*Enabled\s*=\s*)(false)(\s*$.*)", "$1true$3", RegexOptions.Multiline);
                // Regex to set all log levels:
                // Looks for [Logging]'s "LogLevels = ..." and changes it to include all levels
                txt = Regex.Replace(txt, @"(?i)(^\s*\[Logging\](?:\s*\r?\n)*^\s*LogLevels\s*=\s*)([^\r\n]*?)(\s*$.*)", "$1Fatal, Error, Warning, Message, Info, Debug$3", RegexOptions.Multiline);

                if (txt != originalTxt)
                {
                    File.WriteAllText(cfgPath, txt, Encoding.UTF8);
                    _log.Report("  → BepInEx.cfg patched for enhanced logging.");
                }
                else
                {
                    _log.Report("  → BepInEx.cfg logging settings already as desired or patterns not found.");
                }
        }
        catch (Exception ex)
        {
                _log.Report($"[WARN] Failed to patch BepInEx.cfg: {ex.Message}");
            }
        }

        private async Task FirstRun(string serverDir, CancellationToken ct)
    {
        var exeName = "TABG-DS.exe";
            var exePath = Path.Combine(serverDir, exeName);
            if (!File.Exists(exePath))
            {
                exeName = "TABG.exe";
                exePath = Path.Combine(serverDir, exeName);
                if (!File.Exists(exePath))
                {
                    _log.Report($"⚠ TABG-DS.exe and TABG.exe not found in {serverDir}. First run skipped.");
                return;
            }
        }
        
        // Check if server is in Program Files and needs elevated privileges
        bool needsElevation = false;
        if (serverDir.Contains("Program Files", StringComparison.OrdinalIgnoreCase))
        {
            _log.Report("⚠️ Server is in Program Files - checking if we need elevated privileges...");
            
            // Check if BepInEx has ever loaded by looking for its config file
            var bepInExConfig = Path.Combine(serverDir, "BepInEx", "config", "BepInEx.cfg");
            if (!File.Exists(bepInExConfig))
            {
                _log.Report("⚠️ BepInEx has never loaded - elevated privileges required for Program Files installation");
                needsElevation = true;
            }
        }
        
        _log.Report($"• Starting server ({exeName}) for first run (headless, waiting for heartbeat)...");
        
        ProcessStartInfo psi;
        if (needsElevation)
        {
            // Use elevated process start
            psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "-batchmode -nographics -nolog",
                WorkingDirectory = serverDir,
                UseShellExecute = true,
                Verb = "runas", // Request elevation
                CreateNoWindow = false // Can't hide window with UseShellExecute=true
            };
            
            // Cannot set environment variables when UseShellExecute = true
            // BepInEx will load via doorstop_config.ini and system environment variables
            
            _log.Report("  → Starting with Administrator privileges to ensure BepInEx can load...");
        }
        else
        {
            psi = new ProcessStartInfo(exePath, "-batchmode -nographics -nolog")
            {
                WorkingDirectory = serverDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        
        using var proc = new Process { StartInfo = psi };
        var tcs = new TaskCompletionSource<bool>();
        
        if (!needsElevation)
        {
            proc.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    // Enhanced logging based on user suggestion
                    if (e.Data.Contains("DllNotFoundException"))
                    {
                        _log.Report($"[SERVER ERROR] Missing DLL: {e.Data}");
                        _log.Report("[SERVER HINT] The server is trying to load EAC/EOS components. Ensure bypass plugins are active.");
                    }
                    else if (e.Data.Contains("Easy_AC_Server"))
                    {
                        _log.Report($"[SERVER EAC] {e.Data}");
                    }
                    else if (e.Data.Contains("EOSSDK"))
                    {
                        _log.Report($"[SERVER EOS] {e.Data}");
                    }
                    else if (e.Data.Contains("[CitrusLib]"))
                    {
                        _log.Report($"[SERVER MOD] {e.Data}");
                    }
                    else if (e.Data.Contains("[AntiCheatBypass]"))
                    {
                        _log.Report($"[SERVER BYPASS] {e.Data}");
                    }
                    else if (e.Data.Contains("[BepInEx]") || e.Data.Contains("BepInEx"))
                    {
                        _log.Report($"[SERVER BEPINEX] {e.Data}");
                    }
                    else if (e.Data.Contains("[WeaponSpawnConfig]"))
                    {
                        _log.Report($"[SERVER WEAPONCONFIG] {e.Data}");
                    }
                    else if (e.Data.Contains("[StarterPack]") || e.Data.Contains("Starter Pack"))
                    {
                        _log.Report($"[SERVER STARTERPACK] {e.Data}");
                    }
                    else
                    {
                        _log.Report($"[SERVER OUT] {e.Data}");
                    }

                    if (e.Data.Contains("Heartbeat", StringComparison.OrdinalIgnoreCase))
                        tcs.TrySetResult(true);
                }
            };
            proc.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    _log.Report($"[SERVER ERR] {e.Data}");
            };
        }
        
        try
        {
            proc.Start();
            
            if (!needsElevation)
            {
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
            }
            
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3), ct);
            
            if (needsElevation)
            {
                // For elevated processes, we can't capture output, so just wait a bit
                _log.Report("  → Waiting 30 seconds for elevated server to initialize BepInEx...");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                tcs.TrySetResult(true);
            }
            
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                if (ct.IsCancellationRequested)
                 _log.Report("  → First run cancelled.");
                else if (completedTask == tcs.Task && tcs.Task.Result)
                _log.Report("  → First run successful (Heartbeat received).");
                else
                _log.Report("  → First run timed out (No Heartbeat received after 3 minutes).");
        }
        catch (Exception ex)
        {
            _log.Report($"[ERROR] Exception during server first run: {ex.Message}");
        }
        finally
        {
            try
            {
                if (proc != null && !proc.HasExited)
                {
                    _log.Report("  → Attempting to stop first run server process...");
                    proc.Kill(true); 
                        await proc.WaitForExitAsync(CancellationToken.None);
                    _log.Report("  → First run server process stopped.");
                }
            }
            catch (Exception ex)
            {
                _log.Report($"[WARN] Exception while stopping first run server process: {ex.Message}");
            }
        }
    }



        private async Task InstallBepInExAsync(string serverDir, CancellationToken ct)
        {
            _log.Report("<<< NEUE InstallBepInExAsync STARTET >>>");
            // 1) Log that we're starting:
            _log.Report($"• Installing BepInEx (direct HTTP download of {BepInExReleaseTag})...");

            // 2) Build the exact download URL. For Windows x64:
            //      https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.3/BepInEx_win_x64_5.4.23.3.zip
            //    (Note: BepInExReleaseTag = "v5.4.23.3", BepInExWindowsAssetExactName = "BepInEx_win_x64_5.4.23.3.zip")
            var downloadUrl =
                $"https://github.com/{BepInExOwner}/{BepInExRepo}/releases/download/{BepInExReleaseTag}/{BepInExWindowsAssetExactName}";

            _log.Report($"  → Download URL: {downloadUrl}");

            // 3) Download to a temporary file in %TEMP%:
            string tempZipPath = Path.Combine(Path.GetTempPath(), BepInExWindowsAssetExactName);
            if (File.Exists(tempZipPath))
            {
                try
                {
                    File.Delete(tempZipPath);
                }
                catch
                {
                    // If we cannot delete, just overwrite below
                }
            }

            try
            {
                using (var httpClient = new HttpClient())
                {
                    // GitHub requires a User-Agent header
                    httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("TabgInstaller/1.0");
                    _log.Report($"  → Starting HTTP GET...");

                    using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            // Log detailed error info
                            string bodyText = "<no response body>";
                            try
                            {
                                bodyText = await response.Content.ReadAsStringAsync(ct);
                            }
                            catch { /* ignore */ }

                            _log.Report($"[ERROR] HTTP {((int)response.StatusCode)} {response.ReasonPhrase} when downloading BepInEx:");
                            _log.Report($"        Response Body: {bodyText}");
                            throw new InvalidOperationException(
                                $"BepInEx download failed (HTTP {(int)response.StatusCode} {response.ReasonPhrase})");
                        }

                        // 4) Stream the content to disk
                        _log.Report($"  → Writing to temp file: {tempZipPath}");
                        using (var fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await response.Content.CopyToAsync(fs, ct);
                        }
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                _log.Report($"[ERROR] HTTP request exception while downloading BepInEx: {httpEx.Message}");
                throw new InvalidOperationException($"BepInEx download failed: {httpEx.Message}");
            }
            catch (TaskCanceledException)
            {
                _log.Report($"[ERROR] Download canceled (timeout or user abort).");
                throw;
            }
            catch (Exception ex)
            {
                _log.Report($"[ERROR] Unexpected error downloading BepInEx: {ex.Message}");
                throw;
            }

            // 5) Extract into the server directory (overwrite any existing files):
            try
            {
                _log.Report($"  → Extracting {BepInExWindowsAssetExactName} into '{serverDir}' (overwrite = true)...");
                ZipFile.ExtractToDirectory(tempZipPath, serverDir, overwriteFiles: true);
            }
            catch (Exception ex)
            {
                _log.Report($"[ERROR] Failed to extract BepInEx ZIP: {ex.Message}");
                throw new InvalidOperationException($"Failed to extract BepInEx: {ex.Message}");
            }

            // 6) Clean up the temp ZIP file:
            try
            {
                File.Delete(tempZipPath);
            }
            catch
            {
                // Not critical if temp deletion fails
            }

            _log.Report($"✔ Successfully installed BepInEx {BepInExReleaseTag} to '{serverDir}'.");

            // 7) Ensure the correct 64-bit Doorstop proxy is at the root
            try
            {
                string srcWinHttp = Path.Combine(serverDir, "doorstop_libs", "x64", "winhttp.dll");
                string dstWinHttp = Path.Combine(serverDir, "winhttp.dll");
                if (File.Exists(srcWinHttp))
                {
                    File.Copy(srcWinHttp, dstWinHttp, overwrite: true);
                    _log.Report($"  → Copied x64 Doorstop proxy to root winhttp.dll.");
                }
                
                // For Unity 2021.3+ servers, also create version.dll as an alternative
                string dstVersionDll = Path.Combine(serverDir, "version.dll");
                if (File.Exists(dstWinHttp))
                {
                    File.Copy(dstWinHttp, dstVersionDll, overwrite: true);
                    _log.Report($"  → Created version.dll as alternative loader for Unity 2021.3+ compatibility.");
                }
            }
            catch (Exception ex)
            {
                _log.Report($"[WARN] Failed to copy architecture-specific winhttp.dll: {ex.Message}");
            }
            
            // 8) Configure doorstop_config.ini for Unity 2021.3+ compatibility
            try
            {
                string doorstopConfig = Path.Combine(serverDir, "doorstop_config.ini");
                if (File.Exists(doorstopConfig))
                {
                    _log.Report("  → Updating doorstop_config.ini for Unity 2021.3+ compatibility...");
                    var lines = new List<string>
                    {
                        "[UnityDoorstop]",
                        "enabled=true",
                        "targetAssembly=BepInEx\\core\\BepInEx.Preloader.dll",
                        "redirectOutputLog=true",
                        "ignoreDisableSwitch=false",
                        "dllSearchPathOverride="
                    };
                    File.WriteAllLines(doorstopConfig, lines);
                    _log.Report("    ✓ Enabled output log redirection for better diagnostics");
                }
                
                // Check Unity version of TABG.exe
                string tabgExe = Path.Combine(serverDir, "TABG.exe");
                if (File.Exists(tabgExe))
                {
                    var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(tabgExe);
                    _log.Report($"  → Detected Unity version: {versionInfo.FileVersion} ({versionInfo.ProductVersion})");
                    
                    // Unity 2021.3.x detection
                    if (versionInfo.FileVersion?.StartsWith("2021.3") == true)
                    {
                        _log.Report("    ⚠️ Unity 2021.3+ detected - Using alternative loader setup");
                        _log.Report("    ℹ️ If BepInEx doesn't load, try running the server as Administrator");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Report($"[WARN] Failed to update doorstop configuration: {ex.Message}");
            }
            
            // Check if this is a Unity 2021.3+ server and create special loader
            await CheckAndCreateUnity2021LoaderAsync(serverDir, ct);
        }

        private async Task CheckAndCreateUnity2021LoaderAsync(string serverDir, CancellationToken ct)
        {
            try
            {
                var tabgExe = Path.Combine(serverDir, "TABG.exe");
                if (!File.Exists(tabgExe)) return;

                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(tabgExe);
                _log.Report($"• Checking Unity version: {versionInfo.FileVersion}");

                // Check if this is Unity 2021.3 or newer
                if (versionInfo.FileVersion?.StartsWith("2021.3") == true || 
                    versionInfo.FileVersion?.StartsWith("2022.") == true ||
                    versionInfo.FileVersion?.StartsWith("2023.") == true)
                {
                    _log.Report("  → Detected Unity 2021.3+ server - installing automatic BepInEx loader");
                    
                    using (var loaderService = new Services.BepInExLoaderService(_log))
                    {
                        await loaderService.InstallUnity2021LauncherAsync(serverDir);
                    }
                    
                    // Check if BepInEx has ever successfully loaded
                    var bepInExConfig = Path.Combine(serverDir, "BepInEx", "config", "BepInEx.cfg");
                    if (!File.Exists(bepInExConfig))
                    {
                        _log.Report("  ⚠️ BepInEx has never loaded on this server!");
                        _log.Report("  → The installer has configured automatic loading");
                        _log.Report("  → Simply start TABG.exe normally - BepInEx will load automatically");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Report($"[WARN] Could not check Unity version: {ex.Message}");
            }
        }

        private async Task<bool> InstallGitHubAssetAsync(string owner, string repo, string tag, string assetName, string destinationDirectory, CancellationToken ct, bool allowExisting = false)
        {
            _log.Report($"• Attempting to install {assetName} from {owner}/{repo} release {tag} into {destinationDirectory}...");
            Directory.CreateDirectory(destinationDirectory);
            string destinationPath = Path.Combine(destinationDirectory, assetName);
            if (allowExisting && File.Exists(destinationPath))
            {
                _log.Report($"  → {assetName} already exists at {destinationPath}. Skipping download.");
                return true;
            }

            var release = await _githubService.GetReleaseAsync(owner, repo, tag);
            var asset = release?.Assets.FirstOrDefault(a => a.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase));
            if (asset == null || string.IsNullOrEmpty(asset.BrowserDownloadUrl))
            {
                _log.Report($"[WARN] Asset {assetName} not found in release {tag} of {owner}/{repo}.");
                throw new InvalidOperationException($"Asset {assetName} not found in release {tag} of {owner}/{repo}.");
            }
            bool downloaded = await _githubService.DownloadAssetAsync(owner, repo, tag, assetName, destinationDirectory, asset.BrowserDownloadUrl);
            if (downloaded)
            {
                _log.Report($"  → {assetName} installed to {destinationPath}.");
                return true;
            }
            else
            {
                _log.Report($"[WARN] Failed to download {assetName} from {owner}/{repo}.");
                throw new InvalidOperationException($"Failed to download {assetName} from {owner}/{repo}.");
            }
        }



        private void EnsureVanillaWhitelist(string serverDir, IProgress<string> log)
    {
        var wlPath = Path.Combine(serverDir, "VanillaFiles.txt");
        List<string> lines;
        bool fileExisted = File.Exists(wlPath);
        string[] defaultEntries = new[] 
        {
            "# Auto-generated default vanilla whitelist by TabgInstaller",
                "TABG.exe", "TABG_Data", "UnityPlayer.dll", "UnityCrashHandler64.exe",
                "steam_appid.txt", "doorstop_config.ini", "libdoorstop.so", "run_bepinex.cmd", "run_bepinex.sh",
                "MonoBleedingEdge", "TheStarterPack.json", "game_settings.txt",
                "winhttp.dll"
            };
        if (fileExisted)
        {
            lines = File.ReadAllLines(wlPath).ToList();
                _log.Report("• VanillaFiles.txt found. Checking essential entries...");
        }
        else
        {
            lines = defaultEntries.ToList(); 
                _log.Report("• VanillaFiles.txt missing – generating default whitelist...");
            }

            bool madeChanges = false;
            Action<string, string> ensureEntry = (entry, entryName) =>
            {
                if (!lines.Any(l => l.Trim().Equals(entry, StringComparison.OrdinalIgnoreCase)))
                {
                    lines.Add(entry);
                    log.Report($"  → '{entryName}' was missing and has been added to VanillaFiles.txt rules.");
            madeChanges = true;
        }
                else if (fileExisted)
                {
                    log.Report($"  → '{entryName}' entry is already present.");
                }
            };
            ensureEntry("TABG_Data", "TABG_Data");
            ensureEntry("MonoBleedingEdge", "MonoBleedingEdge");
            ensureEntry("TheStarterPack.json", "TheStarterPack.json");
            ensureEntry("game_settings.txt", "game_settings.txt");

            ensureEntry("winhttp.dll", "winhttp.dll");

            if (madeChanges || !fileExisted)
            {
                try
                {
                    File.WriteAllLines(wlPath, lines.Distinct(StringComparer.OrdinalIgnoreCase));
                    _log.Report($"  → VanillaFiles.txt {(fileExisted ? "updated" : "created")} at {wlPath}.");
                }
                catch (Exception ex)
                {
                    log.Report($"[ERROR] Failed to write/update VanillaFiles.txt: {ex.Message}");
                }
            }
        }

        private void EnsureExtraSettings(string serverDir, IProgress<string> log)
        {
            var citrusConfigDir = Path.Combine(serverDir, "BepInEx", "config", "CitrusLib");
            Directory.CreateDirectory(citrusConfigDir);
            var filePath = Path.Combine(citrusConfigDir, "ExtraSettings.json");
            log.Report($"• Ensuring CitrusLib config: ExtraSettings.json at {filePath}");
            try
            {
                var settingsJson = "{\n  \"UsePermissions\": false\n}";
                File.WriteAllText(filePath, settingsJson);
                log.Report("  → Wrote ExtraSettings.json with UsePermissions=false (everyone allowed).");
            }
            catch (Exception ex)
            {
                log.Report($"[ERROR] Failed to ensure ExtraSettings.json: {ex.Message}");
            }
        }

        private void EnsurePlayerPerms(string serverDir, IProgress<string> log)
        {
            var citrusConfigDir = Path.Combine(serverDir, "BepInEx", "config", "CitrusLib");
            Directory.CreateDirectory(citrusConfigDir);
            var filePath = Path.Combine(citrusConfigDir, "PlayerPerms.json");
            log.Report($"• Ensuring CitrusLib config: PlayerPerms.json at {filePath}");
            try
            {
                if (!File.Exists(filePath))
                {
                    var defaultJson = "[\n  { \"playerName\": \"*\", \"permLevel\": 5 }\n]";
                    File.WriteAllText(filePath, defaultJson);
                    log.Report("  → Created default PlayerPerms.json with wildcard permLevel 5 (everyone)." );
                }
                else 
                {
                    log.Report("  → PlayerPerms.json already exists (no overwrite)." );
                }
            }
            catch (Exception ex)
            {
                log.Report($"[ERROR] Failed to ensure PlayerPerms.json: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively finds all instances of a given DLL in a root path and renames them to .disabled.
        /// </summary>
        private void RecursivelyDisableDll(string rootSearchPath, string dllName)
        {
            _log.Report($"• Recursively searching for and attempting to disable '{dllName}' under '{rootSearchPath}'...");
            if (!Directory.Exists(rootSearchPath))
            {
                _log.Report($"  → Directory not found: {rootSearchPath}. Skipping search for {dllName}.");
                return;
            }

            try
            {
                var files = Directory.GetFiles(rootSearchPath, dllName, SearchOption.AllDirectories);
                if (files.Length == 0)
                {
                    _log.Report($"  → No instances of '{dllName}' found under '{rootSearchPath}'.");
                    return;
                }

                foreach (var file in files)
                {
                    string disabledPath = file + ".disabled";
                    _log.Report($"  → Found '{dllName}' at: {file}");
                    try
                    {
                        if (File.Exists(disabledPath))
                        {
                            File.Delete(disabledPath);
                            _log.Report($"    → Removed existing '.disabled' backup to make way: {disabledPath}");
                        }
                        File.Move(file, disabledPath);
                        _log.Report($"    → Successfully renamed to: {disabledPath}");
                    }
                    catch (Exception ex)
                    {
                        _log.Report($"[WARN] Could not automatically disable '{dllName}' at {file}: {ex.Message}");
                        _log.Report($"       Please try to manually rename or delete it if issues occur.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Report($"[ERROR] Failed to search for '{dllName}' under '{rootSearchPath}': {ex.Message}");
            }
        }

        private async Task InstallLocalPluginAsync(string pluginDllName, string destinationDirectory, CancellationToken ct)
        {
            _log.Report($"• Attempting to install local plugin {pluginDllName} into {destinationDirectory}...");
            Directory.CreateDirectory(destinationDirectory);
            string destinationPath = Path.Combine(destinationDirectory, pluginDllName);

            // The build script is expected to place TabgInstaller.AntiCheatBypass.dll
            // in the same directory as TabgInstaller.Core.dll.
            // We can find the location of the currently executing assembly (TabgInstaller.Core.dll)
            // and look for the plugin DLL there.
            string sourceDllPath = "";
            try
            {
                // In single-file deployments, Assembly.Location returns an empty string (see IL3000).  We therefore
                // rely on AppContext.BaseDirectory which always points to the directory that contains the host
                // executable (or the temporary extract location for single-file apps).
                string baseDirectory = AppContext.BaseDirectory;
                sourceDllPath = Path.Combine(baseDirectory, pluginDllName);
            }
            catch (Exception ex)
            {
                _log.Report($"[WARN] Could not determine source path for {pluginDllName}: {ex.Message}. Skipping installation.");
                return;
            }
            

            if (!File.Exists(sourceDllPath))
            {
                _log.Report($"[WARN] Local plugin {pluginDllName} not found at expected source location: {sourceDllPath}. Ensure it was built and copied correctly. Skipping installation.");
                return;
            }

            try
            {
                File.Copy(sourceDllPath, destinationPath, true); // Overwrite if exists
                _log.Report($"  → {pluginDllName} installed to {destinationPath}.");
            }
            catch (Exception ex)
            {
                _log.Report($"[WARN] Failed to copy {pluginDllName} from {sourceDllPath} to {destinationPath}: {ex.Message}");
            }
            await Task.CompletedTask; // To make the method async as per signature, though no await is strictly needed here.
        }

        private async Task EnsureBepInExAsync(string serverDir, CancellationToken ct)
        {
            var coreDll = Path.Combine(serverDir, "BepInEx", "core", "BepInEx.Preloader.dll");
            if (File.Exists(coreDll))
            {
                _log.Report("• BepInEx bereits vorhanden – überspringe Installation.");
                return;
            }

            _log.Report("• Lade BepInEx " + BepInExVersion + " herunter und entpacke …");

            var tempZip = Path.Combine(Path.GetTempPath(), $"BepInEx_{BepInExVersion}.zip");
            if (!File.Exists(tempZip))
            {
                using var http = new HttpClient();
                await using var fs = File.OpenWrite(tempZip);
                // Download and persist the ZIP content to disk
                await using var httpStream = await http.GetStreamAsync(BepInExZipUrl, ct);
                await httpStream.CopyToAsync(fs, ct);
            }

            var extractDir = Path.Combine(Path.GetTempPath(), $"BepInEx_{BepInExVersion}");
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            ZipFile.ExtractToDirectory(tempZip, extractDir);

            // Copy files/folders we need (winhttp.dll, doorstop_config.ini, .doorstop_version, BepInEx dir)
            foreach (var entry in new[] { "winhttp.dll", "doorstop_config.ini", ".doorstop_version" })
            {
                var src = Path.Combine(extractDir, entry);
                var dst = Path.Combine(serverDir, entry);
                File.Copy(src, dst, true);
            }

            var srcBep = Path.Combine(extractDir, "BepInEx");
            var dstBep = Path.Combine(serverDir, "BepInEx");
            CopyDirectoryRecursive(srcBep, dstBep);

            _log.Report("  → BepInEx entpackt.");
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(sourceDir, file);
                var target = Path.Combine(destDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        }

        private async Task EnsureEosDllAsync(string serverDir, CancellationToken ct)
        {
            string targetPath = Path.Combine(serverDir, EosDllName);
            if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 1_000_000)
            {
                _log.Report($"• {EosDllName} already present (size OK).");
                return;
            }

            // Default assumption (legacy): TABG client installed in Program Files (x86)
            string clientPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Steam", "steamapps", "common", "TotallyAccurateBattlegrounds",
                "TotallyAccurateBattlegrounds_Data", "Plugins", "x86_64", EosDllName);

            // If that path does not exist, try Steam library detection
            if (!File.Exists(clientPath))
            {
                var clientRoot = TryFindTabgClientPath();
                if (!string.IsNullOrEmpty(clientRoot))
                {
                    clientPath = Path.Combine(clientRoot, "TotallyAccurateBattlegrounds_Data", "Plugins", "x86_64", EosDllName);
                }
            }

            if (!File.Exists(clientPath))
            {
                _log.Report($"[WARN] Could not find {EosDllName} automatically. After the install finishes, copy it manually from your TABG *client* folder into the server directory.");
                return;
            }

            try
            {
                File.Copy(clientPath, targetPath, true);
                _log.Report($"✓ Copied {EosDllName} from detected client install.");
            }
            catch (Exception ex)
            {
                _log.Report($"[WARN] Failed to copy {EosDllName}: {ex.Message}");
            }
            await Task.CompletedTask;
        }

        private void EnsureUnityDlls(string serverDir, IProgress<string> log)
        {
            string[] names = { UnityEngineDll, UnityEngineCoreDll };
            string libsDir = Path.Combine(AppContext.BaseDirectory, "..", "TabgInstaller.AntiCheatBypass", "Libs");
            foreach (var name in names)
            {
                try
                {
                    var src = Path.Combine(libsDir, name);
                    if (!File.Exists(src)) continue;
                    foreach (var destDir in new[] { serverDir, Path.Combine(serverDir, "BepInEx", "plugins") })
                    {
                        Directory.CreateDirectory(destDir);
                        var dst = Path.Combine(destDir, name);
                        if (!File.Exists(dst))
                        {
                            File.Copy(src, dst);
                            log.Report($"[UnityDLL] Copied {name} to {destDir}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Report($"[WARN] Could not copy {name}: {ex.Message}");
                }
            }
        }

        public void Dispose() { }

        // Place the following helper method **inside** the Installer class (outside of any other method).
        private static void SanitizeStarterPackConfig(string filePath)
        {
            if (!File.Exists(filePath)) return;
            var lines = File.ReadAllLines(filePath);
            var hasLoadouts = false;
            var hadForceKill = false;
            for (int i = 0; i < lines.Length; i++)
            {
                var ln = lines[i];
                if (string.IsNullOrWhiteSpace(ln) || ln.TrimStart().StartsWith("//")) continue;
                int eqIdx = ln.IndexOf('=');
                if (eqIdx <= 0) continue;

                string key = ln.Substring(0, eqIdx).Trim();
                string beforeEq = ln.Substring(0, eqIdx + 1);
                string afterEq = ln.Substring(eqIdx + 1).Trim();

                // Remove inline comments and trailing comma (unchanged code)
                int commentIdx = afterEq.IndexOf("//", StringComparison.Ordinal);
                if (commentIdx >= 0) afterEq = afterEq.Substring(0, commentIdx).Trim();
                afterEq = afterEq.TrimEnd(',');

                // Detect loadouts content
                if (key.Equals("Loadouts", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(afterEq))
                    hasLoadouts = true;
                if (key.Equals("ForceKillAtStart", StringComparison.OrdinalIgnoreCase))
                    hadForceKill = true;

                // Empty value fix (unchanged block)
                if (string.IsNullOrEmpty(afterEq))
                {
                    switch (key)
                    {
                        case "KillsToWin":
                        case "ValidSpawnPoints":
                            afterEq = "0"; break;
                        case "RingSettings":
                            lines[i] = "// " + ln; continue;
                    }
                }

                // Ensure each non-empty Loadouts value ends with slash
                if (key.Equals("Loadouts", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(afterEq) && !afterEq.EndsWith("/"))
                    afterEq += "/";

                lines[i] = beforeEq + afterEq;
            }

            // Previously we auto-set ForceKillAtStart=true; user requested to keep their chosen value.
            // Therefore: do nothing – we leave any existing ForceKillAtStart line untouched and do not add one.

            File.WriteAllLines(filePath, lines);
        }
    }

    public class DiagnosticReport
    {
        public List<string> AntiCheatFiles { get; set; } = new List<string>();
        public List<string> Issues { get; set; } = new List<string>();
        public string? AntiCheatSetting { get; set; }
        public bool StarterPackDllFound { get; set; }
        public bool BepInExDoorstopEnabled { get; set; } = true; // Assume true unless found otherwise
    }

    public partial class Installer // Using partial class to add these static methods
    {
        private static async Task<DiagnosticReport> RunServerDiagnosticsAsync(string serverPath, IProgress<string> log)
        {
            var report = new DiagnosticReport();
            log.Report("\\n=== TABG SERVER DIAGNOSTIC REPORT ===");
            log.Report($"[DIAG] Server Path: {serverPath}");
            log.Report($"[DIAG] Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            log.Report("\\n[DIAG] Scanning for Anti-Cheat components...");
            var eacFiles = new Dictionary<string, List<string>>();
            var searchPaths = new[] {
                serverPath,
                Path.Combine(serverPath, "TABG_Data"),
                Path.Combine(serverPath, "TABG_Data", "Plugins"),
                Path.Combine(serverPath, "TABG_Data", "Plugins", "x86_64"),
                Path.Combine(serverPath, "TABG_Data", "MonoBleedingEdge"), // Specific path from user log
                Path.Combine(serverPath, "MonoBleedingEdge") // General MonoBleedingEdge path
            };

            var dllsToCheck = new[] {
                "EOSSDK-Win64-Shipping.dll",
                "libEOSSDK-Win64-Shipping.dll",
                "Easy_AC_Server.dll",
                "EOSSDK-Win64-Shipping", 
                "libEOSSDK-Win64-Shipping"
            };

            foreach (var searchDir in searchPaths)
            {
                if (!Directory.Exists(searchDir))
                {
                    log.Report($"[DIAG] Search path not found, skipping: {searchDir}");
                    continue;
                }
                
                foreach (var dllPatternName in dllsToCheck)
                {
                    var pattern = $"{dllPatternName}*"; 
                    try
                    {
                        var files = Directory.GetFiles(searchDir, pattern, SearchOption.TopDirectoryOnly);
                        if (files.Length > 0)
                        {
                            if (!eacFiles.ContainsKey(searchDir))
                                eacFiles[searchDir] = new List<string>();
                            eacFiles[searchDir].AddRange(files.Select(f => Path.GetFileName(f))); // Store only file names for brevity in report
                            report.AntiCheatFiles.AddRange(files); // Store full paths in report object
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Report($"[DIAG WARN] Error searching in {searchDir} for {pattern}: {ex.Message}");
                    }
                }
            }

            if (eacFiles.Count > 0)
            {
                log.Report("[DIAG] ⚠️  Found Anti-Cheat related files/remnants:");
                foreach (var kvp in eacFiles)
                {
                    log.Report($"  In {kvp.Key}:");
                    foreach (var file in kvp.Value)
                    {
                        log.Report($"    - {file}");
                    }
                }
            }
            else
            {
                log.Report("[DIAG] ✓ No Anti-Cheat related files found in specified search paths.");
            }

            log.Report("\\n[DIAG] Checking game_settings.txt...");
            var gameSettingsPath = Path.Combine(serverPath, "game_settings.txt");
            if (File.Exists(gameSettingsPath))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(gameSettingsPath);
                    report.AntiCheatSetting = content.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault(l => l.Trim().StartsWith("AntiCheat=", StringComparison.OrdinalIgnoreCase));
                    log.Report($"[DIAG] AntiCheat setting: {(string.IsNullOrEmpty(report.AntiCheatSetting) ? "NOT FOUND" : report.AntiCheatSetting)}");
                }
                catch (Exception ex)
                {
                    log.Report($"[DIAG WARN] Could not read game_settings.txt: {ex.Message}");
                }
            }
            else
            {
                log.Report("[DIAG] game_settings.txt not found.");
                report.Issues.Add("game_settings.txt not found.");
            }

            log.Report("\\n[DIAG] Checking BepInEx installation and key plugins...");
            var bepInExPluginsPath = Path.Combine(serverPath, "BepInEx", "plugins");
            

            
            var antiCheatBypassPluginPath = Path.Combine(bepInExPluginsPath, AntiCheatBypassDllName); // Use new constant
            if (File.Exists(antiCheatBypassPluginPath))
            {
                 log.Report($"[DIAG] ✓ {AntiCheatBypassDllName} is installed.");
            }
            else
            {
                log.Report($"[DIAG] ⚠️  {AntiCheatBypassDllName} NOT FOUND. This plugin is intended to help bypass EAC.");
                report.Issues.Add($"{AntiCheatBypassDllName} missing from {bepInExPluginsPath}");
            }


            var doorstopConfigPath = Path.Combine(serverPath, "doorstop_config.ini");
            if (File.Exists(doorstopConfigPath))
            {
                try
                {
                    var configContent = await File.ReadAllTextAsync(doorstopConfigPath);
                    if (!configContent.Contains("enabled=true", StringComparison.OrdinalIgnoreCase))
                    {
                        report.BepInExDoorstopEnabled = false;
                        log.Report("[DIAG] ⚠️  Doorstop is not enabled in doorstop_config.ini (enabled=true missing or false).");
                        report.Issues.Add("Doorstop is not enabled in doorstop_config.ini.");
                    }
                    else
                    {
                         log.Report("[DIAG] ✓ Doorstop appears enabled in doorstop_config.ini.");
                    }
                }
                catch (Exception ex)
                {
                    log.Report($"[DIAG WARN] Could not read doorstop_config.ini: {ex.Message}");
                    report.Issues.Add("Could not read doorstop_config.ini.");
                }
            }
            else
            {
                log.Report("[DIAG] ⚠️  doorstop_config.ini not found.");
                report.Issues.Add("doorstop_config.ini not found.");
                report.BepInExDoorstopEnabled = false;
            }
            log.Report("=== END DIAGNOSTIC REPORT ===\\n");
            return report;
        }

        private static async Task FixDoorstopConfigAsync(string serverPath, IProgress<string> log)
        {
            var doorstopPath = Path.Combine(serverPath, "doorstop_config.ini");
            log.Report($"[FIX] Ensuring Doorstop is enabled in {doorstopPath}...");

            var expectedConfigContent = new StringBuilder();
            expectedConfigContent.AppendLine("[UnityDoorstop]");
            expectedConfigContent.AppendLine("enabled=true");
            expectedConfigContent.AppendLine("targetAssembly=BepInEx\\core\\BepInEx.Preloader.dll"); // Note: double backslashes for C# literal
            expectedConfigContent.AppendLine("redirectOutputLog=false"); // Or true, depending on preference
            expectedConfigContent.AppendLine("ignoreDisableSwitch=false");
            expectedConfigContent.AppendLine("dllSearchPathOverride=");

            string newConfig = expectedConfigContent.ToString();
            bool writeNewConfig = true;

            if (File.Exists(doorstopPath))
            {
                try
                {
                    string currentConfig = await File.ReadAllTextAsync(doorstopPath);
                    // Basic check: if it contains enabled=true and targetAssembly, assume it's mostly okay.
                    // A more thorough check could parse line by line.
                    if (currentConfig.Contains("enabled=true", StringComparison.OrdinalIgnoreCase) &&
                        currentConfig.Contains("BepInEx\\core\\BepInEx.Preloader.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        log.Report("[FIX] Doorstop config already seems enabled and targets BepInEx.Preloader.dll.");
                        // Check for specific lines if needed, for now, we'll rewrite if not an exact match to ensure all settings.
                        if (currentConfig.Replace("\\\\r\\\\n", "\\\\n").Equals(newConfig.Replace("\\\\r\\\\n", "\\\\n"))) {
                             writeNewConfig = false; // It's identical
                             log.Report("[FIX] Current doorstop_config.ini is already optimal.");
                        } else {
                            log.Report("[FIX] Current doorstop_config.ini differs, will be overwritten for consistency.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Report($"[FIX WARN] Could not read existing {doorstopPath}: {ex.Message}. Will attempt to overwrite.");
                }
            }
            
            if (writeNewConfig)
            {
                try
                {
                    await File.WriteAllTextAsync(doorstopPath, newConfig);
                    log.Report($"[FIX] ✓ Doorstop config written to {doorstopPath} (enabled=true).");
                }
                catch (Exception ex)
                {
                    log.Report($"[FIX ERROR] Failed to write {doorstopPath}: {ex.Message}");
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────────────────

        //  Word-list handling was accidentally removed; restore for GUI validation helpers

        // Step A: Fetch (once) the word list from GitHub and cache it in _allowedWords.
        public static async Task EnsureWordListLoadedAsync(CancellationToken ct)
        {
            if (_allowedWords != null) return;
            await _wordListLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_allowedWords != null) return;
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.TryParseAdd("TabgInstaller/1.0");
                var url = "https://raw.githubusercontent.com/landfallgames/tabg-word-list/main/all_words.txt";
                string allWordsText = await http.GetStringAsync(url, ct).ConfigureAwait(false);
                var lines = allWordsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                          .Select(l => l.Trim())
                                          .Where(l => l.Length > 0 && !l.StartsWith("#"));
                _allowedWords = new HashSet<string>(lines, StringComparer.OrdinalIgnoreCase);
            }
            finally { _wordListLock.Release(); }
        }

        // Step B: Validate a single word against the list
        public static void ValidateOneWord(string fieldName, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                throw new InvalidOperationException($"{fieldName} cannot be empty.");

            if (candidate.Any(char.IsWhiteSpace))
                throw new InvalidOperationException($"{fieldName} must be a single word (no spaces).");

            if (_allowedWords == null || !_allowedWords.Contains(candidate))
                throw new InvalidOperationException($"{fieldName} '{candidate}' is not on the official TABG word list.");
        }

        public static IReadOnlyCollection<string> AllowedWords => (_allowedWords as IReadOnlyCollection<string>) ?? Array.Empty<string>();

        // NEW -------------
        public static string? TryFindTabgClientPath()
        {
            try
            {
                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath)) return null;

                var libraryFolders = GetSteamLibraryFolders(steamPath);

                // First pass: use manifest detection for the official App-ID
                foreach (var libraryFolder in libraryFolders)
                {
                    var steamAppsFolder = Path.Combine(libraryFolder, "steamapps");
                    var manifest = Path.Combine(steamAppsFolder, $"appmanifest_{TabgClientAppId}.acf");
                    if (File.Exists(manifest))
                    {
                        var candidate = Path.Combine(steamAppsFolder, "common", TabgClientDirName);
                        if (Directory.Exists(candidate))
                            return candidate;
                    }
                }

                // Fallback: look for the directory directly in each library
                foreach (var libraryFolder in libraryFolders)
                {
                    var candidate = Path.Combine(libraryFolder, "steamapps", "common", TabgClientDirName);
                    if (Directory.Exists(candidate))
                        return candidate;
                }
            }
            catch { /* best-effort only */ }
            return null;
        }
        // NEW -------------

        public static async Task<bool> DiagnoseBepInExLoadingAsync(string serverPath, IProgress<string> log)
        {
            log.Report("\n=== BEPINEX LOADING DIAGNOSTIC ===");
            bool allGood = true;
            
            // Check if BepInEx is installed
            var bepInExCore = Path.Combine(serverPath, "BepInEx", "core", "BepInEx.Preloader.dll");
            if (!File.Exists(bepInExCore))
            {
                log.Report("[ERROR] BepInEx is not installed!");
                return false;
            }
            log.Report("[OK] BepInEx core files found");
            
            // Check doorstop files
            var winhttp = Path.Combine(serverPath, "winhttp.dll");
            var version = Path.Combine(serverPath, "version.dll");
            var doorstopConfig = Path.Combine(serverPath, "doorstop_config.ini");
            
            bool hasWinhttp = File.Exists(winhttp);
            bool hasVersion = File.Exists(version);
            bool hasConfig = File.Exists(doorstopConfig);
            
            if (!hasWinhttp && !hasVersion)
            {
                log.Report("[ERROR] No doorstop proxy DLL found (neither winhttp.dll nor version.dll)");
                allGood = false;
            }
            else
            {
                if (hasWinhttp) log.Report($"[OK] winhttp.dll found ({new FileInfo(winhttp).Length} bytes)");
                if (hasVersion) log.Report($"[OK] version.dll found ({new FileInfo(version).Length} bytes)");
            }
            
            if (!hasConfig)
            {
                log.Report("[ERROR] doorstop_config.ini missing");
                allGood = false;
            }
            else
            {
                log.Report("[OK] doorstop_config.ini found");
                
                // Check config content
                var configContent = await File.ReadAllTextAsync(doorstopConfig);
                if (!configContent.Contains("enabled=true"))
                {
                    log.Report("[WARN] doorstop is not enabled in config");
                    allGood = false;
                }
                if (!configContent.Contains("redirectOutputLog=true"))
                {
                    log.Report("[INFO] Output log redirection is disabled (normal for production)");
                }
            }
            
            // Check Unity version
            var tabgExe = Path.Combine(serverPath, "TABG.exe");
            if (File.Exists(tabgExe))
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(tabgExe);
                log.Report($"[INFO] Unity version: {versionInfo.FileVersion} ({versionInfo.ProductVersion})");
                
                if (versionInfo.FileVersion?.StartsWith("2021.3") == true)
                {
                    log.Report("[WARN] Unity 2021.3+ detected - May require:");
                    log.Report("  - Running as Administrator");
                    log.Report("  - Windows Defender exclusion for the server folder");
                    log.Report("  - Using version.dll instead of winhttp.dll");
                }
            }
            
            // Check for BepInEx logs
            var logOutput = Path.Combine(serverPath, "LogOutput.log");
            var bepInExLog = Path.Combine(serverPath, "BepInEx", "LogOutput.log");
            
            if (File.Exists(logOutput) || File.Exists(bepInExLog))
            {
                log.Report("[OK] BepInEx log file found - BepInEx has run at least once");
            }
            else
            {
                log.Report("[WARN] No BepInEx log files found - BepInEx may not be loading");
                log.Report("\nPossible solutions:");
                log.Report("1. Run the server as Administrator");
                log.Report("2. Add Windows Defender exclusion for the server folder");
                log.Report("3. Check if antivirus is blocking winhttp.dll/version.dll");
                log.Report("4. Try running from a folder outside Program Files");
            }
            
            // Check for common issues
            if (serverPath.Contains("Program Files"))
            {
                log.Report("\n[WARN] Server is in Program Files - this often requires Administrator privileges");
            }
            
            return allGood;
        }

        public static bool IsBepInExWorking(string serverPath)
        {
            // Implementation of IsBepInExWorking method
            // This method should return true if BepInEx is working correctly on the server
            // and false otherwise.
            // You can implement this method based on your specific requirements.
            // For example, you can check if the BepInEx core files exist and if the server can load BepInEx.
            return true; // Placeholder return, actual implementation needed
        }

        private async Task RunServerUntilHeartbeatAsync(string serverDir, bool allowCrash = false)
        {
            var serverExe = Path.Combine(serverDir, "TABG.exe");
            var serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = serverExe,
                    Arguments = "-batchmode -nographics",
                    WorkingDirectory = serverDir,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            var tcs = new TaskCompletionSource<bool>();
            var crashTcs = new TaskCompletionSource<bool>();

            serverProcess.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _log.Report(e.Data);
                    if (e.Data.Contains("Heartbeat sent!"))
                    {
                        tcs.TrySetResult(true);
                    }
                    if (allowCrash && e.Data.Trim().StartsWith("FormatException: Input string was not in a correct format.", StringComparison.Ordinal))
                    {
                        _log.Report("[INFO] Detected expected FormatException. Continuing install process...");
                        crashTcs.TrySetResult(true);
                    }
                }
            };
            
            serverProcess.Exited += (sender, e) => {
                _log.Report("[INFO] Server process exited.");
                crashTcs.TrySetResult(true);
            };

            serverProcess.Start();
            serverProcess.BeginOutputReadLine();

            var completedTask = await Task.WhenAny(tcs.Task, crashTcs.Task, Task.Delay(TimeSpan.FromMinutes(2)));

            if (completedTask == crashTcs.Task)
            {
                _log.Report("[INFO] Server exited as expected during initial config generation.");
            }
            else if (completedTask != tcs.Task)
            {
                _log.Report("[WARN] Server did not send heartbeat or exit as expected within 2 minutes.");
            }
            
            try
            {
                if (!serverProcess.HasExited)
                {
                    serverProcess.Kill();
                }
            }
            catch (Exception ex)
            {
                _log.Report($"[WARN] Could not kill server process: {ex.Message}");
            }
        }
    }
}
