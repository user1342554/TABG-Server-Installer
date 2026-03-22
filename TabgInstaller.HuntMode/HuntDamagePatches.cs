using System.Reflection;
using HarmonyLib;
using Landfall.Network;
using Landfall.Network.GameModes;
using UnityEngine;
using TabgInstaller.HuntMode.Shared;

namespace TabgInstaller.HuntMode
{
    /// <summary>
    /// Task 3 — Patch BattleRoyaleGameMode.CanApplyDamage.
    ///
    /// Rules:
    ///   - Match not active → let original run.
    ///   - Self-damage (killer hitting themselves): halve fall damage via overrideHealth, allow.
    ///   - Same group (survivor hits survivor or killer hits killer): block entirely.
    ///   - Cross-team: allow.
    /// </summary>
    [HarmonyPatch(typeof(BattleRoyaleGameMode), "CanApplyDamage")]
    public static class HuntDamagePatches
    {
        // Reflection cache for TABGPlayerBase.PlayerIndex and GroupIndex
        private static PropertyInfo _playerIndexProp;
        private static PropertyInfo _groupIndexProp;
        private static FieldInfo _healthField;

        static HuntDamagePatches()
        {
            System.Type playerType = AccessTools.TypeByName("TABGPlayerBase");
            if (playerType != null)
            {
                _playerIndexProp = AccessTools.Property(playerType, "PlayerIndex");
                _groupIndexProp  = AccessTools.Property(playerType, "GroupIndex");
                _healthField     = AccessTools.Field(playerType, "Health");
                if (_healthField == null)
                    _healthField = AccessTools.Field(playerType, "health");
            }
        }

        private static byte GetPlayerIndex(object player)
        {
            if (_playerIndexProp != null)
                return (byte)_playerIndexProp.GetValue(player, null);
            return byte.MaxValue;
        }

        private static byte GetGroupIndex(object player)
        {
            if (_groupIndexProp != null)
                return (byte)_groupIndexProp.GetValue(player, null);
            return byte.MaxValue;
        }

        // Harmony Prefix — return false to skip original when we provide a definitive answer.
        //
        // BattleRoyaleGameMode.CanApplyDamage signature (from decompile context):
        //   bool CanApplyDamage(TABGPlayerBase damager, TABGPlayerBase victim, ref float overrideHealth)
        //
        // Harmony injects __instance, __result and the named params as matching names.
        // Because TABGPlayerBase is a MonoBehaviour we cannot reference the type directly —
        // instead we use object parameters with __result ref.
        public static bool Prefix(
            object damager,
            object victim,
            ref float overrideHealth,
            ref bool __result)
        {
            HuntGameState gs = HuntModePlugin.GameState;
            if (gs == null || !gs.MatchActive)
                return true; // let original run

            byte damagerIdx = GetPlayerIndex(damager);
            byte victimIdx  = GetPlayerIndex(victim);
            byte damagerGrp = GetGroupIndex(damager);
            byte victimGrp  = GetGroupIndex(victim);

            // --- Self-damage ---
            if (damagerIdx == victimIdx)
            {
                // If the damager is the killer, reduce fall damage by 50 %.
                if (gs.IsKiller(damagerIdx) && overrideHealth > 0f)
                {
                    overrideHealth *= HuntConstants.KillerFallDamageMultiplier;
                }
                __result = true;
                return false;
            }

            // --- Friendly-fire (same group) ---
            if (damagerGrp == victimGrp)
            {
                __result = false;
                return false;
            }

            // --- Cross-team: allow ---
            __result = true;
            return false;
        }
    }
}
