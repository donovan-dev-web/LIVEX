using System.Text.Json;
using Prism.Core.World;

namespace Prism.Content;

public static class MovementRuleRuntimeCompiler
{
    public static MovementRules Compile(
        CompiledContentPack pack,
        string waterMaterialId = "water",
        string walkablePathTag = "walkable_path")
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(waterMaterialId);
        ArgumentException.ThrowIfNullOrWhiteSpace(walkablePathTag);
        if (!pack.Registries.Materials.TryGetId(waterMaterialId, out var waterId))
        {
            throw new InvalidOperationException(
                $"Movement rules require water material '{waterMaterialId}' in the compiled pack.");
        }

        var pathSpeedupPermille = ReadPathSpeedupPermille(pack.Parameters);
        var solidMaterials = pack.Materials
            .Where(material => material.Solid)
            .Select(material => pack.Registries.Materials.GetId(material.Id))
            .ToHashSet();
        var pathMaterials = pack.Materials
            .Where(material => material.Tags.Contains(walkablePathTag, StringComparer.Ordinal))
            .Select(material => pack.Registries.Materials.GetId(material.Id))
            .ToHashSet();

        foreach (var template in pack.Templates.Where(template =>
                     StringComparer.Ordinal.Equals(template.Effect?.Type, "path_speed")))
        {
            var minimum = Enumerable.Range(0, 3)
                .Select(axis => Math.Min(template.Effect!.Volume[0][axis], template.Effect.Volume[1][axis]))
                .ToArray();
            var maximum = Enumerable.Range(0, 3)
                .Select(axis => Math.Max(template.Effect!.Volume[0][axis], template.Effect.Volume[1][axis]))
                .ToArray();
            foreach (var block in template.Blocks.Where(block =>
                         Enumerable.Range(0, 3).All(axis =>
                             block.At[axis] >= minimum[axis] && block.At[axis] <= maximum[axis])))
            {
                pathMaterials.Add(pack.Registries.Materials.GetId(block.Block));
            }
        }

        if (!pathMaterials.IsSubsetOf(solidMaterials))
        {
            throw new InvalidOperationException("Path effects must cover only solid materials.");
        }

        return new MovementRules(
            solidMaterials,
            [waterId],
            pathMaterials: pathMaterials,
            pathSpeedupPermille: pathSpeedupPermille);
    }

    private static int ReadPathSpeedupPermille(JsonElement? parameters)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty("movement", out var movement))
        {
            return 2000;
        }

        if (movement.ValueKind != JsonValueKind.Object ||
            !movement.TryGetProperty("pathSpeedupPermille", out var factor) ||
            !factor.TryGetInt32(out var value) ||
            value is < 1000 or > 10000)
        {
            throw new InvalidOperationException(
                "The compiled movement.pathSpeedupPermille parameter must be between 1000 and 10000.");
        }

        return value;
    }
}
