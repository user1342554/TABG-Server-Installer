using System;
using System.Collections.Generic;
using System.IO;
using TabgInstaller.HuntMode.Shared;

namespace TabgInstaller.HuntMode
{
    public class HuntGameState
    {
        // Role tracking
        public byte KillerIndex;
        public HashSet<byte> SurvivorIndices = new HashSet<byte>();

        // Down tracking
        public Dictionary<byte, int> DownCounts = new Dictionary<byte, int>();
        public Dictionary<byte, float> DownTimestamps = new Dictionary<byte, float>();
        public Dictionary<byte, float> ReviveStartTimestamps = new Dictionary<byte, float>();

        // Perk tracking
        public Dictionary<byte, byte[]> PlayerPerks = new Dictionary<byte, byte[]>();

        // Crate tracking
        public int CratesLooted;
        public HashSet<int> LootedCrateIndices = new HashSet<int>();

        // Vehicle
        public bool VehicleUnlocked;
        public HashSet<byte> EscapedPlayers = new HashSet<byte>();
        public float EscapeGraceStartTime = -1f;

        // Match state
        public float MatchStartTime;
        public bool MatchActive;
        public bool PerkSelectionComplete;
        public HashSet<byte> PerkConfirmedPlayers = new HashSet<byte>();

        // Perk timers
        public float LastTrackerPingTime;
        public Dictionary<byte, float> SprinterActiveTimes = new Dictionary<byte, float>();
        public Dictionary<byte, float> SprinterCooldownTimes = new Dictionary<byte, float>();

        // --- Helper methods ---

        public bool IsKiller(byte playerIndex)
        {
            return playerIndex == KillerIndex;
        }

        public bool IsSurvivor(byte playerIndex)
        {
            return SurvivorIndices.Contains(playerIndex);
        }

        public int GetMaxDowns(byte playerIndex)
        {
            if (HasSurvivorPerk(playerIndex, SurvivorPerk.Tough))
                return HuntConstants.ToughMaxDowns;
            return HuntConstants.DefaultMaxDowns;
        }

        public int GetDownCount(byte playerIndex)
        {
            if (DownCounts.TryGetValue(playerIndex, out int count))
                return count;
            return 0;
        }

        public float GetReviveTime(byte victimIdx, byte reviverIdx)
        {
            int downs = GetDownCount(victimIdx);
            int index = Math.Min(downs, HuntConstants.ReviveTimes.Length - 1);
            float baseTime = HuntConstants.ReviveTimes[index];

            if (HasSurvivorPerk(reviverIdx, SurvivorPerk.Medic))
                return baseTime * HuntConstants.MedicReviveMultiplier;

            return baseTime;
        }

        public float GetReviveHP(byte playerIndex)
        {
            int downs = GetDownCount(playerIndex);
            int index = Math.Min(downs, HuntConstants.ReviveHP.Length - 1);
            return HuntConstants.ReviveHP[index];
        }

        public float GetBleedoutTime(byte playerIndex)
        {
            if (HasKillerPerk(KillerPerk.Bloodhound))
                return HuntConstants.BloodhoundBleedoutTime;
            return HuntConstants.BleedoutTime;
        }

        public float GetLootTime(byte playerIndex)
        {
            float baseTime = HuntConstants.BaseLootTime;

            if (IsSurvivor(playerIndex) && HasSurvivorPerk(playerIndex, SurvivorPerk.Saboteur))
                return baseTime * HuntConstants.SaboteurLootMultiplier;

            if (HasKillerPerk(KillerPerk.Locksmith))
                return baseTime * HuntConstants.LocksmithLootMultiplier;

            return baseTime;
        }

        public bool HasKillerPerk(KillerPerk perk)
        {
            if (!PlayerPerks.TryGetValue(KillerIndex, out byte[] perks))
                return false;
            for (int i = 0; i < perks.Length; i++)
            {
                if (perks[i] == (byte)perk)
                    return true;
            }
            return false;
        }

        public bool HasSurvivorPerk(byte playerIndex, SurvivorPerk perk)
        {
            if (!PlayerPerks.TryGetValue(playerIndex, out byte[] perks))
                return false;
            for (int i = 0; i < perks.Length; i++)
            {
                if (perks[i] == (byte)perk)
                    return true;
            }
            return false;
        }

        public int GetKillerStartAmmo()
        {
            if (HasKillerPerk(KillerPerk.Reload))
                return HuntConstants.KillerReloadPerkAmmo;
            return HuntConstants.KillerBaseAmmo;
        }

        public int GetAmmoPerDown()
        {
            if (HasKillerPerk(KillerPerk.Reload))
                return HuntConstants.KillerReloadPerkAmmoPerDown;
            return HuntConstants.KillerAmmoPerDown;
        }

        public void Reset()
        {
            KillerIndex = 0;
            SurvivorIndices.Clear();
            DownCounts.Clear();
            DownTimestamps.Clear();
            ReviveStartTimestamps.Clear();
            PlayerPerks.Clear();
            CratesLooted = 0;
            LootedCrateIndices.Clear();
            VehicleUnlocked = false;
            EscapedPlayers.Clear();
            EscapeGraceStartTime = -1f;
            MatchStartTime = 0f;
            MatchActive = false;
            PerkSelectionComplete = false;
            PerkConfirmedPlayers.Clear();
            LastTrackerPingTime = 0f;
            SprinterActiveTimes.Clear();
            SprinterCooldownTimes.Clear();
        }

        // --- Serialization helpers ---

        public byte[] SerializeCrateUpdate(int crateIndex)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((byte)crateIndex);
                bw.Write((byte)CratesLooted);
                bw.Write((byte)HuntConstants.TotalCrates);
                return ms.ToArray();
            }
        }

        public byte[] SerializeDownState(byte playerIndex)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(playerIndex);
                bw.Write((byte)GetDownCount(playerIndex));
                bw.Write((byte)GetMaxDowns(playerIndex));
                return ms.ToArray();
            }
        }

        public byte[] SerializeMatchState(float currentTime)
        {
            float timeRemaining = Math.Max(0f, (MatchStartTime + HuntConstants.MatchDuration) - currentTime);
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(timeRemaining);
                bw.Write((byte)CratesLooted);
                bw.Write(VehicleUnlocked);
                bw.Write((byte)EscapedPlayers.Count);
                return ms.ToArray();
            }
        }

        public byte[] SerializeRoleAssignment()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(KillerIndex);
                bw.Write((byte)SurvivorIndices.Count);
                foreach (byte idx in SurvivorIndices)
                {
                    bw.Write(idx);
                }
                return ms.ToArray();
            }
        }

        public byte[] SerializeAmmoGrant(int amount)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((byte)amount);
                return ms.ToArray();
            }
        }
    }
}
