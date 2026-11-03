using System.Text.Json;

namespace Prism.Contracts;

/// <summary>
/// Validates the minimal, contract-level shape of a command payload before it reaches a world
/// command handler. Handlers keep their domain validation; this guard rejects payloads that
/// cannot possibly satisfy the documented contract with a single <c>invalid_payload</c> reason.
/// </summary>
public static class CommandPayloadValidator
{
    private static readonly string[] CoordinateNames = ["target", "position", "parcel", "source"];

    private static readonly string[] ItemNames = ["item", "itemId", "itemTypeId"];

    private static readonly string[] ItemOrSeedNames = ["seed", "seedItemId", "item", "itemId"];

    private static readonly string[] ContainerNames = ["container", "position", "target"];

    public static bool TryValidate(string kind, JsonElement payload, out string? detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        detail = null;
        if (payload.ValueKind != JsonValueKind.Object)
        {
            detail = "payload must be a JSON object";
            return false;
        }

        var valid = kind switch
        {
            "Incarner" => TryRequireCoordinate(payload, ["position"], out detail),
            "Retirer" => true,
            "Aller" => TryRequireCoordinate(payload, ["target", "position"], out detail),
            "Miner" or "Labourer" or "Recolter" =>
                TryRequireCoordinate(payload, CoordinateNames, out detail),
            "Poser" => TryRequireCoordinate(payload, CoordinateNames, out detail) &&
                       TryRequireId(payload, ItemNames, out detail) &&
                       TryRequireId(payload, ["material", "materialId"], out detail),
            "Fabriquer" => TryRequireId(payload, ["recipe", "recipeId"], out detail),
            "Planter" => TryRequireCoordinate(payload, CoordinateNames, out detail) &&
                         TryRequireId(payload, ItemOrSeedNames, out detail),
            "Consommer" => TryRequireAny(payload, ItemNames, out _) ||
                           TryRequireCoordinate(payload, CoordinateNames, out detail),
            "Transferer" => TryRequireId(payload, ["recipientAgentId", "recipientId"], out detail) &&
                            TryRequireId(payload, ItemNames, out detail) &&
                            TryRequirePositiveInt(payload, "quantity", out detail),
            "Prendre" or "Deposer" =>
                TryRequireCoordinate(payload, ContainerNames, out detail) &&
                TryRequireId(payload, ItemNames, out detail) &&
                TryRequirePositiveInt(payload, "quantity", out detail),
            "DefinirAccesConteneur" =>
                TryRequireCoordinate(payload, ContainerNames, out detail) &&
                TryRequireArray(payload, "rules", out detail),
            "DemarrerChantier" =>
                TryRequireString(payload, "siteId", out detail) &&
                TryRequireString(payload, "templateId", out detail) &&
                TryRequireCoordinate(payload, ["origin", "position"], out detail),
            "Contribuer" => TryRequireString(payload, "siteId", out detail),
            "DefinirAcces" => TryRequireString(payload, "siteId", out detail) &&
                              TryRequireArray(payload, "rules", out detail),
            _ => true,
        };
        return valid;
    }

    private static bool TryRequireCoordinate(
        JsonElement payload,
        IReadOnlyList<string> propertyNames,
        out string? detail)
    {
        detail = null;
        JsonElement value = default;
        var found = false;
        foreach (var name in propertyNames)
        {
            if (payload.TryGetProperty(name, out value))
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            detail = $"missing coordinate '{propertyNames[0]}'";
            return false;
        }

        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("x", out var x) || x.ValueKind != JsonValueKind.Number ||
            !value.TryGetProperty("y", out var y) || y.ValueKind != JsonValueKind.Number ||
            !value.TryGetProperty("z", out var z) || z.ValueKind != JsonValueKind.Number)
        {
            detail = $"coordinate '{propertyNames[0]}' must contain numeric x, y and z";
            return false;
        }

        return true;
    }

    private static bool TryRequireId(
        JsonElement payload,
        IReadOnlyList<string> propertyNames,
        out string? detail)
    {
        detail = null;
        if (TryRequireAny(payload, propertyNames, out var value))
        {
            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.String)
            {
                return true;
            }

            detail = $"identifier '{propertyNames[0]}' must be a string or a number";
            return false;
        }

        detail = $"missing identifier '{propertyNames[0]}'";
        return false;
    }

    private static bool TryRequireAny(
        JsonElement payload,
        IReadOnlyList<string> propertyNames,
        out JsonElement value)
    {
        foreach (var name in propertyNames)
        {
            if (payload.TryGetProperty(name, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryRequirePositiveInt(JsonElement payload, string name, out string? detail)
    {
        detail = null;
        if (!payload.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var parsed))
        {
            detail = $"missing numeric '{name}'";
            return false;
        }

        if (parsed <= 0)
        {
            detail = $"'{name}' must be positive";
            return false;
        }

        return true;
    }

    private static bool TryRequireString(JsonElement payload, string name, out string? detail)
    {
        detail = null;
        if (!payload.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            detail = $"missing string '{name}'";
            return false;
        }

        return true;
    }

    private static bool TryRequireArray(JsonElement payload, string name, out string? detail)
    {
        detail = null;
        if (!payload.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            detail = $"missing array '{name}'";
            return false;
        }

        return true;
    }
}
