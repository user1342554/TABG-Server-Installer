using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services.AI
{
    public class XaiProvider : IAiProvider
    {
        private static readonly HttpClient _http = new HttpClient() { Timeout = TimeSpan.FromMinutes(10) };

        public async Task<string> SendAsync(string apiKey, string model, IList<AiMessage> messages)
        {
            // X.ai (Grok) OpenAI-compatible route for chat completions
            var url = "https://api.x.ai/v1/chat/completions";

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var convertedMessages = messages.Select(m => new { role = m.Role.ToLower(), content = m.Content }).ToArray();
            var payload = new
            {
                model,
                messages = convertedMessages,
                stream = false
            };

            var json = JsonSerializer.Serialize(payload);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                throw new Exception($"xAI API error {(int)res.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return content ?? string.Empty;
        }

        public async Task<StreamingResponse> SendStreamAsync(string apiKey, string model, IList<AiMessage> messages, Action<string> onToken)
        {
            // X.ai (Grok) OpenAI-compatible route for chat completions
            var url = "https://api.x.ai/v1/chat/completions";

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            var convertedMessages = messages.Select(m => new { role = m.Role.ToLower(), content = m.Content }).ToArray();
            var payload = new
            {
                model,
                messages = convertedMessages,
                stream = true
            };

            var json = JsonSerializer.Serialize(payload);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"xAI API error {(int)response.StatusCode}: {error}");
            }

            var fullContent = new StringBuilder();

            await SseReader.ReadStreamAsync(response, eventText =>
            {
                var data = SseReader.GetDataFromEvent(eventText);
                if (data == "[DONE]") return;

                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
                    {
                        var choice = choices[0];
                        if (choice.TryGetProperty("delta", out var delta) && 
                            delta.TryGetProperty("content", out var content) && 
                            content.ValueKind == JsonValueKind.String)
                        {
                            var token = content.GetString() ?? "";
                            if (!string.IsNullOrEmpty(token))
                            {
                                fullContent.Append(token);
                                onToken(token);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore malformed JSON chunks
                }
            });

            return new StreamingResponse
            {
                Content = fullContent.ToString(),
                Reasoning = null // xAI doesn't expose reasoning
            };
        }
    }
}


