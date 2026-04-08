using System;
using BepInEx;
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

        private void Awake()
        {
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
                    if (Player.localPlayer == null) return;

                    // Only flash if MGL was fired recently (within 5 seconds)
                    if (Time.time - LastMGLFireTime > 5f) return;

                    Vector3 explosionPos = __instance.transform.position;
                    float radius = __instance.radius * 2f;

                    Vector3 playerPos = Player.localPlayer.m_hip != null
                        ? Player.localPlayer.m_hip.transform.position
                        : Player.localPlayer.transform.position;

                    float dist = Vector3.Distance(explosionPos, playerPos);
                    if (dist > radius) return;

                    float rangeMult = Mathf.Clamp01((radius - dist) / radius);
                    var vis = Player.localPlayer.GetComponentInChildren<VisualEffects>();
                    if (vis != null)
                        vis.AddVisualEffect(1, rangeMult * 60f, 60f);
                }
                catch (Exception ex) { Debug.LogWarning($"[MGLFlashbang] Operation failed: {ex.Message}"); }
            }
        }
    }
}
