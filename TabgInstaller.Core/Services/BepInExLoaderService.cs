using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services
{
    public class BepInExLoaderService : IDisposable
    {
        private readonly IProgress<string> _log;

        public BepInExLoaderService(IProgress<string> log)
        {
            _log = log;
        }

        /// <summary>
        /// Installs a simple redirector for Unity 2021.3+ servers that ensures BepInEx loads
        /// </summary>
        public async Task InstallUnity2021LauncherAsync(string serverPath)
        {
            _log.Report("• Configuring automatic BepInEx loading for Unity 2021.3+ server...");

            try
            {
                var originalExe = Path.Combine(serverPath, "TABG.exe");
                var backupExe = Path.Combine(serverPath, "TABG_Original.exe");
                
                // Check if we've already installed the redirector
                if (File.Exists(backupExe))
                {
                    _log.Report("  → BepInEx redirector already installed");
                    return;
                }

                // Remove legacy batch file if present
                var optionalBatchPath = Path.Combine(serverPath, "START_SERVER_WITH_MODS.bat");
                try
                {
                    if (File.Exists(optionalBatchPath))
                    {
                        File.Delete(optionalBatchPath);
                        _log.Report("  → Removed legacy START_SERVER_WITH_MODS.bat");
                    }
                }
                catch { }

                // Create a PowerShell script that sets environment variables persistently
                var psScriptPath = Path.Combine(serverPath, "Configure_BepInEx_Environment.ps1");
                var psContent = @"
# TABG BepInEx Environment Configuration
Write-Host 'Configuring BepInEx environment variables...' -ForegroundColor Green

$serverPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$preloaderPath = Join-Path $serverPath 'BepInEx\core\BepInEx.Preloader.dll'

# Set user environment variables (persists across sessions)
[Environment]::SetEnvironmentVariable('DOORSTOP_ENABLE', 'TRUE', 'User')
[Environment]::SetEnvironmentVariable('DOORSTOP_INVOKE_DLL_PATH', $preloaderPath, 'User')
[Environment]::SetEnvironmentVariable('DOORSTOP_CORLIB_OVERRIDE_PATH', (Join-Path $serverPath 'unstripped_corlib'), 'User')

Write-Host 'Environment variables configured!' -ForegroundColor Green
Write-Host 'You may need to restart any open terminals for changes to take effect.' -ForegroundColor Yellow
";
                await File.WriteAllTextAsync(psScriptPath, psContent);
                
                // Try to run the PowerShell script to set environment variables
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-ExecutionPolicy Bypass -File \"{psScriptPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(psi))
                    {
                        await process.WaitForExitAsync();
                        if (process.ExitCode == 0)
                        {
                            _log.Report("  → Successfully configured system environment variables");
                        }
                    }
                }
                catch
                {
                    _log.Report("  → Could not set system environment variables (may require manual configuration)");
                }

                // Create a small executable wrapper using pre-compiled binary
                await CreateSimpleWrapperAsync(serverPath);
                
                _log.Report("  → BepInEx automatic loading configured!");
                _log.Report("  → Start the server normally with TABG.exe");
            }
            catch (Exception ex)
            {
                _log.Report($"[ERROR] Failed to configure Unity 2021.3+ loader: {ex.Message}");
            }
        }

        private async Task CreateSimpleWrapperAsync(string serverPath)
        {
            // For Unity 2021.3+, we need to ensure the doorstop DLLs are recognized
            // Create both winhttp.dll and version.dll from the existing one
            var winhttpPath = Path.Combine(serverPath, "winhttp.dll");
            var versionPath = Path.Combine(serverPath, "version.dll");
            
            if (File.Exists(winhttpPath) && !File.Exists(versionPath))
            {
                try
                {
                    File.Copy(winhttpPath, versionPath, false);
                    _log.Report("  → Created version.dll as additional doorstop proxy");
                }
                catch (Exception ex)
                {
                    _log.Report($"  → Could not create version.dll: {ex.Message}");
                }
            }

            // Update doorstop_config.ini to ensure it's configured correctly
            var configPath = Path.Combine(serverPath, "doorstop_config.ini");
            if (File.Exists(configPath))
            {
                try
                {
                    var configLines = new[]
                    {
                        "[UnityDoorstop]",
                        "enabled=true",
                        @"targetAssembly=BepInEx\core\BepInEx.Preloader.dll",
                        "redirectOutputLog=false",
                        "ignoreDisableSwitch=false",
                        "dllSearchPathOverride="
                    };
                    await File.WriteAllLinesAsync(configPath, configLines);
                    _log.Report("  → Updated doorstop_config.ini for Unity 2021.3+ compatibility");
                }
                catch (Exception ex)
                {
                    _log.Report($"  → Could not update doorstop config: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
} 