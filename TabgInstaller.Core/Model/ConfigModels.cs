using System.Collections.Generic;

namespace TabgInstaller.Core.Model
{
    // Neuer Typ für Gegenstände im Starter-Pack
    public record Item(string ItemId, int Amount);

    // Neuer Typ für Loadouts
    public record Loadout(string Name, IReadOnlyList<Item> Items);

    public record TheStarterPackConfig
    {
        public string ServerName { get; init; } = "My TABG Server";
        public string Password { get; init; } = "password";
        public int MaxPlayers { get; init; } = 20;
        public string GameMode { get; init; } = "BattleRoyale";
        public string Motd { get; init; } = "Welcome!";
        public IReadOnlyList<Item> ItemsGiven { get; init; } = new List<Item>();
        public IReadOnlyList<Loadout> Loadouts { get; init; } = new List<Loadout>();
        public bool AllowRandomDrops { get; init; } = true;
        public int WarmupTimeSeconds { get; init; } = 30;
        public float CircleSpeed { get; init; } = 1.0f;
        public int TickRate { get; init; } = 60;
        public bool DisableSound { get; init; } = false;
        public int StartingLevel { get; init; } = 1;
    }

    // GameSettingsData, ExtraSettingsConfig, PlayerPermsConfig remain as they were unless user specified changes.
    // The user's diff only covered TheStarterPackConfig and related Item/Loadout.
    // Re-adding them from the previous state if they were not intended to be removed.

    public class GameSettingsData
    {
        // string
        public string ServerName { get; set; } = "enormous";
        public string ServerDescription { get; set; } = "enormous";
        public string ServerBrowserIP { get; set; } = string.Empty;
        public string Password { get; set; } = "enormous";
        // ints
        public int Port { get; set; } = 7777;
        public int MaxPlayers { get; set; } = 70;
        public int GroupsToStart { get; set; } = 0;
        public int MinPlayersToForceStart { get; set; } = 1;
        public int PlayersToStart { get; set; } = 1;
        public int KillsToWin { get; set; } = 0;
        public int RoundsToWin { get; set; } = 0;
        public int NumberOfLivesPerTeam { get; set; } = 0;
        public int MaxNumberOfTeamsAuto { get; set; } = 0;
        public int SpawnBots { get; set; } = 0;
        // ushorts treated as int for simplicity
        public int BombTime { get; set; } = 0;
        public int RoundTime { get; set; } = 0;
        // floats
        public float CarSpawnRate { get; set; } = 1f;
        public float ForceStartTime { get; set; } = 200f;
        public float Countdown { get; set; } = 20f;
        public float BaseRingTime { get; set; } = 60f;
        public float TimeBeforeFirstRing { get; set; } = 0f;
        public float StripLootByPercentage { get; set; } = 0f;
        // float arrays (store as string csv in file, we parse manually)
        public string RingSizes { get; set; } = "4240.0,500.0,400.0,200.0,100.0,50.0";
        public string RingSpeeds { get; set; } = "120,120,150,180,240,300";
        // bools
        public bool Relay { get; set; } = true;
        public bool AutoTeam { get; set; } = false;
        public bool AllowRespawnMinigame { get; set; } = true;
        public bool UseTimedForceStart { get; set; } = true;
        public bool AntiCheat { get; set; } = false;
        public bool LAN { get; set; } = false;
        public bool AllowSpectating { get; set; } = false;
        public bool SpawnBotsBool { get; set; } = false; // maybe not used
        public bool UsePlayFabStats { get; set; } = false;
        public bool DebugDeathmatch { get; set; } = false;
        public bool NoRing { get; set; } = false;
        public bool UseSouls { get; set; } = false;
        public bool UseKicks { get; set; } = false;
        public bool AntiCheatDebugLogging { get; set; } = false;
        public bool AntiCheatEventLogging { get; set; } = false;
        public bool AllowRejoins { get; set; } = false;
        // enums stored as string
        public string TeamMode { get; set; } = "SQUAD";
        public string GameMode { get; set; } = "BattleRoyale";
    }

    public record ExtraSettingsConfig
    {
        // Empty as per previous state, assuming CitrusLib handles defaults
    }

    public record PlayerPermsConfig
    {
        // Empty as per previous state, assuming CitrusLib handles defaults
        public List<object> Permissions { get; init; } = new List<object>(); // Example, adjust if known structure
    }
} 