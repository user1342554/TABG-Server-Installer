using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Landfall.Network;
using TabgInstaller.HuntMode.Shared;

namespace TabgInstaller.HuntMode
{
    // =========================================================================
    // Task 6 — Vehicle patch
    // =========================================================================

    // -------------------------------------------------------------------------
    // Patch: RequestSeatCommand.Run — Prefix
    //
    // Message layout (per spec, parsed with BinaryReader):
    //   byte  playerIndex
    //   int32 carIndex
    //   int32 seatIndex
    //   byte  seatAction  (0 = GetIn, 1 = GetOut)
    //
    // Rules:
    //   - GetIn (0) while vehicle not yet unlocked: block.
    //   - GetIn (0) by the killer: block (killer cannot use escape vehicle).
    //   - Anything else: let through.
    // -------------------------------------------------------------------------
    [HarmonyPatch(typeof(RequestSeatCommand), "Run")]
    public static class HuntVehiclePatch
    {
        private const byte SeatActionGetIn = 0;

        public static bool Prefix(ServerClient serverClient, byte[] msgData, byte senderIndex)
        {
            HuntGameState gs = HuntModePlugin.GameState;
            if (gs == null || !gs.MatchActive)
                return true; // let original run

            if (msgData == null || msgData.Length < 7)
                return true; // malformed — let original handle

            byte playerIndex;
            int  carIndex;
            int  seatIndex;
            byte seatAction;

            try
            {
                using (var ms = new MemoryStream(msgData))
                using (var br = new BinaryReader(ms))
                {
                    playerIndex = br.ReadByte();
                    carIndex    = br.ReadInt32();
                    seatIndex   = br.ReadInt32();
                    seatAction  = br.ReadByte();
                }
            }
            catch (Exception ex)
            {
                HuntModePlugin.LogWarning("RequestSeatCommand parse error: " + ex.Message);
                return true; // let original try
            }

            if (seatAction == SeatActionGetIn)
            {
                // Block if vehicle not yet unlocked
                if (!gs.VehicleUnlocked)
                {
                    HuntModePlugin.Log($"Player {playerIndex} tried to enter vehicle before unlock — blocked.");
                    return false;
                }

                // Block if the player is the killer
                if (gs.IsKiller(playerIndex))
                {
                    HuntModePlugin.Log($"Killer (player {playerIndex}) tried to enter escape vehicle — blocked.");
                    return false;
                }
            }

            return true; // all other seat actions pass through
        }
    }
}
