using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

namespace TabgInstaller.Gui.Services
{
    public class AudioManager : IDisposable
    {
        private MediaPlayer _mediaPlayer;
        private readonly Action<string> _logger;
        private bool _isPlaying = false;

        public AudioManager(Action<string> logger = null)
        {
            _logger = logger ?? (_ => { });
            _mediaPlayer = new MediaPlayer();
        }

        public async Task<bool> PlayMusicAsync(string musicPath, float volume = 0.8f)
        {
            try
            {
                _logger($"Attempting to play music from: {musicPath}");
                
                if (string.IsNullOrEmpty(musicPath))
                {
                    _logger("Music path is null or empty - continuing without music");
                    return false;
                }
                
                // Handle relative paths - check in current directory first
                var fullPath = musicPath;
                if (!Path.IsPathRooted(musicPath))
                {
                    fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, musicPath);
                    _logger($"Resolved relative path to: {fullPath}");
                }
                
                if (!File.Exists(fullPath))
                {
                    _logger($"Music file not found at path: {fullPath}");
                    _logger("No music file found - continuing without music");
                    return false;
                }

                _logger($"Opening music file: {fullPath}");
                _mediaPlayer.Open(new Uri(fullPath, UriKind.Absolute));
                
                var clampedVolume = Math.Clamp(volume, 0.0, 1.0);
                _mediaPlayer.Volume = clampedVolume;
                _logger($"Set volume to: {clampedVolume}");
                
                _logger("Starting playback...");
                _mediaPlayer.MediaEnded += (s, e) => 
                {
                    if (_isPlaying)
                    {
                        _logger("Music ended - restarting for continuous playback");
                        _mediaPlayer.Position = TimeSpan.Zero;
                        _mediaPlayer.Play();
                    }
                };
                _mediaPlayer.Play();
                _isPlaying = true;

                // Give it a moment to start playing
                await Task.Delay(100);
                
                _logger($"Music playback started successfully! Volume: {clampedVolume}, File: {Path.GetFileName(fullPath)}");
                return true;
            }
            catch (Exception ex)
            {
                _logger($"Failed to play music: {ex.Message}");
                _logger($"Exception type: {ex.GetType().Name}");
                return false;
            }
        }

        public async Task FadeOutAsync(int durationMs = 1000)
        {
            if (!_isPlaying || _mediaPlayer == null)
                return;

            try
            {
                var originalVolume = _mediaPlayer.Volume;
                var fadeSteps = 20;
                var stepDelay = durationMs / fadeSteps;
                var volumeStep = originalVolume / fadeSteps;

                for (int i = fadeSteps; i >= 0; i--)
                {
                    _mediaPlayer.Volume = volumeStep * i;
                    await Task.Delay(stepDelay);
                }

                _mediaPlayer.Stop();
                _isPlaying = false;
                _logger("Music faded out and stopped");
            }
            catch (Exception ex)
            {
                _logger($"Error during music fade out: {ex.Message}");
                StopMusic();
            }
        }

        public void StopMusic()
        {
            try
            {
                if (_isPlaying && _mediaPlayer != null)
                {
                    _mediaPlayer.Stop();
                    _isPlaying = false;
                    _logger("Music stopped");
                }
            }
            catch (Exception ex)
            {
                _logger($"Error stopping music: {ex.Message}");
            }
        }

        public bool IsPlaying => _isPlaying;

        public void Dispose()
        {
            StopMusic();
            _mediaPlayer?.Close();
            _mediaPlayer = null;
        }
    }
}
