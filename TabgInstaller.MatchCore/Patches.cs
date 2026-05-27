using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Landfall.Network;
using Landfall.Network.GameModes;

namespace TabgInstaller.MatchCore
{
    [HarmonyPatch(typeof(ServerClient), "Awake")]
    internal static class ServerCapturePatch
    {
        private static void Postfix(ServerClient __instance)
        {
            MatchCoreRuntime.ClearVotes();
            MatchCorePlugin.LoggerSafe("ServerClient captured.");
        }
    }

    [HarmonyPatch(typeof(GameRoom), "StartCountDown")]
    internal static class CountdownStartedPatch
    {
        private static void Postfix()
        {
            MatchCoreRuntime.ClearVotes();
        }
    }

    [HarmonyPatch(typeof(ChatMessageCommand), "Run")]
    internal static class VoteStartChatPatch
    {
        private static void Postfix(byte[] msgData, ServerClient world, byte sender)
        {
            string message = ReadChatMessage(msgData);
            if (message == null) return;
            if (!message.Trim().Equals("/votestart", StringComparison.OrdinalIgnoreCase)) return;

            TABGPlayerServer player = world?.GameRoomReference?.FindPlayer(sender);
            MatchCoreRuntime.HandleVoteStart(world, player);
        }

        private static string ReadChatMessage(byte[] msgData)
        {
            if (msgData == null || msgData.Length < 2) return null;
            try
            {
                using (var input = new MemoryStream(msgData))
                using (var reader = new BinaryReader(input))
                {
                    reader.ReadByte();
                    int count = reader.ReadByte();
                    if (count <= 0 || count > msgData.Length - 2) return null;
                    return Encoding.Unicode.GetString(reader.ReadBytes(count));
                }
            }
            catch
            {
                return null;
            }
        }
    }

    [HarmonyPatch(typeof(TABGBaseGameMode), "Run")]
    internal static class MatchTimerPatch
    {
        private static void Postfix(TABGBaseGameMode __instance, GameState state)
        {
            MatchCoreRuntime.TickTimers(__instance, state);
        }
    }

    [HarmonyPatch(typeof(BattleRoyaleGameMode), "CheckGameState")]
    internal static class WinConditionPatch
    {
        private static bool Prefix(BattleRoyaleGameMode __instance, GameState state)
        {
            return MatchCoreRuntime.HandleWinCondition(__instance, state);
        }
    }

    [HarmonyPatch(typeof(BattleRoyaleGameMode), "GetNewSpawnPoint")]
    internal static class SpawnPointPatch
    {
        private static bool Prefix(BattleRoyaleGameMode __instance, ref SpawnPointWrapper __result)
        {
            if (!MatchCoreRuntime.TryGetSpawnPoint(__instance, out var spawn)) return true;
            __result = spawn;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class RespawnLoadoutPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(RespawnEntityCommand), "Run", new[]
            {
                typeof(ServerClient), typeof(List<TABGPlayerServer>), typeof(UnityEngine.Vector3), typeof(byte)
            });
        }

        private static void Postfix(ServerClient world, List<TABGPlayerServer> players)
        {
            world?.WaitThenDoAction(0.35f, () => MatchCoreRuntime.GiveRespawnLoadout(world, players));
        }
    }

    [HarmonyPatch(typeof(PlayerDeadDeadBehaviourCommand), "Run")]
    internal static class KillRewardPatch
    {
        private static void Postfix(TABGPlayerServer victimPlayer, ServerClient world)
        {
            MatchCoreRuntime.ApplyKillRewards(victimPlayer, world);
        }
    }

    [HarmonyPatch(typeof(PlayerDeadWithDownBehaviourCommand), "Run")]
    internal static class DownBehaviourPatch
    {
        private static bool Prefix(ServerClient world, TABGPlayerServer victimPlayer, TABGPlayerServer damagerPlayer, byte[] bufferDat, byte[] recievers, byte senderIndex)
        {
            var settings = MatchCorePlugin.Settings;
            if (settings == null || settings.CanGoDown) return true;
            if (victimPlayer == null || world == null) return true;

            world.GameRoomReference.CurrentGameMode.KillPlayer(victimPlayer, damagerPlayer);
            world.GameRoomReference.CheckGameState();
            return false;
        }
    }

    [HarmonyPatch]
    internal static class DropAllLootPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(DropAllLootCommand), "Run", new[] { typeof(ServerClient), typeof(List<TABGPlayerServer>) });
        }

        private static bool Prefix(List<TABGPlayerServer> players)
        {
            var settings = MatchCorePlugin.Settings;
            if (settings == null || settings.DropItemsOnDeath) return true;
            MatchCoreRuntime.DropControl(players);
            return false;
        }
    }

    [HarmonyPatch(typeof(Spelldrop_Server), "Start")]
    internal static class SpellDropPatch
    {
        private static void Postfix(Spelldrop_Server __instance)
        {
            MatchCoreRuntime.ConfigureSpellDrop(__instance);
        }
    }

    [HarmonyPatch(typeof(ServerClient), "SpawnTheRingIn")]
    internal static class RingSettingsPatch
    {
        private static void Postfix(ServerClient __instance)
        {
            MatchCoreRuntime.ApplyRingSettings(__instance?.SpawnedRing);
        }
    }

    [HarmonyPatch(typeof(TheRing), "GetNewRingPosition")]
    internal static class RingPositionPatch
    {
        private static bool Prefix(TheRing __instance, float newCircleSize)
        {
            return !MatchCoreRuntime.TryOverrideRingPosition(__instance, newCircleSize);
        }
    }

    [HarmonyPatch]
    internal static class StartFlyingPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GameRoom), "StartFlying");
        }

        private static void Postfix(GameRoom __instance)
        {
            var field = ReflectionHelpers.Field(typeof(GameRoom), "m_server");
            MatchCoreRuntime.ForceDrop(field?.GetValue(__instance) as ServerClient);
        }
    }
}
