// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Prism.Contracts;
using Simulation.Core.Perception;
using Simulation.Core.World;

namespace Simulation.Console.Integration;

/// <summary>
/// Adapte une observation PRISM (<see cref="PrismAgentObservation"/>) en observations
/// SYNE (<see cref="Observation"/>), pour que la boucle de cognition consomme l'état
/// perçu par PRISM plutôt que le monde local (SYNE-210, étape 1 de P9).
/// </summary>
/// <remarks>
/// Mappage initial, sujet à calibrage :
///   - Bloc (Block)                 → obstacle (position cellule, rayon 0,5)
///   - Objet au sol (GroundItem)    → node (label = type d'item, quantité courante)
///   - Agent (Agent)                → entity (position, confiance 1,0)
///   - Site de chantier (ConstructionSite) → node (label = siteId, quantité = travail achevé)
///   - Résumé de région (RegionSummary) → ignoré (information agrégée, pas un fait local)
/// Seules les entrées Appeared/Changed sont émises ; une entrée Disappeared laisse la
/// croyance correspondante expirer naturellement (pas d'observation négative).
/// </remarks>
public static class PrismObservationMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<Observation> ToObservations(PrismAgentObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (observation.Changes.ValueKind != JsonValueKind.Object ||
            !observation.Changes.TryGetProperty("changes", out var changes) ||
            changes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<Observation>(changes.GetArrayLength());
        foreach (var change in changes.EnumerateArray())
        {
            if (change.ValueKind != JsonValueKind.Object ||
                !change.TryGetProperty("change", out var changeKind) ||
                changeKind.ValueKind != JsonValueKind.String ||
                !change.TryGetProperty("entry", out var entry) ||
                entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var kind = changeKind.GetString();
            if (kind is not ("Appeared" or "Changed"))
            {
                continue;
            }

            var mapped = MapEntry(entry, observation.Tick);
            if (mapped is not null)
            {
                result.Add(mapped);
            }
        }

        return result;
    }

    private static Observation? MapEntry(JsonElement entry, long tick)
    {
        if (!entry.TryGetProperty("kind", out var kindElement) ||
            kindElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var kind = kindElement.GetString();
        if (!TryReadInt(entry, "x", out var x) ||
            !TryReadInt(entry, "y", out var y))
        {
            return null;
        }

        var position = new Position(x, y);
        var entityId = entry.TryGetProperty("id", out var idElement) && idElement.TryGetUInt64(out var id)
            ? id
            : 0UL;

        return kind switch
        {
            "Block" => new Observation(
                entityId == 0 ? DeterministicId($"block:{x}:{y}") : entityId,
                "obstacle",
                position,
                1.0,
                (ulong)tick,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["z"] = ReadInt(entry, "z").ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["materialId"] = ReadInt(entry, "materialId").ToString(System.Globalization.CultureInfo.InvariantCulture),
                }),
            "GroundItem" => new Observation(
                entityId == 0 ? DeterministicId($"ground:{x}:{y}") : entityId,
                "node",
                position,
                1.0,
                (ulong)tick,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["label"] = ReadInt(entry, "itemTypeId").ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["amount"] = ReadInt(entry, "quantity").ToString(System.Globalization.CultureInfo.InvariantCulture),
                }),
            "Agent" => new Observation(
                entityId,
                "entity",
                position,
                1.0,
                (ulong)tick,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["x"] = x.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["y"] = y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                }),
            "ConstructionSite" => new Observation(
                entityId == 0 ? DeterministicId($"site:{ReadString(entry, "siteId")}") : entityId,
                "node",
                position,
                1.0,
                (ulong)tick,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["label"] = ReadString(entry, "siteId"),
                    ["amount"] = ReadInt(entry, "quantity").ToString(System.Globalization.CultureInfo.InvariantCulture),
                }),
            _ => null,
        };
    }

    private static bool TryReadInt(JsonElement element, string property, out int value)
    {
        value = 0;
        return element.TryGetProperty(property, out var propertyElement) &&
               propertyElement.TryGetInt32(out value);
    }

    private static int ReadInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var propertyElement) && propertyElement.TryGetInt32(out var value)
            ? value
            : 0;

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var propertyElement) && propertyElement.ValueKind == JsonValueKind.String
            ? propertyElement.GetString() ?? string.Empty
            : string.Empty;

    private static ulong DeterministicId(string value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }
}
