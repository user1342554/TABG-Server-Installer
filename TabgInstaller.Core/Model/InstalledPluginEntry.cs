using System;
using Newtonsoft.Json;

namespace TabgInstaller.Core.Model
{
    /// <summary>
    /// Tracks a single community plugin installed on a server instance.
    /// </summary>
    public class InstalledPluginEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("installedVersion")]
        public string InstalledVersion { get; set; } = "";

        [JsonProperty("installedAt")]
        public string InstalledAt { get; set; } = "";

        [JsonProperty("updatedAt")]
        public string? UpdatedAt { get; set; }

        [JsonProperty("pinned")]
        public bool Pinned { get; set; }

        [JsonProperty("dllNames")]
        public string[] DllNames { get; set; } = Array.Empty<string>();
    }
}
