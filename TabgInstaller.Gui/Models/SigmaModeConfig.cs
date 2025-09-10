using System.Text.Json.Serialization;

namespace TabgInstaller.Gui.Models
{
    public class SigmaModeConfig
    {
        [JsonPropertyName("musicPath")]
        public string MusicPath { get; set; } = @"C:\SigmaMode\walk_in_the_cold_slowed.mp3";
        
        [JsonPropertyName("timeoutSeconds")]
        public int TimeoutSeconds { get; set; } = 180;
        
        [JsonPropertyName("fadeOutDurationMs")]
        public int FadeOutDurationMs { get; set; } = 300;
        
        [JsonPropertyName("steamAppId")]
        public int SteamAppId { get; set; } = 823130; // TABG
        
        [JsonPropertyName("enableFanControl")]
        public bool EnableFanControl { get; set; } = true;
        
        [JsonPropertyName("musicVolume")]
        public float MusicVolume { get; set; } = 0.8f;
        
        [JsonPropertyName("tabgProcessName")]
        public string TabgProcessName { get; set; } = "TABG";
        
        [JsonPropertyName("tabgWindowTitle")]
        public string TabgWindowTitle { get; set; } = "Totally Accurate Battlegrounds";
    }
}
