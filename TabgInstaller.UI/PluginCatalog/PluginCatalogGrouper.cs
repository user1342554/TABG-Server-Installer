using System;
using System.Collections.Generic;
using System.Linq;
using TabgInstaller.Core;

namespace TabgInstaller.UI.PluginCatalog;

public sealed record PluginCatalogGroup(
    PluginDefinition Primary,
    IReadOnlyList<PluginDefinition> Definitions,
    string Label,
    string[] DllNames);

public static class PluginCatalogGrouper
{
    public static IReadOnlyList<PluginCatalogGroup> Collapse(IEnumerable<PluginDefinition> definitions)
    {
        return definitions
            .GroupBy(GetPluginDllKey, StringComparer.OrdinalIgnoreCase)
            .Select(CreateGroup)
            .ToArray();
    }

    public static string[] GetCatalogDllNames(IReadOnlyList<PluginDefinition> definitions)
    {
        return definitions
            .SelectMany(definition => definition.DllNames ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(dll => dll, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static PluginCatalogGroup CreateGroup(IGrouping<string, PluginDefinition> group)
    {
        var definitions = group.ToArray();
        var primary = definitions[0];
        var dlls = GetCatalogDllNames(definitions);
        return new PluginCatalogGroup(primary, definitions, BuildLabel(definitions, primary, dlls), dlls);
    }

    private static string BuildLabel(IReadOnlyList<PluginDefinition> definitions, PluginDefinition primary, string[] dlls)
    {
        if (definitions.Count == 1)
            return primary.Label;

        var names = definitions
            .Select(definition => SplitLabel(definition.Label).Name)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var suffix = dlls.Length == 0
            ? primary.Id
            : string.Join(", ", dlls);

        return $"{string.Join(" / ", names)} - {suffix}";
    }

    private static string GetPluginDllKey(PluginDefinition definition)
    {
        var dlls = definition.DllNames ?? Array.Empty<string>();
        return dlls.Length == 0
            ? "id:" + definition.Id
            : "dll:" + string.Join("|", dlls.OrderBy(dll => dll, StringComparer.OrdinalIgnoreCase));
    }

    private static (string Name, string Description) SplitLabel(string label)
    {
        const string marker = " - ";
        var index = label.IndexOf(marker, StringComparison.Ordinal);
        return index < 0
            ? (label, "")
            : (label[..index], label[(index + marker.Length)..]);
    }
}
