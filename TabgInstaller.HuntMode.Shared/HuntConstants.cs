namespace TabgInstaller.HuntMode.Shared
{
    public static class HuntConstants
    {
        public const float MatchDuration = 480f;
        public const int RequiredPlayers = 5;
        public const int MinPlayers = 2;
        public const float LobbyTimeout = 120f;
        public const float PostEscapeGracePeriod = 10f;

        public const float KillerBaseHP = 300f;
        public const float KillerBaseSpeed = 1.2f;
        public const float KillerIronhideHP = 400f;
        public const float KillerIronhideSpeed = 1.1f;
        public const float KillerCrate5Speed = 1.0f;
        public const float KillerFallDamageMultiplier = 0.5f;
        public const int KillerBaseAmmo = 12;
        public const int KillerAmmoPerDown = 4;
        public const int KillerReloadPerkAmmo = 18;
        public const int KillerReloadPerkAmmoPerDown = 6;

        public const float SurvivorBaseHP = 100f;
        public const float SurvivorBaseSpeed = 1.0f;

        public const int DefaultMaxDowns = 3;
        public const int ToughMaxDowns = 4;
        public const float BleedoutTime = 45f;
        public const float BloodhoundBleedoutTime = 22.5f;

        public static readonly float[] ReviveTimes = { 5f, 8f, 10f };
        public static readonly float[] ReviveHP = { 100f, 50f, 30f };
        public const float MedicReviveMultiplier = 0.6f;

        public const int TotalCrates = 5;
        public const float BaseLootTime = 3f;
        public const float SaboteurLootMultiplier = 0.6f;
        public const float LocksmithLootMultiplier = 1.5f;
        public const float CrateHealAmount = 50f;
        public const int CrateUnlockVehicle = 4;
        public const float EscapeDistancePastRing = 15f;

        public const float TrackerInterval = 30f;
        public const float TrackerPingDuration = 3f;
        public const float ScoutDetectionRange = 30f;
        public const float SprinterSpeedMultiplier = 1.15f;
        public const float SprinterDuration = 5f;
        public const float SprinterCooldown = 20f;

        public const int SmokeGrenadeItemId = 208;
    }
}
