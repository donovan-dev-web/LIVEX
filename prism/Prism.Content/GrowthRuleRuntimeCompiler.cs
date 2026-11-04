using System.Collections.ObjectModel;
using Prism.Core.Time;

namespace Prism.Content;

public static class GrowthRuleRuntimeCompiler
{
    public static IReadOnlyDictionary<ushort, CropGrowthRule> Compile(
        CompiledContentPack pack,
        string waterMaterialId = "water")
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(waterMaterialId);
        if (!pack.Registries.Materials.TryGetId(waterMaterialId, out var waterId))
        {
            throw new InvalidOperationException(
                $"Growth rules require water material '{waterMaterialId}' in the compiled pack.");
        }

        var growthById = pack.GrowthRules.ToDictionary(rule => rule.Id, StringComparer.Ordinal);
        var runtimeRules = new Dictionary<ushort, CropGrowthRule>();
        foreach (var material in pack.Materials.OrderBy(material => material.Id, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(material.Growth))
            {
                continue;
            }

            if (material.Requires is not { Below: { Length: > 0 } below, WaterWithin: { } waterRadius })
            {
                throw new InvalidOperationException(
                    $"Growing material '{material.Id}' requires a supporting material and water radius.");
            }

            if (!growthById.TryGetValue(material.Growth, out var growth))
            {
                throw new InvalidOperationException(
                    $"Growing material '{material.Id}' references missing growth rule '{material.Growth}'.");
            }

            if (!pack.Registries.Materials.TryGetId(below, out var farmlandId))
            {
                throw new InvalidOperationException(
                    $"Growing material '{material.Id}' requires missing support material '{below}'.");
            }

            var factors = growth.SeasonFactorsPermille;
            var runtimeRule = new CropGrowthRule(
                pack.Registries.Materials.GetId(material.Id),
                farmlandId,
                waterId,
                growth.Stages,
                growth.StageTicks,
                growth.JitterTicks,
                growth.WitherAfterTicks,
                waterRadius,
                GetSeasonFactor(factors, "spring", growth.Id),
                GetSeasonFactor(factors, "summer", growth.Id),
                GetSeasonFactor(factors, "autumn", growth.Id),
                GetSeasonFactor(factors, "winter", growth.Id));
            runtimeRule.Validate();
            runtimeRules.Add(runtimeRule.CropMaterialId, runtimeRule);
        }

        return new ReadOnlyDictionary<ushort, CropGrowthRule>(runtimeRules);
    }

    private static int GetSeasonFactor(
        IReadOnlyDictionary<string, int>? factors,
        string season,
        string growthRuleId)
    {
        if (factors is null || !factors.TryGetValue(season, out var factor))
        {
            throw new InvalidOperationException(
                $"Growth rule '{growthRuleId}' is missing the '{season}' season factor.");
        }

        return factor;
    }
}
