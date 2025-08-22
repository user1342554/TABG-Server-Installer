using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services.AI
{
    public static class SseReader
    {
        public static async Task ReadStreamAsync(HttpResponseMessage response, Action<string> onEvent)
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            var eventBuilder = new StringBuilder();

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrEmpty(line))
                {
                    // End of event
                    if (eventBuilder.Length > 0)
                    {
                        onEvent(eventBuilder.ToString());
                        eventBuilder.Clear();
                    }
                }
                else
                {
                    eventBuilder.AppendLine(line);
                }
            }

            // Handle final event if no trailing newline
            if (eventBuilder.Length > 0)
            {
                onEvent(eventBuilder.ToString());
            }
        }

        public static string GetDataFromEvent(string eventText)
        {
            var lines = eventText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("data: "))
                {
                    return line.Substring(6); // Remove "data: " prefix
                }
            }
            return string.Empty;
        }
    }
}
