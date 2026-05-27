using System;
using System.Collections.Generic;

namespace TabgInstaller.AdminRadar.Server
{
    public static class RadarPrivacy
    {
        public const string HiddenTargetName = "[hidden target]";

        public static string SanitizeBotDebugTargetName(
            string targetName,
            bool includeRealPlayers,
            ISet<string> dummyTargetNames)
        {
            if (string.IsNullOrWhiteSpace(targetName))
                return string.Empty;
            if (includeRealPlayers)
                return targetName;
            if (IsNonIdentifyingTargetMarker(targetName))
                return targetName;
            if (dummyTargetNames != null && dummyTargetNames.Contains(targetName))
                return targetName;

            return HiddenTargetName;
        }

        private static bool IsNonIdentifyingTargetMarker(string targetName)
        {
            return string.Equals(targetName, "last-heard", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetName, "last-seen", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetName, "none", StringComparison.OrdinalIgnoreCase);
        }
    }
}
