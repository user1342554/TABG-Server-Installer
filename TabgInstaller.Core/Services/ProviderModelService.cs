using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public interface IProviderModelService
    {
        List<ProviderConfig> GetProviders();
        ProviderConfig? GetProvider(string name);
        List<ModelInfo> GetModelsForProvider(string providerName);
    }

    public class ProviderModelService : IProviderModelService
    {
        private readonly ProvidersConfiguration _configuration;

        public ProviderModelService()
        {
            var modelsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models.json");
            
            // If not found in base directory, try parent directories (for development)
            if (!File.Exists(modelsPath))
            {
                var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (currentDir?.Parent != null && !File.Exists(modelsPath))
                {
                    modelsPath = Path.Combine(currentDir.Parent.FullName, "models.json");
                    if (!File.Exists(modelsPath))
                        currentDir = currentDir.Parent;
                    else
                        break;
                }
            }

            if (File.Exists(modelsPath))
            {
                try
                {
                    var json = File.ReadAllText(modelsPath);
                    _configuration = JsonConvert.DeserializeObject<ProvidersConfiguration>(json) ?? new ProvidersConfiguration();
                    System.Diagnostics.Debug.WriteLine($"Loaded models.json from: {modelsPath}");
                    System.Diagnostics.Debug.WriteLine($"Providers count: {_configuration.Providers.Count}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading models.json: {ex.Message}");
                    _configuration = GetDefaultConfiguration();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"models.json not found at: {modelsPath}");
                // Fallback configuration if file not found
                _configuration = GetDefaultConfiguration();
            }
        }

        public List<ProviderConfig> GetProviders()
        {
            return _configuration.Providers;
        }

        public ProviderConfig? GetProvider(string name)
        {
            return _configuration.Providers.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public List<ModelInfo> GetModelsForProvider(string providerName)
        {
            var provider = GetProvider(providerName);
            return provider?.Models ?? new List<ModelInfo>();
        }

        private ProvidersConfiguration GetDefaultConfiguration()
        {
            return new ProvidersConfiguration
            {
                Providers = new List<ProviderConfig>
                {
                    new ProviderConfig
                    {
                        Name = "OpenAI",
                        ApiEndpoint = "https://api.openai.com/v1/chat/completions",
                        AuthType = "Bearer",
                        Models = new List<ModelInfo>
                        {
                            new ModelInfo { Id = "gpt-4o", DisplayName = "GPT-4o" },
                            new ModelInfo { Id = "gpt-4o-mini", DisplayName = "GPT-4o Mini" }
                        }
                    },
                    new ProviderConfig
                    {
                        Name = "Ollama",
                        ApiEndpoint = "http://localhost:11434/v1/chat/completions",
                        AuthType = "none",
                        Models = new List<ModelInfo>
                        {
                            new ModelInfo { Id = "Tr3cks/deepseek-r1-tool-calling:8b", DisplayName = "DeepSeek-R1 8B with Tools (Best for TABG)", SupportsFunctionCalling = true },
                            new ModelInfo { Id = "llama3.2:latest", DisplayName = "Llama 3.2 Latest (Recommended)" },
                            new ModelInfo { Id = "mistral:latest", DisplayName = "Mistral 7B (Fast & Light)" },
                            new ModelInfo { Id = "gemma2:9b", DisplayName = "Gemma 2 9B (Google)" },
                            new ModelInfo { Id = "qwen2.5:14b", DisplayName = "Qwen 2.5 14B (Powerful)" },
                            new ModelInfo { Id = "phi3:medium", DisplayName = "Phi-3 Medium (Microsoft)" },
                            new ModelInfo { Id = "deepseek-coder-v2:16b", DisplayName = "DeepSeek Coder V2 16B (For Code)" }
                        }
                    }
                }
            };
        }
    }
} 