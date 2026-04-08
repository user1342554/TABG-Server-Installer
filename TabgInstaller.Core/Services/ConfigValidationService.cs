using System;
using System.Collections.Generic;
using System.Globalization;

namespace TabgInstaller.Core.Services
{
    /// <summary>
    /// Validates game-server configuration values and returns per-property warning messages.
    /// Operates on plain dictionaries so it stays free of any GUI dependency.
    /// </summary>
    public class ConfigValidationService
    {
        // Each rule targets zero or more property names and may produce warnings keyed by property name.
        private delegate void ValidationRule(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings);

        private static readonly List<ValidationRule> _rules = new()
        {
            RangeViolation,
            KillsToWinNotBrawl,
            RoundsToWinNotBomb,
            BombTimeNotBomb,
            RingSettingsNotBattleRoyale,
            NoRingWithRingTuning,
            MaxPlayersCap,
            PlayersToStartExceedsMax,
            MinPlayersToForceStartExceedsMax,
            ForceStartTimeIgnored,
            GroupsToStartNotBrawl,
            LanWithRelay,
            BotsInBattleRoyale,
        };

        /// <summary>
        /// Validates all current property values and returns a dictionary of property-name to
        /// list-of-warning-strings. Properties with no warnings are omitted from the result.
        /// </summary>
        /// <param name="values">Property name to current value.</param>
        /// <param name="numericMins">Property name to numeric minimum (from NumericMinimum).</param>
        /// <param name="numericMaxes">Property name to numeric maximum (from NumericMaximum).</param>
        public Dictionary<string, List<string>> Validate(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes)
        {
            var warnings = new Dictionary<string, List<string>>();
            try
            {
                foreach (var rule in _rules)
                {
                    try
                    {
                        rule(values, numericMins, numericMaxes, warnings);
                    }
                    catch
                    {
                        // Skip individual rule failures
                    }
                }
            }
            catch
            {
                // Defensive: return whatever we have so far
            }
            return warnings;
        }

        // ──────────────────────────────────────────────
        // Helper methods
        // ──────────────────────────────────────────────

        private static void AddWarning(Dictionary<string, List<string>> warnings, string property, string message)
        {
            if (!warnings.ContainsKey(property))
                warnings[property] = new List<string>();
            warnings[property].Add(message);
        }

        private static string GetString(Dictionary<string, object> values, string key, string fallback = "")
        {
            if (values.TryGetValue(key, out var v) && v != null)
                return v.ToString() ?? fallback;
            return fallback;
        }

