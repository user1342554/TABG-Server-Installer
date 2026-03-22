using UnityEngine;

namespace TabgInstaller.HuntMode.Client
{
    public class HuntClientState
    {
        // Role
        public bool IsKiller;
        public byte MyIndex;
        public byte KillerIndex;

        // Match state
        public float TimeRemaining;
        public bool MatchActive;
        public bool VehicleUnlocked;

        // Crates
        public int CratesLooted;
        public int CratesTotal = 5;

        // Player state arrays
        public int[] PlayerDownCounts = new int[256];
        public int[] PlayerMaxDowns = new int[256];
        public string[] PlayerNames = new string[256];
        public bool[] PlayerDead = new bool[256];

        // Vehicle
        public Vector3 VehiclePosition;
        public bool VehicleRevealed;

        // Tracker ping (killer only)
        public Vector3 TrackerPingPosition;
        public float TrackerPingEndTime;

        // Perks
        public byte[] MyPerks = new byte[2];

        // Perk selection
        public bool PerkSelectionActive;
        public bool PerkSelectionConfirmed;
        public byte SelectedPerk1;
        public byte SelectedPerk2;

        // End screen
        public string ResultText;
        public bool ShowResult;

        public HuntClientState()
        {
            for (int i = 0; i < 256; i++)
            {
                PlayerNames[i] = "";
                PlayerMaxDowns[i] = 3;
            }
        }
    }
}
