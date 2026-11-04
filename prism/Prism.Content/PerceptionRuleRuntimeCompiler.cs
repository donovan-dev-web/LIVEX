using System.Collections.ObjectModel;
using Prism.Core.Perception;

namespace Prism.Content;

public static class PerceptionRuleRuntimeCompiler
{
    public static PerceptionRules Compile(
        CompiledContentPack pack,
        string airMaterialId = "air",
        string waterMaterialId = "water")
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(airMaterialId);
        ArgumentException.ThrowIfNullOrWhiteSpace(waterMaterialId);
        if (pack.Registries.Tags.Count > sizeof(uint) * 8)
        {
            throw new InvalidOperationException(
                "Perception region summaries support at most 32 registered tags.");
        }

        if (!pack.Registries.Materials.TryGetId(airMaterialId, out var airId))
        {
            throw new InvalidOperationException(
                $"Perception rules require air material '{airMaterialId}' in the compiled pack.");
        }

        if (!pack.Registries.Materials.TryGetId(waterMaterialId, out var waterId))
        {
            throw new InvalidOperationException(
                $"Perception rules require water material '{waterMaterialId}' in the compiled pack.");
        }

        var parentsByTagId = pack.Tags.ToDictionary(
            tag => pack.Registries.Tags.GetId(tag.Id),
            tag => string.IsNullOrWhiteSpace(tag.Parent)
                ? (ushort?)null
                : pack.Registries.Tags.GetId(tag.Parent));
        var materials = new Dictionary<ushort, PerceptionMaterialRule>();
        foreach (var material in pack.Materials.OrderBy(material => material.Id, StringComparer.Ordinal))
        {
            var tagMask = 0u;
            foreach (var tag in material.Tags)
            {
                var tagId = pack.Registries.Tags.GetId(tag);
                var visited = new HashSet<ushort>();
                while (true)
                {
                    if (!visited.Add(tagId))
                    {
                        throw new InvalidOperationException(
                            $"The compiled tag hierarchy contains a cycle at tag '{tag}'.");
                    }

                    tagMask |= 1u << tagId;
                    if (!parentsByTagId.TryGetValue(tagId, out var parentId) || parentId is null)
                    {
                        break;
                    }

                    tagId = parentId.Value;
                }
            }

            var materialId = pack.Registries.Materials.GetId(material.Id);
            materials.Add(
                materialId,
                new PerceptionMaterialRule(
                    material.Solid,
                    materialId == waterId,
                    material.Perceivable,
                    tagMask));
        }

        return new PerceptionRules(new ReadOnlyDictionary<ushort, PerceptionMaterialRule>(materials), airId);
    }
}
