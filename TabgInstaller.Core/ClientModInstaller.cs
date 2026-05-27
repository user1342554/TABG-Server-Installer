using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TabgInstaller.Core
{
    /// <summary>
    /// Creates a modded copy of TABG and installs BepInEx + client plugins.
    /// EAC blocks BepInEx in the original Steam folder, so we copy the game
    /// to a separate directory, add steam_appid.txt, install BepInEx there,
    /// and the player launches the copy directly (with Steam open in background).
    /// </summary>
    public static class ClientModInstaller
    {
        private static readonly HttpClient s_httpClient = new HttpClient();

        static ClientModInstaller()
        {
            s_httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("TabgInstaller/1.0");
        }

        private const string BepInExWindowsUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip";
        private const string BepInExUnixUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_unix_5.4.22.0.zip";
        private const string AntiCheatRemoverReleaseUrl = "https://api.github.com/repos/C0mputery/AntiCheatBootErrorRemover/releases/latest";
        private const string SteamAppId = "823130";

        /// <summary>
        /// Create a modded TABG copy and install BepInEx + selected plugins.
        /// </summary>
        /// <param name="tabgSourceDir">Original Steam TABG folder</param>
        /// <param name="moddedDir">Where to create the modded copy</param>
        /// <param name="selectedPlugins">Plugin DLL filenames to install</param>
        /// <param name="log">Progress reporter</param>
        public static async Task<bool> InstallAsync(string tabgSourceDir, string moddedDir, List<string> selectedPlugins, IProgress<string> log)
        {
            try
            {
                // Validate source
                string? srcExe = ResolveClientExecutable(tabgSourceDir);
                string srcLauncher = Path.Combine(tabgSourceDir, "TABG_Launcher.exe");
                if (srcExe == null && !File.Exists(srcLauncher))
                {
                    log.Report("ERROR: Not a valid TABG folder. Expected TotallyAccurateBattlegrounds executable.");
                    return false;
                }

                var isWindowsClient = srcExe == null ||
                    srcExe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

                // Copy game files if modded dir doesn't have the exe yet
                string dstExe = Path.Combine(moddedDir, Path.GetFileName(srcExe ?? srcLauncher));
                if (!File.Exists(dstExe))
                {
                    log.Report($"Copying TABG to {moddedDir}...");
                    log.Report("This may take a minute...");
                    CopyDirectory(tabgSourceDir, moddedDir, log);
                    log.Report("Game files copied.");
                }
                else
                {
                    log.Report("Modded TABG copy already exists. Updating mods...");
                }

                // Remove EAC folder from the copy so it doesn't interfere
                string eacDir = Path.Combine(moddedDir, "EasyAntiCheat");
                if (Directory.Exists(eacDir))
                {
                    try { Directory.Delete(eacDir, true); }
                    catch (Exception ex)
                    {
                        log.Report($"[WARN] Could not remove EasyAntiCheat directory: {ex.Message}");
                    }
                    log.Report("Removed EasyAntiCheat from modded copy.");
                }

                // Add steam_appid.txt
                string steamAppIdPath = Path.Combine(moddedDir, "steam_appid.txt");
                File.WriteAllText(steamAppIdPath, SteamAppId);
                log.Report("Created steam_appid.txt");

                // Install BepInEx
                string bepinexCore = Path.Combine(moddedDir, "BepInEx", "core", "BepInEx.dll");
                string winDoorstop = Path.Combine(moddedDir, "winhttp.dll");
                var needsBepInEx = !File.Exists(bepinexCore) || (isWindowsClient && !File.Exists(winDoorstop));
                if (needsBepInEx)
                {
                    log.Report(isWindowsClient
                        ? "Downloading BepInEx 5.4.22 for Windows/Proton..."
                        : "Downloading BepInEx 5.4.22 for Linux...");
                    string zipPath = Path.Combine(moddedDir, "bepinex_temp.zip");

                    var data = await s_httpClient.GetByteArrayAsync(isWindowsClient ? BepInExWindowsUrl : BepInExUnixUrl);
                    File.WriteAllBytes(zipPath, data);

                    log.Report("Extracting BepInEx...");
                    ZipFile.ExtractToDirectory(zipPath, moddedDir, overwriteFiles: true);
                    File.Delete(zipPath);
                    log.Report("BepInEx installed.");
                }
                else
                {
                    log.Report("BepInEx already installed.");
                }

                // Ensure doorstop config
                string doorstopPath = Path.Combine(moddedDir, "doorstop_config.ini");
                var targetAssembly = isWindowsClient
                    ? "BepInEx\\core\\BepInEx.Preloader.dll"
                    : "BepInEx/core/BepInEx.Preloader.dll";
                File.WriteAllText(doorstopPath,
                    "[UnityDoorstop]\r\n" +
                    "enabled=true\r\n" +
                    $"targetAssembly={targetAssembly}\r\n" +
                    "redirectOutputLog=false\r\n" +
                    "ignoreDisableSwitch=false\r\n" +
                    "dllSearchPathOverride=\r\n");

                if (!OperatingSystem.IsWindows() && !isWindowsClient)
                    ConfigureUnixBepInExScript(moddedDir, log);

                // Create plugins dir and install selected plugins
                string pluginsDir = Path.Combine(moddedDir, "BepInEx", "plugins");
                Directory.CreateDirectory(pluginsDir);
                AddImplicitClientDependencies(selectedPlugins);

                string bundledDir = FindClientPluginsDir();
                var missingPlugins = new List<string>();
                if (bundledDir != null)
                {
                    foreach (var pluginName in selectedPlugins)
                    {
                        var src = Path.Combine(bundledDir, pluginName);
                        if (File.Exists(src))
                        {
                            var dst = Path.Combine(pluginsDir, pluginName);
                            File.Copy(src, dst, overwrite: true);
                            log.Report($"Installed: {pluginName}");
                        }
                        else
                        {
                            log.Report($"WARNING: {pluginName} not found in bundled client-plugins.");
                            missingPlugins.Add(pluginName);
                        }
                    }
                }
                else
                {
                    log.Report("ERROR: Could not find bundled client-plugins directory.");
                    return false;
                }

                await InstallAntiCheatBootErrorRemoverAsync(pluginsDir, log);
                if (missingPlugins.Count > 0)
                {
                    log.Report("Client mod install completed with missing DLLs: " + string.Join(", ", missingPlugins));
                    return false;
                }

                log.Report("");
                log.Report("=== DONE! ===");
                log.Report($"Modded TABG is at: {moddedDir}");
                log.Report(isWindowsClient && !OperatingSystem.IsWindows()
                    ? "To play: keep Steam open, then use Start modded client in this installer."
                    : "To play: Open Steam, then run the TABG executable or run_bepinex.sh from the modded folder.");
                log.Report(isWindowsClient && !OperatingSystem.IsWindows()
                    ? "The Linux GUI starts the Windows copy through Steam Proton."
                    : "Do NOT launch through Steam — launch the exe directly!");
                return true;
            }
            catch (Exception ex)
            {
                log.Report($"ERROR: {ex.Message}");
                return false;
            }
        }

        private static string? ResolveClientExecutable(string dir)
        {
            var candidates = new[]
            {
                "TotallyAccurateBattlegrounds.x86_64",
                "TotallyAccurateBattlegrounds.exe",
                "TABG.x86_64",
                "TABG.exe"
            };

            foreach (var candidate in candidates)
            {
                var path = Path.Combine(dir, candidate);
                if (File.Exists(path))
                    return path;
            }

            return Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*Battlegrounds*", SearchOption.TopDirectoryOnly).FirstOrDefault(File.Exists)
                : null;
        }

        private static void ConfigureUnixBepInExScript(string moddedDir, IProgress<string> log)
        {
            var runScript = Path.Combine(moddedDir, "run_bepinex.sh");
            if (!File.Exists(runScript))
                return;

            var exe = ResolveClientExecutable(moddedDir);
            if (exe == null)
                return;

            try
            {
                var script = File.ReadAllText(runScript);
                script = System.Text.RegularExpressions.Regex.Replace(
                    script,
                    @"(?m)^executable_name\s*=.*$",
                    $"executable_name=\"{Path.GetFileName(exe)}\"");
                File.WriteAllText(runScript, script);
                System.Diagnostics.Process.Start("chmod", $"+x \"{runScript}\"")?.WaitForExit(2000);
                log.Report($"Configured run_bepinex.sh for {Path.GetFileName(exe)}.");
            }
            catch (Exception ex)
            {
                log.Report($"[WARN] Could not configure run_bepinex.sh: {ex.Message}");
            }
        }

        private static void AddImplicitClientDependencies(List<string> selectedPlugins)
        {
            if (selectedPlugins == null)
                return;

            var needsModSettings = selectedPlugins.Any(name =>
                name.Equals("TabgInstaller.ProximityChat.Client.dll", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("TabgInstaller.FlyingControls.dll", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("TabgInstaller.CoordsDisplay.dll", StringComparison.OrdinalIgnoreCase));
            if (needsModSettings &&
                !selectedPlugins.Any(name => name.Equals("TabgInstaller.ModSettings.dll", StringComparison.OrdinalIgnoreCase)))
            {
                selectedPlugins.Add("TabgInstaller.ModSettings.dll");
            }
        }

        private static async Task InstallAntiCheatBootErrorRemoverAsync(string pluginsDir, IProgress<string> log)
        {
            var targetPath = Path.Combine(pluginsDir, "AntiCheatBootErrorRemover.dll");
            try
            {
                var json = await s_httpClient.GetStringAsync(AntiCheatRemoverReleaseUrl);
                var release = JObject.Parse(json);
                var asset = release["assets"]?
                    .FirstOrDefault(item => string.Equals(
                        item?["name"]?.ToString(),
                        "AntiCheatBootErrorRemover.dll",
                        StringComparison.OrdinalIgnoreCase));
                var downloadUrl = asset?["browser_download_url"]?.ToString();
                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    log.Report("WARNING: AntiCheatBootErrorRemover.dll was not found in the latest release.");
                    return;
                }

                var data = await s_httpClient.GetByteArrayAsync(downloadUrl);
                File.WriteAllBytes(targetPath, data);
                log.Report("Installed: AntiCheatBootErrorRemover.dll");
            }
            catch (Exception ex)
            {
                log.Report($"WARNING: Could not install AntiCheatBootErrorRemover.dll: {ex.Message}");
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir, IProgress<string> log)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(dir);
                // Skip EAC directory during copy
                if (dirName.Equals("EasyAntiCheat", StringComparison.OrdinalIgnoreCase))
                    continue;
                CopyDirectory(dir, Path.Combine(destDir, dirName), log);
            }
        }

        /// <summary>
        /// Searches for the client-plugins directory near the application.
        /// Checks the app directory first, then walks up parent directories as a fallback
        /// for development environments where the exe is nested inside bin/Debug/etc.
        /// </summary>
        private static string? FindClientPluginsDir()
        {
            return BundledAssetLocator.FindClientPluginsDirectory();
        }
    }
}
