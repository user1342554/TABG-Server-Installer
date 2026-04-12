using System.Collections.Generic;
using Newtonsoft.Json;

namespace TabgInstaller.Core.Model
{
    /// <summary>
    /// Root model for installed-plugins.json stored per server instance.
    /// </summary>
    public class InstalledPluginsData
    {
        [JsonProperty("plugins")]
        public List<InstalledPluginEntry> Plugins { get; set; } = new();
    }
}
