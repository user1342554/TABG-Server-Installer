using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

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
        private const string BepInExUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip";
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
                string srcExe = Path.Combine(tabgSourceDir, "TotallyAccurateBattlegrounds.exe");
                string srcLauncher = Path.Combine(tabgSourceDir, "TABG_Launcher.exe");
                if (!File.Exists(srcExe) && !File.Exists(srcLauncher))
                {
                    log.Report("ERROR: Not a valid TABG folder. Expected TotallyAccurateBattlegrounds.exe");
                    return false;
                }

                // Copy game files if modded dir doesn't have the exe yet
                string dstExe = Path.Combine(moddedDir, "TotallyAccurateBattlegrounds.exe");
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
                    try { Directory.Delete(eacDir, true); } catch { }
                    log.Report("Removed EasyAntiCheat from modded copy.");
                }

                // Add steam_appid.txt
                string steamAppIdPath = Path.Combine(moddedDir, "steam_appid.txt");
                File.WriteAllText(steamAppIdPath, SteamAppId);
                log.Report("Created steam_appid.txt");

                // Install BepInEx
                string bepinexCore = Path.Combine(moddedDir, "BepInEx", "core", "BepInEx.dll");
                if (!File.Exists(bepinexCore))
                {
                    log.Report("Downloading BepInEx 5.4.22...");
                    string zipPath = Path.Combine(moddedDir, "bepinex_temp.zip");

                    using (var http = new HttpClient())
                    {
                        var data = await http.GetByteArrayAsync(BepInExUrl);
                        File.WriteAllBytes(zipPath, data);
                    }

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
                File.WriteAllText(doorstopPath,
                    "[UnityDoorstop]\r\n" +
                    "enabled=true\r\n" +
                    "targetAssembly=BepInEx\\core\\BepInEx.Preloader.dll\r\n" +
                    "redirectOutputLog=false\r\n" +
                    "ignoreDisableSwitch=false\r\n" +
                    "dllSearchPathOverride=\r\n");

                // Create plugins dir and install selected plugins
                string pluginsDir = Path.Combine(moddedDir, "BepInEx", "plugins");
                Directory.CreateDirectory(pluginsDir);

                string bundledDir = FindClientPluginsDir();
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
                        }
                    }
                }
                else
                {
                    log.Report("ERROR: Could not find bundled client-plugins directory.");
                    return false;
                }

                log.Report("");
                log.Report("=== DONE! ===");
                log.Report($"Modded TABG is at: {moddedDir}");
                log.Report("To play: Open Steam, then run TotallyAccurateBattlegrounds.exe from the modded folder.");
                log.Report("Do NOT launch through Steam — launch the exe directly!");
                return true;
            }
            catch (Exception ex)
            {
                log.Report($"ERROR: {ex.Message}");
                return false;
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

        private static string FindClientPluginsDir()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "client-plugins"),
                Path.Combine(baseDir, "..", "client-plugins"),
                Path.Combine(baseDir, "..", "..", "client-plugins"),
                Path.Combine(baseDir, "..", "..", "..", "client-plugins"),
            };

            foreach (var c in candidates)
            {
                if (Directory.Exists(c) && Directory.GetFiles(c, "*.dll").Length > 0)
                    return Path.GetFullPath(c);
            }
            return null;
        }
    }
}
