using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services.AI
{
    public class LocalAIBackend : IModelBackend
    {
        private readonly HttpClient _httpClient;
        private readonly int _port;

        public LocalAIBackend(int port = 8080)
        {
            _port = port;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri($"http://localhost:{port}/"),
                Timeout = TimeSpan.FromMinutes(5)
            };
        }

        public async Task<ToolCallResult> SendAsync(
            ChatMessage[] messages,
            FunctionSpec[] functions,
            string model,
            CancellationToken cancellationToken)
        {
            try
            {
                // Use OpenAI-compatible API format for local server
                var request = new
                {
                    model = model,
                    messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                    temperature = 0.7,
                    max_tokens = 4096,
                    tools = functions?.Length > 0 ? functions.Select(f => new
                    {
                        type = "function",
                        function = new
                        {
                            name = f.Name,
                            description = f.Description,
                            parameters = f.Parameters
                        }
                    }).ToArray() : null,
                    tool_choice = functions?.Length > 0 ? "auto" : null
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("v1/chat/completions", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return new ToolCallResult
                    {
                        Success = false,
                        ErrorMessage = $"Local AI server error {response.StatusCode}: {error}"
                    };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                dynamic result = JsonConvert.DeserializeObject(responseJson);

                if (result?.choices == null || result.choices.Count == 0)
                {
                    return new ToolCallResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid response from local AI server"
                    };
                }

                var choice = result.choices[0];
                var toolCalls = new List<ToolCall>();

                if (choice.message.tool_calls != null)
                {
                    foreach (var tc in choice.message.tool_calls)
                    {
                        toolCalls.Add(new ToolCall
                        {
                            Id = tc.id?.ToString() ?? Guid.NewGuid().ToString(),
                            Type = "function",
                            Function = new FunctionCall
                            {
                                Name = tc.function.name,
                                Arguments = tc.function.arguments?.ToString() ?? "{}"
                            }
                        });
                    }
                }

                return new ToolCallResult
                {
                    Success = true,
                    AssistantMessage = choice.message.content?.ToString(),
                    ToolCalls = toolCalls
                };
            }
            catch (HttpRequestException ex)
            {
                // Try Ollama-compatible fallback
                try
                {
                    using var ollamaClient = new HttpClient { BaseAddress = new Uri("http://localhost:11434/"), Timeout = TimeSpan.FromMinutes(5) };
                    var ollamaPayload = new
                    {
                        model = model,
                        messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                        stream = false,
                        options = new { temperature = 0.7, num_ctx = 8192 }
                    };
                    var ollamaJson = JsonConvert.SerializeObject(ollamaPayload);
                    var ollamaReq = new StringContent(ollamaJson, Encoding.UTF8, "application/json");
                    var ollamaResp = await ollamaClient.PostAsync("api/chat", ollamaReq, cancellationToken);
                    if (!ollamaResp.IsSuccessStatusCode)
                    {
                        var err = await ollamaResp.Content.ReadAsStringAsync();
                        return new ToolCallResult { Success = false, ErrorMessage = $"Cannot connect to local AI or Ollama. Last error: {err}" };
                    }
                    var respJson = await ollamaResp.Content.ReadAsStringAsync();
                    dynamic parsed = JsonConvert.DeserializeObject(respJson);
                    string assistant = parsed?.message?.content?.ToString() ?? parsed?.response?.ToString() ?? "";
                    return new ToolCallResult { Success = true, AssistantMessage = assistant, ToolCalls = new List<ToolCall>() };
                }
                catch (Exception ollamaEx)
                {
                    return new ToolCallResult
                    {
                        Success = false,
                        ErrorMessage = $"Cannot connect to local AI server on port {_port} and Ollama fallback failed: {ollamaEx.Message}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ToolCallResult
                {
                    Success = false,
                    ErrorMessage = $"Error calling local AI server: {ex.Message}"
                };
            }
        }

        public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken)
        {
            // Local AI doesn't need API key, just check if server is running
            return await IsServerRunningAsync(cancellationToken);
        }

        public async Task<bool> IsServerRunningAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("health", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                // Try OpenAI-compatible endpoint
                try
                {
                    var response = await _httpClient.GetAsync("v1/models", cancellationToken);
                    return response.IsSuccessStatusCode;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
