using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services.AI
{
    public class AnthropicProvider : IAiProvider
    {
        private static readonly HttpClient _http = new HttpClient() { Timeout = TimeSpan.FromMinutes(10) };

        public async Task<string> SendAsync(string apiKey, string model, IList<AiMessage> messages)
        {
            var url = "https://api.anthropic.com/v1/messages";

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Anthropic expects user/assistant content array
            var converted = new List<object>();
            foreach (var m in messages)
            {
                converted.Add(new { role = m.Role.ToLower(), content = m.Content });
            }

            var payload = new
            {
                model,
                max_tokens = 1024,
                messages = converted
            };

            var json = JsonSerializer.Serialize(payload);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                throw new Exception($"Anthropic API error {(int)res.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var content = root.GetProperty("content")[0].GetProperty("text").GetString();
            return content ?? string.Empty;
        }

        public async Task<StreamingResponse> SendStreamAsync(string apiKey, string model, IList<AiMessage> messages, Action<string> onToken)
        {
            var url = "https://api.anthropic.com/v1/messages";

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");
            req.Headers.Add("anthropic-beta", "thinking-2024-12-11"); // Enable thinking for Claude
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            // Anthropic expects user/assistant content array
            var converted = new List<object>();
            foreach (var m in messages)
            {
                converted.Add(new { role = m.Role.ToLower(), content = m.Content });
            }

            var payload = new
            {
                model,
                max_tokens = 1024,
                messages = converted,
                stream = true,
                thinking = true // Enable thinking stream
            };

            var json = JsonSerializer.Serialize(payload);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Anthropic API error {(int)response.StatusCode}: {error}");
            }

            var fullContent = new StringBuilder();
            var reasoning = new StringBuilder();

            await SseReader.ReadStreamAsync(response, eventText =>
            {
                var data = SseReader.GetDataFromEvent(eventText);
                if (string.IsNullOrEmpty(data)) return;

                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("type", out var typeEl))
                    {
                        var eventType = typeEl.GetString();
                        
                        switch (eventType)
                        {
                            case "content_block_delta":
                                if (root.TryGetProperty("delta", out var delta) && 
                                    delta.TryGetProperty("text", out var text))
                                {
                                    var token = text.GetString() ?? "";
                                    if (!string.IsNullOrEmpty(token))
                                    {
                                        fullContent.Append(token);
                                        onToken(token);
                                    }
                                }
                                break;
                                
                            case "thinking_delta":
                                // Extended thinking stream from Claude
                                if (root.TryGetProperty("delta", out var thinkingDelta) && 
                                    thinkingDelta.TryGetProperty("text", out var thinkingText))
                                {
                                    var thinkingToken = thinkingText.GetString() ?? "";
                                    if (!string.IsNullOrEmpty(thinkingToken))
                                    {
                                        reasoning.Append(thinkingToken);
                                        // Optionally stream thinking tokens too
                                        onToken($"💭 {thinkingToken}");
                                    }
                                }
                                break;
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
                Reasoning = reasoning.Length > 0 ? reasoning.ToString() : null
            };
        }
    }
}


