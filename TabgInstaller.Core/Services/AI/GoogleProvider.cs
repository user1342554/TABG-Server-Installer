using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services.AI
{
    public class GoogleProvider : IAiProvider
    {
        private static readonly HttpClient _http = new HttpClient() { Timeout = TimeSpan.FromMinutes(10) };

        public async Task<string> SendAsync(string apiKey, string model, IList<AiMessage> messages)
        {
            // Gemini Generative Language API - responses:generate
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var parts = new List<object>();
            foreach (var m in messages)
            {
                if (m.Role == "user" || m.Role == "system")
                    parts.Add(new { role = "user", parts = new[] { new { text = m.Content } } });
                else
                    parts.Add(new { role = "model", parts = new[] { new { text = m.Content } } });
            }

            var payload = new
            {
                contents = parts
            };

            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var text = root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            return text ?? string.Empty;
        }

        public async Task<StreamingResponse> SendStreamAsync(string apiKey, string model, IList<AiMessage> messages, Action<string> onToken)
        {
            // For now, Google Provider doesn't support streaming, so we'll use the regular endpoint and simulate streaming
            var result = await SendAsync(apiKey, model, messages);
            
            // Simulate streaming by sending tokens
            var words = result.Split(' ');
            foreach (var word in words)
            {
                onToken(word + " ");
                await Task.Delay(50); // Small delay to simulate streaming
            }
            
            return new StreamingResponse
            {
                Content = result,
                Reasoning = null
            };
        }
    }
}


