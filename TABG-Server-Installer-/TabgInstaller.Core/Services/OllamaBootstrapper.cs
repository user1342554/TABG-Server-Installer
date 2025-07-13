using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services
{
    public interface IOllamaBootstrapper
    {
        Task<bool> EnsureOllamaInstalledAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
        Task<bool> EnsureModelInstalledAsync(string modelName, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
        Task<Process?> StartOllamaServerAsync(CancellationToken cancellationToken = default);
        Task RemoveModelAsync(string modelName = null);
        Task<bool> InstallModelAsync(string modelName = null);
    }

    public class OllamaBootstrapper : IOllamaBootstrapper
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private const string OllamaDownloadUrl = "https://ollama.com/download/OllamaSetup.exe";
        private const string DefaultModel = "llama3.2:latest";

        public async Task<bool> EnsureOllamaInstalledAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report("Checking if Ollama is installed...");

            // Check if ollama is in PATH
            if (IsOllamaInstalled())
            {
                progress?.Report("Ollama is already installed.");
                return true;
            }

            progress?.Report("Ollama not found. Downloading installer...");

            try
            {
                // Download Ollama installer
                var tempPath = Path.Combine(Path.GetTempPath(), "OllamaSetup.exe");
                
                using (var response = await _httpClient.GetAsync(OllamaDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength ?? -1;
                    var canReportProgress = totalBytes != -1;
                    
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        var totalRead = 0L;
                        var read = 0;

                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                            totalRead += read;

                            if (canReportProgress)
                            {
                                var percentage = (int)((totalRead * 100) / totalBytes);
                                progress?.Report($"Downloading Ollama: {percentage}%");
                            }
                        }
                    }
                }

                progress?.Report("Installing Ollama...");

                // Run installer silently
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    Arguments = "/S", // Silent install
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync(cancellationToken);
                    
                    if (process.ExitCode == 0)
                    {
                        progress?.Report("Ollama installed successfully.");
                        
                        // Clean up installer
                        try { File.Delete(tempPath); } catch { }
                        
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Failed to install Ollama: {ex.Message}");
            }

            return false;
        }

        public async Task<bool> EnsureModelInstalledAsync(string modelName, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report($"Checking if model {modelName} is installed...");

            var ollama = new AI.OllamaBackend();
            if (await ollama.HasModelAsync(modelName, cancellationToken))
            {
                progress?.Report($"Model {modelName} is already installed.");
                return true;
            }

            progress?.Report($"Pulling model {modelName}...");

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = $"pull {modelName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    process.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            progress?.Report(e.Data);
                    };

                    process.BeginOutputReadLine();
                    await process.WaitForExitAsync(cancellationToken);

                    if (process.ExitCode == 0)
                    {
                        progress?.Report($"Model {modelName} installed successfully.");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Failed to install model: {ex.Message}");
            }

            return false;
        }

        public async Task<Process?> StartOllamaServerAsync(CancellationToken cancellationToken = default)
        {
            // Check if already running
            var ollama = new AI.OllamaBackend();
            if (await ollama.IsRunningAsync(cancellationToken))
                return null;

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process != null)
                {
                    // Wait a moment for server to start
                    await Task.Delay(2000, cancellationToken);

                    // Verify it's running
                    if (await ollama.IsRunningAsync(cancellationToken))
                        return process;
                }
            }
            catch { }

            return null;
        }

        public async Task RemoveModelAsync(string modelName = DefaultModel)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = $"rm {modelName}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                }
            }
            catch { }
        }

        public async Task<bool> InstallModelAsync(string modelName = DefaultModel)
        {
            return await EnsureModelInstalledAsync(modelName);
        }

        private bool IsOllamaInstalled()
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    process.WaitForExit(1000);
                    return process.ExitCode == 0;
                }
            }
            catch { }

            return false;
        }
    }
} 