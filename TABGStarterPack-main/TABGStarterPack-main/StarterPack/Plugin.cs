using BepInEx;
using HarmonyLib;
using Landfall.Network;
using Landfall.Network.GameModes;
using System;
using System.Collections.Generic;

namespace StarterPack
{
    [BepInPlugin("com.starterpack.tabg.contagious", "Starter Pack", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo("Plugin com.starterpack.tabg.contagious is loaded!");

            Harmony harmony = new Harmony("com.starterpack.tabg.contagious");
            //Config: Ensure config is loaded, but it will not force rings unless RingSettings provided
            harmony.Patch(AccessTools.Method(typeof(ServerClient), "Init", null, null),
               new HarmonyMethod(AccessTools.Method(typeof(Config), "Setup", null, null)), null,
               null, null, null);

            //Ring Manager - DISABLED to use vanilla ring behavior
            // harmony.Patch(AccessTools.Method(typeof(TheRing), "GetNewRingPosition", null, null),
            //     new HarmonyMethod(AccessTools.Method(typeof(RingManager), "GetNewRingPosition", null, null)), null,
            //     null, null, null);

            //Respawning Players
            harmony.Patch(AccessTools.Method(typeof(PlayerDeadDeadBehaviourCommand), "Run", null, null),
                new HarmonyMethod(AccessTools.Method(typeof(RespawningPlayers), "Run", null, null)), null,
                null, null, null);
            harmony.Patch(AccessTools.Method(typeof(BattleRoyaleGameMode), "HandlePlayerDead", null, null),
                new HarmonyMethod(AccessTools.Method(typeof(RespawningPlayers), "HandlePlayerDead", null, null)), null,
                null, null, null);
            // DISABLED - Ring death handling removed to prevent ring interference
            // harmony.Patch(AccessTools.Method(typeof(PlayerRingDeathCommand), "Run", null, null),
            //     new HarmonyMethod(AccessTools.Method(typeof(RespawningPlayers), "RingDeathRun", null, null)), null,
            //     null, null, null);

            //Auto Launching
            harmony.Patch(AccessTools.Method(typeof(AutoDropAllPlayersCommand), "Run", null, null),
                new HarmonyMethod(AccessTools.Method(typeof(RespawningPlayers), "AutoDropAllPlayersRun", null, null)), null,
                null, null, null);
            harmony.Patch(AccessTools.Method(typeof(GameRoom), "StartFlying", null, null),
                null, new HarmonyMethod(AccessTools.Method(typeof(RespawningPlayers), "StartFlying", null, null)),
                null, null, null);

            //Drop Settings
            harmony.Patch(AccessTools.Method(typeof(DropAllLootCommand), "Run", new Type[] { typeof(ServerClient), typeof(List<TABGPlayerServer>) }),
                new HarmonyMethod(AccessTools.Method(typeof(ControlDrop), "DropAllLootCommandRun", new Type[] { typeof(ServerClient), typeof(List<TABGPlayerServer>) })));
            harmony.Patch(AccessTools.Method(typeof(ItemDropCommand), "Run", null, null),
                new HarmonyMethod(AccessTools.Method(typeof(ControlDrop), "ItemDropCommandRun", null, null)),
                null, null, null, null);
            harmony.Patch(AccessTools.Method(typeof(ItemThrownCommand), "Run", null, null),
                new HarmonyMethod(AccessTools.Method(typeof(ControlDrop), "ItemThrownCommandRun", null, null)),
                null, null, null, null);


            //Lobby Spawn Controller
            harmony.Patch(AccessTools.Method(typeof(BattleRoyaleGameMode), "GetNewSpawnPoint", null, null),
                new HarmonyMethod(AccessTools.Method(typeof(LobbySpawnController), "GetNewSpawnPoint", null, null)), null,
                null, null, null);

            //Vote To Start
            harmony.Patch(AccessTools.Method(typeof(ChatMessageCommand), "Run", null, null),
                null, new HarmonyMethod(AccessTools.Method(typeof(VoteToStart), "ChatCommandPostfix", null, null)),
                null, null, null);
            harmony.Patch(AccessTools.Method(typeof(GameRoom), "StartFlying", null, null),
                null, new HarmonyMethod(AccessTools.Method(typeof(VoteToStart), "Reset", null, null)),
                null, null, null);

            //Spell Drop Controller
            harmony.Patch(AccessTools.Method(typeof(Spelldrop_Server), "Start", null, null), null,
                new HarmonyMethod(AccessTools.Method(typeof(SpellDropController), "Start", null, null)),
                null, null, null);
            /*harmony.Patch(AccessTools.Method(typeof(GameRoom), "DroppedOpened", null, null),
                 new HarmonyMethod(AccessTools.Method(typeof(SpellDropController), "DroppedOpened", null, null)), null,
                 null, null, null);*/

            //Match Timeout
            harmony.Patch(AccessTools.Method(typeof(TABGBaseGameMode), "Run", null, null),
                new HarmonyMethod(AccessTools.Method(typeof(MatchTimeout), "Run", null, null)), null,
                null, null, null);
            harmony.Patch(AccessTools.Method(typeof(GameRoom), "StartFlying", null, null),
                null, new HarmonyMethod(AccessTools.Method(typeof(MatchTimeout), "ResetTimer", null, null)),
                null, null, null);
            harmony.Patch(AccessTools.Method(typeof(GameRoom), "EndMatch", null, null),
                null, new HarmonyMethod(AccessTools.Method(typeof(MatchTimeout), "ResetTimer", null, null)),
                null, null, null);

            //Win Conditions
            harmony.Patch(AccessTools.Method(typeof(BattleRoyaleGameMode), "CheckGameState", null, null),
                new HarmonyMethod(AccessTools.Method(typeof(WinConditions), "CheckGameState", null, null)), null,
                null, null, null);
        }
    }
}