using System.Text.Json;
using Prism.Core.Actions;

namespace Prism.Content;

public static class CraftingRuleRuntimeCompiler
{
    public static CraftingRules Compile(CompiledContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        var parentByTag = pack.Tags.ToDictionary(tag => tag.Id, tag => tag.Parent, StringComparer.Ordinal);
        var tagsByItem = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var item in pack.Items)
        {
            var itemTags = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in item.Tags)
            {
                var current = tag;
                while (current is not null && itemTags.Add(current))
                {
                    current = parentByTag.GetValueOrDefault(current);
                }
            }

            tagsByItem.Add(item.Id, itemTags);
        }

        var itemRules = pack.Items.Select(item => new ActionItemRule(
            pack.Registries.Items.GetId(item.Id),
            item.MaxStack,
            item.ToolTier,
            Durability: item.Durability,
            WearPerAction: item.UsePerAction,
            ShelfLifeTicks: item.ShelfLifeTicks));
        var recipeRules = pack.Recipes.Select(recipe =>
        {
            ushort? stationMaterialId = null;
            if (!string.IsNullOrWhiteSpace(recipe.Station))
            {
                if (!pack.Registries.Materials.TryGetId(recipe.Station, out var resolvedStation))
                {
                    throw new InvalidOperationException(
                        $"Recipe '{recipe.Id}' references unknown station material '{recipe.Station}'.");
                }

                stationMaterialId = resolvedStation;
            }

            ushort? toolItemTypeId = null;
            if (!string.IsNullOrWhiteSpace(recipe.Tool))
            {
                if (!pack.Registries.Items.TryGetId(recipe.Tool, out var resolvedTool))
                {
                    throw new InvalidOperationException(
                        $"Recipe '{recipe.Id}' references unknown tool item '{recipe.Tool}'.");
                }

                toolItemTypeId = resolvedTool;
            }

            var inputs = recipe.Inputs.Select(input =>
            {
                var acceptedItemIds = input.Item is { } exactItem
                    ? [pack.Registries.Items.GetId(exactItem)]
                    : pack.Items
                        .Where(item => tagsByItem[item.Id].Contains(input.Tag!))
                        .Select(item => pack.Registries.Items.GetId(item.Id))
                        .Order()
                        .ToArray();
                return new CraftingIngredientRule(acceptedItemIds, input.Quantity);
            }).ToArray();
            var outputs = recipe.Outputs.Select(product =>
                new CraftingProductRule(
                    pack.Registries.Items.GetId(product.Item),
                    product.Quantity)).ToArray();
            var byProducts = (recipe.ByProducts ?? []).Select(product =>
                new CraftingProductRule(
                    pack.Registries.Items.GetId(product.Item),
                    product.Quantity)).ToArray();

            return new CraftingRecipeRule(
                pack.Registries.Recipes.GetId(recipe.Id),
                recipe.Id,
                inputs,
                outputs,
                byProducts,
                stationMaterialId,
                toolItemTypeId,
                recipe.DurationTicks,
                recipe.MinMastery,
                recipe.MasteryCategory!);
        });

        return new CraftingRules(
            recipeRules,
            itemRules,
            ReadMasterySpeedupPermille(pack.Parameters));
    }

    private static int ReadMasterySpeedupPermille(JsonElement? parameters)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty("mastery", out var mastery))
        {
            return 500;
        }

        if (mastery.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("The compiled mastery parameter must be an object.");
        }

        if (!mastery.TryGetProperty("maxSpeedupPermille", out var value))
        {
            return 500;
        }

        if (!value.TryGetInt32(out var speedupPermille) ||
            speedupPermille is < 0 or > 1000)
        {
            throw new InvalidOperationException(
                "The compiled mastery.maxSpeedupPermille parameter must be between 0 and 1000.");
        }

        return speedupPermille;
    }
}
