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
    public class OpenAiBackend : IModelBackend
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public OpenAiBackend(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.openai.com/"),
                Timeout = TimeSpan.FromMinutes(5)
            };
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var reason = outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString();
                        Console.WriteLine($"Retry {retryCount} after {timespan}s: {reason}");
                    });
        }

        public async Task<ToolCallResult> SendAsync(
            ChatMessage[] messages,
            FunctionSpec[] functions,
            string model,
            CancellationToken cancellationToken)
        {
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
                        ErrorMessage = $"OpenAI API error {response.StatusCode}: {error}"
                    };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var openAiResponse = JsonConvert.DeserializeObject<OpenAiResponse>(responseJson);

                var choice = openAiResponse?.Choices?.FirstOrDefault();
                if (choice?.Message == null)
                {
                    return new ToolCallResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid response from OpenAI API"
                    };
                }

                return new ToolCallResult
                {
                    Success = true,
                    ToolCalls = choice.Message.ToolCalls ?? new List<ToolCall>(),
                    AssistantMessage = choice.Message.Content
                };
            }
            catch (Exception ex)
            {
                return new ToolCallResult
                {
                    Success = false,
                    ErrorMessage = $"Exception calling OpenAI API: {ex.Message}"
                };
            }
        }

        public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken)
        {
            using var testClient = new HttpClient();
            testClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            try
            {
                var response = await testClient.GetAsync("https://api.openai.com/v1/models", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
} 