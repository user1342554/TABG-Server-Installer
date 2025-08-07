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
                            new ModelInfo { Id = "gpt-5", DisplayName = "GPT-5", SupportsFunctionCalling = true },
                            new ModelInfo { Id = "gpt-oss-120b", DisplayName = "GPT-OSS 120B", SupportsFunctionCalling = true },
                            new ModelInfo { Id = "gpt-oss-20b", DisplayName = "GPT-OSS 20B", SupportsFunctionCalling = true },
                            new ModelInfo { Id = "gpt-4o", DisplayName = "GPT-4o", SupportsFunctionCalling = true },
                            new ModelInfo { Id = "gpt-4o-mini", DisplayName = "GPT-4o Mini", SupportsFunctionCalling = true }
                        }
                    }
                }
            };
        }
    }
} 