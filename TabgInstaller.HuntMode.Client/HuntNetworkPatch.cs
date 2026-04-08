using System;
using HarmonyLib;

namespace TabgInstaller.HuntMode.Client
{
    /// <summary>
    /// Attempts to hook into the game's network event dispatcher to intercept
    /// Hunt Mode custom event codes 73-80. Tries several known method signatures.
    /// If none are found, logs a warning — the HUD will not receive server data.
    /// </summary>
    public static class HuntNetworkPatch
    {
        private static bool _patched = false;

        public static void ApplyManualPatch(Harmony harmony)
        {
            if (_patched) return;

            // Attempt 1: ServerClient.HandleEvent
            if (TryPatch(harmony, "ServerClient", "HandleEvent")) return;

            // Attempt 2: NetworkPlayer.OnEvent
            if (TryPatch(harmony, "NetworkPlayer", "OnEvent")) return;

            // Attempt 3: GameEventHandler.HandleEvent
            if (TryPatch(harmony, "GameEventHandler", "HandleEvent")) return;

            // Attempt 4: PhotonEventHandler.OnEvent
            if (TryPatch(harmony, "PhotonEventHandler", "OnEvent")) return;

            // Attempt 5: GameNetworkManager.HandleEvent
            if (TryPatch(harmony, "GameNetworkManager", "HandleEvent")) return;

            HuntClientPlugin.Log?.LogWarning(
                "[HuntMode.Client] Could not find a network event handler to patch. " +
                "Hunt Mode HUD will not receive server events. " +
                "Manual hook setup required.");
        }

        private static bool TryPatch(Harmony harmony, string typeName, string methodName)
        {
            try
            {
                var method = AccessTools.Method(typeName + ":" + methodName);
                if (method == null) return false;

                var prefix = typeof(HuntNetworkPatch).GetMethod(
                    nameof(NetworkEventPrefix),
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

                harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                _patched = true;
                HuntClientPlugin.Log?.LogInfo(
                    "[HuntMode.Client] Patched network handler: " + typeName + "." + methodName);
                return true;
            }
            catch (Exception ex)
            {
                HuntClientPlugin.Log?.LogDebug(
                    "[HuntMode.Client] Could not patch " + typeName + "." + methodName + ": " + ex.Message);
                return false;
            }
        }

        // PREFIX — inspects the first byte/code parameter of the event.
        // The exact signature depends on the game; we try to extract eventCode via reflection.
        // Returns true to always run the original method.
        private static bool NetworkEventPrefix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length == 0) return true;

                // Try to extract event code from first argument (byte or int)
                byte eventCode = 0;
                bool gotCode = false;

                if (__args[0] is byte b) { eventCode = b; gotCode = true; }
                else if (__args[0] is int i && i >= 0 && i <= 255) { eventCode = (byte)i; gotCode = true; }
                else if (__args[0] is short s && s >= 0 && s <= 255) { eventCode = (byte)s; gotCode = true; }

                if (!gotCode) return true;
                if (eventCode < 73 || eventCode > 80) return true;

                // Try to extract byte[] data from second argument
                byte[] data = null;
                if (__args.Length > 1)
                {
                    if (__args[1] is byte[] rawBytes)
                    {
                        data = rawBytes;
                    }
                    else
                    {
                        // Some Photon-style events wrap data in a Hashtable or object[]
                        // Try to extract via reflection
                        try
                        {
                            var arg = __args[1];
                            if (arg != null)
                            {
                                var t = arg.GetType();
                                // Check for Hashtable["data"] pattern
                                var indexer = t.GetProperty("Item");
                                if (indexer != null)
                                {
                                    var val = indexer.GetValue(arg, new object[] { (byte)245 });
                                    if (val is byte[] photonBytes)
                                        data = photonBytes;
                                }
                            }
                        }
                        catch (Exception ex) { HuntClientPlugin.Log?.LogDebug($"[HuntMode.Client] Network operation failed: {ex.Message}"); }
                    }
                }

                if (data != null)
                    HuntClientPlugin.HandleHuntEvent(eventCode, data);
            }
            catch (Exception ex) { HuntClientPlugin.Log?.LogDebug($"[HuntMode.Client] Network operation failed: {ex.Message}"); }

            return true; // Always run original
        }
    }
}
