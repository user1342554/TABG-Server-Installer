using System.Collections.Generic;
using UnityEngine;

namespace TabgInstaller.FakePlayers
{
    internal enum WeaponCombatClass
    {
        Unarmed,
        Shotgun,
        Smg,
        AssaultRifle,
        Lmg,
        Sniper,
        AutoSniper,
        Pistol,
        Launcher,
        Special
    }

    internal enum FirePlan
    {
        Semi,
        Burst,
        FullAuto
    }

    internal struct WeaponProfile
    {
        public readonly WeaponCombatClass CombatClass;
        public readonly FirePlan FirePlan;
        public readonly float MinRange;
        public readonly float PreferredRange;
        public readonly float MaxRange;
        public readonly float BaseDamage;
        public readonly float FireInterval;
        public readonly int BurstShots;
        public readonly int MagazineSize;
        public readonly float ReloadSeconds;
        public readonly float CloseHitChance;
        public readonly float FarHitChance;

        public WeaponProfile(
            WeaponCombatClass combatClass,
            FirePlan firePlan,
            float minRange,
            float preferredRange,
            float maxRange,
            float baseDamage,
            float fireInterval,
            int burstShots,
            int magazineSize,
            float reloadSeconds,
            float closeHitChance,
            float farHitChance)
        {
            CombatClass = combatClass;
            FirePlan = firePlan;
            MinRange = minRange;
            PreferredRange = preferredRange;
            MaxRange = maxRange;
            BaseDamage = baseDamage;
            FireInterval = fireInterval;
            BurstShots = burstShots;
            MagazineSize = magazineSize;
            ReloadSeconds = reloadSeconds;
            CloseHitChance = closeHitChance;
            FarHitChance = farHitChance;
        }
    }

    internal static class AiDummyCatalog
    {
        public const float MoveSpeed = 2.2f;
        public const float CombatMoveSpeed = 2.55f;
        public const float ChaseRange = 180f;
        public const float LootSearchRange = 260f;
        public const float WeaponlessLootSearchRange = 700f;
        public const float PickupRange = 5.2f;
        public const float ShootRange = 30f;
        public const float PreferredFightRange = 16f;
        public const float MinFightRange = 6f;
        public const float UnarmedDangerRange = 42f;
        public const float UnarmedEvadeDistance = 34f;
        public const float DamagePerShot = 5.5f;
        public const float AutoDamagePerBullet = 2.4f;
        public const float AutoBulletInterval = 0.11f;
        public const float LowHealthRetreatThreshold = 34f;
        public const float CriticalHealthRetreatThreshold = 22f;
        public const float CoverRefreshInterval = 1.15f;
        public const float FireAnimationWindow = 0.2f;
        public const float ShotDamageDelay = 0.07f;
        public const float ShotTraceRadius = 0.08f;
        public const float TargetStickinessSeconds = 1.8f;
        public const float MuzzleForwardOffset = 0.45f;
        public const float PoiArriveDistance = 18f;
        public const float BadTerrainRepathPenalty = 65f;
        public const bool EnableGunDamage = true;
        public const bool EnableGrenadeThrows = false;
        public const bool EnableGrenadeDamage = false;
        public const float WarmupTime = 4f;
        public const float WallProbeDistance = 3.0f;
        public const float WallProbeRadius = 0.55f;
        public const float NetworkSendInterval = 0.08f;
        public const float TerrainProbeUp = 8f;
        public const float TerrainProbeDown = 320f;
        public const float MaxVerticalSpeed = 7.5f;
        public const float StuckCheckInterval = 0.6f;
        public const float StuckSeconds = 2.2f;
        public const float LastSeenMemory = 8f;
        public const float ThreatMemorySeconds = 13f;
        public const float GunshotHearRange = 155f;
        public const float MovementHearRange = 46f;
        public const float MovementHearSpeed = 5.2f;
        public const float SoundThreatMemorySeconds = 8f;
        public const float LootThreatSuppressionSeconds = 4.2f;
        public const float UnarmedPanicSeconds = 2.6f;
        public const float SearchRepathInterval = 0.95f;
        public const float SearchSweepRadius = 19f;
        public const float PathRebuildInterval = 0.45f;
        public const float PathDestinationRebuildDistance = 7f;
        public const float PathCornerReachDistance = 2.6f;
        public const float NavMeshSampleDistance = 8f;
        public const float VehicleSearchRange = 90f;
        public const float VehicleEnterRange = 6.5f;
        public const float VehicleUseMinTargetDistance = 80f;
        public const float VehicleMoveSpeed = 7.5f;
        public const float GrenadeThrowRange = 28f;
        public const float GrenadeSplashRadius = 8f;
        public const float GrenadeFuseTime = 2.35f;
        public const float DropStartHeight = 215f;
        public const float DropHorizontalSpeed = 42f;
        public const float DropVerticalSpeed = 33f;
        public const float DropFinishDistance = 5f;
        public const float DropExitDistance = 230f;
        public const float DropLandingSafetyLift = 3.0f;
        public const float DropPositionLockSeconds = 3.0f;
        public const int AiGrenadeItemId = 208;

