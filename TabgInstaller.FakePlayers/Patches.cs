using System;
using HarmonyLib;
using Landfall.Network;

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
            if (!ReferenceEquals(FakePlayersPlugin.ServerRef, __instance))
                FakePlayersPlugin.ResetStaticMatchState();

            FakePlayersPlugin.ServerRef = __instance;
            FakePlayersPlugin.QueueAutoSpawn(__instance);
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
                return true;

            FakePlayersPlugin.PruneMissingFakePlayers(__instance);
            if (FakePlayersPlugin.FakeIndices.Count == 0)
                return true;

            var players = __instance.Players;
            int count = mustBeReady
                ? players.FindAll(p => p.Ready).Count
                : players.Count;

            __result = Math.Max(count, 1);
            return false;
        }
    }

    /// <summary>
    /// Observes the vanilla teammate marker command before the server relays it.
    /// The original command still owns validation and client delivery.
    /// </summary>
    [HarmonyPatch(typeof(PlayerMarkerChangedCommand), nameof(PlayerMarkerChangedCommand.Run))]
    internal static class TeamMarkerOrderPatch
    {
        static void Prefix(byte[] msgData, ServerClient world)
        {
            try
            {
                FakePlayersPlugin.RecordTeamMarker(world, msgData);
            }
            catch (Exception ex)
            {
                FakePlayersPlugin.Log($"Team marker patch error: {ex.Message}");
            }
        }
    }
}
