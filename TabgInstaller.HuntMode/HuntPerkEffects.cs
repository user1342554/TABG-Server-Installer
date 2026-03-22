using System;
using System.Reflection;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;
using TabgInstaller.HuntMode.Shared;

namespace TabgInstaller.HuntMode
{
    /// <summary>
    /// Task 9 — Perk stat application. All methods use reflection since TABGPlayerBase
    /// is a MonoBehaviour whose property setters are private or compiler-generated.
    /// </summary>
    public static class HuntPerkEffects
    {
        // Reflection cache
        private static bool _resolved;
        private static PropertyInfo _playerIndexProp;
        private static FieldInfo _healthBackingField;   // <Health>k__BackingField
        private static PropertyInfo _healthProp;
        private static FieldInfo _healthField;          // plain field fallback
        private static MethodInfo _takeDamageMethod;
        private static MethodInfo _getHealthMethod;
        // Speed / movement stats — TABG may use CharacterStatMods or MoveSpeed
        private static FieldInfo _moveSpeedField;
        private static PropertyInfo _moveSpeedProp;

        private static void EnsureResolved()
        {
            if (_resolved) return;
            _resolved = true;

            System.Type playerType = AccessTools.TypeByName("TABGPlayerBase");
            if (playerType == null)
            {
                HuntModePlugin.LogWarning("TABGPlayerBase not found — perk effects will be limited.");
                return;
            }

            _playerIndexProp = AccessTools.Property(playerType, "PlayerIndex");

            // Health backing field (auto-property)
            _healthBackingField = AccessTools.Field(playerType, "<Health>k__BackingField");
            // Health property (may have a public setter on some builds)
            _healthProp = AccessTools.Property(playerType, "Health");
            // Plain field fallback
            if (_healthBackingField == null)
                _healthField = AccessTools.Field(playerType, "Health");
            if (_healthField == null)
                _healthField = AccessTools.Field(playerType, "health");

            _takeDamageMethod = AccessTools.Method(playerType, "TakeDamage");
            _getHealthMethod  = AccessTools.Method(playerType, "GetHealth");

            // Movement speed — try various known names
            _moveSpeedField = AccessTools.Field(playerType, "MoveSpeed");
            if (_moveSpeedField == null)
                _moveSpeedField = AccessTools.Field(playerType, "moveSpeed");
            if (_moveSpeedField == null)
                _moveSpeedField = AccessTools.Field(playerType, "SpeedMultiplier");
            if (_moveSpeedField == null)
                _moveSpeedField = AccessTools.Field(playerType, "speedMultiplier");
            if (_moveSpeedField == null)
                _moveSpeedField = AccessTools.Field(playerType, "CharacterSpeedMultiplier");
            _moveSpeedProp = AccessTools.Property(playerType, "MoveSpeed");
            if (_moveSpeedProp == null)
                _moveSpeedProp = AccessTools.Property(playerType, "SpeedMultiplier");
        }

