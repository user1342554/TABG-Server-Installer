using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services.AI
{
    public interface IModelBackend
    {
        Task<ToolCallResult> SendAsync(
            ChatMessage[] messages,
            FunctionSpec[] functions,
            string model,
            CancellationToken cancellationToken);

        Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken);
    }
} 