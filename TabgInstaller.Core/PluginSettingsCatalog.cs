using System;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core
{
    public static class PluginSettingsCatalog
    {
        public static readonly PluginConfigDefinition[] AdditionalServerPlugins =
        {
            Config(
                "AdminRadar",
                "Admin Radar Server",
                "Server-authorized radar broadcast settings.",
                PluginSettingScope.Server,
                "tabginstaller.adminradar.server.cfg",
                Setting("Radar", "Enabled", "Enabled", "Broadcast server-authorized radar positions.", PluginSettingValueType.Boolean, "true"),
                Setting("Radar", "BroadcastIntervalSeconds", "Broadcast interval (s)", "How often to send radar updates.", PluginSettingValueType.Single, "0.5"),
                Setting("Radar", "Recipients", "Recipients", "Comma-separated player indexes that receive radar, or * for everyone.", PluginSettingValueType.String, "*"),
                Setting("Radar", "IncludeDeadPlayers", "Include dead players", "Include dead players in radar updates.", PluginSettingValueType.Boolean, "false")),

            Config(
                "HuntMode",
                "Hunt Mode",
                "Server-side 4v1 Hunt Mode match tuning.",
                PluginSettingScope.Server,
                "tabginstaller.huntmode.cfg",
                Setting("HuntMode", "Enabled", "Enabled", "Enable Hunt Mode (4v1).", PluginSettingValueType.Boolean, "true"),
                Setting("HuntMode", "MatchDuration", "Match duration (s)", "Match duration in seconds.", PluginSettingValueType.Single, "480"),
                Setting("HuntMode", "ZoneRadius", "Zone radius", "Play zone radius.", PluginSettingValueType.Single, "200"),
                Setting("HuntMode", "ZoneCenterX", "Zone center X", "Zone center X coordinate.", PluginSettingValueType.Single, "-191"),
                Setting("HuntMode", "ZoneCenterY", "Zone center Y", "Zone center Y coordinate.", PluginSettingValueType.Single, "132"),
                Setting("HuntMode", "ZoneCenterZ", "Zone center Z", "Zone center Z coordinate.", PluginSettingValueType.Single, "-205"),
                Setting("HuntMode", "VehicleSpawnX", "Vehicle spawn X", "Escape vehicle spawn X coordinate.", PluginSettingValueType.Single, "-100"),
                Setting("HuntMode", "VehicleSpawnY", "Vehicle spawn Y", "Escape vehicle spawn Y coordinate.", PluginSettingValueType.Single, "132"),
                Setting("HuntMode", "VehicleSpawnZ", "Vehicle spawn Z", "Escape vehicle spawn Z coordinate.", PluginSettingValueType.Single, "-100")),

            Config(
                "BigSmoke",
                "Big Smoke Grenade",
                "Giant purple smoke grenade behavior.",
                PluginSettingScope.Server,
                "tabginstaller.customgrenades.cfg",
                Setting("BigSmoke", "Enabled", "Enabled", "Enable giant purple smoke behavior for smoke grenades.", PluginSettingValueType.Boolean, "true"),
                Setting("BigSmoke", "SmokeSizeMultiplier", "Smoke size multiplier", "Particle size multiplier for Big Smoke grenades.", PluginSettingValueType.Single, "8"),
                Setting("BigSmoke", "SmokeLifetimeMultiplier", "Smoke lifetime multiplier", "Particle lifetime multiplier for Big Smoke grenades.", PluginSettingValueType.Single, "3"),
                Setting("BigSmoke", "SmokeEmissionMultiplier", "Smoke emission multiplier", "Particle emission multiplier for Big Smoke grenades.", PluginSettingValueType.Single, "3"),
                Setting("BigSmoke", "GrenadeScaleMultiplier", "Grenade scale multiplier", "GameObject scale multiplier for Big Smoke grenades.", PluginSettingValueType.Single, "4")),

            Config(
                "MGLFlashbang",
                "MGL Flashbang",
                "MGL explosion flashbang behavior.",
                PluginSettingScope.Server,
                "tabginstaller.mglflashbang.cfg",
                Setting("Flashbang", "Enabled", "Enabled", "Enable flashbang behavior for MGL explosions.", PluginSettingValueType.Boolean, "true"),
                Setting("Flashbang", "TriggerWindowSeconds", "Trigger window (s)", "Seconds after firing an MGL where the next explosion can flash.", PluginSettingValueType.Single, "5"),
                Setting("Flashbang", "RadiusMultiplier", "Radius multiplier", "Explosion radius multiplier used for flash range.", PluginSettingValueType.Single, "2"),
                Setting("Flashbang", "BlindIntensity", "Blind intensity", "Maximum visual effect intensity.", PluginSettingValueType.Single, "60"),
                Setting("Flashbang", "BlindDuration", "Blind duration", "Maximum visual effect duration.", PluginSettingValueType.Single, "60")),

            Config(
                "UnusedVehicles",
                "Unused Vehicles",
                "Hidden TABG vehicle spawning and commands.",
                PluginSettingScope.Server,
                "tabginstaller.unusedvehicles.cfg",
                Setting("Spawning", "SpawnChance", "Spawn chance", "Chance to add an unused vehicle near each normal vehicle spawn.", PluginSettingValueType.Single, "0.2"),
                Setting("Spawning", "MaxSpawns", "Max spawns", "Maximum unused vehicles to add per match.", PluginSettingValueType.Int32, "15"),
                Setting("Commands", "EnableCommands", "Enable commands", "Register /spawn, /vehicles, and vehicle help commands.", PluginSettingValueType.Boolean, "true")),

            Config(
                "SoloTesting",
                "Solo Testing",
                "Local testing helpers for one-player matches.",
                PluginSettingScope.Server,
                "tabginstaller.solotesting.cfg",
                Setting("SoloTesting", "Enabled", "Enabled", "Enable solo-testing game-state patches.", PluginSettingValueType.Boolean, "true"),
                Setting("SoloTesting", "MinimumPlayersToStart", "Minimum players to start", "Minimum players required to start countdown.", PluginSettingValueType.Int32, "1"),
                Setting("SoloTesting", "PreventSoloWinWhenAlone", "Prevent solo win", "Prevent immediate win checks when only one player is present.", PluginSettingValueType.Boolean, "true")),

            Config(
                "FakePlayers",
                "Fake Players",
                "Dummy player command limits and permissions.",
                PluginSettingScope.Server,
                "tabginstaller.fakeplayers.cfg",
                Setting("Commands", "MaxFakeSpawnCount", "Max fake spawn count", "Maximum fake players spawned by one /spawndummy command.", PluginSettingValueType.Int32, "200"),
                Setting("Commands", "MaxAiSpawnCount", "Max AI spawn count", "Maximum AI dummy players spawned by one /spawnaidummy command.", PluginSettingValueType.Int32, "32"),
                Setting("Commands", "CommandsUsableByEveryone", "Commands usable by everyone", "Bypass Citrus permissions for FakePlayers commands.", PluginSettingValueType.Boolean, "true")),

            NoSettings("Citruslib", "Citruslib", "Core server modding API dependency.", PluginSettingScope.Server),
        };

        public static readonly PluginConfigDefinition[] ClientPlugins =
        {
            Config(
                "FlyingControls",
                "Flying Controls",
                "Client controls and physics for custom flying vehicles.",
                PluginSettingScope.Client,
                "tabginstaller.flyingcontrols.cfg",
                Setting("Keybinds", "Ascend", "Ascend key", "Key to fly upward.", PluginSettingValueType.KeyCode, "Space"),
                Setting("Keybinds", "Descend", "Descend key", "Key to fly downward.", PluginSettingValueType.KeyCode, "LeftControl"),
                Setting("Physics", "ThrustForce", "Thrust force", "Forward/backward thrust force.", PluginSettingValueType.Single, "22"),
                Setting("Physics", "TurnForce", "Turn force", "Turning yaw force.", PluginSettingValueType.Single, "10"),
                Setting("Physics", "LiftForce", "Lift force", "Upward force when pressing ascend.", PluginSettingValueType.Single, "22"),
                Setting("Physics", "HoverForce", "Hover force", "Base hover force that counteracts gravity.", PluginSettingValueType.Single, "11"),
                Setting("Physics", "DescentForce", "Descent force", "Downward force when pressing descend.", PluginSettingValueType.Single, "8"),
                Setting("Physics", "MaxSpeed", "Max speed", "Maximum flight speed.", PluginSettingValueType.Single, "45"),
                Setting("Physics", "Stabilization", "Stabilization", "Auto-leveling strength.", PluginSettingValueType.Single, "8")),

            Config(
                "CoordsDisplay",
                "Coords Display",
                "Client coordinate overlay settings.",
                PluginSettingScope.Client,
                "tabginstaller.coordsdisplay.cfg",
                Setting("Keybinds", "ToggleCoords", "Toggle key", "Key to toggle coordinate display.", PluginSettingValueType.KeyCode, "F5"),
                Setting("Display", "FontSize", "Font size", "Font size for coordinate text.", PluginSettingValueType.Int32, "18")),

            Config(
                "ModSettings",
                "Mod Settings",
                "In-game mod settings menu keybind.",
                PluginSettingScope.Client,
                "tabginstaller.modsettings.cfg",
                Setting("Menu", "OpenKey", "Open menu key", "Key to open or close the in-game settings menu.", PluginSettingValueType.KeyCode, "F9")),

            Config(
                "EnhancedClient",
                "Enhanced Client",
                "Client LOD, item draw distance, haze, and HUD controls.",
                PluginSettingScope.Client,
                "tabginstaller.enhancedclient.cfg",
                Setting("Keybinds", "ToggleLodUnlock", "Toggle LOD key", "Toggle full map/object LOD loading.", PluginSettingValueType.KeyCode, "F1"),
                Setting("Keybinds", "ToggleUi", "Toggle UI key", "Toggle in-game UI visibility.", PluginSettingValueType.KeyCode, "F2"),
                Setting("Keybinds", "ToggleHaze", "Toggle haze key", "Toggle atmospheric haze.", PluginSettingValueType.KeyCode, "F3"),
                Setting("Visuals", "ItemDrawDistance", "Item draw distance", "Pickup/item draw distance in meters.", PluginSettingValueType.Single, "2147483647"),
                Setting("Visuals", "StartWithLodUnlocked", "Start LOD unlocked", "Load all map/object chunks when the client camera is ready.", PluginSettingValueType.Boolean, "false"),
                Setting("Visuals", "StartWithHazeDisabled", "Start haze disabled", "Disable haze when the client camera is ready.", PluginSettingValueType.Boolean, "false"),
                Setting("Visuals", "BlockChunkUnloadsWhenUnlocked", "Block chunk unloads", "Prevent streamed chunks from unloading while LOD unlock is enabled.", PluginSettingValueType.Boolean, "true")),

            Config(
                "PopupBlocker",
                "Popup Blocker",
                "Client anti-cheat popup suppression settings.",
                PluginSettingScope.Client,
                "tabginstaller.popupblocker.cfg",
                Setting("Popups", "BlockAntiCheatPopups", "Block anti-cheat popups", "Suppress anti-cheat boot/fail message boxes.", PluginSettingValueType.Boolean, "true"),
                Setting("AntiCheat", "SkipSessionWhenUnavailable", "Skip missing EAC session", "Avoid null anti-cheat session calls when the modded client starts without EAC.", PluginSettingValueType.Boolean, "true"),
                Setting("Diagnostics", "LogBlockedMessages", "Log blocked messages", "Write blocked popup messages to the BepInEx log.", PluginSettingValueType.Boolean, "false")),

            Config(
                "ProximityChatClient",
                "Proximity Chat Client",
                "Client voice capture and playback settings.",
                PluginSettingScope.Client,
                "tabginstaller.proximitychat.client.cfg",
                Setting("ProximityChat", "Enabled", "Enabled", "Enable or disable voice chat.", PluginSettingValueType.Boolean, "true"),
                Setting("ProximityChat", "MicSensitivity", "Mic sensitivity", "Voice activity detection threshold (RMS).", PluginSettingValueType.Single, "0.01"),
                Setting("ProximityChat", "MasterVolume", "Master volume", "Overall voice chat volume.", PluginSettingValueType.Single, "1"),
                Setting("ProximityChat", "MicrophoneDevice", "Microphone device", "Microphone device name. Empty uses the system default.", PluginSettingValueType.String, "")),

            Config(
                "AdminRadarClient",
                "Admin Radar Client",
                "Client radar overlay and dummy marker settings.",
                PluginSettingScope.Client,
                "tabginstaller.adminradar.client.cfg",
                Setting("Radar", "ToggleKey", "Toggle key", "Key to show or hide the server-authorized radar.", PluginSettingValueType.KeyCode, "F6"),
                Setting("Radar", "Visible", "Visible", "Show radar overlay.", PluginSettingValueType.Boolean, "true"),
                Setting("Radar", "RangeMeters", "Range (m)", "World range covered by the radar.", PluginSettingValueType.Single, "350"),
                Setting("Radar", "SizePixels", "Size (px)", "Radar size in pixels.", PluginSettingValueType.Int32, "220"),
                Setting("Radar", "ShowNames", "Show names", "Show player names next to radar markers.", PluginSettingValueType.Boolean, "true"),
                Setting("Dummy Highlighter", "ShowWorldMarkers", "Show world markers", "Show screen-space labels over dummy players.", PluginSettingValueType.Boolean, "true"),
                Setting("Dummy Highlighter", "OnlyDummies", "Only dummies", "Only draw world markers for AIPlayer dummy names.", PluginSettingValueType.Boolean, "true"),
                Setting("Dummy Highlighter", "MaxDistanceMeters", "Max marker distance (m)", "Maximum distance for dummy world markers.", PluginSettingValueType.Single, "2500")),

            Config(
                "CustomGrenades",
                "Big Smoke Client",
                "Client-side Big Smoke visual behavior.",
                PluginSettingScope.Client,
                "tabginstaller.customgrenades.cfg",
                Setting("BigSmoke", "Enabled", "Enabled", "Enable giant purple smoke behavior for smoke grenades.", PluginSettingValueType.Boolean, "true"),
                Setting("BigSmoke", "SmokeSizeMultiplier", "Smoke size multiplier", "Particle size multiplier for Big Smoke grenades.", PluginSettingValueType.Single, "8"),
                Setting("BigSmoke", "SmokeLifetimeMultiplier", "Smoke lifetime multiplier", "Particle lifetime multiplier for Big Smoke grenades.", PluginSettingValueType.Single, "3"),
                Setting("BigSmoke", "SmokeEmissionMultiplier", "Smoke emission multiplier", "Particle emission multiplier for Big Smoke grenades.", PluginSettingValueType.Single, "3"),
                Setting("BigSmoke", "GrenadeScaleMultiplier", "Grenade scale multiplier", "GameObject scale multiplier for Big Smoke grenades.", PluginSettingValueType.Single, "4")),

            Config(
                "MGLFlashbangClient",
                "MGL Flashbang Client",
                "Client-side MGL flashbang visual behavior.",
                PluginSettingScope.Client,
                "tabginstaller.mglflashbang.cfg",
                Setting("Flashbang", "Enabled", "Enabled", "Enable flashbang behavior for MGL explosions.", PluginSettingValueType.Boolean, "true"),
                Setting("Flashbang", "TriggerWindowSeconds", "Trigger window (s)", "Seconds after firing an MGL where the next explosion can flash.", PluginSettingValueType.Single, "5"),
                Setting("Flashbang", "RadiusMultiplier", "Radius multiplier", "Explosion radius multiplier used for flash range.", PluginSettingValueType.Single, "2"),
                Setting("Flashbang", "BlindIntensity", "Blind intensity", "Maximum visual effect intensity.", PluginSettingValueType.Single, "60"),
                Setting("Flashbang", "BlindDuration", "Blind duration", "Maximum visual effect duration.", PluginSettingValueType.Single, "60")),

            NoSettings("HuntModeClient", "Hunt Mode Client", "HUD-only companion for Hunt Mode.", PluginSettingScope.Client),
            NoSettings("JuggernautClient", "Juggernaut Client", "External bundled client companion with no installer-exposed settings.", PluginSettingScope.Client),
        };

        private static PluginConfigDefinition Config(
            string id,
            string displayName,
            string description,
            PluginSettingScope scope,
            string configFileName,
            params PluginSettingDefinition[] settings)
        {
            return new PluginConfigDefinition
            {
                Id = id,
                DisplayName = displayName,
                Description = description,
                Scope = scope,
                ConfigFileName = configFileName,
                Settings = settings
            };
        }

        private static PluginConfigDefinition NoSettings(
            string id,
            string displayName,
            string description,
            PluginSettingScope scope)
        {
            return new PluginConfigDefinition
            {
                Id = id,
                DisplayName = displayName,
                Description = description,
                Scope = scope
            };
        }

        private static PluginSettingDefinition Setting(
            string section,
            string key,
            string label,
            string description,
            PluginSettingValueType valueType,
            string defaultValue,
            string[]? options = null,
            bool isMultiline = false)
        {
            return new PluginSettingDefinition
            {
                Section = section,
                Key = key,
                Label = label,
                Description = description,
                ValueType = valueType,
                DefaultValue = defaultValue,
                Options = options ?? Array.Empty<string>(),
                IsMultiline = isMultiline
            };
        }
    }
}
