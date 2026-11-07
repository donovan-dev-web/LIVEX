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

    /// <summary>Adapte directement une enveloppe d'observation du contrat PRISM.</summary>
    public static IReadOnlyList<Observation> ToObservations(
        ObservationEnvelope envelope,
        PrismResourceSites? resourceSites = null,
        PrismItemCatalog? itemCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return ToObservations(
            new PrismAgentObservation(
                envelope.ProtocolVersion,
                envelope.Tick,
                envelope.AgentId,
                envelope.Changes,
                envelope.Self),
            resourceSites,
            itemCatalog);
    }

    /// <summary>
    /// Adapte une observation d'agent. <paramref name="resourceSites"/> permet de
    /// reconnaître un bloc ressource : il produit alors <b>en plus</b> de l'obstacle
    /// une observation de gisement (<c>resource-&lt;kind&gt;-&lt;n&gt;</c>) sur la case
    /// d'approche — c'est ce qui donne à la cognition une cible (boire, explorer)
    /// et non un mur de plus.
    /// </summary>
    public static IReadOnlyList<Observation> ToObservations(
        PrismAgentObservation observation,
        PrismResourceSites? resourceSites = null,
        PrismItemCatalog? itemCatalog = null)
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
                !TryReadChangeKind(change, out string changeKind) ||
                !change.TryGetProperty("entry", out var entry) ||
                entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (changeKind is not ("Appeared" or "Changed"))
            {
                continue;
            }

            var mapped = MapEntry(entry, observation.Tick, itemCatalog);
            if (mapped is not null)
            {
                result.Add(mapped);
            }

            if (resourceSites is not null && TryMapResourceNode(entry, observation.Tick, resourceSites, out Observation? node))
            {
                result.Add(node!);
            }
        }

        // Effet d'abri éprouvé (SYNE-222) : PRISM le publie dans l'auto-observation,
        // pas dans les changements — l'agent sait qu'il est abrité, et de quel abri.
        if (TryMapStructureUse(observation, out Observation? structure))
        {
            result.Add(structure!);
        }

        return result;
    }

    /// <summary>
    /// Traduit un bloc ressource perçu (eau, bois, minerai) en observation de
    /// gisement : libellé canonique (<c>resource-water-3</c>) et position de la
    /// <b>case d'approche</b>, la seule où l'agent peut se tenir.
    /// </summary>
    private static bool TryMapResourceNode(
        JsonElement entry,
        long tick,
        PrismResourceSites resourceSites,
        out Observation? observation)
    {
        observation = null;
        if (!TryReadEntryKind(entry, out string kind) || kind != "Block" ||
            !TryReadInt(entry, "x", out int x) ||
            !TryReadInt(entry, "y", out int y) ||
            !TryReadInt(entry, "z", out int z) ||
            !resourceSites.TryDescribe(x, y, z, out string label, out Position approach))
        {
            return false;
        }

        observation = new Observation(
            DeterministicId(label),
            "node",
            approach,
            1.0,
            (ulong)tick,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["label"] = label,
                ["amount"] = "1",
            });
        return true;
    }

    /// <summary>
    /// Construit l'observation « structure » d'un agent abrité : identifiant du
    /// site, bonus de récupération de fatigue et position de l'agent (le site est
    /// sous ses pieds, il l'occupe).
    /// </summary>
    private static bool TryMapStructureUse(PrismAgentObservation observation, out Observation? structure)
    {
        structure = null;
        JsonElement self = observation.Self;
        if (self.ValueKind != JsonValueKind.Object ||
            !self.TryGetProperty("sheltered", out JsonElement sheltered) ||
            sheltered.ValueKind != JsonValueKind.True ||
            !self.TryGetProperty("shelterSiteId", out JsonElement siteIdElement) ||
            siteIdElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(siteIdElement.GetString()))
        {
            return false;
        }

        string siteId = siteIdElement.GetString()!;
        if (!PrismWorldSynchronizer.TryReadSelfPosition(self, out Simulation.Core.World.Position position))
        {
            position = default;
        }

        int bonus = self.TryGetProperty("fatigueRecoveryBonusPermille", out JsonElement bonusElement) &&
                    bonusElement.TryGetInt32(out int permille)
            ? permille
            : 0;

        structure = new Observation(
            DeterministicId($"structure:{siteId}"),
            "structure",
            position,
            1.0,
            (ulong)observation.Tick,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["label"] = siteId,
                ["shelterBonusPermille"] = bonus.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
        return true;
    }

    /// <summary>
    /// Identifiants des objets <b>portés</b> par l'agent, lus dans
    /// <c>self.inventory[]</c> de l'observation (étape 3b de SYNE-220).
    /// </summary>
    /// <remarks>
    /// Sans eux, SYNE savait qu'il avait faim mais pas qu'il portait de quoi manger :
    /// `Eat` restait intraduisible et l'agent ramassait de la nourriture sans jamais
    /// la consommer. Les entrées à quantité nulle sont ignorées ; une pile vide ou
    /// absente donne une liste vide (l'inventaire observé fait foi, pas un cache).
    /// </remarks>
    public static IReadOnlyList<ushort> ToCarriedItemIds(ObservationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return ToCarriedItemIds(new PrismAgentObservation(
            envelope.ProtocolVersion,
            envelope.Tick,
            envelope.AgentId,
            envelope.Changes,
            envelope.Self));
    }

    /// <inheritdoc cref="ToCarriedItemIds(ObservationEnvelope)"/>
    public static IReadOnlyList<ushort> ToCarriedItemIds(PrismAgentObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        JsonElement self = observation.Self;
        if (self.ValueKind != JsonValueKind.Object ||
            !self.TryGetProperty("inventory", out JsonElement inventory) ||
            inventory.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var ids = new SortedSet<ushort>();
        foreach (JsonElement stack in inventory.EnumerateArray())
        {
            if (stack.ValueKind != JsonValueKind.Object ||
                !stack.TryGetProperty("itemTypeId", out JsonElement idElement) ||
                !idElement.TryGetUInt16(out ushort itemTypeId) ||
                !stack.TryGetProperty("quantity", out JsonElement quantityElement) ||
                !quantityElement.TryGetInt32(out int quantity) || quantity <= 0)
            {
                continue;
            }

            ids.Add(itemTypeId);
        }

        return ids.ToList();
    }

    /// <summary>
    /// Résumés de région d'une observation PRISM (SYNE-221) : un masque de tags de
    /// ressources exposées par chunk, pour la mémoire spatiale. Les résumés
    /// <c>Disappeared</c> sont ignorés : le chunk n'est pas « effacé », il cesse
    /// simplement d'être confirmé et la décroissance temporelle l'oublie.
    /// </summary>
    public static IReadOnlyList<SpatialChunkObservation> ToSpatialChunks(ObservationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return ToSpatialChunks(new PrismAgentObservation(
            envelope.ProtocolVersion,
            envelope.Tick,
            envelope.AgentId,
            envelope.Changes,
            envelope.Self));
    }

    /// <inheritdoc cref="ToSpatialChunks(ObservationEnvelope)"/>
    public static IReadOnlyList<SpatialChunkObservation> ToSpatialChunks(PrismAgentObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (observation.Changes.ValueKind != JsonValueKind.Object ||
            !observation.Changes.TryGetProperty("changes", out var changes) ||
            changes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<SpatialChunkObservation>();
        foreach (var change in changes.EnumerateArray())
        {
            if (change.ValueKind != JsonValueKind.Object ||
                !TryReadChangeKind(change, out string changeKind) ||
                changeKind is not ("Appeared" or "Changed") ||
                !change.TryGetProperty("entry", out var entry) ||
                entry.ValueKind != JsonValueKind.Object ||
                !TryReadEntryKind(entry, out string entryKind) ||
                entryKind != "RegionSummary" ||
                !TryReadInt(entry, "x", out var chunkX) ||
                !TryReadInt(entry, "y", out var chunkY))
            {
                continue;
            }

            uint tagMask = entry.TryGetProperty("tagMask", out var maskElement) && maskElement.TryGetUInt32(out uint mask)
                ? mask
                : 0u;
            if (tagMask == 0)
            {
                continue;
            }

            result.Add(new SpatialChunkObservation(chunkX, chunkY, tagMask));
        }

        return result;
    }

    /// <summary>
    /// Lit le genre de changement. PRISM sérialise l'enum <b>en nombre</b>
    /// (<c>PerceptionChangeKind</c>), le libellé n'est accepté que pour la
    /// tolérance de test : se fier au seul libellé faisait silencieusement
    /// ignorer toutes les observations réelles.
    /// </summary>
    private static bool TryReadChangeKind(JsonElement change, out string kind)
    {
        kind = string.Empty;
        if (!change.TryGetProperty("change", out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            kind = value.GetString() ?? string.Empty;
            return kind.Length > 0;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int ordinal))
        {
            return false;
        }

        kind = ordinal switch
        {
            0 => "Appeared",
            1 => "Changed",
            2 => "Disappeared",
            _ => string.Empty,
        };
        return kind.Length > 0;
    }

    /// <summary>Lit le genre d'entrée (libellé ou ordinal de <c>PerceptionEntityKind</c>).</summary>
    private static bool TryReadEntryKind(JsonElement entry, out string kind)
    {
        kind = string.Empty;
        if (!entry.TryGetProperty("kind", out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            kind = value.GetString() ?? string.Empty;
            return kind.Length > 0;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int ordinal))
        {
            return false;
        }

        kind = ordinal switch
        {
            0 => "Block",
            1 => "GroundItem",
            2 => "Agent",
            3 => "RegionSummary",
            4 => "ConstructionSite",
            _ => string.Empty,
        };
        return kind.Length > 0;
    }

    private static Observation? MapEntry(JsonElement entry, long tick, PrismItemCatalog? itemCatalog)
    {
        if (!TryReadEntryKind(entry, out string kind))
        {
            return null;
        }

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
            // Objet au sol **comestible** (étape 3b) : il devient une cible de
            // nourriture nommée (`resource-food-<id>`), la seule forme que la
            // cognition sait reconnaître (`Knowledge.KindFromLabel`). Sans
            // catalogue, l'étiquette reste l'identifiant brut : aucun changement
            // pour un run qui ne sait pas lire le contenu.
            "GroundItem" => new Observation(
                entityId == 0 ? DeterministicId($"ground:{x}:{y}") : entityId,
                "node",
                position,
                1.0,
                (ulong)tick,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["label"] = itemCatalog?.IsFood((ushort)ReadInt(entry, "itemTypeId")) == true
                        ? string.Concat(
                            "resource-food-",
                            (entityId == 0 ? DeterministicId($"ground:{x}:{y}") : entityId)
                                .ToString(System.Globalization.CultureInfo.InvariantCulture))
                        : ReadInt(entry, "itemTypeId").ToString(System.Globalization.CultureInfo.InvariantCulture),
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
                // Type dédié (SYNE-222) : un chantier n'est pas un gisement, il a
                // ses propres croyances (`site-<id>` état + avancement).
                "site",
                position,
                1.0,
                (ulong)tick,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["label"] = ReadString(entry, "siteId"),
                    ["state"] = ReadInt(entry, "state").ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["amount"] = ReadInt(entry, "quantity").ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["required"] = ReadInt(entry, "workRequired").ToString(System.Globalization.CultureInfo.InvariantCulture),
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
