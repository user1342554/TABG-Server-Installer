using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    /// <summary>
    /// Reads and writes TheStarterPack.txt in key=value text format.
    /// </summary>
    public static class StarterPackConfigService
    {
        public static string GetPath(string serverDir)
        {
            var p1 = Path.Combine(serverDir, "TheStarterPack.txt");
            if (File.Exists(p1)) return p1;
            return p1; // default path even if doesn't exist yet
        }

        public static StarterPackTextSettings Read(string serverDir)
        {
            var path = GetPath(serverDir);
            var settings = new StarterPackTextSettings();
            if (!File.Exists(path)) return settings;

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;
                var idx = line.IndexOf('=');
                if (idx < 1) continue;
                var key = line.Substring(0, idx).Trim();
                var value = line.Substring(idx + 1).Trim();
                dict[key] = value;
            }

            if (dict.TryGetValue("WinCondition", out var wc)) settings.WinCondition = wc;
            if (dict.TryGetValue("KillsToWin", out var ktw) && int.TryParse(ktw, out var ktwVal)) settings.KillsToWin = ktwVal;
            if (dict.TryGetValue("ForceKillAtStart", out var fk)) settings.ForceKillAtStart = ParseBool(fk);
            if (dict.TryGetValue("DropItemsOnDeath", out var did)) settings.DropItemsOnDeath = ParseBool(did);
            if (dict.TryGetValue("ItemsGiven", out var ig)) settings.ItemsGiven = ig;
            if (dict.TryGetValue("Loadouts", out var lo)) settings.Loadouts = lo;
            if (dict.TryGetValue("HealOnKill", out var hok)) settings.HealOnKill = ParseBool(hok);
            if (dict.TryGetValue("HealOnKillAmount", out var hoka) && float.TryParse(hoka, NumberStyles.Float, CultureInfo.InvariantCulture, out var hokaVal)) settings.HealOnKillAmount = hokaVal;
            if (dict.TryGetValue("CanGoDown", out var cgd)) settings.CanGoDown = ParseBool(cgd);
            if (dict.TryGetValue("CanLockOut", out var clo)) settings.CanLockOut = ParseBool(clo);
            if (dict.TryGetValue("ValidSpawnPoints", out var vsp)) settings.ValidSpawnPoints = vsp;
            if (dict.TryGetValue("CustomSpawnPoint", out var csp)) settings.CustomSpawnPoint = csp;
            if (dict.TryGetValue("PercentOfVotes", out var pov) && int.TryParse(pov, out var povVal)) settings.PercentOfVotes = povVal;
            if (dict.TryGetValue("MinNumberOfPlayers", out var mnp) && int.TryParse(mnp, out var mnpVal)) settings.MinNumberOfPlayers = mnpVal;
            if (dict.TryGetValue("TimeToStart", out var tts) && int.TryParse(tts, out var ttsVal)) settings.TimeToStart = ttsVal;
            if (dict.TryGetValue("SpelldropEnabled", out var sde)) settings.SpelldropEnabled = ParseBool(sde);
            if (dict.TryGetValue("MinSpellDropDelay", out var msdd) && int.TryParse(msdd, out var msddVal)) settings.MinSpellDropDelay = msddVal;
            if (dict.TryGetValue("MaxSpellDropDelay", out var mxsdd) && int.TryParse(mxsdd, out var mxsddVal)) settings.MaxSpellDropDelay = mxsddVal;
            if (dict.TryGetValue("SpellDropOffset", out var sdo) && int.TryParse(sdo, out var sdoVal)) settings.SpellDropOffset = sdoVal;
            if (dict.TryGetValue("PreMatchTimeout", out var pmt) && float.TryParse(pmt, NumberStyles.Float, CultureInfo.InvariantCulture, out var pmtVal)) settings.PreMatchTimeout = pmtVal;
            if (dict.TryGetValue("PeriMatchTimeout", out var pmtt) && float.TryParse(pmtt, NumberStyles.Float, CultureInfo.InvariantCulture, out var pmttVal)) settings.PeriMatchTimeout = pmttVal;
            if (dict.TryGetValue("RingSettings", out var rs)) settings.RingSettings = rs;

            return settings;
        }

        public static void Write(string serverDir, StarterPackTextSettings s)
        {
            var path = GetPath(serverDir);
            var lines = new List<string>
            {
                "//Default,KillsToWin,or Debug (wont end unless timer expires)",
                $"WinCondition={s.WinCondition}",
                "//How many kills to win in KillsToWin mode",
                $"KillsToWin={s.KillsToWin?.ToString() ?? ""}",
                "",
                "//Force kill the players out of the trucks",
                $"ForceKillAtStart={s.ForceKillAtStart.ToString().ToLower()}",
                "",
                "//Drop Settings",
                $"DropItemsOnDeath={s.DropItemsOnDeath.ToString().ToLower()}",
                $"ItemsGiven={s.ItemsGiven}",
                "",
                "//Loadouts",
                $"Loadouts={s.Loadouts}",
                "",
                "//Will players be healed when they get a kill",
                $"HealOnKill={s.HealOnKill.ToString().ToLower()}",
                "//What % of HP will people be healed when they get a kill",
                $"HealOnKillAmount={s.HealOnKillAmount.ToString(CultureInfo.InvariantCulture)}",
                "//Will players on a team together go down or just respawn instantly",
                $"CanGoDown={s.CanGoDown.ToString().ToLower()}",
                $"CanLockOut={s.CanLockOut.ToString().ToLower()}",
                "",
                "//0(Tall Work),1(Circle Town),2(Western),3(Containers),4(Chaos),5(Factory),6(Custom)",
                $"ValidSpawnPoints={s.ValidSpawnPoints}",
                "//Custom spawn point coordinates (used when ValidSpawnPoints contains 6)",
                $"CustomSpawnPoint={s.CustomSpawnPoint}",
                "",
                "//Required percent of votes to start",
                $"PercentOfVotes={s.PercentOfVotes}",
                "//Required number of players to start",
                $"MinNumberOfPlayers={s.MinNumberOfPlayers}",
                "//Time after voting for the match to begin",
                $"TimeToStart={s.TimeToStart}",
                "",
                $"SpelldropEnabled={s.SpelldropEnabled.ToString().ToLower()}",
                "//Min time in seconds between drops",
                $"MinSpellDropDelay={s.MinSpellDropDelay}",
                "//Max time in seconds between drops",
                $"MaxSpellDropDelay={s.MaxSpellDropDelay}",
                "//Delay in seconds before first drop spawns",
                $"SpellDropOffset={s.SpellDropOffset}",
                "",
                "//Time in minutes that the PRE LOBBY can last for before forcing a restart",
                $"PreMatchTimeout={s.PreMatchTimeout.ToString(CultureInfo.InvariantCulture)}",
                "//Time in minutes that the MATCH can last for before forcing a restart",
                $"PeriMatchTimeout={s.PeriMatchTimeout.ToString(CultureInfo.InvariantCulture)}"
            };

            File.WriteAllLines(path, lines);
        }

        private static bool ParseBool(string value)
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("True", StringComparison.OrdinalIgnoreCase);
        }
    }
}
