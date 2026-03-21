using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TabgInstaller.FlyingControls
{
    /// <summary>
    /// CLIENT-SIDE plugin that adds flying controls to cut vehicles.
    ///
    /// The game's Car.FixedUpdate only applies driving forces when wheels touch ground.
    /// Flying vehicles (Heli, UFO, Hover_Bike, Hover_Car) never have grounded wheels,
    /// so they can't be steered. This plugin patches Car.FixedUpdate on the CLIENT
    /// to add aerial controls when driving a flying vehicle.
    ///
    /// Controls:
    ///   W/S     = forward/back thrust
    ///   A/D     = turn left/right (yaw)
    ///   Space   = ascend
    ///   No input = gentle hover with slow descent
    ///   Auto-stabilization prevents flipping
    /// </summary>
    [BepInPlugin("tabginstaller.flyingcontrols", "TABG Flying Vehicle Controls", "1.0.0")]
    public class FlyingControlsPlugin : BaseUnityPlugin
    {
        private static readonly HashSet<string> FlyingVehicleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Heli", "Ufo", "Hover_Bike", "Hover_Car"
        };

        // Cache: remember which Car instances are flying vehicles
        private static readonly Dictionary<int, bool> FlyingCache = new Dictionary<int, bool>();

        private void Awake()
        {
            var harmony = new Harmony("tabginstaller.flyingcontrols");
            harmony.PatchAll(typeof(FlyingPhysicsPatch));
            Logger.LogInfo("[FlyingControls] Client-side flying controls loaded!");
            Logger.LogInfo("[FlyingControls] Controls: W/S = thrust, A/D = turn, Space = ascend");
        }

        [HarmonyPatch(typeof(Car), "FixedUpdate")]
        internal static class FlyingPhysicsPatch
        {
            static void Postfix(Car __instance)
            {
                try
                {
                    if (!IsFlyingVehicle(__instance)) return;
                    if (!__instance.driverSeat || !__instance.driverSeat.occupant) return;

                    // Only apply controls if WE are driving (not other players' vehicles)
                    if (!__instance.isSimulatedByMe && Player.localPlayer != null)
                    {
                        // Check if local player is in this vehicle's driver seat
                        if (__instance.driverSeat.occupant != Player.localPlayer.transform.root)
                            return;
                    }

                    var rig = __instance.mainRig;
                    var input = __instance.input;
                    if (rig == null || input == null) return;

                    float forward = input.inputDirection.z;  // W/S
                    float turn = input.inputDirection.x;     // A/D
                    bool liftUp = input.space;               // Space bar

                    // ── HOVER ──
                    // Base hover force counteracts gravity so the vehicle floats
                    rig.AddForce(Vector3.up * 11f, ForceMode.Acceleration);

                    // ── ASCEND ──
                    if (liftUp)
                        rig.AddForce(Vector3.up * 22f, ForceMode.Acceleration);

                    // ── GENTLE DESCENT ──
                    // When no forward input and not pressing space, sink slowly
                    if (Mathf.Abs(forward) < 0.1f && !liftUp)
                        rig.AddForce(Vector3.down * 4f, ForceMode.Acceleration);

                    // ── FORWARD/BACK THRUST ──
                    // Tilts slightly forward when thrusting for visual feedback
                    rig.AddForce(rig.transform.forward * forward * 22f, ForceMode.Acceleration);
                    if (Mathf.Abs(forward) > 0.1f)
                        rig.AddTorque(rig.transform.right * forward * -1.5f, ForceMode.Acceleration);

                    // ── YAW (TURNING) ──
                    rig.AddTorque(Vector3.up * turn * 10f, ForceMode.Acceleration);

                    // ── AUTO-STABILIZATION ──
                    // Keeps the vehicle level so it doesn't barrel roll
                    Vector3 currentUp = rig.transform.up;
                    Vector3 correction = Vector3.Cross(currentUp, Vector3.up);
                    rig.AddTorque(correction * 6f, ForceMode.Acceleration);

                    // ── AIR DRAG ──
                    rig.velocity *= 0.994f;
                    rig.angularVelocity *= 0.93f;

                    // ── SPEED LIMIT ──
                    float maxSpeed = 45f;
                    if (rig.velocity.magnitude > maxSpeed)
                        rig.velocity = rig.velocity.normalized * maxSpeed;
                }
                catch { }
            }

            static bool IsFlyingVehicle(Car car)
            {
                if (car == null) return false;

                int id = car.GetInstanceID();
                if (FlyingCache.TryGetValue(id, out bool cached))
                    return cached;

                string name = car.gameObject.name.Replace("(Clone)", "").Trim();
                bool isFlying = false;

                foreach (var flyName in FlyingVehicleNames)
                {
                    if (name.IndexOf(flyName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isFlying = true;
                        break;
                    }
                }

                // Also check for Rotor component as indicator
                if (!isFlying && car.GetComponentInChildren<Rotor>() != null)
                    isFlying = true;

                FlyingCache[id] = isFlying;
                return isFlying;
            }
        }
    }
}
