using System.Text.Json;
using Prism.Core.Perception;

namespace Prism.Content;

/// <summary>
/// Compiles the <c>perception</c> block of the pack parameters into the runtime settings used by
/// <see cref="Prism.Core.World.AgentLifecycleSystem"/>. Without it the host silently used
/// <see cref="PerceptionSettings"/> defaults, so every per-pack calibration was ignored.
/// </summary>
public static class PerceptionSettingsRuntimeCompiler
{
    public static PerceptionSettings Compile(CompiledContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var defaults = new PerceptionSettings();
        if (pack.Parameters is not JsonElement parameters ||
            parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("perception", out var perception) ||
            perception.ValueKind != JsonValueKind.Object)
        {
            return defaults;
        }

        var settings = new PerceptionSettings(
            ReadParameter(perception, "nearRadius", defaults.NearRadius),
            ReadParameter(perception, "midRadiusChunks", defaults.MidRadiusRegions),
            ReadParameter(perception, "maxObservedPerAgent", defaults.MaximumObservedPerAgent));
        settings.Validate();
        return settings;
    }

    private static int ReadParameter(JsonElement section, string name, int fallback)
    {
        if (!section.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        if (!value.TryGetInt32(out var result))
        {
            throw new InvalidOperationException(
                $"The compiled perception.{name} parameter must be an integer.");
        }

        return result;
    }
}
