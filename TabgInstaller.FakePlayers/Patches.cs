using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Landfall.Network;
using Landfall.TABG;

namespace TabgInstaller.FakePlayers
{
    /// <summary>
    /// Captures the ServerClient singleton when the server starts.
    /// </summary>
    [HarmonyPatch(typeof(ServerClient), "Awake")]
    internal static class CaptureServerPatch
    {
        static void Postfix(ServerClient __instance)
        {
            FakePlayersPlugin.ServerRef = __instance;
        }
    }

    /// <summary>
    /// Intercepts admin commands to:
    ///   1. Handle spawndummy / removedummy / dummycount
    ///   2. Skip the admin check so ANY player can run ANY command
    ///
    /// The original HandleCommand rejects non-admin players immediately.
    /// This prefix replaces that logic entirely: it parses the command,
    /// handles our custom ones, and for all other commands it invokes
    /// them via the handler's RunAdminCommand — bypassing the admin gate.
    /// </summary>
    [HarmonyPatch(typeof(AdminCommandHandler), "HandleCommand")]
    internal static class CommandPatch
    {
        static bool Prefix(AdminCommandHandler __instance, byte[] serverData, ServerClient world, byte sender)
        {
            // Parse command string from packet
            string command;
            try
            {
                using (var ms = new MemoryStream(serverData))
                using (var br = new BinaryReader(ms))
                {
                    command = br.ReadString();
                }
            }
            catch
            {
                return false; // Bad packet, swallow it
            }

            string lower = command.Trim().ToLowerInvariant();

            // ---- spawndummy [count] ----
            if (lower.StartsWith("spawndummy"))
            {
                int count = 1;
                string[] parts = lower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                    int.TryParse(parts[1], out count);
                count = Math.Max(1, Math.Min(count, 200));

                FakePlayersPlugin.SpawnFakePlayers(world, count);
                return false;
            }

            // ---- removedummy [count] ----
            if (lower.StartsWith("removedummy"))
            {
                int count = 0; // 0 = remove all
                string[] parts = lower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                    int.TryParse(parts[1], out count);

                FakePlayersPlugin.RemoveFakePlayers(world, count);
                return false;
            }

            // ---- dummycount ----
            if (lower == "dummycount")
            {
                FakePlayersPlugin.Log($"Active fake players: {FakePlayersPlugin.FakeIndices.Count}");
                return false;
            }

            // ---- All other commands: run without admin check ----
            try
            {
                __instance.RunAdminCommand(command, world, null);
            }
            catch (Exception ex)
            {
                FakePlayersPlugin.Log($"Command '{command}' error: {ex.Message}");
            }

            return false; // Always skip original (which has the admin gate)
        }
    }

    /// <summary>
    /// Makes GetNumberOfPlayers count fake players (bots) as real players
    /// so they trigger countdown, force-start, etc.
    /// Only active when fake players have been spawned.
    /// </summary>
    [HarmonyPatch(typeof(GameRoom), "GetNumberOfPlayers", new Type[] { typeof(bool) })]
    internal static class PlayerCountPatch
    {
        static bool Prefix(GameRoom __instance, ref int __result, bool mustBeReady)
        {
            if (FakePlayersPlugin.FakeIndices.Count == 0)
                return true; // No fakes, run original

            var players = __instance.Players;
            int count = mustBeReady
                ? players.FindAll(p => p.Ready).Count
                : players.Count;

            int groups = __instance.GetNumberOfGroups();
            if (groups < 2)
            {
                __result = 1;
                return false;
            }

            __result = Math.Max(count, 1);
            return false;
        }
    }
}
