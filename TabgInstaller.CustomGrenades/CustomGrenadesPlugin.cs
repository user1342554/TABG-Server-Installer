using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TabgInstaller.CustomGrenades
{
    /// <summary>
    /// Runs on BOTH client AND server.
    /// Makes all smoke grenades (ID 208) into big purple smoke grenades.
    /// </summary>
    [BepInPlugin("tabginstaller.customgrenades", "TABG Big Smoke Grenade", "1.0.0")]
    public class CustomGrenadesPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            var harmony = new Harmony("tabginstaller.customgrenades");
            harmony.PatchAll(typeof(SmokePatch));
            Logger.LogInfo("[BigSmoke] Loaded! All smoke grenades are now big purple smoke.");
        }

        [HarmonyPatch(typeof(ParticleSystem), "Play", new Type[] { })]
        internal static class SmokePatch
        {
            static void Postfix(ParticleSystem __instance)
            {
                try
                {
                    var grenade = __instance.GetComponentInParent<Grenade>();
                    if (grenade == null) return;

                    var pickup = __instance.GetComponentInParent<Pickup>();
                    if (pickup == null || pickup.m_itemIndex != 208) return;

                    if (grenade.GetComponent<BigSmokeMarker>() != null) return;

                    foreach (var ps in grenade.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var main = ps.main;
                        main.startColor = new ParticleSystem.MinMaxGradient(
                            new Color(0.3f, 0.0f, 0.5f, 1f),
                            new Color(0.5f, 0.1f, 0.8f, 0.6f));

                        if (main.startSize.mode == ParticleSystemCurveMode.TwoConstants)
                            main.startSize = new ParticleSystem.MinMaxCurve(
                                main.startSize.constantMin * 8f,
                                main.startSize.constantMax * 8f);
                        else if (main.startSize.mode == ParticleSystemCurveMode.Constant)
                            main.startSize = main.startSize.constant * 8f;

                        if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
                            main.startLifetime = new ParticleSystem.MinMaxCurve(
                                main.startLifetime.constantMin * 3f,
                                main.startLifetime.constantMax * 3f);
                        else if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
                            main.startLifetime = main.startLifetime.constant * 3f;

                        var emission = ps.emission;
                        if (emission.rateOverTime.mode == ParticleSystemCurveMode.Constant)
                            emission.rateOverTime = emission.rateOverTime.constant * 3f;
                    }

                    grenade.transform.localScale *= 4f;
                    grenade.gameObject.AddComponent<BigSmokeMarker>();
                }
                catch (Exception ex) { Debug.LogWarning($"[CustomGrenades] Giant smoke creation failed: {ex.Message}"); }
            }
        }
    }

    public class BigSmokeMarker : MonoBehaviour { }
}
