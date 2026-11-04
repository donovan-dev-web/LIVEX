using System.Collections.Frozen;
using Prism.Core.Actions;

namespace Prism.Content;

public static class ConsumptionRuleRuntimeCompiler
{
    public static ConsumptionRules Compile(
        CompiledContentPack pack,
        string edibleTagId = "edible",
        string drinkableTagId = "drinkable")
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(edibleTagId);
        ArgumentException.ThrowIfNullOrWhiteSpace(drinkableTagId);

        var effectsById = (pack.Effects ?? []).ToDictionary(
            effect => effect.Id,
            effect => (IReadOnlyDictionary<string, int>)effect.Values.ToFrozenDictionary(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var parentByTag = pack.Tags.ToDictionary(
            tag => tag.Id,
            tag => tag.Parent,
            StringComparer.Ordinal);

        var items = pack.Items
            .Where(item =>
                HasTagOrAncestor(item.Tags, edibleTagId, parentByTag) &&
                !string.IsNullOrWhiteSpace(item.Effects))
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => new ConsumableItemRule(
                pack.Registries.Items.GetId(item.Id),
                GetEffects(item.Effects!, effectsById, $"Item '{item.Id}'")))
            .ToArray();

        var sources = pack.Materials
            .Where(material =>
                HasTagOrAncestor(material.Tags, drinkableTagId, parentByTag) &&
                !string.IsNullOrWhiteSpace(material.Effects))
            .OrderBy(material => material.Id, StringComparer.Ordinal)
            .Select(material => new DrinkSourceRule(
                pack.Registries.Materials.GetId(material.Id),
                GetEffects(material.Effects!, effectsById, $"Material '{material.Id}'")))
            .ToArray();

        return new ConsumptionRules(items, sources);
    }

    private static IReadOnlyDictionary<string, int> GetEffects(
        string effectId,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> effectsById,
        string owner)
    {
        if (!effectsById.TryGetValue(effectId, out var effects))
        {
            throw new InvalidOperationException($"{owner} references unknown effect '{effectId}'.");
        }

        return effects;
    }

    private static bool HasTagOrAncestor(
        IEnumerable<string> tags,
        string requiredTag,
        IReadOnlyDictionary<string, string?> parentByTag)
    {
        foreach (var tag in tags)
        {
            var current = tag;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (current is not null && visited.Add(current))
            {
                if (StringComparer.Ordinal.Equals(current, requiredTag))
                {
                    return true;
                }

                current = parentByTag.GetValueOrDefault(current);
            }
        }

        return false;
    }
}
