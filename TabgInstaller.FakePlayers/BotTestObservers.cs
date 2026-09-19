using System;
using System.Collections.Generic;
using Landfall.Network;

namespace TabgInstaller.FakePlayers
{
    internal static class BotTestObservers
    {
        private static readonly HashSet<string> Names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static void Configure(string names)
        {
            Names.Clear();
            foreach(var name in (names??string.Empty).Split(';'))
                if(!string.IsNullOrWhiteSpace(name))Names.Add(name.Trim());
        }

        internal static bool Contains(TABGPlayerServer player)
            =>player!=null && !player.Bot && Names.Contains(player.PlayerName??string.Empty);
    }
}
