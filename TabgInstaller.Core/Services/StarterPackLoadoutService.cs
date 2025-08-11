using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TabgInstaller.Core.Services
{
    public class StarterPackLoadoutService
    {
        public record Loadout(string Name, int Percent, List<Item> Items);
        public record Item(string Id, int Quantity);

        public string GetStarterPackPath(string serverDir)
        {
            var p1 = Path.Combine(serverDir, "TheStarterPack.txt");
            if (File.Exists(p1)) return p1;
            var p2 = Path.Combine(serverDir, "datapack.txt");
            return p2;
        }

        public List<Loadout> ReadLoadouts(string starterPackPath)
        {
            if (!File.Exists(starterPackPath)) return new List<Loadout>();
            var lines = File.ReadAllLines(starterPackPath);
            var line = lines.FirstOrDefault(l => l.StartsWith("Loadouts=", StringComparison.OrdinalIgnoreCase));
            if (line == null) return new List<Loadout>();
            var value = line.Substring("Loadouts=".Length);
            return ParseLoadoutsValue(value);
        }

        public string BuildLoadoutsValue(IEnumerable<Loadout> loadouts)
        {
            var parts = new List<string>();
            foreach (var lo in loadouts)
            {
                var items = string.Join(',', lo.Items.Select(it => $"{it.Id}:{it.Quantity}"));
                var spaceAndItems = string.IsNullOrWhiteSpace(items) ? string.Empty : " " + items;
                parts.Add($"{lo.Name}:{lo.Percent}%{spaceAndItems}/");
            }
            return string.Concat(parts);
        }

        public List<Loadout> ParseLoadoutsValue(string value)
        {
            var result = new List<Loadout>();
            if (string.IsNullOrWhiteSpace(value)) return result;
            var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var rx = new Regex("^(?<name>[^:]+):(?<pct>\\d+)%\\s*(?<rest>.*)$");
            foreach (var seg in segments)
            {
                var m = rx.Match(seg.Trim());
                if (!m.Success) continue;
                var name = m.Groups["name"].Value.Trim();
                var pct = int.TryParse(m.Groups["pct"].Value, out var p) ? p : 100;
                var rest = m.Groups["rest"].Value.Trim();
                var items = new List<Item>();
                if (!string.IsNullOrWhiteSpace(rest))
                {
                    var pairs = rest.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var pair in pairs)
                    {
                        var parts = pair.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (parts.Length == 2 && int.TryParse(parts[1], out var q))
                        {
                            items.Add(new Item(parts[0], q));
                        }
                    }
                }
                result.Add(new Loadout(name, pct, items));
            }
            return result;
        }
    }
}


