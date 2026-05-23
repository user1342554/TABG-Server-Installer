namespace TabgInstaller.Core.Model
{
    /// <summary>MatchCore commission-style match settings.</summary>
    public class FreddoCommissionSettings
    {
        public string BanList { get; set; } = "";
        public string LoadoutCurses { get; set; } = "";
        public string LoadoutBlessings { get; set; } = "";

        // Grenades on death - attacker gets
        public bool GrenadeAttackerEnabled { get; set; } = false;
        public float GrenadeAttackerChance { get; set; } = 1.0f;
        public int GrenadeAttackerId { get; set; } = 198; // Healing Grenade

        // Grenades on death - corpse drops
        public bool GrenadeCorpseEnabled { get; set; } = false;
        public float GrenadeCorpseChance { get; set; } = 0.2f;
        public int GrenadeCorpseId { get; set; } = 186; // Big Weird Grenade

        public float StreamingDistance { get; set; } = -1f; // -1 = normal
        public int Lives { get; set; } = 256; // 256 = infinite
    }

    /// <summary>MatchCore loot-drop compatibility settings.</summary>
    public class StarterPackFixesSettings
    {
        public bool EnableLootDrops { get; set; } = false;
    }

    /// <summary>ServerLogger player identity logging settings.</summary>
    public class ServerLoggerSettings
    {
        public bool LogToBepInExConsole { get; set; } = true;
        public bool WriteCsv { get; set; } = true;
        public bool WriteLegacyServerLoggerTxt { get; set; } = true;
        public bool FallbackPlayerScan { get; set; } = true;
        public float FallbackScanIntervalSeconds { get; set; } = 2f;
        public string LogDirectory { get; set; } = "server-logs";
        public string CsvFileName { get; set; } = "players.csv";
        public string LegacyFileName { get; set; } = "ServerLogger.txt";
    }
}
