using Prism.Core.Actions;

namespace Prism.Content;

public static class CropActionRuleRuntimeCompiler
{
    public static CropActionRules Compile(
        CompiledContentPack pack,
        string farmlandMaterialId = "farmland",
        string hoeTagId = "hoe")
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(farmlandMaterialId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hoeTagId);

        if (!pack.Registries.Materials.TryGetId(farmlandMaterialId, out var farmlandId))
        {
            throw new InvalidOperationException(
                $"Crop action rules require farmland material '{farmlandMaterialId}'.");
        }

        var growthRules = GrowthRuleRuntimeCompiler.Compile(pack);
        var cropRules = pack.Materials
            .Where(material => growthRules.ContainsKey(pack.Registries.Materials.GetId(material.Id)))
            .OrderBy(material => material.Id, StringComparer.Ordinal)
            .Select(material =>
            {
                var cropMaterialId = pack.Registries.Materials.GetId(material.Id);
                var growthRule = growthRules[cropMaterialId];
                ushort? seedItemId = null;
                if (!string.IsNullOrWhiteSpace(material.SeedItem))
                {
                    if (!pack.Registries.Items.TryGetId(material.SeedItem, out var resolvedSeed))
                    {
                        throw new InvalidOperationException(
                            $"Growing material '{material.Id}' references unknown seed item '{material.SeedItem}'.");
                    }

                    seedItemId = resolvedSeed;
                }

                var harvestDrops = material.Drops.Select(drop =>
                    new ActionDropRule(
                        pack.Registries.Items.GetId(drop.Item),
                        drop.Quantity.Min,
                        drop.Quantity.Max,
                        drop.ChancePermille,
                        drop.WhenState)).ToArray();
                return new CropActionRule(growthRule, seedItemId, harvestDrops);
            }).ToArray();

        var items = pack.Items.Select(item => new ActionItemRule(
            pack.Registries.Items.GetId(item.Id),
            item.MaxStack,
            item.ToolTier,
            Durability: item.Durability,
            WearPerAction: item.UsePerAction,
            ShelfLifeTicks: item.ShelfLifeTicks)).ToArray();
        var parentByTag = pack.Tags.ToDictionary(tag => tag.Id, tag => tag.Parent, StringComparer.Ordinal);
        var hoeItemId = pack.Items
            .Where(item => HasTagOrAncestor(item.Tags, hoeTagId, parentByTag))
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => (ushort?)pack.Registries.Items.GetId(item.Id))
            .FirstOrDefault();

        return new CropActionRules(
            cropRules,
            pack.Materials
                .Where(material => material.Tillable)
                .Select(material => pack.Registries.Materials.GetId(material.Id)),
            farmlandId,
            hoeItemId,
            items);
    }

    private static bool HasTagOrAncestor(
        IEnumerable<string> itemTags,
        string requiredTag,
        IReadOnlyDictionary<string, string?> parentByTag)
    {
        foreach (var itemTag in itemTags)
        {
            var current = itemTag;
            while (current is not null)
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
