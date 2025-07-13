using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TabgInstaller.Core.Model
{
    public class ProviderConfig
    {
        public string Name { get; set; } = "";
        public string ApiEndpoint { get; set; } = "";
        public string AuthType { get; set; } = "";
        public List<ModelInfo> Models { get; set; } = new();
    }

    public class ModelInfo
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        
        [JsonProperty("contextWindow")]
        public int ContextWindow { get; set; } = 8192;
        
        [JsonProperty("supportsFunctionCalling")]
        public bool SupportsFunctionCalling { get; set; } = false;
    }

    public class ProvidersConfiguration
    {
        public List<ProviderConfig> Providers { get; set; } = new();
    }

    public class ChatMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; } = "";
        
        [JsonProperty("content")]
        public string Content { get; set; } = "";
        
        public static ChatMessage System(string content) => new() { Role = "system", Content = content };
        public static ChatMessage User(string content) => new() { Role = "user", Content = content };
        public static ChatMessage Assistant(string content) => new() { Role = "assistant", Content = content };
    }

    public class FunctionSpec
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class ToolCall
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";
        
        [JsonProperty("type")]
        public string Type { get; set; } = "function";
        
        [JsonProperty("function")]
        public FunctionCall Function { get; set; } = new();
    }

    public class FunctionCall
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";
        
        [JsonProperty("arguments")]
        public string Arguments { get; set; } = "";
    }

    public class ToolCallResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<ToolCall> ToolCalls { get; set; } = new();
        public string? AssistantMessage { get; set; }
    }

    // Request/Response models for different providers
    public class OpenAiRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; } = "";
        
        [JsonProperty("messages")]
        public List<ChatMessage> Messages { get; set; } = new();
        
        [JsonProperty("tools")]
        public List<object>? Tools { get; set; }
        
        [JsonProperty("tool_choice")]
        public string? ToolChoice { get; set; }
    }

    public class OpenAiResponse
    {
        [JsonProperty("choices")]
        public List<OpenAiChoice> Choices { get; set; } = new();
    }

    public class OpenAiChoice
    {
        [JsonProperty("message")]
        public OpenAiMessage Message { get; set; } = new();
    }

    public class OpenAiMessage
    {
        [JsonProperty("content")]
        public string? Content { get; set; }
        
        [JsonProperty("tool_calls")]
        public List<ToolCall>? ToolCalls { get; set; }
    }

    public class AnthropicRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; } = "";
        
        [JsonProperty("messages")]
        public List<ChatMessage> Messages { get; set; } = new();
        
        [JsonProperty("tools")]
        public List<object>? Tools { get; set; }
        
        [JsonProperty("max_tokens")]
        public int MaxTokens { get; set; } = 4096;
        
        [JsonProperty("system", NullValueHandling = NullValueHandling.Ignore)]
        public string? System { get; set; }
    }

    public class AnthropicResponse
    {
        [JsonProperty("content")]
        public List<AnthropicContent> Content { get; set; } = new();
    }

    public class AnthropicContent
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";
        
        [JsonProperty("text")]
        public string? Text { get; set; }
        
        [JsonProperty("id")]
        public string? Id { get; set; }
        
        [JsonProperty("name")]
        public string? Name { get; set; }
        
        [JsonProperty("input")]
        public object? Input { get; set; }
    }
} 