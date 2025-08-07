using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services.AI
{
    public interface IUnifiedBackend
    {
        Task<ToolCallResult> SendAsync(
            string provider,
            string model,
            ChatMessage[] messages,
            FunctionSpec[] functions,
            CancellationToken cancellationToken);

        Task<bool> ValidateApiKeyAsync(string provider, string apiKey, CancellationToken cancellationToken);
    }

    public class UnifiedBackend : IUnifiedBackend
    {
        private readonly ISecureKeyStore _keyStore;
        private readonly IProviderModelService _providerService;

        public UnifiedBackend(ISecureKeyStore keyStore, IProviderModelService providerService)
        {
            _keyStore = keyStore;
            _providerService = providerService;
        }

        public async Task<ToolCallResult> SendAsync(
            string provider,
            string model,
            ChatMessage[] messages,
            FunctionSpec[] functions,
            CancellationToken cancellationToken)
        {
            var backend = CreateBackend(provider);
            if (backend == null)
            {
                return new ToolCallResult
                {
                    Success = false,
                    ErrorMessage = $"Unknown provider: {provider}"
                };
            }

            // Get model info to check if it supports function calling
            var providerConfig = _providerService.GetProvider(provider);
            var modelInfo = providerConfig?.Models?.FirstOrDefault(m => m.Id == model);
            
            // If model doesn't support function calling, don't pass functions
            if (modelInfo != null && !modelInfo.SupportsFunctionCalling)
            {
                functions = Array.Empty<FunctionSpec>();
            }

            return await backend.SendAsync(messages, functions, model, cancellationToken);
        }

        public async Task<bool> ValidateApiKeyAsync(string provider, string apiKey, CancellationToken cancellationToken)
        {
            IModelBackend? backend = provider.ToLower() switch
            {
                "openai" => new OpenAiBackend(apiKey),
                "anthropic" => new AnthropicBackend(apiKey),
                "google" => new GeminiBackend(apiKey),
                "xai" => new GrokBackend(apiKey),
                _ => null
            };

            if (backend == null)
                return false;

            return await backend.ValidateApiKeyAsync(apiKey, cancellationToken);
        }

        private IModelBackend? CreateBackend(string provider)
        {
            // Check for local AI first
            if (provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
            {
                return new LocalAIBackend();
            }

            var apiKey = _keyStore.GetKey(provider);
            if (string.IsNullOrEmpty(apiKey))
                return null;

            return provider.ToLower() switch
            {
                "openai" => new OpenAiBackend(apiKey),
                "anthropic" => new AnthropicBackend(apiKey),
                "google" => new GeminiBackend(apiKey),
                "xai" => new GrokBackend(apiKey),
                _ => null
            };
        }
    }
} 