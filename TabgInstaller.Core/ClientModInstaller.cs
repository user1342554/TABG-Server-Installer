using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace TabgInstaller.Core
{
    /// <summary>
    /// Installs BepInEx and client-side mods (like FlyingControls) onto a TABG game client.
    /// Players need this to steer flying vehicles (Heli, UFO, Hover Bike, Hover Car).
    /// </summary>
    public static class ClientModInstaller
    {
        private const string BepInExUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip";

        /// <summary>
        /// Install BepInEx and client plugins onto a TABG game client directory.
        /// </summary>
        public static async Task<bool> InstallAsync(string tabgClientDir, IProgress<string> log)
        {
            try
            {
                // Validate path
                string exePath = Path.Combine(tabgClientDir, "TotallyAccurateBattlegrounds.exe");
                string launcherPath = Path.Combine(tabgClientDir, "TABG_Launcher.exe");
                if (!File.Exists(exePath) && !File.Exists(launcherPath))
                {
                    log.Report("ERROR: Not a valid TABG client folder. Expected TotallyAccurateBattlegrounds.exe");
                    return false;
                }

                // Install BepInEx if not present
                string bepinexCore = Path.Combine(tabgClientDir, "BepInEx", "core", "BepInEx.dll");
                if (!File.Exists(bepinexCore))
                {
                    log.Report("Downloading BepInEx 5.4.22...");
                    string zipPath = Path.Combine(tabgClientDir, "bepinex_temp.zip");

                    using (var http = new HttpClient())
                    {
                        var data = await http.GetByteArrayAsync(BepInExUrl);
                        File.WriteAllBytes(zipPath, data);
                    }

                    log.Report("Extracting BepInEx...");
                    ZipFile.ExtractToDirectory(zipPath, tabgClientDir, overwriteFiles: true);
                    File.Delete(zipPath);
                    log.Report("BepInEx installed.");
                }
                else
                {
                    log.Report("BepInEx already installed.");
                }

                // Ensure doorstop config
                string doorstopPath = Path.Combine(tabgClientDir, "doorstop_config.ini");
                if (!File.Exists(doorstopPath))
                {
                    File.WriteAllText(doorstopPath,
                        "[UnityDoorstop]\r\n" +
                        "enabled=true\r\n" +
                        "targetAssembly=BepInEx\\core\\BepInEx.Preloader.dll\r\n" +
                        "redirectOutputLog=false\r\n" +
                        "ignoreDisableSwitch=false\r\n" +
                        "dllSearchPathOverride=\r\n");
                    log.Report("Created doorstop_config.ini");
                }

                // Create plugins dir
                string pluginsDir = Path.Combine(tabgClientDir, "BepInEx", "plugins");
                Directory.CreateDirectory(pluginsDir);

                // Copy client plugins from bundled directory
                string bundledClientPlugins = FindClientPluginsDir();
                if (bundledClientPlugins != null)
                {
                    foreach (var dll in Directory.GetFiles(bundledClientPlugins, "*.dll"))
                    {
                        string destPath = Path.Combine(pluginsDir, Path.GetFileName(dll));
                        File.Copy(dll, destPath, overwrite: true);
                        log.Report($"Installed client plugin: {Path.GetFileName(dll)}");
                    }
                }
                else
                {
                    log.Report("WARNING: Could not find bundled client-plugins directory.");
                }

                log.Report("Client mod installation complete!");
                log.Report("Launch TABG normally from Steam to play with flying controls.");
                return true;
            }
            catch (Exception ex)
            {
                log.Report($"ERROR: {ex.Message}");
                return false;
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
