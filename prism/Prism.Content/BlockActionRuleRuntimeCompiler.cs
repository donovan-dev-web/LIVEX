using System.Text.Json;
using Prism.Core.Actions;

namespace Prism.Content;

public static class BlockActionRuleRuntimeCompiler
{
    public static BlockActionRules Compile(CompiledContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        var items = pack.Items.Select(item =>
        {
            ushort? placeMaterialId = null;
            if (!string.IsNullOrWhiteSpace(item.Places))
            {
                placeMaterialId = pack.Registries.Materials.GetId(item.Places);
            }

            return new ActionItemRule(
                pack.Registries.Items.GetId(item.Id),
                item.MaxStack,
                item.ToolTier,
                placeMaterialId,
                item.Durability,
                item.UsePerAction,
                item.ShelfLifeTicks);
        }).ToArray();
        var materials = pack.Materials.Select(material => new MiningMaterialRule(
            pack.Registries.Materials.GetId(material.Id),
            material.Solid,
            material.Hardness,
            material.MinToolTier,
            material.Drops.Select(drop => new ActionDropRule(
                pack.Registries.Items.GetId(drop.Item),
                drop.Quantity.Min,
                drop.Quantity.Max,
                drop.ChancePermille,
                drop.WhenState)).ToArray(),
            Placeable: items.Any(item => item.PlaceMaterialId == pack.Registries.Materials.GetId(material.Id))))
            .ToArray();

        return new BlockActionRules(
            materials,
            items,
            ReadParameter(pack.Parameters, "mining", "baseTicksPerHardness", 10),
            ReadParameter(pack.Parameters, "mastery", "maxSpeedupPermille", 500));
    }

    private static int ReadParameter(
        JsonElement? parameters,
        string groupName,
        string parameterName,
        int defaultValue)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty(groupName, out var group))
        {
            return defaultValue;
        }

        if (group.ValueKind != JsonValueKind.Object ||
            !group.TryGetProperty(parameterName, out var parameter) ||
            !parameter.TryGetInt32(out var value))
        {
            throw new InvalidOperationException(
                $"The compiled parameter '{groupName}.{parameterName}' must be an integer.");
        }

        return value;
    }
}
