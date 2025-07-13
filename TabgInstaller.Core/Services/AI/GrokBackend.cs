using System;
using System.Threading;
using System.Threading.Tasks;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services.AI
{
    public class GrokBackend : IModelBackend
    {
        private readonly string _apiKey;

        public GrokBackend(string apiKey)
        {
            _apiKey = apiKey;
        }

        public Task<ToolCallResult> SendAsync(ChatMessage[] messages, FunctionSpec[] functions, string model, CancellationToken cancellationToken)
        {
            // Simplified implementation - would need full xAI Grok API integration
            return Task.FromResult(new ToolCallResult
            {
                Success = false,
                ErrorMessage = "Grok backend not fully implemented yet"
            });
        }

        public Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken)
        {
            // Basic validation - would need actual API call
            return Task.FromResult(!string.IsNullOrEmpty(apiKey));
        }
    }
} 