        private static double GetDouble(Dictionary<string, object> values, string key, double fallback = 0)
        {
            if (values.TryGetValue(key, out var v))
            {
                if (v is double d) return d;
                if (v is float f) return f;
                if (v is int i) return i;
                if (v is long l) return l;
                if (v is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }
            return fallback;
        }

        private static bool GetBool(Dictionary<string, object> values, string key, bool fallback = false)
        {
            if (values.TryGetValue(key, out var v))
            {
                if (v is bool b) return b;
                if (v is string s && bool.TryParse(s, out var parsed)) return parsed;
            }
            return fallback;
        }

        // ──────────────────────────────────────────────
        // R1: Range violation
        // ──────────────────────────────────────────────
        private static void RangeViolation(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings)
        {
            foreach (var kvp in values)
            {
                var name = kvp.Key;
                var val = GetDouble(values, name, double.NaN);
                if (double.IsNaN(val)) continue;

                numericMins.TryGetValue(name, out var min);
                numericMaxes.TryGetValue(name, out var max);

                // Skip properties that have no meaningful bounds defined
                if (min == 0 && max == double.MaxValue) continue;

                if (val < min || val > max)
                {
                    AddWarning(warnings, name,
                        $"Value {val} is outside the valid range ({min}\u2013{max})");
                }
            }
        }

        // ──────────────────────────────────────────────
        // R2: KillsToWin has no effect unless GameMode is Brawl
        // ──────────────────────────────────────────────
        private static void KillsToWinNotBrawl(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings)
        {
            var killsToWin = GetDouble(values, "KillsToWin", 20);
            var gameMode = GetString(values, "GameMode", "BattleRoyale");
            if (killsToWin != 20 && gameMode != "Brawl")
            {
                AddWarning(warnings, "KillsToWin",
                    "KillsToWin has no effect unless GameMode is Brawl");
            }
        }

        // ──────────────────────────────────────────────
        // R3: RoundsToWin has no effect unless GameMode is Bomb
        // ──────────────────────────────────────────────
        private static void RoundsToWinNotBomb(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings)
        {
            var roundsToWin = GetDouble(values, "RoundsToWin", 3);
            var gameMode = GetString(values, "GameMode", "BattleRoyale");
            if (roundsToWin != 3 && gameMode != "Bomb")
            {
                AddWarning(warnings, "RoundsToWin",
                    "RoundsToWin has no effect unless GameMode is Bomb");
            }
        }

        // ──────────────────────────────────────────────
        // R4: BombTime has no effect unless GameMode is Bomb
        // ──────────────────────────────────────────────
        private static void BombTimeNotBomb(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings)
        {
            var bombTime = GetDouble(values, "BombTime", 30);
            var gameMode = GetString(values, "GameMode", "BattleRoyale");
            if (bombTime != 30 && gameMode != "Bomb")
            {
                AddWarning(warnings, "BombTime",
                    "BombTime has no effect unless GameMode is Bomb");
            }
        }

        // ──────────────────────────────────────────────
        // R5: Ring settings have no effect unless GameMode is BattleRoyale
        // ──────────────────────────────────────────────
        private static void RingSettingsNotBattleRoyale(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings)
        {
            var gameMode = GetString(values, "GameMode", "BattleRoyale");
            if (gameMode == "BattleRoyale") return;

            const string defaultRingSizes = "4240.0,500.0,400.0,200.0,100.0,50.0";
            const string defaultRingSpeeds = "120,120,150,180,240,300";
            const double defaultBaseRingTime = 200.0;
            const double defaultTimeBeforeFirstRing = 70.0;

            var ringSizes = GetString(values, "RingSizes", defaultRingSizes);
            var ringSpeeds = GetString(values, "RingSpeeds", defaultRingSpeeds);
            var baseRingTime = GetDouble(values, "BaseRingTime", defaultBaseRingTime);
            var timeBeforeFirstRing = GetDouble(values, "TimeBeforeFirstRing", defaultTimeBeforeFirstRing);

            bool changed = ringSizes != defaultRingSizes
                        || ringSpeeds != defaultRingSpeeds
                        || baseRingTime != defaultBaseRingTime
                        || timeBeforeFirstRing != defaultTimeBeforeFirstRing;

            if (changed)
            {
                var ringProps = new[] { "RingSizes", "RingSpeeds", "BaseRingTime", "TimeBeforeFirstRing" };
                foreach (var prop in ringProps)
                {
                    if (values.ContainsKey(prop))
                    {
                        AddWarning(warnings, prop,
                            "Ring settings have no effect unless GameMode is BattleRoyale");
                    }
                }
            }
        }

        // ──────────────────────────────────────────────
        // R6: NoRing=true AND ring tuning values differ from default
        // ──────────────────────────────────────────────
        private static void NoRingWithRingTuning(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings)
        {
            if (!GetBool(values, "NoRing", false)) return;

            const string defaultRingSizes = "4240.0,500.0,400.0,200.0,100.0,50.0";
            const string defaultRingSpeeds = "120,120,150,180,240,300";
            const double defaultBaseRingTime = 200.0;
            const double defaultTimeBeforeFirstRing = 70.0;

            var ringSizes = GetString(values, "RingSizes", defaultRingSizes);
            var ringSpeeds = GetString(values, "RingSpeeds", defaultRingSpeeds);
            var baseRingTime = GetDouble(values, "BaseRingTime", defaultBaseRingTime);
            var timeBeforeFirstRing = GetDouble(values, "TimeBeforeFirstRing", defaultTimeBeforeFirstRing);

            bool tuned = ringSizes != defaultRingSizes
                      || ringSpeeds != defaultRingSpeeds
                      || baseRingTime != defaultBaseRingTime
                      || timeBeforeFirstRing != defaultTimeBeforeFirstRing;

            if (tuned)
            {
                var ringProps = new[] { "RingSizes", "RingSpeeds", "BaseRingTime", "TimeBeforeFirstRing" };
                foreach (var prop in ringProps)
                {
                    if (values.ContainsKey(prop))
                    {
                        AddWarning(warnings, prop,
                            "Ring tuning values are ignored when NoRing is enabled");
                    }
                }
            }
        }

        // ──────────────────────────────────────────────
        // R7: MaxPlayers > 253
        // ──────────────────────────────────────────────
        private static void MaxPlayersCap(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings)
        {
            var maxPlayers = GetDouble(values, "MaxPlayers", 70);
            if (maxPlayers > 253)
            {
                AddWarning(warnings, "MaxPlayers",
                    "Server supports a maximum of 253 players");
            }
        }

        // ──────────────────────────────────────────────
        // R8: PlayersToStart > MaxPlayers
        // ──────────────────────────────────────────────
        private static void PlayersToStartExceedsMax(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings)
        {
            var playersToStart = GetDouble(values, "PlayersToStart", 2);
            var maxPlayers = GetDouble(values, "MaxPlayers", 70);
            if (playersToStart > maxPlayers)
            {
                AddWarning(warnings, "PlayersToStart",
                    "PlayersToStart exceeds MaxPlayers \u2014 game will never start");
            }
        }

        // ──────────────────────────────────────────────
        // R9: MinPlayersToForceStart > MaxPlayers
        // ──────────────────────────────────────────────
        private static void MinPlayersToForceStartExceedsMax(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings)
        {
            var minForce = GetDouble(values, "MinPlayersToForceStart", 2);
            var maxPlayers = GetDouble(values, "MaxPlayers", 70);
            if (minForce > maxPlayers)
            {
                AddWarning(warnings, "MinPlayersToForceStart",
                    "MinPlayersToForceStart exceeds MaxPlayers");
            }
        }

        // ──────────────────────────────────────────────
        // R10: ForceStartTime ignored when UseTimedForceStart is false
        // ──────────────────────────────────────────────
        private static void ForceStartTimeIgnored(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings)
        {
            var forceStartTime = GetDouble(values, "ForceStartTime", 200.0);
            var useTimed = GetBool(values, "UseTimedForceStart", true);
            if (forceStartTime != 200.0 && !useTimed)
            {
                AddWarning(warnings, "ForceStartTime",
                    "ForceStartTime is ignored when UseTimedForceStart is disabled");
            }
        }

        // ──────────────────────────────────────────────
        // R11: GroupsToStart has no effect unless GameMode is Brawl
        // ──────────────────────────────────────────────
        private static void GroupsToStartNotBrawl(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings)
        {
            var groupsToStart = GetDouble(values, "GroupsToStart", 10);
            var gameMode = GetString(values, "GameMode", "BattleRoyale");
            if (groupsToStart != 10 && gameMode != "Brawl")
            {
                AddWarning(warnings, "GroupsToStart",
                    "GroupsToStart has no effect unless GameMode is Brawl");
            }
        }

        // ──────────────────────────────────────────────
        // R12: LAN=true AND Relay=true
        // ──────────────────────────────────────────────
        private static void LanWithRelay(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings)
        {
            var lan = GetBool(values, "LAN", false);
            var relay = GetBool(values, "Relay", true);
            if (lan && relay)
            {
                AddWarning(warnings, "Relay",
                    "Relay is typically disabled for LAN servers");
            }
        }

        // ──────────────────────────────────────────────
        // R13: SpawnBots > 0 AND GameMode = BattleRoyale
        // ──────────────────────────────────────────────
        private static void BotsInBattleRoyale(
            Dictionary<string, object> values,
            Dictionary<string, double> numericMins,
            Dictionary<string, double> numericMaxes,
            Dictionary<string, List<string>> warnings)
        {
            var spawnBots = GetDouble(values, "SpawnBots", 0);
            var gameMode = GetString(values, "GameMode", "BattleRoyale");
            if (spawnBots > 0 && gameMode == "BattleRoyale")
            {
                AddWarning(warnings, "SpawnBots",
                    "Bots in BattleRoyale mode may cause instability");
            }
        }
    }
}
