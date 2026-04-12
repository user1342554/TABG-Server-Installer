using System.Collections.Generic;
using Newtonsoft.Json;

namespace TabgInstaller.Core.Model
{
    /// <summary>
    /// Root model for the auto-generated registry.json fetched from GitHub.
    /// </summary>
    public class PluginRegistryResponse
    {
        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("generatedAt")]
        public string GeneratedAt { get; set; } = "";

        [JsonProperty("plugins")]
        public List<PluginManifest> Plugins { get; set; } = new();
    }
}
