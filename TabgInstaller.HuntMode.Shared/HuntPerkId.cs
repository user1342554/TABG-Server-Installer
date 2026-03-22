namespace TabgInstaller.HuntMode.Shared
{
    public enum KillerPerk : byte
    {
        None = 0, Tracker = 1, Ironhide = 2, Bloodhound = 3, Reload = 4, Locksmith = 5
    }

    public enum SurvivorPerk : byte
    {
        None = 0, Medic = 1, Scout = 2, Tough = 3, Sprinter = 4, Saboteur = 5, Smokescreen = 6
    }

    public static class PerkInfo
    {
        public static string GetName(KillerPerk perk)
        {
            if (perk == KillerPerk.Tracker) return "Tracker";
            if (perk == KillerPerk.Ironhide) return "Ironhide";
            if (perk == KillerPerk.Bloodhound) return "Bloodhound";
            if (perk == KillerPerk.Reload) return "Reload";
            if (perk == KillerPerk.Locksmith) return "Locksmith";
            return "None";
        }

        public static string GetDescription(KillerPerk perk)
        {
            if (perk == KillerPerk.Tracker) return "Every 30s, nearest survivor pinged on HUD for 3s";
            if (perk == KillerPerk.Ironhide) return "+100 HP (400 total), speed reduced to 1.1x";
            if (perk == KillerPerk.Bloodhound) return "Downed survivors bleed out 50% faster (22.5s)";
            if (perk == KillerPerk.Reload) return "Start with 18 shells, +6 per down";
            if (perk == KillerPerk.Locksmith) return "Crate looting takes survivors 50% longer (4.5s)";
            return "";
        }

        public static string GetName(SurvivorPerk perk)
        {
            if (perk == SurvivorPerk.Medic) return "Medic";
            if (perk == SurvivorPerk.Scout) return "Scout";
            if (perk == SurvivorPerk.Tough) return "Tough";
            if (perk == SurvivorPerk.Sprinter) return "Sprinter";
            if (perk == SurvivorPerk.Saboteur) return "Saboteur";
            if (perk == SurvivorPerk.Smokescreen) return "Smokescreen";
            return "None";
        }

        public static string GetDescription(SurvivorPerk perk)
        {
            if (perk == SurvivorPerk.Medic) return "Revive 40% faster (3s / 4.8s / 6s)";
            if (perk == SurvivorPerk.Scout) return "Directional indicator when Killer within 30m";
            if (perk == SurvivorPerk.Tough) return "4 downs to die instead of 3";
            if (perk == SurvivorPerk.Sprinter) return "1.15x speed for 5s after damage (20s cooldown)";
            if (perk == SurvivorPerk.Saboteur) return "Loot crates 40% faster (1.8s)";
            if (perk == SurvivorPerk.Smokescreen) return "Start with 2 smoke grenades";
            return "";
        }
    }
}
