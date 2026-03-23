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
            FakePlayersPlugin.ServerRef = __instance;
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
