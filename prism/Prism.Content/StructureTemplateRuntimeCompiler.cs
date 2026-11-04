using Prism.Core.World;

namespace Prism.Content;

public static class StructureTemplateRuntimeCompiler
{
    public static IReadOnlyDictionary<string, StructureTemplateRule> Compile(CompiledContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var templates = new SortedDictionary<string, StructureTemplateRule>(StringComparer.Ordinal);
        foreach (var template in pack.Templates)
        {
            var rule = new StructureTemplateRule(
                template.Id,
                template.Size[0],
                template.Size[1],
                template.Size[2],
                template.Blocks
                    .Select(block => new StructureBlockRule(
                        block.At[0],
                        block.At[1],
                        block.At[2],
                        pack.Registries.Materials.GetId(block.Block)))
                    .ToArray(),
                template.Placement.FlatGround,
                template.Placement.ClearVolume,
                template.WorkTicks,
                template.IntegrityThresholdPermille,
                template.Effect is { } effect
                    ? new StructureEffectRule(
                        effect.Type,
                        new BlockCoordinate(
                            Math.Min(effect.Volume[0][0], effect.Volume[1][0]),
                            Math.Min(effect.Volume[0][1], effect.Volume[1][1]),
                            Math.Min(effect.Volume[0][2], effect.Volume[1][2])),
                        new BlockCoordinate(
                            Math.Max(effect.Volume[0][0], effect.Volume[1][0]),
                            Math.Max(effect.Volume[0][1], effect.Volume[1][1]),
                            Math.Max(effect.Volume[0][2], effect.Volume[1][2])),
                        effect.FatigueRecoveryBonusPermille)
                    : null,
                template.Materials
                    .Select(material => new StructureMaterialRule(
                        pack.Registries.Items.GetId(material.Item),
                        material.Quantity))
                    .ToArray());
            if (!templates.TryAdd(rule.Id, rule))
            {
                throw new InvalidOperationException($"Structure template '{rule.Id}' is duplicated.");
            }
        }

        return templates;
    }
}