        public static readonly Vector3[] DropTargets =
        {
            new Vector3(-313f, 170f, -530f), new Vector3(-465f, 175f, -481f),
            new Vector3(-169f, 125f, 159f), new Vector3(-573f, 130f, 301f),
            new Vector3(-422f, 130f, 82f), new Vector3(141f, 140f, -375f),
            new Vector3(-13f, 140f, -523f), new Vector3(723f, 125f, -689f),
            new Vector3(439f, 130f, -274f), new Vector3(478f, 125f, -446f),
            new Vector3(631f, 126f, 487f), new Vector3(454f, 140f, 313f),
            new Vector3(-492f, 120f, 333f), new Vector3(-787f, 125f, 69f),
            new Vector3(-23f, 140f, 338f), new Vector3(-162f, 140f, -214f),
            new Vector3(-534f, 170f, -311f), new Vector3(-674f, 125f, -8f),
            new Vector3(-392f, 130f, 400f), new Vector3(385f, 135f, 468f),
            new Vector3(659f, 125f, 600f), new Vector3(-411f, 120f, 517f),
            new Vector3(685f, 140f, 8f), new Vector3(-254f, 140f, -128f),
            new Vector3(-689f, 145f, 510f), new Vector3(366f, 130f, -646f),
            new Vector3(-74f, 125f, 611f), new Vector3(98f, 125f, -95f),
            new Vector3(433f, 140f, 73f), new Vector3(-385f, 175f, -771f),
            new Vector3(636f, 125f, 240f), new Vector3(-32f, 129f, -645f),
            new Vector3(-70f, 133f, -635f), new Vector3(-71f, 141f, -594f),
            new Vector3(-91f, 137f, -558f), new Vector3(-63f, 131f, -513f),
            new Vector3(-24f, 132f, -494f), new Vector3(16f, 137f, -515f),
            new Vector3(28f, 132f, -557f), new Vector3(10f, 124f, -600f)
        };

        public static readonly Vector3[] PoiTargets =
        {
            new Vector3(-520f, 0f, -500f), new Vector3(-405f, 0f, 145f),
            new Vector3(-115f, 0f, 215f), new Vector3(100f, 0f, -100f),
            new Vector3(425f, 0f, -350f), new Vector3(610f, 0f, 520f),
            new Vector3(-720f, 0f, 80f), new Vector3(430f, 0f, 120f),
            new Vector3(-40f, 0f, -560f)
        };

        private static readonly HashSet<int> ShootableWeaponIds = new HashSet<int>
        {
            151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162,
            163, 164, 169, 171, 172, 174, 184,
            176, 177, 178, 179, 180, 181, 182, 185, 203, 217, 218, 219, 220,
            264, 265, 266, 267, 269, 270, 271, 272, 273, 274, 275, 276, 277, 278, 279, 280, 281, 283, 284,
            285, 286, 287, 288, 289, 290, 291,
            292, 293, 294, 297, 298, 300, 301,
            302, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313, 314, 315, 316,
            317, 319, 320, 321, 322, 323, 325, 326, 327, 328
        };

