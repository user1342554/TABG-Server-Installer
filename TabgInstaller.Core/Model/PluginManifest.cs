using System;
using Newtonsoft.Json;

namespace TabgInstaller.Core.Model
{
    /// <summary>
    /// Represents a community plugin manifest from the marketplace registry.
    /// </summary>
    public class PluginManifest
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("version")]
        public string Version { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("author")]
        public string Author { get; set; } = "";

        [JsonProperty("authorUrl")]
        public string? AuthorUrl { get; set; }

        [JsonProperty("repositoryUrl")]
        public string? RepositoryUrl { get; set; }

        [JsonProperty("downloadUrl")]
        public string DownloadUrl { get; set; } = "";

        [JsonProperty("dllNames")]
        public string[] DllNames { get; set; } = Array.Empty<string>();

        /// <summary>One of "server", "client", or "both".</summary>
        [JsonProperty("type")]
        public string Type { get; set; } = "server";

        [JsonProperty("compatibleTabgVersions")]
        public string[] CompatibleTabgVersions { get; set; } = new[] { "*" };

        [JsonProperty("minInstallerVersion")]
        public string MinInstallerVersion { get; set; } = "";

        [JsonProperty("bepInExVersion")]
        public string BepInExVersion { get; set; } = "";

        [JsonProperty("dependencies")]
        public string[] Dependencies { get; set; } = Array.Empty<string>();

        [JsonProperty("tags")]
        public string[] Tags { get; set; } = Array.Empty<string>();

        [JsonProperty("iconUrl")]
        public string? IconUrl { get; set; }

        [JsonProperty("requiresClientMod")]
        public bool RequiresClientMod { get; set; }

        [JsonProperty("clientPluginId")]
        public string? ClientPluginId { get; set; }

        [JsonProperty("changelog")]
        public string? Changelog { get; set; }
    }
}
