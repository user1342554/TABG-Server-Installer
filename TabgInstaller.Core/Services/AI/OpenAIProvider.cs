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
    public class OpenAIProvider : IAiProvider
    {
        private static readonly HttpClient _http = new HttpClient() { Timeout = TimeSpan.FromMinutes(10) };

        public async Task<string> SendAsync(string apiKey, string model, IList<AiMessage> messages)
        {
            // GPT-5 "thinking" often uses the unified Responses API
            var useResponses = model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase) && !model.Contains("chat", StringComparison.OrdinalIgnoreCase);
            var url = useResponses
                ? "https://api.openai.com/v1/responses"
                : "https://api.openai.com/v1/chat/completions";

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage res;
            if (useResponses)
            {
                // collapse messages to a single input (basic support)
                var sb = new StringBuilder();
                foreach (var m in messages)
                {
                    sb.Append('[').Append(m.Role).Append("] ").AppendLine(m.Content);
                }
                var payload = new
                {
                    model,
                    input = sb.ToString()
                };
                var json = JsonSerializer.Serialize(payload);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                res = await _http.SendAsync(req);
            }
            else
            {
                            var convertedMessages = messages.Select(m => new { role = m.Role.ToLower(), content = m.Content }).ToArray();
            var payload = new
            {
                model,
                messages = convertedMessages,
                stream = false
            };
                var json = JsonSerializer.Serialize(payload);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                res = await _http.SendAsync(req);
            }

            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                throw new Exception($"OpenAI API error {(int)res.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (useResponses)
            {
                // Prefer output_text if available
                if (root.TryGetProperty("output_text", out var outTxt))
                    return outTxt.GetString() ?? string.Empty;

                // Newer responses payload: root.output[] -> item.type=="message" -> content[] (type=="output_text").text
                if (root.TryGetProperty("output", out var outputEl) && outputEl.ValueKind == JsonValueKind.Array)
                {
                    var sbOut = new StringBuilder();
                    foreach (var item in outputEl.EnumerateArray())
                    {
                        if (item.TryGetProperty("type", out var t) && t.GetString() == "message")
                        {
                            if (item.TryGetProperty("content", out var contentArr) && contentArr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var c in contentArr.EnumerateArray())
                                {
                                    // Either nested { type: "output_text", text: "..." } or { text: "..." }
                                    if (c.TryGetProperty("text", out var txtEl))
                                    {
                                        var s = txtEl.GetString();
                                        if (!string.IsNullOrEmpty(s)) sbOut.AppendLine(s);
                                        continue;
                                    }
                                    if (c.TryGetProperty("type", out var ct) && ct.GetString() == "output_text" && c.TryGetProperty("text", out var txt2))
                                    {
                                        var s2 = txt2.GetString();
                                        if (!string.IsNullOrEmpty(s2)) sbOut.AppendLine(s2);
                                    }
                                }
                            }
                        }
                    }
                    var result = sbOut.ToString().Trim();
                    if (!string.IsNullOrEmpty(result)) return result;
                }

                // Fallback to raw body if structure unknown
                return body;
            }
            else
            {
                var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                return content ?? string.Empty;
            }
        }

        public async Task<StreamingResponse> SendStreamAsync(string apiKey, string model, IList<AiMessage> messages, Action<string> onToken)
        {
            // Try streaming first, fallback to non-streaming if organization not verified
            try
            {
                return await TryStreamingRequest(apiKey, model, messages, onToken);
            }
            catch (Exception ex) when (ex.Message.Contains("organization must be verified") || ex.Message.Contains("unsupported_value"))
            {
                // Notify user about fallback (optional)
                onToken("ℹ️ Using simulated streaming (OpenAI organization not verified for real streaming)\n\n");
                
                // Fallback to non-streaming mode for unverified organizations
                var result = await SendAsync(apiKey, model, messages);
                
                // Simulate streaming by sending words with small delays
                var words = result.Split(' ');
                foreach (var word in words)
                {
                    onToken(word + " ");
                    await Task.Delay(30); // Small delay to simulate streaming
                }
                
                return new StreamingResponse
                {
                    Content = result,
                    Reasoning = null
                };
            }
        }

        private async Task<StreamingResponse> TryStreamingRequest(string apiKey, string model, IList<AiMessage> messages, Action<string> onToken)
        {
            // For streaming, always use chat/completions endpoint
            var url = "https://api.openai.com/v1/chat/completions";

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
                throw new Exception($"OpenAI API error {(int)response.StatusCode}: {error}");
            }

            var fullContent = new StringBuilder();
            var reasoning = new StringBuilder();

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
                        if (choice.TryGetProperty("delta", out var delta))
                        {
                            if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                            {
                                var token = content.GetString() ?? "";
                                if (!string.IsNullOrEmpty(token))
                                {
                                    fullContent.Append(token);
                                    onToken(token);
                                }
                            }
                            
                            // Check for reasoning tokens (GPT-5 thinking models)
                            if (delta.TryGetProperty("reasoning", out var reasoningEl) && reasoningEl.ValueKind == JsonValueKind.String)
                            {
                                var reasoningToken = reasoningEl.GetString() ?? "";
                                if (!string.IsNullOrEmpty(reasoningToken))
                                {
                                    reasoning.Append(reasoningToken);
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
                Reasoning = reasoning.Length > 0 ? reasoning.ToString() : null
            };
        }
    }
}


