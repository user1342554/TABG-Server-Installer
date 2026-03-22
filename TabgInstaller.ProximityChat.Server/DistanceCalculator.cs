using UnityEngine;

namespace TabgInstaller.ProximityChat.Server
{
    public static class DistanceCalculator
    {
        public static bool IsInRange(Vector3 senderPos, Vector3 receiverPos, float maxRange)
        {
            return Vector3.Distance(senderPos, receiverPos) <= maxRange;
        }
    }
}
