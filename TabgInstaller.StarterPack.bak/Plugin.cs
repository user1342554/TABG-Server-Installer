using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;

namespace TabgInstaller.StarterPack
{
    [BepInPlugin("com.TabgInstaller.StarterPack", "TabgInstaller.StarterPack", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private Harmony harmony;
        
        private void Awake()
        {
            Log = Logger;
            
            try
            {
                Logger.LogInfo("TabgInstaller.StarterPack is loading!");
                
                // Load configuration
                Config.LoadConfig();
                
                // Create Harmony instance
                harmony = new Harmony("com.TabgInstaller.StarterPack");
                
                // Apply all patches
                ApplyPatches();
                
                Logger.LogInfo("TabgInstaller.StarterPack loaded successfully!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load TabgInstaller.StarterPack: {ex}");
            }
        }
        
        private void ApplyPatches()
        {
            try
            {
                // Ring Manager patches
                PatchMethod("Landfall.Network.Ring", "StartRingCircle", 
                    typeof(RingManager), nameof(RingManager.SetRing), HarmonyPatchType.Prefix);
                    
                // Respawning Players patches  
                PatchMethod("Landfall.Network.AutoDropAllPlayersCommand", "Run",
                    typeof(RespawningPlayers), nameof(RespawningPlayers.AutoDropAllPlayersRun), HarmonyPatchType.Prefix);
                    
                PatchMethod("Landfall.Network.GameRoom", "StartFlying",
                    typeof(RespawningPlayers), nameof(RespawningPlayers.StartFlying), HarmonyPatchType.Postfix);
                    
                PatchMethod("Landfall.Network.PlayerDeadDeadBehaviourCommand", "Run",
                    typeof(RespawningPlayers), nameof(RespawningPlayers.Run), HarmonyPatchType.Prefix);
                    
                PatchMethod("Landfall.Network.GameModes.BattleRoyaleGameMode", "HandlePlayerDead",
                    typeof(RespawningPlayers), nameof(RespawningPlayers.HandlePlayerDead), HarmonyPatchType.Prefix);
                    
                PatchMethod("Landfall.Network.RingDeathCommand", "Run",
                    typeof(RespawningPlayers), nameof(RespawningPlayers.RingDeathRun), HarmonyPatchType.Prefix);
                
                // Control Drop patches
                PatchMethod("Landfall.Network.DropAllLootCommand", "Run",
                    typeof(ControlDrop), nameof(ControlDrop.DropAllLootCommandRun), HarmonyPatchType.Prefix);
                    
                PatchMethod("Landfall.Network.ItemDropCommand", "Run",
                    typeof(ControlDrop), nameof(ControlDrop.ItemDropCommandRun), HarmonyPatchType.Prefix);
                    
                PatchMethod("Landfall.Network.ItemThrownCommand", "Run",
                    typeof(ControlDrop), nameof(ControlDrop.ItemThrownCommandRun), HarmonyPatchType.Prefix);
                
                // Lobby Spawn Controller patch
                PatchMethod("Landfall.Network.GameModes.TABGBaseGameMode", "GetNewSpawnPoint",
                    typeof(LobbySpawnController), nameof(LobbySpawnController.GetNewSpawnPoint), HarmonyPatchType.Prefix);
                
                // Vote To Start patch
                PatchMethod("Landfall.Network.ChatCommand", "Run",
                    typeof(VoteToStart), nameof(VoteToStart.ChatCommandPostfix), HarmonyPatchType.Postfix);
                
                // Match Timeout patch
                PatchMethod("Landfall.Network.GameModes.TABGBaseGameMode", "Update",
                    typeof(MatchTimeout), nameof(MatchTimeout.Run), HarmonyPatchType.Postfix);
                
                // Spell Drop Controller patch
                PatchMethod("Spelldrop_Server", "Start",
                    typeof(SpellDropController), "Start", HarmonyPatchType.Postfix);
                
                // Win Conditions patch
                PatchMethod("Landfall.Network.GameModes.TABGBaseGameMode", "CheckGameState",
                    typeof(WinConditions), nameof(WinConditions.CheckGameState), HarmonyPatchType.Prefix);
                
                // GameRoom patches for state changes
                PatchMethod("Landfall.Network.GameRoom", "StartBattle",
                    typeof(Plugin), nameof(OnGameStart), HarmonyPatchType.Postfix);
                    
                PatchMethod("Landfall.Network.GameRoom", "EndMatch",
                    typeof(Plugin), nameof(OnGameEnd), HarmonyPatchType.Postfix);
                
                Logger.LogInfo("All StarterPack patches applied successfully!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply patches: {ex}");
            }
        }
        
        private void PatchMethod(string typeName, string methodName, Type patchClass, string patchMethod, HarmonyPatchType patchType)
        {
            try
            {
                // Try to find the type first without assembly specification
                Type targetType = Type.GetType(typeName);
                
                // If not found, try with Assembly-CSharp
                if (targetType == null)
                {
                    targetType = Type.GetType($"{typeName}, Assembly-CSharp");
                }
                
                if (targetType == null)
                {
                    Logger.LogWarning($"Could not find type: {typeName}");
                    return;
                }
                
                MethodInfo original = targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (original == null)
                {
                    Logger.LogWarning($"Could not find method: {typeName}.{methodName}");
                    return;
                }
                
                MethodInfo patch = patchClass.GetMethod(patchMethod, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (patch == null)
                {
                    Logger.LogWarning($"Could not find patch method: {patchClass.Name}.{patchMethod}");
                    return;
                }
                
                switch (patchType)
                {
                    case HarmonyPatchType.Prefix:
                        harmony.Patch(original, prefix: new HarmonyMethod(patch));
                        break;
                    case HarmonyPatchType.Postfix:
                        harmony.Patch(original, postfix: new HarmonyMethod(patch));
                        break;
                    case HarmonyPatchType.Transpiler:
                        harmony.Patch(original, transpiler: new HarmonyMethod(patch));
                        break;
                }
                
                Logger.LogDebug($"Patched {typeName}.{methodName} with {patchClass.Name}.{patchMethod}");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to patch {typeName}.{methodName}: {ex.Message}");
            }
        }
        
        // Game state callbacks
        private static void OnGameStart(object __instance)
        {
            try
            {
                MatchTimeout.ResetTimer();
                VoteToStart.Reset();
                Log?.LogInfo("Game started - resetting timers and vote counts");
            }
            catch (Exception ex)
            {
                Log?.LogError($"Error in OnGameStart: {ex}");
            }
        }
        
        private static void OnGameEnd(object __instance)
        {
            try
            {
                MatchTimeout.ResetTimer();
                VoteToStart.Reset();
                Log?.LogInfo("Game ended - resetting timers and vote counts");
            }
            catch (Exception ex)
            {
                Log?.LogError($"Error in OnGameEnd: {ex}");
            }
        }
        
        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
} 
