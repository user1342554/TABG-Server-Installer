namespace TabgInstaller.Core.Model
{
    /// <summary>
    /// Represents all settings from TheStarterPack.txt (key=value text format).
    /// This is distinct from TheStarterPackConfig which is the JSON format.
    /// </summary>
    public class StarterPackTextSettings
    {
        // Match settings
        public string WinCondition { get; set; } = "Default"; // Default, KillsToWin, Debug, Endless
        public int? KillsToWin { get; set; }
        public bool ForceKillAtStart { get; set; } = false;

        // Drop settings
        public bool DropItemsOnDeath { get; set; } = false;
        public string ItemsGiven { get; set; } = "";

        // Loadouts - raw string including optional GunGame/ReverseGunGame/KeepInventory prefix
        public string Loadouts { get; set; } = "";

        // Player settings
        public bool HealOnKill { get; set; } = false;
        public float HealOnKillAmount { get; set; } = 20f;
        public bool CanGoDown { get; set; } = false;
        public bool CanLockOut { get; set; } = false;

        // Spawn settings
        public string ValidSpawnPoints { get; set; } = "2";
        public string CustomSpawnPoint { get; set; } = "";

        // Vote settings
        public int PercentOfVotes { get; set; } = 50;
        public int MinNumberOfPlayers { get; set; } = 2;
        public int TimeToStart { get; set; } = 30;

        // Spell drop settings
        public bool SpelldropEnabled { get; set; } = true;
        public int MinSpellDropDelay { get; set; } = 180;
        public int MaxSpellDropDelay { get; set; } = 420;
        public int SpellDropOffset { get; set; } = 30;

        // Match timeout
        public float PreMatchTimeout { get; set; } = 15f;
        public float PeriMatchTimeout { get; set; } = 15f;

        // Ring settings (plugin format - currently disabled but stored)
        public string RingSettings { get; set; } = "";

        /// <summary>Extracts the loadout mode prefix if present.</summary>
        public string GetLoadoutMode()
        {
            if (Loadouts.StartsWith("GunGame/")) return "GunGame";
            if (Loadouts.StartsWith("ReverseGunGame/")) return "ReverseGunGame";
            if (Loadouts.StartsWith("KeepInventory/")) return "KeepInventory";
            return "Normal";
        }

        /// <summary>Gets the loadout string without the mode prefix.</summary>
        public string GetLoadoutsWithoutPrefix()
        {
            var mode = GetLoadoutMode();
            if (mode == "Normal") return Loadouts;
            return Loadouts.Substring(mode.Length + 1); // +1 for the /
        }

        /// <summary>Sets the loadout string with the given mode prefix.</summary>
        public void SetLoadoutsWithPrefix(string mode, string loadouts)
        {
            if (mode == "Normal" || string.IsNullOrEmpty(mode))
                Loadouts = loadouts;
            else
                Loadouts = mode + "/" + loadouts;
        }
    }
}