        private static readonly HashSet<int> AutomaticWeaponIds = new HashSet<int>
        {
            151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162,
            176, 178, 217, 218, 220,
            264, 269, 271,
            302, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313, 314, 315, 316
        };

        private static readonly HashSet<int> BurstWeaponIds = new HashSet<int>
        {
            155, 156, 157, 158, 160, 264, 315, 320
        };

        public static bool IsShootableWeapon(int weaponId)
        {
            return ShootableWeaponIds.Contains(weaponId);
        }

        public static bool IsAutomaticWeapon(int weaponId)
        {
            return AutomaticWeaponIds.Contains(weaponId);
        }

        public static int GetWeaponScore(int itemId, string name)
        {
            WeaponProfile profile = GetWeaponProfile(itemId, name);
            int score = profile.FirePlan == FirePlan.FullAuto ? 70 : 52;
            string lower = (name ?? string.Empty).ToLowerInvariant();
            switch (profile.CombatClass)
            {
                case WeaponCombatClass.Sniper:
                case WeaponCombatClass.AutoSniper:
                    score += 26;
                    break;
                case WeaponCombatClass.AssaultRifle:
                case WeaponCombatClass.Lmg:
                    score += 20;
                    break;
                case WeaponCombatClass.Smg:
                    score += 13;
                    break;
                case WeaponCombatClass.Shotgun:
                    score += 6;
                    break;
                case WeaponCombatClass.Pistol:
                    score -= 8;
                    break;
                case WeaponCombatClass.Launcher:
                    score += 10;
                    break;
            }

            if (lower.Contains("barrett") || lower.Contains("awm") || lower.Contains("sniper"))
                score += 24;
            if (lower.Contains("scar") || lower.Contains("ak") || lower.Contains("m4") || lower.Contains("aug"))
                score += 18;
            if (lower.Contains("mp") || lower.Contains("vector") || lower.Contains("uzi"))
                score += 10;
            if (lower.Contains("shotgun") || lower.Contains("crossbow") || lower.Contains("flintlock"))
                score -= 18;
            if (lower.Contains("pistol") || lower.Contains("revolver"))
                score -= 12;
            return score;
        }

