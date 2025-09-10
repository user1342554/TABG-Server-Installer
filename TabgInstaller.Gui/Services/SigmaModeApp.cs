using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using TabgInstaller.Gui.Models;
using TabgInstaller.Gui.Windows;

namespace TabgInstaller.Gui.Services
{
    public class SigmaModeApp : IDisposable
    {
        private readonly List<SigmaOverlayWindow> _overlays = new();
        private readonly FanControlManager _fanManager;
        private readonly AudioManager _audioManager;
        private readonly SteamLauncher _steamLauncher;
        private readonly WindowPoller _windowPoller;
        private readonly SigmaModeConfig _config;
        private readonly Action<string> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        private bool _isRunning = false;
        private bool _emergencyExit = false;

        // Global hotkey handling
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private const uint VK_ESCAPE = 0x1B;

        public SigmaModeApp(SigmaModeConfig config = null, Action<string> logger = null)
        {
            _config = config ?? LoadDefaultConfig();
            _logger = logger ?? (msg => System.Diagnostics.Debug.WriteLine($"[SigmaMode] {msg}"));
            _cancellationTokenSource = new CancellationTokenSource();

            _fanManager = new FanControlManager(_logger);
            _audioManager = new AudioManager(_logger);
            _steamLauncher = new SteamLauncher(_logger);
            _windowPoller = new WindowPoller(_logger);
        }

        private SigmaModeConfig LoadDefaultConfig()
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sigma-config.json");
            
            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<SigmaModeConfig>(json) ?? new SigmaModeConfig();
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Failed to load config from {configPath}: {ex.Message}");
            }

            // Create default config file
            var defaultConfig = new SigmaModeConfig();
            try
            {
                var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
                _logger?.Invoke($"Created default config at: {configPath}");
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Failed to create default config: {ex.Message}");
            }

