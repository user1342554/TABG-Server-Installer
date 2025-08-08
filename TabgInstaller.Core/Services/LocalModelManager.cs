using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services
{
    public class LocalModelInfo
    {
        public string ModelId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string HuggingFaceRepo { get; set; } = "";
        public List<ModelShard> Shards { get; set; } = new();
        public long TotalSize { get; set; }
        public string LocalPath { get; set; } = "";
        public bool IsInstalled { get; set; }
        public string OllamaModelName { get; set; } = "";
    }

    public class ModelShard
    {
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
        public string Sha256 { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
    }

    public interface ILocalModelManager
    {
        Task<List<LocalModelInfo>> GetAvailableModelsAsync();
        Task<bool> IsModelInstalledAsync(string modelId);
        Task<string> GetModelPathAsync(string modelId);
        Task DownloadModelAsync(string modelId, string destinationDirectory, IProgress<(string message, double percentage)> progress, CancellationToken cancellationToken);
        Task<Process?> StartLocalInferenceServerAsync(string modelId, int port = 8080, CancellationToken cancellationToken = default);
        Task StopLocalInferenceServerAsync();
        bool ValidateModelChecksum(string filePath, string expectedChecksum);
        void SetModelsDirectory(string directory);
        string GetModelsDirectory();
        Task<bool> InstallOllamaModelAsync(string modelId, IProgress<string> progress, CancellationToken cancellationToken);
    }

    public class LocalModelManager : ILocalModelManager
    {
        private readonly HttpClient _httpClient;
        private Process? _inferenceProcess;
        private string _modelsDirectory;
        private readonly Dictionary<string, LocalModelInfo> _modelRegistry;
        private readonly string _settingsPath;

        private class LocalAISettings
        {
            public string ModelsDirectory { get; set; } = "";
        }

        public LocalModelManager()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromHours(4) }; // Long timeout for large downloads
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TABGInstaller/1.0");
            var baseLocalApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDir = Path.Combine(baseLocalApp, "TABGInstaller");
            Directory.CreateDirectory(appDir);
            _settingsPath = Path.Combine(appDir, "LocalAISettings.json");

            // Default
            _modelsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TABGInstaller", "Models");

            // 1) Environment override
            var envOverride = Environment.GetEnvironmentVariable("TABGI_MODELS_DIR")
                               ?? Environment.GetEnvironmentVariable("TABG_MODELS_DIR");
            if (!string.IsNullOrWhiteSpace(envOverride) && Directory.Exists(envOverride))
            {
                _modelsDirectory = envOverride!;
            }
            else
            {
                // 2) Load persisted settings
                try
                {
                    if (File.Exists(_settingsPath))
                    {
                        var json = File.ReadAllText(_settingsPath);
                        var settings = JsonSerializer.Deserialize<LocalAISettings>(json);
                        if (!string.IsNullOrWhiteSpace(settings?.ModelsDirectory) && Directory.Exists(settings.ModelsDirectory))
                        {
                            _modelsDirectory = settings!.ModelsDirectory;
                        }
                    }
                }
                catch
                {
                    // ignore settings load errors; will try auto-detect
                }

                // 3) Auto-detect across drives if current dir has no models
                if (!HasAnyModelInstalledIn(_modelsDirectory))
                {
                    var detected = TryAutoDetectModelsDirectory();
                    if (!string.IsNullOrEmpty(detected))
                    {
                        _modelsDirectory = detected;
                    }
                }
            }
            
            // Register the real GPT-OSS models from Hugging Face
            _modelRegistry = new Dictionary<string, LocalModelInfo>
            {
                ["gpt-oss-20b"] = new LocalModelInfo
                {
                    ModelId = "gpt-oss-20b",
                    DisplayName = "GPT-OSS 20B (10.3 GiB)",
                    HuggingFaceRepo = "openai/gpt-oss-20b",
                    OllamaModelName = "gpt-oss:20b",
                    // 4.79 + 4.80 + 0.02 + small files ≈ 9.6 GiB; include overhead ~10.3 GiB
                    TotalSize = 11_058_841_600L,
                    Shards = new List<ModelShard>
                    {
                        new ModelShard
                        {
                            FileName = "model-00000-of-00002.safetensors",
                            FileSize = 5_142_068_224L,
                            Sha256 = "16d0f997dcfc4462089d536bffe51b4bcea2f872f5c430be09ef8ed392312427",
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-20b/resolve/main/model-00000-of-00002.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00001-of-00002.safetensors",
                            FileSize = 5_153_960_960L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-20b/resolve/main/model-00001-of-00002.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model.safetensors.index.json",
                            FileSize = 23_552L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-20b/resolve/main/model.safetensors.index.json"
                        },
                        new ModelShard
                        {
                            FileName = "config.json",
                            FileSize = 1_024L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-20b/resolve/main/config.json"
                        },
                        new ModelShard
                        {
                            FileName = "tokenizer.json",
                            FileSize = 17_209_245L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-20b/resolve/main/tokenizer.json"
                        },
                        new ModelShard
                        {
                            FileName = "tokenizer_config.json",
                            FileSize = 1_024L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-20b/resolve/main/tokenizer_config.json"
                        }
                    }
                },
                ["gpt-oss-120b"] = new LocalModelInfo
                {
                    ModelId = "gpt-oss-120b",
                    DisplayName = "GPT-OSS 120B (69.5 GiB)",
                    HuggingFaceRepo = "openai/gpt-oss-120b",
                    OllamaModelName = "gpt-oss:120b",
                    // 14 shards × ~4.63 GiB ≈ 64.8 GiB + tokenizer/config overhead ≈ 69.5 GiB
                    TotalSize = 74_632_499_200L,
                    Shards = new List<ModelShard>
                    {
                        // Add all 15 shards for the 120B model
                        new ModelShard
                        {
                            FileName = "model-00000-of-00014.safetensors",
                            FileSize = 4_360_478_720L, // ~4.06 GB
                            Sha256 = "695218884684c611fe08a74751ee443f971e9bd9bc062edba822da3fe45969b7",
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00000-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00001-of-00014.safetensors",
                            FileSize = 4_970_786_816L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00001-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00002-of-00014.safetensors",
                            FileSize = 4_970_786_816L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00002-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00003-of-00014.safetensors",
                            FileSize = 4_970_786_816L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00003-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00004-of-00014.safetensors",
                            FileSize = 4_970_786_816L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00004-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00005-of-00014.safetensors",
                            FileSize = 4_970_786_816L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00005-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00006-of-00014.safetensors",
                            FileSize = 4_970_786_816L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00006-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00007-of-00014.safetensors",
                            FileSize = 4_970_786_816L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00007-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00008-of-00014.safetensors",
                            FileSize = 4_970_786_816L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00008-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00009-of-00014.safetensors",
                            FileSize = 4_970_786_816L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00009-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00010-of-00014.safetensors",
                            FileSize = 4_970_786_816L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00010-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00011-of-00014.safetensors",
                            FileSize = 4_970_786_816L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00011-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00012-of-00014.safetensors",
                            FileSize = 4_970_786_816L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00012-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model-00013-of-00014.safetensors",
                            FileSize = 4_970_786_816L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model-00013-of-00014.safetensors"
                        },
                        new ModelShard
                        {
                            FileName = "model.safetensors.index.json",
                            FileSize = 100_000L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/model.safetensors.index.json"
                        },
                        new ModelShard
                        {
                            FileName = "config.json",
                            FileSize = 1_024L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/config.json"
                        },
                        new ModelShard
                        {
                            FileName = "tokenizer.json",
                            FileSize = 17_209_245L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/tokenizer.json"
                        },
                        new ModelShard
                        {
                            FileName = "tokenizer_config.json",
                            FileSize = 1_024L,
                            DownloadUrl = "https://huggingface.co/openai/gpt-oss-120b/resolve/main/tokenizer_config.json"
                        }
                    }
                }
            };
        }

        public void SetModelsDirectory(string directory)
        {
            _modelsDirectory = directory;
            Directory.CreateDirectory(_modelsDirectory);
            // persist
            try
            {
                var settings = new LocalAISettings { ModelsDirectory = _modelsDirectory };
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch
            {
                // ignore persistence errors
            }
        }

        public string GetModelsDirectory() => _modelsDirectory;

        public async Task<List<LocalModelInfo>> GetAvailableModelsAsync()
        {
            var models = _modelRegistry.Values.ToList();
            
            // Check installation status for each model
            foreach (var model in models)
            {
                model.IsInstalled = await IsModelInstalledAsync(model.ModelId);
                if (model.IsInstalled)
                {
                    model.LocalPath = await GetModelPathAsync(model.ModelId);
                }
            }
            
            return models;
        }

        public async Task<bool> IsModelInstalledAsync(string modelId)
        {
            return await Task.Run(() =>
            {
                if (!_modelRegistry.TryGetValue(modelId, out var modelInfo))
                    return false;

                var modelDir = Path.Combine(_modelsDirectory, modelId);
                if (!Directory.Exists(modelDir))
                    return false;

                // Check if all required shards exist
                foreach (var shard in modelInfo.Shards)
                {
                    var shardPath = Path.Combine(modelDir, shard.FileName);
                    if (!File.Exists(shardPath))
                        return false;
                    
                    // Optionally check file size
                    var fileInfo = new FileInfo(shardPath);
                    if (fileInfo.Length != shard.FileSize && shard.FileSize > 0)
                        return false;
                }

                return true;
            });
        }

        private bool HasAnyModelInstalledIn(string directory)
        {
            try
            {
                foreach (var kv in _modelRegistry)
                {
                    var modelId = kv.Key;
                    var info = kv.Value;
                    var modelDir = Path.Combine(directory, modelId);
                    if (!Directory.Exists(modelDir))
                        continue;
                    // consider installed if at least 1 registered shard exists
                    if (info.Shards.Any(s => File.Exists(Path.Combine(modelDir, s.FileName))))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private string TryAutoDetectModelsDirectory()
        {
            try
            {
                // Candidate roots: user AppData, LocalAppData, and all fixed drives
                var candidates = new List<string>();
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                candidates.Add(Path.Combine(appData, "TABGInstaller", "Models"));
                candidates.Add(Path.Combine(localApp, "TABGInstaller", "Models"));

                // Common root-level folders on each fixed drive
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
                {
                    var root = drive.RootDirectory.FullName;
                    candidates.Add(Path.Combine(root, "TABGInstaller", "Models"));
                    candidates.Add(Path.Combine(root, "TABGAI", "Models"));
                    candidates.Add(Path.Combine(root, "AI", "Models"));
                    candidates.Add(Path.Combine(root, "Models"));
                }

                foreach (var candidate in candidates.Distinct())
                {
                    if (Directory.Exists(candidate) && HasAnyModelInstalledIn(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        public async Task<string> GetModelPathAsync(string modelId)
        {
            return await Task.Run(() => Path.Combine(_modelsDirectory, modelId));
        }

        public async Task DownloadModelAsync(string modelId, string destinationDirectory, IProgress<(string message, double percentage)> progress, CancellationToken cancellationToken)
        {
            if (!_modelRegistry.TryGetValue(modelId, out var modelInfo))
            {
                throw new ArgumentException($"Unknown model: {modelId}");
            }

            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                _modelsDirectory = destinationDirectory;
            }

            Directory.CreateDirectory(_modelsDirectory);
            var modelDir = Path.Combine(_modelsDirectory, modelId);
            Directory.CreateDirectory(modelDir);

            // Check what's already downloaded
            var existingShards = new HashSet<string>();
            long totalDownloaded = 0;
            
            foreach (var shard in modelInfo.Shards)
            {
                var shardPath = Path.Combine(modelDir, shard.FileName);
                if (File.Exists(shardPath))
                {
                    var fileInfo = new FileInfo(shardPath);
                    if (fileInfo.Length == shard.FileSize || shard.FileSize == 0)
                    {
                        existingShards.Add(shard.FileName);
                        totalDownloaded += fileInfo.Length;
                    }
                }
            }

            if (existingShards.Count == modelInfo.Shards.Count)
            {
                progress?.Report(($"Model {modelInfo.DisplayName} is already fully downloaded!", 100));
                return;
            }

            progress?.Report(($"Starting download of {modelInfo.DisplayName}...", 0));
            
            var shardsToDownload = modelInfo.Shards.Where(s => !existingShards.Contains(s.FileName)).ToList();
            var totalShardsSize = shardsToDownload.Sum(s => s.FileSize);
            long downloadedBytes = totalDownloaded;

            try
            {
                for (int i = 0; i < shardsToDownload.Count; i++)
                {
                    var shard = shardsToDownload[i];
                    var shardPath = Path.Combine(modelDir, shard.FileName);
                    
                    progress?.Report(($"Downloading {shard.FileName} ({i + 1}/{shardsToDownload.Count})...", 
                        (double)downloadedBytes / modelInfo.TotalSize * 100));

                    await DownloadFileAsync(
                        shard.DownloadUrl, 
                        shardPath, 
                        shard.FileSize,
                        (shardProgress) =>
                        {
                            var overallProgress = (downloadedBytes + (shard.FileSize * shardProgress / 100.0)) / modelInfo.TotalSize * 100;
                            progress?.Report(($"Downloading {shard.FileName}: {shardProgress:F1}%", overallProgress));
                        },
                        cancellationToken);

                    downloadedBytes += shard.FileSize;
                    
                    // Verify checksum if available
                    if (!string.IsNullOrEmpty(shard.Sha256))
                    {
                        progress?.Report(($"Verifying {shard.FileName}...", (double)downloadedBytes / modelInfo.TotalSize * 100));
                        if (!ValidateModelChecksum(shardPath, shard.Sha256))
                        {
                            File.Delete(shardPath);
                            throw new Exception($"Checksum verification failed for {shard.FileName}");
                        }
                    }
                }

                progress?.Report(($"{modelInfo.DisplayName} downloaded successfully!", 100));
                
                // After successful download, register with Ollama
                progress?.Report(("Registering model with Ollama...", 100));
                await InstallOllamaModelAsync(modelId, 
                    new Progress<string>(msg => progress?.Report((msg, 100))), 
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Clean up partial downloads on error
                foreach (var shard in shardsToDownload)
                {
                    var shardPath = Path.Combine(modelDir, shard.FileName);
                    if (File.Exists(shardPath))
                    {
                        try { File.Delete(shardPath); } catch { }
                    }
                }
                throw new Exception($"Failed to download model: {ex.Message}", ex);
            }
        }

        private async Task DownloadFileAsync(string url, string destinationPath, long expectedSize, Action<double> progressCallback, CancellationToken cancellationToken)
        {
            var tempPath = destinationPath + ".tmp";
            
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? expectedSize;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[81920]; // 80KB buffer for better performance
                    var totalRead = 0L;
                    var lastReportTime = DateTime.UtcNow;

                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        totalRead += bytesRead;

                        // Report progress at most once per second to avoid UI flooding
                        if ((DateTime.UtcNow - lastReportTime).TotalMilliseconds > 100)
                        {
                            if (totalBytes > 0)
                            {
                                var percentage = (double)totalRead / totalBytes * 100;
                                progressCallback?.Invoke(percentage);
                            }
                            lastReportTime = DateTime.UtcNow;
                        }
                    }
                    
                    // Final progress report
                    progressCallback?.Invoke(100);
                }

                // Move temp file to final location
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
                File.Move(tempPath, destinationPath);
            }
            catch
            {
                // Clean up temp file on error
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
                throw;
            }
        }

        public bool ValidateModelChecksum(string filePath, string expectedChecksum)
        {
            if (string.IsNullOrEmpty(expectedChecksum))
                return true; // Skip validation if no checksum provided
                
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = sha256.ComputeHash(stream);
                var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                return hashString.Equals(expectedChecksum.ToLowerInvariant());
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> InstallOllamaModelAsync(string modelId, IProgress<string> progress, CancellationToken cancellationToken)
        {
            if (!_modelRegistry.TryGetValue(modelId, out var modelInfo))
                return false;

            var modelDir = Path.Combine(_modelsDirectory, modelId);
            
            // Create a Modelfile for Ollama
            var modelfilePath = Path.Combine(modelDir, "Modelfile");
            var modelfileContent = $@"FROM {modelDir}
TEMPLATE ""{{{{ if .System }}}}{{{{ .System }}}}{{{{ end }}}}{{{{ if .Prompt }}}}{{{{ .Prompt }}}}{{{{ end }}}}{{{{ if .Response }}}}{{{{ .Response }}}}{{{{ end }}}}""
PARAMETER temperature 0.7
PARAMETER top_p 0.9
PARAMETER num_ctx 128000";

            await File.WriteAllTextAsync(modelfilePath, modelfileContent, cancellationToken);

            try
            {
                // First try to pull the model from Ollama's registry
                progress?.Report($"Attempting to pull {modelInfo.OllamaModelName} from Ollama registry...");
                
                var pullProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = $"pull {modelInfo.OllamaModelName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });

                if (pullProcess != null)
                {
                    await pullProcess.WaitForExitAsync(cancellationToken);
                    
                    if (pullProcess.ExitCode == 0)
                    {
                        progress?.Report($"Successfully pulled {modelInfo.OllamaModelName} from Ollama!");
                        return true;
                    }
                }
                
                // If pull fails, try to create from local files
                progress?.Report($"Creating Ollama model from local files...");
                
                var createProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = $"create {modelInfo.OllamaModelName} -f \"{modelfilePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = modelDir
                });

                if (createProcess != null)
                {
                    await createProcess.WaitForExitAsync(cancellationToken);
                    
                    if (createProcess.ExitCode == 0)
                    {
                        progress?.Report($"Successfully created {modelInfo.OllamaModelName} in Ollama!");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Error registering with Ollama: {ex.Message}");
            }

            return false;
        }

        public async Task<Process?> StartLocalInferenceServerAsync(string modelId, int port = 8080, CancellationToken cancellationToken = default)
        {
            if (!await IsModelInstalledAsync(modelId))
            {
                throw new InvalidOperationException($"Model {modelId} is not installed");
            }

            if (_inferenceProcess != null && !_inferenceProcess.HasExited)
            {
                await StopLocalInferenceServerAsync();
            }

            if (!_modelRegistry.TryGetValue(modelId, out var modelInfo))
            {
                throw new ArgumentException($"Unknown model: {modelId}");
            }

            // Start Ollama serve if not running
            var serveProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            // Wait for server to start
            await Task.Delay(3000, cancellationToken);

            // Set the model to use
            Environment.SetEnvironmentVariable("OLLAMA_MODEL", modelInfo.OllamaModelName);

            _inferenceProcess = serveProcess;
            return _inferenceProcess;
        }

        public async Task StopLocalInferenceServerAsync()
        {
            if (_inferenceProcess != null && !_inferenceProcess.HasExited)
            {
                _inferenceProcess.Kill(true);
                await _inferenceProcess.WaitForExitAsync();
                _inferenceProcess.Dispose();
                _inferenceProcess = null;
            }
        }
    }
}