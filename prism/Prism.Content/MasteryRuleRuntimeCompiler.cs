using System.Text.Json;
using Prism.Core.Actions;

namespace Prism.Content;

public static class MasteryRuleRuntimeCompiler
{
    public static MasteryRules Compile(CompiledContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return new MasteryRules(
            ReadParameter(pack.Parameters, "gainPerActionPermille", 5),
            ReadParameter(pack.Parameters, "maxSpeedupPermille", 500));
    }

    private static int ReadParameter(JsonElement? parameters, string name, int defaultValue)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty("mastery", out var mastery))
        {
            return defaultValue;
        }

        if (mastery.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("The compiled mastery parameter must be an object.");
        }

        if (!mastery.TryGetProperty(name, out var value))
        {
            return defaultValue;
        }

        if (!value.TryGetInt32(out var result) || result is < 0 or > 1000)
        {
            throw new InvalidOperationException(
                $"The compiled mastery.{name} parameter must be between 0 and 1000.");
        }

        return result;
    }
}
