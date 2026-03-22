using System;
using System.IO;
using BepInEx;
using HarmonyLib;
using TabgInstaller.HuntMode.Shared;
using UnityEngine;

namespace TabgInstaller.HuntMode.Client
{
    [BepInPlugin("tabginstaller.huntmode.client", "Hunt Mode Client", "1.0.0")]
    public class HuntClientPlugin : BaseUnityPlugin
    {
        public static HuntClientPlugin Instance { get; private set; }
        public static HuntClientState ClientState { get; private set; }
        public static BepInEx.Logging.ManualLogSource Log { get; private set; }

        private HuntHUD _hud;
        private PerkSelectionUI _perkUI;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ClientState = new HuntClientState();

            _hud = new HuntHUD();
            _perkUI = new PerkSelectionUI();

            var harmony = new Harmony("tabginstaller.huntmode.client");
            harmony.PatchAll(typeof(HuntClientPlugin));

            HuntNetworkPatch.ApplyManualPatch(harmony);

            Logger.LogInfo("[HuntMode.Client] Hunt Mode Client loaded.");
        }

        private void OnGUI()
        {
            if (ClientState == null) return;

            if (ClientState.PerkSelectionActive && !ClientState.PerkSelectionConfirmed)
            {
                _perkUI.Draw(ClientState);
            }
            else if (ClientState.MatchActive)
            {
                _hud.Draw(ClientState);
            }

            if (ClientState.ShowResult)
            {
                _hud.DrawResult(ClientState);
            }
        }

        /// <summary>
        /// Entry point called by the network patch when a Hunt Mode event is received.
        /// eventCode: 73-80, data: raw bytes following the event code byte.
        /// </summary>
        public static void HandleHuntEvent(byte eventCode, byte[] data)
        {
            if (ClientState == null) return;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var reader = new BinaryReader(ms))
                {
                    if (eventCode == HuntEventCodes.RoleAssignment)
                        ParseRoleAssignment(reader);
                    else if (eventCode == HuntEventCodes.PerkConfirm)
                        ParsePerkConfirm(reader);
                    else if (eventCode == HuntEventCodes.CrateUpdate)
                        ParseCrateUpdate(reader);
                    else if (eventCode == HuntEventCodes.VehicleState)
                        ParseVehicleState(reader);
                    else if (eventCode == HuntEventCodes.DownState)
                        ParseDownState(reader);
                    else if (eventCode == HuntEventCodes.MatchState)
                        ParseMatchState(reader);
                    else if (eventCode == HuntEventCodes.TrackerPing)
                        ParseTrackerPing(reader);
                }
            }
            catch (Exception ex)
            {
                Log?.LogWarning("[HuntMode.Client] Error parsing event " + eventCode + ": " + ex.Message);
            }
        }

        // ---- Event parsers ----

        private static void ParseRoleAssignment(BinaryReader r)
        {
            // byte myIndex, byte killerIndex, byte survivorCount, then survivorCount * (byte idx + string name)
            byte myIndex = r.ReadByte();
            byte killerIndex = r.ReadByte();
            byte survivorCount = r.ReadByte();

            ClientState.MyIndex = myIndex;
            ClientState.KillerIndex = killerIndex;
            ClientState.IsKiller = (myIndex == killerIndex);

            for (int i = 0; i < survivorCount; i++)
            {
                byte idx = r.ReadByte();
                string name = r.ReadString();
                ClientState.PlayerNames[idx] = name;
            }

            // Also store killer name if possible (killerIndex slot)
            // Activate perk selection
            ClientState.PerkSelectionActive = true;
            ClientState.PerkSelectionConfirmed = false;
            ClientState.SelectedPerk1 = 0;
            ClientState.SelectedPerk2 = 0;
        }

        private static void ParsePerkConfirm(BinaryReader r)
        {
            // byte perk1, byte perk2
            ClientState.MyPerks[0] = r.ReadByte();
            ClientState.MyPerks[1] = r.ReadByte();
            ClientState.PerkSelectionActive = false;
            ClientState.PerkSelectionConfirmed = true;
            ClientState.MatchActive = true;
        }

        private static void ParseCrateUpdate(BinaryReader r)
        {
            // byte crateIndex, byte totalLooted
            r.ReadByte(); // crateIndex (which crate was looted — unused client-side beyond count)
            ClientState.CratesLooted = r.ReadByte();
        }

        private static void ParseVehicleState(BinaryReader r)
        {
            // bool unlocked, float x, float y, float z
            ClientState.VehicleUnlocked = r.ReadBoolean();
            float x = r.ReadSingle();
            float y = r.ReadSingle();
            float z = r.ReadSingle();
            ClientState.VehiclePosition = new Vector3(x, y, z);
            ClientState.VehicleRevealed = true;
        }

        private static void ParseDownState(BinaryReader r)
        {
            // byte playerIdx, byte downCount, byte maxDowns
            byte idx = r.ReadByte();
            byte downCount = r.ReadByte();
            byte maxDowns = r.ReadByte();
            bool dead = r.ReadBoolean();
            ClientState.PlayerDownCounts[idx] = downCount;
            ClientState.PlayerMaxDowns[idx] = maxDowns;
            ClientState.PlayerDead[idx] = dead;
        }

        private static void ParseMatchState(BinaryReader r)
        {
            // float timeRemaining, byte cratesLooted, bool vehicleUnlocked, byte escapedCount
            ClientState.TimeRemaining = r.ReadSingle();
            ClientState.CratesLooted = r.ReadByte();
            ClientState.VehicleUnlocked = r.ReadBoolean();
            byte escaped = r.ReadByte();

            // Check for end condition
            if (r.BaseStream.Position < r.BaseStream.Length)
            {
                bool matchOver = r.ReadBoolean();
                if (matchOver && r.BaseStream.Position < r.BaseStream.Length)
                {
                    ClientState.ResultText = r.ReadString();
                    ClientState.ShowResult = true;
                    ClientState.MatchActive = false;
                }
            }
        }

        private static void ParseTrackerPing(BinaryReader r)
        {
            // Only relevant for killer; float x, y, z, float duration
            float x = r.ReadSingle();
            float y = r.ReadSingle();
            float z = r.ReadSingle();
            float duration = r.ReadSingle();
            ClientState.TrackerPingPosition = new Vector3(x, y, z);
            ClientState.TrackerPingEndTime = Time.time + duration;
        }
    }
}
