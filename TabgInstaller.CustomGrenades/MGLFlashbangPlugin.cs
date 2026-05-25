using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TabgInstaller.CustomGrenades
{
    /// <summary>
    /// CLIENT-SIDE: MGL (ID 203) explosions flashbang nearby players.
    /// Tracks when MGL fires, then flashes on the next explosion within a time window.
    /// </summary>
    [BepInPlugin("tabginstaller.mglflashbang", "TABG MGL Flashbang", "1.0.0")]
    public class MGLFlashbangPlugin : BaseUnityPlugin
    {
        internal static float LastMGLFireTime = -999f;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> TriggerWindowSeconds;
        internal static ConfigEntry<float> RadiusMultiplier;
        internal static ConfigEntry<float> BlindIntensity;
        internal static ConfigEntry<float> BlindDuration;

        private void Awake()
        {
            Enabled = Config.Bind("Flashbang", "Enabled", true, "Enable flashbang behavior for MGL explosions.");
            TriggerWindowSeconds = Config.Bind("Flashbang", "TriggerWindowSeconds", 5f, "Seconds after firing an MGL where the next explosion can flash.");
            RadiusMultiplier = Config.Bind("Flashbang", "RadiusMultiplier", 2f, "Explosion radius multiplier used for flash range.");
            BlindIntensity = Config.Bind("Flashbang", "BlindIntensity", 60f, "Maximum visual effect intensity.");
            BlindDuration = Config.Bind("Flashbang", "BlindDuration", 60f, "Maximum visual effect duration.");

            var harmony = new Harmony("tabginstaller.mglflashbang");

            // Patch Gun.Shoot via reflection (complex parameter types)
            foreach (var method in typeof(Gun).GetMethods())
            {
                if (method.Name == "Shoot" && method.GetParameters().Length > 1)
                {
                    harmony.Patch(method,
                        postfix: new HarmonyMethod(typeof(MGLFlashbangPlugin), nameof(OnGunShoot)));
                    Logger.LogInfo($"[MGLFlashbang] Patched Gun.Shoot ({method.GetParameters().Length} params)");
                    break;
                }
            }

            harmony.PatchAll(typeof(FlashOnExplosion));
            Logger.LogInfo("[MGLFlashbang] Loaded! MGL now shoots flashbangs.");
        }

        public static void OnGunShoot(Gun __instance)
        {
            try
            {
                if (Enabled != null && !Enabled.Value) return;

                var pickup = __instance.GetComponentInParent<Pickup>();
                if (pickup != null && pickup.m_itemIndex == 203)
                    LastMGLFireTime = Time.time;
            }
            catch (Exception ex) { Debug.LogWarning($"[MGLFlashbang] Operation failed: {ex.Message}"); }
        }

        /// <summary>
        /// On any explosion within 5 seconds of MGL fire, flash the player.
        /// </summary>
        [HarmonyPatch(typeof(Explosion), "Explode", typeof(NetworkOwner))]
        internal static class FlashOnExplosion
        {
            static void Postfix(Explosion __instance)
            {
                try
                {
                    if (Enabled != null && !Enabled.Value) return;
                    if (Player.localPlayer == null) return;

                    // Only flash if MGL was fired recently (within 5 seconds)
                    float triggerWindow = Mathf.Max(0f, TriggerWindowSeconds?.Value ?? 5f);
                    if (Time.time - LastMGLFireTime > triggerWindow) return;

                    Vector3 explosionPos = __instance.transform.position;
                    float radius = __instance.radius * Mathf.Max(0.1f, RadiusMultiplier?.Value ?? 2f);

                    Vector3 playerPos = Player.localPlayer.m_hip != null
                        ? Player.localPlayer.m_hip.transform.position
                        : Player.localPlayer.transform.position;

                    float dist = Vector3.Distance(explosionPos, playerPos);
                    if (dist > radius) return;

                    float rangeMult = Mathf.Clamp01((radius - dist) / radius);
                    var vis = Player.localPlayer.GetComponentInChildren<VisualEffects>();
                    if (vis != null)
                        vis.AddVisualEffect(
                            1,
                            rangeMult * Mathf.Max(0f, BlindIntensity?.Value ?? 60f),
                            Mathf.Max(0f, BlindDuration?.Value ?? 60f));
                }
                catch (Exception ex) { Debug.LogWarning($"[MGLFlashbang] Operation failed: {ex.Message}"); }
            }
        }
    }
}
