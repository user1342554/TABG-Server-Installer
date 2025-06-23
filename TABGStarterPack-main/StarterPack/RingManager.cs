using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace StarterPack
{
    internal class RingManager
    {
        public static bool GetNewRingPosition(Vector3 startPosition, float lastRingSize, float newCircleSize, TheRing __instance)
        {
            if (Config.chosenRing != null)
            {
                if (__instance.currentRingID == 0)
                {
                    __instance.currentWhiteRingPosition = Config.chosenRing.location;
                }
                __instance.currentWhiteSize = Config.chosenRing.sizes[__instance.currentRingID];
                __instance.white.transform.position = __instance.currentWhiteRingPosition;
                __instance.white.transform.localScale = Vector3.one * Config.chosenRing.sizes[__instance.currentRingID];
                return false;
            }
            return true;
        }
    }
}
