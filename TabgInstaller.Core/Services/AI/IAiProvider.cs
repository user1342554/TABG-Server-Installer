using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services.AI
{
    public record AiMessage(string Role, string Content);

    public class StreamingResponse
    {
        public string Content { get; set; } = "";
        public string? Reasoning { get; set; }
        public bool HasReasoning => !string.IsNullOrEmpty(Reasoning);
    }

    public interface IAiProvider
    {
        Task<string> SendAsync(string apiKey, string model, IList<AiMessage> messages);
        Task<StreamingResponse> SendStreamAsync(string apiKey, string model, IList<AiMessage> messages, Action<string> onToken);
    }
}


