using System.Text.Json;
using Prism.Core.Actions;

namespace Prism.Content;

/// <summary>
/// Compiles the container spoilage calibration (<c>container.decayPermillePerTick</c> and
/// <c>granary.decaySlowdownPermille</c>) from the pack parameters. A missing key keeps its default,
/// so packs written before the storage chain existed keep the historical behaviour of their
/// containers.
/// </summary>
public static class ContainerDecayRuntimeCompiler
{
    public static ContainerDecayRules Compile(CompiledContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var defaults = new ContainerDecayRules();
        if (pack.Parameters is not JsonElement parameters || parameters.ValueKind != JsonValueKind.Object)
        {
            return defaults;
        }

        var compiled = defaults with
        {
            BasePermillePerTick = ReadParameter(
                parameters,
                "container",
                "decayPermillePerTick",
                defaults.BasePermillePerTick),
            StorageSlowdownPermille = ReadParameter(
                parameters,
                "granary",
                "decaySlowdownPermille",
                defaults.StorageSlowdownPermille),
        };
        compiled.Validate();
        return compiled;
    }

    private static int ReadParameter(
        JsonElement parameters,
        string sectionName,
        string name,
        int fallback)
    {
        if (!parameters.TryGetProperty(sectionName, out var section) ||
            section.ValueKind != JsonValueKind.Object ||
            !section.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        if (!value.TryGetInt32(out var result))
        {
            throw new InvalidOperationException(
                $"The compiled {sectionName}.{name} parameter must be an integer.");
        }

        return result;
    }
}
