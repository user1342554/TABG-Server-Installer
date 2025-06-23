using System;
using UnityEngine;

namespace TabgInstaller.StarterPack
{
    internal class RingManager
    {
        public static bool GetNewRingPosition(Vector3 startPosition, float lastRingSize, float newCircleSize, object __instance)
        {
            if (Config.chosenRing != null)
            {
                try
                {
                    // Use reflection to access TheRing properties since we don't have the game assembly reference
                    var ringType = __instance.GetType();
                    var currentRingIdField = ringType.GetField("currentRingID");
                    var currentWhiteRingPositionField = ringType.GetField("currentWhiteRingPosition");
                    var currentWhiteSizeField = ringType.GetField("currentWhiteSize");
                    var whiteField = ringType.GetField("white");
                    
                    if (currentRingIdField != null && currentWhiteRingPositionField != null && 
                        currentWhiteSizeField != null && whiteField != null)
                    {
                        int currentRingId = (int)currentRingIdField.GetValue(__instance);
                        
                        if (currentRingId == 0)
                        {
                            currentWhiteRingPositionField.SetValue(__instance, Config.chosenRing.Location);
                        }
                        
                        if (currentRingId < Config.chosenRing.Sizes.Length)
                        {
                            currentWhiteSizeField.SetValue(__instance, Config.chosenRing.Sizes[currentRingId]);
                            
                            var white = whiteField.GetValue(__instance) as GameObject;
                            if (white != null)
                            {
                                white.transform.position = (Vector3)currentWhiteRingPositionField.GetValue(__instance);
                                white.transform.localScale = Vector3.one * Config.chosenRing.Sizes[currentRingId];
                            }
                        }
                        
                        Plugin.Log?.LogInfo($"Ring {currentRingId} set to position {Config.chosenRing.Location} with size {Config.chosenRing.Sizes[currentRingId]}");
                        return false; // Skip original method
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"Error in RingManager.GetNewRingPosition: {ex}");
                }
            }
            
            return true; // Execute original method
        }
    }
} 
