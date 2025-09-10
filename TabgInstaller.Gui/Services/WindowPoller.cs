using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TabgInstaller.Gui.Services
{
    public class WindowPoller
    {
        private readonly Action<string> _logger;

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        public WindowPoller(Action<string> logger = null)
        {
            _logger = logger ?? (_ => { });
        }

        public async Task<bool> WaitForTabgWindowAsync(string processName = "TABG", string windowTitle = "", 
                                                      int timeoutSeconds = 180, CancellationToken cancellationToken = default)
        {
            _logger($"Waiting for TABG to fully load to main menu (timeout: {timeoutSeconds}s)...");
            
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            var startTime = DateTime.UtcNow;
            var pollInterval = TimeSpan.FromMilliseconds(2000); // Check every 2 seconds
            var processFoundTime = DateTime.MinValue;
            var windowFoundTime = DateTime.MinValue;

            while (DateTime.UtcNow - startTime < timeout)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger("TABG detection cancelled");
                    return false;
                }

                try
                {
                    Process tabgProcess = null;
                    
                    // Check for multiple possible TABG process names
                    var possibleNames = new[] { "TABG", "TotallyAccurateBattlegrounds", "TABG.exe", "Totally Accurate Battlegrounds" };
                    
                    foreach (var name in possibleNames)
                    {
                        var processes = Process.GetProcessesByName(name.Replace(".exe", ""));
                        if (processes.Length > 0)
                        {
                            tabgProcess = processes[0];
                            break;
                        }
                    }
                    
                    // Also check all running processes for TABG-related names
                    if (tabgProcess == null)
                    {
                        var allProcesses = Process.GetProcesses();
                        foreach (var process in allProcesses)
                        {
                            try
                            {
                                if (process.ProcessName.Contains("TABG", StringComparison.OrdinalIgnoreCase) ||
                                    process.ProcessName.Contains("Totally", StringComparison.OrdinalIgnoreCase) ||
                                    process.ProcessName.Contains("Accurate", StringComparison.OrdinalIgnoreCase) ||
                                    process.ProcessName.Contains("Battlegrounds", StringComparison.OrdinalIgnoreCase))
                                {
                                    tabgProcess = process;
                                    break;
                                }
                            }
                            catch
                            {
                                process?.Dispose();
                            }
                        }
                        
                        // Clean up unused processes
                        foreach (var process in allProcesses)
                        {
                            if (process != tabgProcess)
                                process?.Dispose();
                        }
                    }

                    if (tabgProcess != null)
                    {
                        if (processFoundTime == DateTime.MinValue)
                        {
                            processFoundTime = DateTime.UtcNow;
                            _logger($"TABG process found: '{tabgProcess.ProcessName}' (PID: {tabgProcess.Id})");
                            _logger("Waiting for TABG window and main menu to load...");
                        }

                        // Check if process has a main window (means it's loading/loaded)
                        try
                        {
                            if (!tabgProcess.HasExited && tabgProcess.MainWindowHandle != IntPtr.Zero)
                            {
                                if (windowFoundTime == DateTime.MinValue)
                                {
                                    windowFoundTime = DateTime.UtcNow;
                                    _logger($"TABG window detected! Waiting for main menu to fully load...");
                                }

                                // Wait for window to be visible and stable (main menu loaded)
                                if (IsWindow(tabgProcess.MainWindowHandle) && IsWindowVisible(tabgProcess.MainWindowHandle) && !IsIconic(tabgProcess.MainWindowHandle))
                                {
                                    var windowTime = DateTime.UtcNow - windowFoundTime;
                                    
                                    // Wait at least 30 seconds after window is visible for main menu to load
                                    if (windowTime >= TimeSpan.FromSeconds(30))
                                    {
                                        _logger($"TABG main menu should be loaded! (Window visible for {windowTime.TotalSeconds:F0}s)");
                                        _logger("Stopping Sigma Mode - TABG is ready!");
                                        tabgProcess?.Dispose();
                                        return true;
                                    }
                                    else
                                    {
                                        _logger($"TABG window visible, waiting {30 - windowTime.TotalSeconds:F0} more seconds for main menu...");
                                    }
                                }
                                else
                                {
                                    _logger("TABG window exists but not visible yet...");
                                }
                            }
                            else
                            {
                                var processTime = DateTime.UtcNow - processFoundTime;
                                _logger($"TABG process running for {processTime.TotalSeconds:F0}s, waiting for window...");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger($"Error checking TABG window: {ex.Message}");
                        }
                        finally
                        {
                            if (tabgProcess != null && tabgProcess != Process.GetCurrentProcess())
                                tabgProcess.Dispose();
                        }
                    }
                    else
                    {
                        if (processFoundTime != DateTime.MinValue)
                        {
                            _logger("TABG process disappeared, resetting detection...");
                            processFoundTime = DateTime.MinValue;
                            windowFoundTime = DateTime.MinValue;
                        }
                        else
                        {
                            var elapsed = DateTime.UtcNow - startTime;
                            _logger($"No TABG process found yet... ({elapsed.TotalSeconds:F0}s elapsed)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger($"Error during TABG detection: {ex.Message}");
                }

                // Wait before next poll
                await Task.Delay(pollInterval, cancellationToken);
            }

            _logger($"Timeout reached - TABG main menu not loaded within {timeoutSeconds} seconds");
            return false;
        }

        public bool IsTabgRunning(string processName = "TABG")
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                var isRunning = processes.Length > 0;
                
                foreach (var process in processes)
                {
                    process?.Dispose();
                }
                
                return isRunning;
            }
            catch (Exception ex)
            {
                _logger($"Error checking if TABG is running: {ex.Message}");
                return false;
            }
        }
    }
}
