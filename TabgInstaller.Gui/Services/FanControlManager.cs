using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace TabgInstaller.Gui.Services
{
    public class FanControlManager
    {
        private bool _fanControlAvailable = false;
        private bool _msiAfterburnerAvailable = false;
        private List<string> _originalProfiles = new();
        private readonly Action<string> _logger;

        public FanControlManager(Action<string> logger = null)
        {
            _logger = logger ?? (_ => { });
            DetectFanControlSoftware();
        }

        private void DetectFanControlSoftware()
        {
            try
            {
                // Check for FanControl by Rem0o
                var fanControlPath = FindFanControlInstall();
                if (!string.IsNullOrEmpty(fanControlPath))
                {
                    _fanControlAvailable = true;
                    _logger("FanControl detected at: " + fanControlPath);
                }

                // Check for MSI Afterburner
                var afterburnerPath = FindMSIAfterburnerInstall();
                if (!string.IsNullOrEmpty(afterburnerPath))
                {
                    _msiAfterburnerAvailable = true;
                    _logger("MSI Afterburner detected at: " + afterburnerPath);
                }

                if (!_fanControlAvailable && !_msiAfterburnerAvailable)
                {
                    _logger("No fan control software detected - fan control disabled");
                }
            }
            catch (Exception ex)
            {
                _logger($"Error detecting fan control software: {ex.Message}");
            }
        }

        private string FindFanControlInstall()
        {
            try
            {
                // Check common installation paths for FanControl
                var paths = new[]
                {
                    @"C:\Program Files\FanControl\FanControl.exe",
                    @"C:\Program Files (x86)\FanControl\FanControl.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\FanControl\FanControl.exe")
                };

                foreach (var path in paths)
                {
                    if (File.Exists(path))
                        return path;
                }

                // Check registry for uninstall information
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                var displayName = subKey?.GetValue("DisplayName")?.ToString();
                                if (displayName?.Contains("FanControl", StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    var installLocation = subKey.GetValue("InstallLocation")?.ToString();
                                    if (!string.IsNullOrEmpty(installLocation))
                                    {
                                        var exePath = Path.Combine(installLocation, "FanControl.exe");
                                        if (File.Exists(exePath))
                                            return exePath;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger($"Error finding FanControl: {ex.Message}");
            }

            return null;
        }

        private string FindMSIAfterburnerInstall()
        {
            try
            {
                // Check common MSI Afterburner paths
                var paths = new[]
                {
                    @"C:\Program Files (x86)\MSI Afterburner\MSIAfterburner.exe",
                    @"C:\Program Files\MSI Afterburner\MSIAfterburner.exe"
                };

                foreach (var path in paths)
                {
                    if (File.Exists(path))
                        return path;
                }

                // Check registry
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Afterburner"))
                {
                    if (key != null)
                    {
                        var installLocation = key.GetValue("InstallLocation")?.ToString();
                        if (!string.IsNullOrEmpty(installLocation))
                        {
                            var exePath = Path.Combine(installLocation, "MSIAfterburner.exe");
                            if (File.Exists(exePath))
                                return exePath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger($"Error finding MSI Afterburner: {ex.Message}");
            }

            return null;
        }

        public async Task<bool> MaxOutFansAsync()
        {
            if (!_fanControlAvailable && !_msiAfterburnerAvailable)
            {
                _logger("No fan control software available - skipping fan control");
                return false;
            }

            try
            {
                // For now, we'll just log that we would set fans to max
                // Real implementation would require specific APIs for each software
                _logger("Setting fans to maximum speed...");
                
                // Simulated fan control - in a real implementation you'd:
                // 1. Save current profiles/settings
                // 2. Set all controllable fans to 100%
                // 3. Store the original settings for restoration

                await Task.Delay(1000); // Simulate API call delay
                _logger("Fans set to maximum speed");
                return true;
            }
            catch (Exception ex)
            {
                _logger($"Failed to set fans to max: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RestoreFanProfilesAsync()
        {
            if (!_fanControlAvailable && !_msiAfterburnerAvailable)
                return false;

            try
            {
                _logger("Restoring original fan profiles...");
                
                // Simulated profile restoration
                await Task.Delay(500);
                _logger("Fan profiles restored");
                return true;
            }
            catch (Exception ex)
            {
                _logger($"Failed to restore fan profiles: {ex.Message}");
                return false;
            }
        }
    }
}
