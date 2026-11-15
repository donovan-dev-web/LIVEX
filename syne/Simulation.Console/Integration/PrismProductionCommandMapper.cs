// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Traduction des commandes de production SYNE-223 en commandes PRISM (write-back).

using System.Globalization;
using System.Text.Json;
using Prism.Contracts;

namespace Simulation.Console.Integration;

/// <summary>
/// Traduit une <see cref="ProductionCommand"/> en <see cref="CommandEnvelope"/>
/// exécutable par l'hôte : acquisition (<c>ObtenirObjet</c>), fabrication
/// (<c>FabriquerMacro</c>) et pose de station (<c>Poser</c>).
/// </summary>
/// <remarks>
/// <para>
/// Les identifiants sont envoyés sous forme de <b>noms de contenu</b> : les
/// registres PRISM les résolvent (tri ordinal stable), et SYNE n'a donc pas à
/// recopier une table d'entiers qui pourrait dériver du pack.
/// </para>
/// <para>
/// Une fabrication qui exige une station sans position connue n'est
/// <b>pas</b> émise : mieux vaut attendre la pose que faire fabriquer à
/// l'emplacement de l'agent, ce qui produirait une pièce sans station — donc un
/// objet qui n'existe pas.
/// </para>
/// </remarks>
public static class PrismProductionCommandMapper
{
    /// <summary>Traduit une commande, ou <c>null</c> si elle n'est pas encore exécutable.</summary>
    public static CommandEnvelope? Map(
        long tick,
        ulong agentId,
        string commandId,
        ProductionCommand command,
        PrismBlockPosition? station,
        PrismItemCatalog itemCatalog)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(itemCatalog);

        string kind;
        JsonElement payload;
        switch (command)
        {
            case GatherProductionCommand gather:
                kind = "ObtenirObjet";
                payload = JsonSerializer.SerializeToElement(
                    new { item = gather.Item, quantity = gather.Quantity },
                    ProtocolJson.Options);
                break;

            case CraftProductionCommand craft when craft.StationItem is null:
                kind = "FabriquerMacro";
                payload = JsonSerializer.SerializeToElement(
                    new { recipe = craft.RecipeId },
                    ProtocolJson.Options);
                break;

            case CraftProductionCommand craft:
                if (station is not { } cell)
                {
                    return null;
                }

                kind = "FabriquerMacro";
                payload = JsonSerializer.SerializeToElement(
                    new
                    {
                        recipe = craft.RecipeId,
                        station = new { x = cell.X, y = cell.Y, z = cell.Z },
                    },
                    ProtocolJson.Options);
                break;

            case PlaceProductionCommand place:
                string material = itemCatalog.TryGetPlaces(place.StationItem, out string? placed)
                    ? placed
                    : place.StationItem;
                kind = "Poser";
                payload = JsonSerializer.SerializeToElement(
                    new
                    {
                        target = new
                        {
                            x = station?.X ?? 0,
                            y = station?.Y ?? 0,
                            z = station?.Z ?? 0,
                        },
                        item = place.StationItem,
                        material,
                    },
                    ProtocolJson.Options);
                break;

            default:
                throw new InvalidOperationException(
                    $"Commande de production inconnue : {command.GetType().Name}.");
        }

        return new CommandEnvelope(
            ProtocolVersion.Current.ToString(),
            tick,
            commandId,
            agentId,
            kind,
            payload);
    }

    /// <summary>
    /// Vrai si cet événement marque la <b>fin réelle</b> d'une étape de production.
    /// </summary>
    /// <remarks>
    /// PRISM publie deux choses différentes sous le genre <c>action_completed</c> :
    /// <list type="bullet">
    /// <item>la <b>réponse de dispatch</b> d'une macro-commande acceptée, avec
    /// <c>result.started = true</c> — l'action ne fait que commencer ;</item>
    /// <item>la <b>fin de la macro</b>, avec <c>result.completed = true</c> et les
    /// quantités réellement acquises.</item>
    /// </list>
    /// Les actions instantanées (<c>Poser</c>) n'ont pas de <c>started</c> : leur
    /// réponse de dispatch <i>est</i> leur fin. Confondre les deux faisait avancer
    /// le plan à chaque acceptation : la fabrication suivante partait sans entrées
    /// (<c>input_missing</c>) et la récolte n'était jamais laissée finir
    /// (<c>action_already_active</c>).
    /// </remarks>
    public static bool IsCompletion(string kind, JsonElement payload, ProductionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!string.Equals(kind, "action_completed", StringComparison.Ordinal) ||
            payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        bool HasTrueFlag(string name) =>
            payload.TryGetProperty("result", out JsonElement result) &&
            result.ValueKind == JsonValueKind.Object &&
            result.TryGetProperty(name, out JsonElement flag) &&
            flag.ValueKind == JsonValueKind.True;

        return command is PlaceProductionCommand
            ? !HasTrueFlag("started")
            : HasTrueFlag("completed");
    }

    /// <summary>Identifiant déterministe d'une commande de production (tick, agent, étape).</summary>
    public static string CommandId(long tick, ulong agentId, int stepIndex, ProductionCommand command)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentNullException.ThrowIfNull(command);
        string step = command switch
        {
            GatherProductionCommand gather => string.Concat("gather-", gather.Item),
            CraftProductionCommand craft => string.Concat("craft-", craft.RecipeId),
            PlaceProductionCommand place => string.Concat("place-", place.StationItem),
            _ => "unknown",
        };
        return string.Concat(
            "syne:",
            tick.ToString(CultureInfo.InvariantCulture),
            ":",
            agentId.ToString(CultureInfo.InvariantCulture),
            ":prod:",
            stepIndex.ToString(CultureInfo.InvariantCulture),
            ":",
            step);
    }
}
