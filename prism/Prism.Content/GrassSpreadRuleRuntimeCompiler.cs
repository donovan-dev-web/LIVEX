using Prism.Core.World;

namespace Prism.Content;

public static class GrassSpreadRuleRuntimeCompiler
{
    public static GrassSpreadRule Compile(
        CompiledContentPack pack,
        string grassMaterialId = "grass",
        string soilMaterialId = "dirt",
        int defaultChancePermille = 1000)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(grassMaterialId);
        ArgumentException.ThrowIfNullOrWhiteSpace(soilMaterialId);
        if (defaultChancePermille is < 0 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultChancePermille));
        }

        if (!pack.Registries.Materials.TryGetId(grassMaterialId, out var grassId))
        {
            throw new InvalidOperationException(
                $"Grass spread requires grass material '{grassMaterialId}' in the compiled pack.");
        }

        if (!pack.Registries.Materials.TryGetId(soilMaterialId, out var soilId))
        {
            throw new InvalidOperationException(
                $"Grass spread requires soil material '{soilMaterialId}' in the compiled pack.");
        }

        var chancePermille = GetChancePermille(pack.Parameters, defaultChancePermille);
        var rule = new GrassSpreadRule(grassId, soilId, chancePermille);
        rule.Validate();
        return rule;
    }

    private static int GetChancePermille(System.Text.Json.JsonElement? parameters, int defaultValue)
    {
        if (parameters is not { ValueKind: System.Text.Json.JsonValueKind.Object } root ||
            !root.TryGetProperty("updates", out var updates) ||
            updates.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !updates.TryGetProperty("grassSpreadChancePermille", out var chance))
        {
            return defaultValue;
        }

        if (!chance.TryGetInt32(out var value))
        {
            throw new InvalidOperationException(
                "Compiled parameter 'updates.grassSpreadChancePermille' must be an integer.");
        }

        return value;
    }
}