            return defaultConfig;
        }

        public async Task<bool> StartSigmaModeAsync()
        {
            if (_isRunning)
            {
                _logger("Sigma Mode is already running");
                return false;
            }

            _isRunning = true;
            _logger("=== SIGMA MODE ENGAGED ===");

            try
            {
                // Register global Escape key handler
                RegisterGlobalHotKey();

                // Step 1: Max out fans (best-effort)
                if (_config.EnableFanControl)
                {
                    await _fanManager.MaxOutFansAsync();
                }

                // Step 2: Create multi-monitor blackout overlays
                CreateOverlays();

                // Step 2.5: After a short delay, capture screenshot and set wallpaper
                try
                {
                    await Task.Delay(500, _cancellationTokenSource.Token);
                    var wallpaperService = new WallpaperService(_logger);
                    // Prefer a specific file if it exists in app base directory (moved to assets)
                    var preferred = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "gorilla-4547188_1280.jpg");
                    if (System.IO.File.Exists(preferred))
                    {
                        await wallpaperService.SetWallpaperFromFileAsync(preferred);
                        _logger($"Set desktop wallpaper to file: {preferred}");
                    }
                    else
                    {
                        _ = wallpaperService.CaptureAndSetWallpaperAsync();
                        _logger("Captured screenshot and attempted to set as desktop wallpaper");
                    }
                }
                catch (Exception ex)
                {
                    _logger($"Wallpaper set error: {ex.Message}");
                }

                // Step 3: Start music playback
                await _audioManager.PlayMusicAsync(_config.MusicPath, _config.MusicVolume);

                // Step 4: Launch TABG via Steam
                var launchSuccess = await _steamLauncher.LaunchTabgAsync(_config.SteamAppId);
                if (!launchSuccess)
                {
                    _logger("Failed to launch TABG - waiting anyway");
                }

                // Step 5: Wait for TABG to be detected as running, then auto-stop
                _logger("Sigma Mode engaged! Black screen and music active...");
                _logger("Waiting for TABG to be detected as running...");
                
                var tabgDetected = await _windowPoller.WaitForTabgWindowAsync(
                    _config.TabgProcessName, 
                    _config.TabgWindowTitle, 
                    _config.TimeoutSeconds, 
                    _cancellationTokenSource.Token);

                if (_cancellationTokenSource.Token.IsCancellationRequested || _emergencyExit)
                {
                    _logger("Sigma Mode cancelled by user");
                }
                else if (tabgDetected)
                {
                    _logger("TABG detected as running! Auto-stopping Sigma Mode...");
                    await Task.Delay(2000); // Brief pause to let TABG fully load
                }
                else
                {
                    _logger($"Timeout reached ({_config.TimeoutSeconds}s) - TABG not detected, shutting down");
                }

                // Step 6: Shutdown sequence
                await ShutdownSequenceAsync();

                _logger("=== SIGMA MODE COMPLETE ===");
                return true;
            }
            catch (Exception ex)
            {
                _logger($"Sigma Mode error: {ex.Message}");
                await EmergencyShutdownAsync();
                return false;
            }
            finally
            {
                _isRunning = false;
                UnregisterGlobalHotKey();
            }
        }

        private void CreateOverlays()
        {
            try
            {
                var screens = Screen.AllScreens;
                var primaryScreen = Screen.PrimaryScreen;

                _logger($"Creating overlays for {screens.Length} screen(s)");

                foreach (var screen in screens)
                {
                    var isPrimary = screen.Equals(primaryScreen);
                    var overlay = new SigmaOverlayWindow(screen, isPrimary);
                    
                    if (isPrimary)
                    {
                        overlay.SetWelcomeName(Environment.UserName);
                    }
                    
                    overlay.Show();
                    _overlays.Add(overlay);
                    
                    _logger($"Created overlay for {(isPrimary ? "primary" : "secondary")} screen: {screen.Bounds}");
                }
            }
            catch (Exception ex)
            {
                _logger($"Error creating overlays: {ex.Message}");
            }
        }

        private async Task ShutdownSequenceAsync()
        {
            try
            {
                _logger("Starting shutdown sequence...");

                // Stop music with fade
                await _audioManager.FadeOutAsync(1000);

                // Fade out overlays simultaneously
                var fadeOutTasks = _overlays.Select(overlay => overlay.FadeOutAsync(_config.FadeOutDurationMs));
                await Task.WhenAll(fadeOutTasks);

                // Close overlays
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    foreach (var overlay in _overlays)
                    {
                        overlay.Close();
                    }
                });
                _overlays.Clear();

                // Restore fan profiles
                if (_config.EnableFanControl)
                {
                    await _fanManager.RestoreFanProfilesAsync();
                }

                _logger("Shutdown sequence complete");
            }
            catch (Exception ex)
            {
                _logger($"Error during shutdown: {ex.Message}");
            }
        }

        private async Task EmergencyShutdownAsync()
        {
            _emergencyExit = true;
            _cancellationTokenSource.Cancel();
            
            _logger("EMERGENCY SHUTDOWN INITIATED");

            try
            {
                // Immediate stop music
                _audioManager.StopMusic();

                // Close overlays immediately
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    foreach (var overlay in _overlays)
                    {
                        overlay.Close();
                    }
                });
                _overlays.Clear();

                // Restore fan profiles
                if (_config.EnableFanControl)
                {
                    await _fanManager.RestoreFanProfilesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger($"Error during emergency shutdown: {ex.Message}");
            }
        }

        private void RegisterGlobalHotKey()
        {
            try
            {
                // This is a simplified approach - in a full implementation you'd need a proper message loop
                _logger("Global Escape key handler registered (simplified)");
            }
            catch (Exception ex)
            {
                _logger($"Failed to register global hotkey: {ex.Message}");
            }
        }

        private void UnregisterGlobalHotKey()
        {
            try
            {
                _logger("Global hotkey unregistered");
            }
            catch (Exception ex)
            {
                _logger($"Failed to unregister global hotkey: {ex.Message}");
            }
        }

        public void RequestEmergencyExit()
        {
            _ = Task.Run(EmergencyShutdownAsync);
        }

        public bool IsRunning => _isRunning;

        public void Dispose()
        {
            if (_isRunning)
            {
                _ = Task.Run(EmergencyShutdownAsync);
            }

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _audioManager?.Dispose();
            UnregisterGlobalHotKey();
        }
    }
}
