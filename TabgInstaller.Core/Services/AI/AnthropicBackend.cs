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
    public class AnthropicBackend : IModelBackend
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public AnthropicBackend(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.anthropic.com/"),
                Timeout = TimeSpan.FromMinutes(5)
            };
            _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

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
            var request = new AnthropicRequest
            {
                Model = model,
                Messages = messages.Where(m => m.Role != "system").ToList(),
                MaxTokens = 4096
            };

            // Add system message if present
            var systemMessage = messages.FirstOrDefault(m => m.Role == "system");
            if (systemMessage != null)
            {
                request.System = systemMessage.Content;
            }

            if (functions != null && functions.Length > 0)
            {
                request.Tools = functions.Select(f => new
                {
                    name = f.Name,
                    description = f.Description,
                    input_schema = f.Parameters
                }).Cast<object>().ToList();
            }

            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _retryPolicy.ExecuteAsync(async () => 
                    await _httpClient.PostAsync("v1/messages", content, cancellationToken));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return new ToolCallResult
                    {
                        Success = false,
                        ErrorMessage = $"Anthropic API error {response.StatusCode}: {error}"
                    };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var anthropicResponse = JsonConvert.DeserializeObject<AnthropicResponse>(responseJson);

                var toolCalls = new List<ToolCall>();
                string? assistantMessage = null;

                foreach (var contentItem in anthropicResponse?.Content ?? new List<AnthropicContent>())
                {
                    if (contentItem.Type == "text")
                    {
                        assistantMessage = contentItem.Text;
                    }
                    else if (contentItem.Type == "tool_use")
                    {
                        toolCalls.Add(new ToolCall
                        {
                            Id = contentItem.Id ?? Guid.NewGuid().ToString(),
                            Type = "function",
                            Function = new FunctionCall
                            {
                                Name = contentItem.Name ?? "",
                                Arguments = JsonConvert.SerializeObject(contentItem.Input)
                            }
                        });
                    }
                }

                return new ToolCallResult
                {
                    Success = true,
                    ToolCalls = toolCalls,
                    AssistantMessage = assistantMessage
                };
            }
            catch (Exception ex)
            {
                return new ToolCallResult
                {
                    Success = false,
                    ErrorMessage = $"Exception calling Anthropic API: {ex.Message}"
                };
            }
        }

        public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken)
        {
            using var testClient = new HttpClient();
            testClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            testClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            try
            {
                // Test with a minimal request
                var testRequest = new
                {
                    model = "claude-3-haiku-20240307",
                    messages = new[] { new { role = "user", content = "Hi" } },
                    max_tokens = 1
                };

                var json = JsonConvert.SerializeObject(testRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await testClient.PostAsync("https://api.anthropic.com/v1/messages", content, cancellationToken);
                
                // 401 = invalid key, 200 = valid
                return response.StatusCode != System.Net.HttpStatusCode.Unauthorized;
            }
            catch
            {
                return false;
            }
        }
    }
} 