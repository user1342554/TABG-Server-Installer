using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace TabgInstaller.Gui.Services
{
    public class SteamLauncher
    {
        private readonly Action<string> _logger;

        public SteamLauncher(Action<string> logger = null)
        {
            _logger = logger ?? (_ => { });
        }

        public async Task<bool> LaunchTabgAsync(int steamAppId = 823130)
        {
            try
            {
                var steamPath = FindSteamInstall();
                if (string.IsNullOrEmpty(steamPath))
                {
                    _logger("Steam installation not found");
                    return false;
                }

                _logger($"Launching TABG (AppID: {steamAppId}) via Steam...");

                // Try Steam URL protocol first
                var steamUrl = $"steam://rungameid/{steamAppId}";
                try
                {
                    Process.Start(new ProcessStartInfo(steamUrl) { UseShellExecute = true });
                    _logger($"Launched TABG via Steam URL: {steamUrl}");
                    return true;
                }
                catch (Exception urlEx)
                {
                    _logger($"Steam URL launch failed: {urlEx.Message}");
                }

                // Fallback to direct Steam executable launch
                try
                {
                    var steamExe = Path.Combine(steamPath, "steam.exe");
                    if (!File.Exists(steamExe))
                    {
                        _logger($"Steam executable not found at: {steamExe}");
                        return false;
                    }

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = steamExe,
                        Arguments = $"-applaunch {steamAppId}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Minimized
                    };

                    var process = Process.Start(startInfo);
                    _logger($"Launched Steam with arguments: {startInfo.Arguments}");
                    return true;
                }
                catch (Exception exeEx)
                {
                    _logger($"Steam executable launch failed: {exeEx.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger($"Failed to launch TABG: {ex.Message}");
                return false;
            }
        }

        private string FindSteamInstall()
        {
            try
            {
                // Check registry for Steam installation
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    if (key != null)
                    {
                        var steamPath = key.GetValue("SteamPath")?.ToString();
                        if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                        {
                            _logger($"Found Steam at: {steamPath}");
                            return steamPath;
                        }
                    }
                }

                // Check common installation paths
                var commonPaths = new[]
                {
                    @"C:\Program Files (x86)\Steam",
                    @"C:\Program Files\Steam",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
                };

                foreach (var path in commonPaths)
                {
                    if (Directory.Exists(path) && File.Exists(Path.Combine(path, "steam.exe")))
                    {
                        _logger($"Found Steam at: {path}");
                        return path;
                    }
                }

                _logger("Steam installation not found in common locations");
                return null;
            }
            catch (Exception ex)
            {
                _logger($"Error finding Steam installation: {ex.Message}");
                return null;
            }
        }
    }
}
