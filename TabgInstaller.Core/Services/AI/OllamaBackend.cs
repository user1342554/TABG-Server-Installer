using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Polly;
using Polly.Extensions.Http;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services.AI
{
    public class OllamaBackend : IModelBackend
    {
        private readonly HttpClient _httpClient;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public OllamaBackend()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:11434/"),
                Timeout = TimeSpan.FromMinutes(10) // Longer timeout for local models
            };

            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }

        public async Task<ToolCallResult> SendAsync(
            ChatMessage[] messages,
            FunctionSpec[] functions,
            string model,
            CancellationToken cancellationToken)
        {
            // Ollama uses OpenAI-compatible API
            var request = new OpenAiRequest
            {
                Model = model,
                Messages = messages.ToList()
            };

            if (functions != null && functions.Length > 0)
            {
                request.Tools = functions.Select(f => new
                {
                    type = "function",
                    function = new
                    {
                        name = f.Name,
                        description = f.Description,
                        parameters = f.Parameters
                    }
                }).Cast<object>().ToList();

                request.ToolChoice = "auto";
            }

            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _retryPolicy.ExecuteAsync(async () => 
                    await _httpClient.PostAsync("v1/chat/completions", content, cancellationToken));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return new ToolCallResult
                    {
                        Success = false,
                        ErrorMessage = $"Ollama API error {response.StatusCode}: {error}"
                    };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var ollamaResponse = JsonConvert.DeserializeObject<OpenAiResponse>(responseJson);

                var choice = ollamaResponse?.Choices?.FirstOrDefault();
                if (choice?.Message == null)
                {
                    return new ToolCallResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid response from Ollama API"
                    };
                }

                return new ToolCallResult
                {
                    Success = true,
                    ToolCalls = choice.Message.ToolCalls ?? new List<ToolCall>(),
                    AssistantMessage = choice.Message.Content
                };
            }
            catch (HttpRequestException ex)
            {
                return new ToolCallResult
                {
                    Success = false,
                    ErrorMessage = $"Cannot connect to Ollama. Make sure Ollama is running: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new ToolCallResult
                {
                    Success = false,
                    ErrorMessage = $"Exception calling Ollama API: {ex.Message}"
                };
            }
        }

        public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken)
        {
            // Ollama doesn't use API keys, just check if it's running
            try
            {
                var response = await _httpClient.GetAsync("api/tags", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsRunningAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("api/tags", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> HasModelAsync(string modelName, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("api/tags", cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return false;

                var json = await response.Content.ReadAsStringAsync();
                dynamic result = JsonConvert.DeserializeObject(json);
                
                if (result?.models != null)
                {
                    foreach (var model in result.models)
                    {
                        if (model.name == modelName)
                            return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
} 