using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services.AI
{
    public class GeminiVertexProvider : IAiProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _projectId;
        private readonly string _location;

        public GeminiVertexProvider(string projectId, string location = "global")
        {
            _projectId = projectId;
            _location = string.IsNullOrWhiteSpace(location) ? "global" : location;
            _httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(10) };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<string> SendAsync(string accessToken, string model, IList<AiMessage> messages)
        {
            // Refresh Authorization header per request from provided token or env
            var token = string.IsNullOrWhiteSpace(accessToken)
                ? System.Environment.GetEnvironmentVariable("GOOGLE_ACCESS_TOKEN") ?? string.Empty
                : accessToken;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"https://{_location}-aiplatform.googleapis.com/v1/projects/{_projectId}/locations/{_location}/publishers/google/models/{model}:generateContent";

            var userParts = new List<object>();
            foreach (var m in messages)
            {
                if (m.Role == "system" || m.Role == "user")
                {
                    userParts.Add(new { text = m.Content });
                }
            }

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = userParts
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var res = await _httpClient.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                throw new Exception($"Gemini Vertex API error {(int)res.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var candidates = root.GetProperty("candidates");
            if (candidates.GetArrayLength() > 0)
            {
                var parts = candidates[0].GetProperty("content").GetProperty("parts");
                if (parts.GetArrayLength() > 0 && parts[0].TryGetProperty("text", out var t))
                {
                    return t.GetString() ?? string.Empty;
                }
            }
            return string.Empty;
        }

        public async Task<StreamingResponse> SendStreamAsync(string accessToken, string model, IList<AiMessage> messages, Action<string> onToken)
        {
            // Refresh Authorization header per request from provided token or env
            var token = string.IsNullOrWhiteSpace(accessToken)
                ? System.Environment.GetEnvironmentVariable("GOOGLE_ACCESS_TOKEN") ?? string.Empty
                : accessToken;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"https://{_location}-aiplatform.googleapis.com/v1/projects/{_projectId}/locations/{_location}/publishers/google/models/{model}:streamGenerateContent";

            var userParts = new List<object>();
            foreach (var m in messages)
            {
                if (m.Role == "system" || m.Role == "user")
                {
                    userParts.Add(new { text = m.Content });
                }
            }

            var payload = new
            {
                contents = new[]
                {
                    new { role = "user", parts = userParts }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini Vertex API error {(int)response.StatusCode}: {error}");
            }

            var fullContent = new StringBuilder();

            await SseReader.ReadStreamAsync(response, eventText =>
            {
                var data = SseReader.GetDataFromEvent(eventText);
                if (string.IsNullOrEmpty(data)) return;

                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
                    {
                        var candidate = candidates[0];
                        if (candidate.TryGetProperty("content", out var content) &&
                            content.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
                        {
                            var part = parts[0];
                            if (part.TryGetProperty("text", out var text))
                            {
                                var token = text.GetString() ?? "";
                                if (!string.IsNullOrEmpty(token))
                                {
                                    fullContent.Append(token);
                                    onToken(token);
                                }
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
                Reasoning = null // Gemini doesn't expose reasoning separately
            };
        }
    }
}