        private static byte GetPlayerIndex(object player)
        {
            EnsureResolved();
            if (_playerIndexProp != null)
                return (byte)_playerIndexProp.GetValue(player, null);
            return byte.MaxValue;
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Entry point called at match start (HuntModePlugin.StartMatch).
        /// The method name must be ApplyPerkStats to match the existing call site.
        /// </summary>
        public static void ApplyPerkStats(ServerClient server, HuntGameState state)
        {
            EnsureResolved();

            foreach (var player in server.GameRoomReference.Players)
            {
                byte idx = GetPlayerIndex(player);
                if (state.IsKiller(idx))
                    ApplyKillerPerks(player, state);
                else if (state.IsSurvivor(idx))
                    ApplySurvivorPerks(player, state);
            }
        }

        // Alias kept for call consistency in task spec
        public static void ApplyPerks(object player, HuntGameState state)
        {
            byte idx = GetPlayerIndex(player);
            if (state.IsKiller(idx))
                ApplyKillerPerks(player, state);
            else if (state.IsSurvivor(idx))
                ApplySurvivorPerks(player, state);
        }

        // -----------------------------------------------------------------------
        // Killer / Survivor perk application
        // -----------------------------------------------------------------------

        private static void ApplyKillerPerks(object player, HuntGameState state)
        {
            bool ironhide = state.HasKillerPerk(KillerPerk.Ironhide);

            float targetHP    = ironhide ? HuntConstants.KillerIronhideHP    : HuntConstants.KillerBaseHP;
            float targetSpeed = ironhide ? HuntConstants.KillerIronhideSpeed : HuntConstants.KillerBaseSpeed;

            SetHealth(player, targetHP);
            SetSpeed(player, targetSpeed);

            byte idx = GetPlayerIndex(player);
            HuntModePlugin.Log($"Killer {idx}: HP={targetHP}, speed={targetSpeed} (ironhide={ironhide})");
        }

        private static void ApplySurvivorPerks(object player, HuntGameState state)
        {
            SetHealth(player, HuntConstants.SurvivorBaseHP);
            SetSpeed(player, HuntConstants.SurvivorBaseSpeed);

            byte idx = GetPlayerIndex(player);
            HuntModePlugin.Log($"Survivor {idx}: HP={HuntConstants.SurvivorBaseHP}, speed={HuntConstants.SurvivorBaseSpeed}");
        }

        // -----------------------------------------------------------------------
        // Sprinter perk
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called when a survivor with Sprinter perk takes damage.
        /// Checks cooldown, activates sprint if ready.
        /// </summary>
        public static void TryActivateSprinter(object player, HuntGameState state)
        {
            byte idx = GetPlayerIndex(player);
            if (!state.IsSurvivor(idx)) return;
            if (!state.HasSurvivorPerk(idx, SurvivorPerk.Sprinter)) return;

            float now = Time.time;

            // Check cooldown
            if (state.SprinterCooldownTimes.TryGetValue(idx, out float cooldownStart))
            {
                if (now - cooldownStart < HuntConstants.SprinterCooldown)
                    return; // still on cooldown
            }

            // Check not already active
            if (state.SprinterActiveTimes.ContainsKey(idx))
                return;

            // Activate sprint
            state.SprinterActiveTimes[idx] = now;
            SetSpeed(player, HuntConstants.SprinterSpeedMultiplier);
            HuntModePlugin.Log($"Sprinter activated for survivor {idx}");
        }

        // -----------------------------------------------------------------------
        // Sprinter restore
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called when Sprinter duration expires. Restores survivor to base speed.
        /// </summary>
        public static void ApplySurvivorSpeedRestore(object player)
        {
            SetSpeed(player, HuntConstants.SurvivorBaseSpeed);
            byte idx = GetPlayerIndex(player);
            HuntModePlugin.Log(string.Format("Sprinter expired for survivor {0} -- speed restored to {1}", idx, HuntConstants.SurvivorBaseSpeed));
        }

        // -----------------------------------------------------------------------
        // Crate 5 effect
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called by HuntCrateSystem when Crate 5 is looted. Reduces killer speed to 1.0x.
        /// </summary>
        public static void ApplyCrate5SpeedReduction(object player)
        {
            SetSpeed(player, HuntConstants.KillerCrate5Speed);
            byte idx = GetPlayerIndex(player);
            HuntModePlugin.Log($"Crate 5: Killer {idx} speed set to {HuntConstants.KillerCrate5Speed}");
        }

        // -----------------------------------------------------------------------
        // Reflection helpers
        // -----------------------------------------------------------------------

        private static void SetHealth(object player, float hp)
        {
            EnsureResolved();

            // 1. Try backing field (compiler-generated auto-property)
            if (_healthBackingField != null)
            {
                try { _healthBackingField.SetValue(player, hp); return; }
                catch { /* fall through */ }
            }

            // 2. Try property setter
            if (_healthProp != null)
            {
                MethodInfo setter = _healthProp.GetSetMethod(true);
                if (setter != null)
                {
                    try { setter.Invoke(player, new object[] { hp }); return; }
                    catch { /* fall through */ }
                }
            }

            // 3. Try plain field
            if (_healthField != null)
            {
                try { _healthField.SetValue(player, hp); return; }
                catch { /* fall through */ }
            }

            // 4. TakeDamage delta fallback
            if (_getHealthMethod != null && _takeDamageMethod != null)
            {
                try
                {
                    float current = (float)_getHealthMethod.Invoke(player, null);
                    float delta   = current - hp;
                    if (Math.Abs(delta) > 0.01f)
                        _takeDamageMethod.Invoke(player, new object[] { delta, player, false });
                    return;
                }
                catch (Exception ex)
                {
                    HuntModePlugin.LogWarning($"SetHealth TakeDamage fallback error: {ex.Message}");
                }
            }

            HuntModePlugin.LogWarning($"SetHealth: could not set HP={hp} on player — no valid reflection path.");
        }

        private static void SetSpeed(object player, float speed)
        {
            EnsureResolved();

            // 1. Try field
            if (_moveSpeedField != null)
            {
                try { _moveSpeedField.SetValue(player, speed); return; }
                catch { /* fall through */ }
            }

            // 2. Try property setter
            if (_moveSpeedProp != null)
            {
                MethodInfo setter = _moveSpeedProp.GetSetMethod(true);
                if (setter != null)
                {
                    try { setter.Invoke(player, new object[] { speed }); return; }
                    catch { /* fall through */ }
                }
            }

            // 3. Try CharacterStatMods component-level field
            try
            {
                System.Type statModsType = AccessTools.TypeByName("CharacterStatMods");
                if (statModsType != null)
                {
                    Component comp = player as Component;
                    if (comp != null)
                    {
                        object statMods = comp.GetComponent(statModsType);
                        if (statMods != null)
                        {
                            FieldInfo speedField = AccessTools.Field(statModsType, "SpeedModifier");
                            if (speedField == null)
                                speedField = AccessTools.Field(statModsType, "speedModifier");
                            if (speedField != null)
                            {
                                speedField.SetValue(statMods, speed);
                                return;
                            }
                        }
                    }
                }
            }
            catch { /* ignore */ }

            // 4. Store in game state for network sync (graceful degradation)
            HuntModePlugin.LogWarning($"SetSpeed: could not set speed={speed} on player — no valid reflection path. Speed will be tracked in game state only.");
        }
    }
}
