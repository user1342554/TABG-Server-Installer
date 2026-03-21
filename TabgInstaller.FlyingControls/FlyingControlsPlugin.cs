using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TabgInstaller.FlyingControls
{
    [BepInPlugin("tabginstaller.flyingcontrols", "TABG Flying Vehicle Controls", "1.0.0")]
    public class FlyingControlsPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<float> ThrustForce;
        internal static ConfigEntry<float> TurnForce;
        internal static ConfigEntry<float> LiftForce;
        internal static ConfigEntry<float> HoverForce;
        internal static ConfigEntry<float> DescentForce;
        internal static ConfigEntry<float> MaxSpeed;
        internal static ConfigEntry<float> Stabilization;
        internal static ConfigEntry<KeyCode> LiftKey;
        internal static ConfigEntry<KeyCode> DescendKey;

        private static readonly HashSet<string> FlyingVehicleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Heli", "Ufo", "Hover_Bike", "Hover_Car"
        };

        private static readonly Dictionary<int, bool> FlyingCache = new Dictionary<int, bool>();
        private static readonly HashSet<int> DisabledHoverRaycasters = new HashSet<int>();

        private void Awake()
        {
            LiftKey = Config.Bind("Keybinds", "Ascend", KeyCode.Space, "Key to fly upward");
            DescendKey = Config.Bind("Keybinds", "Descend", KeyCode.LeftControl, "Key to fly downward");

            ThrustForce = Config.Bind("Physics", "ThrustForce", 22f, "Forward/backward thrust force");
            TurnForce = Config.Bind("Physics", "TurnForce", 10f, "Turning (yaw) force");
            LiftForce = Config.Bind("Physics", "LiftForce", 22f, "Upward force when pressing ascend key");
            HoverForce = Config.Bind("Physics", "HoverForce", 11f, "Base hover force (counteracts gravity)");
            DescentForce = Config.Bind("Physics", "DescentForce", 8f, "Downward force when pressing descend key");
            MaxSpeed = Config.Bind("Physics", "MaxSpeed", 45f, "Maximum flight speed");
            Stabilization = Config.Bind("Physics", "Stabilization", 8f, "Auto-leveling strength");

            try
            {
                TabgInstaller.ModSettings.ModSettingsUI.Register("Flying Controls", "Ascend Key", "Key to fly upward", LiftKey);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Flying Controls", "Descend Key", "Key to fly downward", DescendKey);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Flying Controls", "Thrust Force", "Forward/backward power", ThrustForce);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Flying Controls", "Turn Force", "Turning speed", TurnForce);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Flying Controls", "Lift Force", "Ascend power", LiftForce);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Flying Controls", "Hover Force", "Base hover", HoverForce);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Flying Controls", "Descent Force", "Descend power", DescentForce);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Flying Controls", "Max Speed", "Speed cap", MaxSpeed);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Flying Controls", "Stabilization", "Auto-leveling", Stabilization);
            }
            catch { }

            var harmony = new Harmony("tabginstaller.flyingcontrols");
            harmony.PatchAll(typeof(FlyingPhysicsPatch));

            Logger.LogInfo("[FlyingControls] Loaded! Ascend=" + LiftKey.Value + ", Descend=" + DescendKey.Value);
        }

        /// <summary>
        /// PREFIX patch — returns false to SKIP the entire original Car.FixedUpdate
        /// for flying vehicles when occupied. This prevents the original wheel-based
        /// physics, anti-gravity, and HoverRaycaster from fighting our flight controls.
        /// </summary>
        [HarmonyPatch(typeof(Car), "FixedUpdate")]
        internal static class FlyingPhysicsPatch
        {
            static bool Prefix(Car __instance)
            {
                try
                {
                    if (!IsFlyingVehicle(__instance)) return true; // Not flying: run original
                    if (!__instance.driverSeat || !__instance.driverSeat.occupant) return true; // No driver: run original

                    // Only take over if WE are in this vehicle
                    bool isOurs = __instance.isSimulatedByMe;
                    if (!isOurs && Player.localPlayer != null)
                    {
                        if (__instance.driverSeat.occupant == Player.localPlayer.transform.root)
                            isOurs = true;
                    }
                    if (!isOurs) return true; // Someone else driving: run original

                    // Disable HoverRaycasters on this vehicle (they fight our controls)
                    int id = __instance.GetInstanceID();
                    if (!DisabledHoverRaycasters.Contains(id))
                    {
                        DisabledHoverRaycasters.Add(id);
                        foreach (var hr in __instance.GetComponentsInChildren<HoverRaycaster>())
                            hr.enabled = false;
                    }

                    var rig = __instance.mainRig;
                    var input = __instance.input;
                    if (rig == null || input == null) return true;

                    // Mark as simulated by us
                    __instance.isSimulatedByMe = true;

                    float forward = input.inputDirection.z;  // W/S
                    float turn = input.inputDirection.x;     // A/D
                    bool goUp = Input.GetKey(LiftKey.Value);
                    bool goDown = Input.GetKey(DescendKey.Value);

                    // === GRAVITY CANCEL + HOVER ===
                    // Exactly cancel gravity (9.81) then add hover
                    rig.AddForce(Vector3.up * (9.81f + HoverForce.Value), ForceMode.Acceleration);

                    // === ASCEND ===
                    if (goUp)
                        rig.AddForce(Vector3.up * LiftForce.Value, ForceMode.Acceleration);

                    // === DESCEND ===
                    if (goDown)
                        rig.AddForce(Vector3.down * DescentForce.Value, ForceMode.Acceleration);

                    // === IDLE: slow descent ===
                    if (!goUp && !goDown && Mathf.Abs(forward) < 0.1f)
                        rig.AddForce(Vector3.down * 2f, ForceMode.Acceleration);

                    // === FORWARD/BACK THRUST ===
                    rig.AddForce(rig.transform.forward * forward * ThrustForce.Value, ForceMode.Acceleration);

                    // Slight pitch when thrusting (visual feedback)
                    if (Mathf.Abs(forward) > 0.1f)
                        rig.AddTorque(rig.transform.right * forward * -1f, ForceMode.Acceleration);

                    // === YAW (TURNING) ===
                    rig.AddTorque(Vector3.up * turn * TurnForce.Value, ForceMode.Acceleration);

                    // === AUTO-STABILIZATION ===
                    // Strong auto-leveling to prevent flipping
                    Vector3 correction = Vector3.Cross(rig.transform.up, Vector3.up);
                    rig.AddTorque(correction * Stabilization.Value, ForceMode.Acceleration);

                    // === DRAG ===
                    rig.velocity *= (1f - 0.01f);        // Linear drag
                    rig.angularVelocity *= (1f - 0.08f); // Angular drag (stop spinning fast)

                    // === SPEED LIMIT ===
                    if (rig.velocity.magnitude > MaxSpeed.Value)
                        rig.velocity = rig.velocity.normalized * MaxSpeed.Value;

                    // Unfreeze if frozen
                    if (__instance.isFrozen)
                        __instance.UnFreeze();

                    return false; // SKIP original Car.FixedUpdate entirely
                }
                catch
                {
                    return true; // On error, run original
                }
            }

            static bool IsFlyingVehicle(Car car)
            {
                if (car == null) return false;
                int id = car.GetInstanceID();
                if (FlyingCache.TryGetValue(id, out bool cached)) return cached;

                string name = car.gameObject.name.Replace("(Clone)", "").Trim();
                bool isFlying = false;
                foreach (var flyName in FlyingVehicleNames)
                    if (name.IndexOf(flyName, StringComparison.OrdinalIgnoreCase) >= 0) { isFlying = true; break; }
                if (!isFlying && car.GetComponentInChildren<Rotor>() != null)
                    isFlying = true;

                FlyingCache[id] = isFlying;
                return isFlying;
            }
        }
    }
}
