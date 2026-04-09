namespace TabgInstaller.Core
{
    /// <summary>
    /// Shared progress estimation logic used by both <c>InstallerPanel</c> and
    /// <c>SetupWizardWindow</c>.  Keeps the mapping from log messages to progress
    /// percentages in one place so changes to log text don't silently break one
    /// panel while the other still works.
    /// </summary>
    public static class ProgressEstimator
    {
        /// <summary>
        /// Estimate installation progress (0-100) from a log message.
        /// Returns -1 if the message doesn't match any known milestone.
        /// Order matters: most specific patterns are checked first.
        /// </summary>
        public static int Estimate(string message)
        {
            if (string.IsNullOrEmpty(message)) return -1;

            // --- Completion (check before generic "complete") ---
            if (message.Contains("Installation complete")) return 100;

            // --- Late stages ---
            if (message.Contains("PlayerPerms.json")) return 95;
            if (message.Contains("ExtraSettings.json")) return 90;
            if (message.Contains("TheStarterPack.txt")) return 85;
            if (message.Contains("StarterPackSetup")) return 80;
            if (message.Contains("StarterPack.dll")) return 75;

            // --- Plugin installation ---
            if (message.Contains("bundled plugins copied")) return 65;
            if (message.Contains("Copying bundled plugin") || message.Contains("bundled plugin")) return 60;
            if (message.Contains("Citruslib.dll")) return 55;
            if (message.Contains("Downloading Citruslib") || message.Contains("Installing Citruslib")) return 45;

            // --- BepInEx ---
            if (message.Contains("doorstop")) return 38;
            if (message.Contains("BepInEx installed") || message.Contains("Extracting BepInEx")) return 32;
            if (message.Contains("Downloading BepInEx") || message.Contains("Installing BepInEx")) return 20;

            // --- Early stages ---
            if (message.Contains("game_settings.txt")) return 12;
            if (message.Contains("VanillaFiles.txt")) return 8;
            if (message.Contains("Killing running server")) return 3;
            if (message.Contains("Creating backup")) return 1;

            return -1;
        }

        /// <summary>
        /// Returns a human-readable stage name for the given progress percentage.
        /// </summary>
        public static string GetStageName(int percent) => percent switch
        {
            <= 5 => "Preparing...",
            <= 30 => "Installing BepInEx...",
            <= 55 => "Installing plugins...",
            <= 75 => "Configuring StarterPack...",
            <= 95 => "Finalizing...",
            _ => "Complete!"
        };
    }
}