        public static WeaponProfile GetWeaponProfile(int itemId, string name)
        {
            string lower = (name ?? string.Empty).ToLowerInvariant();

            if (itemId < 0)
                return new WeaponProfile(WeaponCombatClass.Unarmed, FirePlan.Semi, 2f, 4f, 5f, 1f, 0.8f, 1, 1, 1f, 0f, 0f);

            if ((BurstWeaponIds.Contains(itemId) || lower.Contains("burst") || lower.Contains("famas") || lower.Contains("beam")) && !lower.Contains("sniper"))
                return new WeaponProfile(WeaponCombatClass.AssaultRifle, FirePlan.Burst, 10f, 25f, 58f, 5.4f, 0.095f, 3, 30, 2.25f, 0.58f, 0.27f);

            if ((itemId >= 292 && itemId <= 301) || itemId == 326 || lower.Contains("shotgun") || lower.Contains("mossberg") || lower.Contains("blunderbuss") || lower.Contains("aa-12") || lower.Contains("rainmaker") || lower.Contains("arnold"))
            {
                FirePlan plan = itemId == 292 || itemId == 296 ? FirePlan.FullAuto : FirePlan.Semi;
                return new WeaponProfile(WeaponCombatClass.Shotgun, plan, 3.5f, 8.5f, 26f, 15.5f, plan == FirePlan.FullAuto ? 0.18f : 0.92f, 1, plan == FirePlan.FullAuto ? 12 : 5, 2.6f, 0.72f, 0.16f);
            }

            if ((itemId >= 317 && itemId <= 328) || lower.Contains("awp") || lower.Contains("barrett") || lower.Contains("kar98") || lower.Contains("vss") || lower.Contains("sniper"))
            {
                FirePlan plan = itemId == 303 || itemId == 328 || lower.Contains("vss") ? FirePlan.FullAuto : FirePlan.Semi;
                WeaponCombatClass combatClass = plan == FirePlan.FullAuto ? WeaponCombatClass.AutoSniper : WeaponCombatClass.Sniper;
                return new WeaponProfile(combatClass, plan, plan == FirePlan.FullAuto ? 16f : 24f, plan == FirePlan.FullAuto ? 36f : 52f, plan == FirePlan.FullAuto ? 72f : 96f, plan == FirePlan.FullAuto ? 7.2f : 26f, plan == FirePlan.FullAuto ? 0.14f : 1.35f, 1, plan == FirePlan.FullAuto ? 20 : 8, plan == FirePlan.FullAuto ? 2.7f : 3.25f, 0.62f, 0.38f);
            }

            if ((itemId >= 302 && itemId <= 316) || lower.Contains("smg") || lower.Contains("mp5") || lower.Contains("mp-") || lower.Contains("vector") || lower.Contains("uzi") || lower.Contains("ump"))
                return new WeaponProfile(WeaponCombatClass.Smg, FirePlan.FullAuto, 5.5f, 16f, 42f, 4.7f, 0.085f, 1, 28, 1.85f, 0.64f, 0.2f);

            if ((itemId >= 151 && itemId <= 165) || lower.Contains("ak") || lower.Contains("aug") || lower.Contains("scar") || lower.Contains("m16") || lower.Contains("ar"))
                return new WeaponProfile(WeaponCombatClass.AssaultRifle, AutomaticWeaponIds.Contains(itemId) ? FirePlan.FullAuto : FirePlan.Semi, 9f, 27f, 64f, 7.2f, 0.12f, 1, 30, 2.15f, 0.62f, 0.28f);

            if ((itemId >= 217 && itemId <= 220) || itemId == 176 || itemId == 177 || itemId == 178 || lower.Contains("minigun") || lower.Contains("mg") || lower.Contains("bar"))
                return new WeaponProfile(WeaponCombatClass.Lmg, FirePlan.FullAuto, 11f, 32f, 70f, 6.4f, 0.075f, 1, 80, 3.15f, 0.56f, 0.22f);

            if (itemId == 179 || itemId == 181 || itemId == 182 || lower.Contains("launcher") || lower.Contains("rocket") || lower.Contains("missile"))
                return new WeaponProfile(WeaponCombatClass.Launcher, FirePlan.Semi, 12f, 30f, 68f, 18f, 1.45f, 1, 6, 3.4f, 0.45f, 0.18f);

            if ((itemId >= 264 && itemId <= 284) || lower.Contains("pistol") || lower.Contains("revolver") || lower.Contains("deagle") || lower.Contains("glock") || lower.Contains("m1911"))
            {
                FirePlan plan = AutomaticWeaponIds.Contains(itemId) ? FirePlan.FullAuto : FirePlan.Semi;
                return new WeaponProfile(WeaponCombatClass.Pistol, plan, 4.5f, 15f, 34f, plan == FirePlan.FullAuto ? 4.2f : 8.5f, plan == FirePlan.FullAuto ? 0.1f : 0.55f, 1, plan == FirePlan.FullAuto ? 20 : 8, 1.7f, 0.58f, 0.18f);
            }

            FirePlan fallbackPlan = AutomaticWeaponIds.Contains(itemId) ? FirePlan.FullAuto : FirePlan.Semi;
            return new WeaponProfile(WeaponCombatClass.Special, fallbackPlan, 7f, 20f, 44f, fallbackPlan == FirePlan.FullAuto ? 4.5f : 7f, fallbackPlan == FirePlan.FullAuto ? 0.12f : 0.75f, 1, fallbackPlan == FirePlan.FullAuto ? 25 : 8, 2.2f, 0.5f, 0.2f);
        }
    }
}
