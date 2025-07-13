using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace TabgInstaller.Core.Model
{
    public record GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = default!;

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; init; } = new();
    }

    public record GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = default!;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; init; } = default!;
    }
} 