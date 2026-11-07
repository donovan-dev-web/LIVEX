// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.Json;
using Prism.Contracts;
using Simulation.Core.Cognition;
using Simulation.Core.World;

namespace Simulation.Console.Integration;

/// <summary>Coordonnée de bloc PRISM (ADR-013 : 1 bloc = 1 m = 1 unité SYNE).</summary>
public readonly record struct PrismBlockPosition(int X, int Y, int Z);

/// <summary>
/// Ce que le pont sait en plus du désir, pour émettre une commande <b>exécutable</b>
/// (étape 3b de SYNE-220) : le catalogue d'objets de l'hôte et les objets que
/// l'agent porte d'après sa dernière observation.
/// </summary>
/// <remarks>
/// Sans vocabulaire, le pont reste au minimum historique (<c>Aller</c>,
/// <c>Consommer</c> à la source) : aucun comportement nouveau n'apparaît pour un
/// run dont le contenu n'est pas lisible. C'est aussi ce qui rend le mode couplé
/// testable sans pack de contenu.
/// </remarks>
public sealed record PrismCommandVocabulary(
    PrismItemCatalog Items,
    IReadOnlyCollection<ushort>? CarriedItemIds = null)
{
    /// <summary>Plus petit identifiant comestible effectivement porté, s'il y en a un.</summary>
    public ushort? CarriedFoodItemId
    {
        get
        {
            if (CarriedItemIds is not { Count: > 0 } carried)
            {
                return null;
            }

            ushort? smallest = null;
            foreach (ushort itemTypeId in carried)
            {
                if (!Items.IsFood(itemTypeId))
                {
                    continue;
                }

                if (smallest is null || itemTypeId < smallest)
                {
                    smallest = itemTypeId;
                }
            }

            return smallest;
        }
    }
}

/// <summary>
/// Traduit les décisions BDI du tick en commandes PRISM (P9, write-back) : c'est
/// la seconde moitié du couplage — jusqu'ici SYNE lisait le monde de PRISM sans
/// jamais lui dire quoi faire, donc les agents restaient immobiles.
/// </summary>
/// <remarks>
/// <para>
/// Le vocabulaire est volontairement étroit et le restera : une décision n'est
/// traduite que si PRISM sait exécuter exactement ce qu'elle demande.
/// <list type="bullet">
/// <item><c>SeekFood</c>, <c>SecureFood</c> → <b><c>ObtenirObjet</c></b> (tag
/// <c>edible</c>, quantité 1, indice de position) quand le catalogue de contenu
/// est lisible ; sinon <c>Aller</c> vers la cible, comme avant.</item>
/// <item><c>SeekWater</c>, <c>Explore</c>, <c>Socialize</c>, <c>Flee</c> →
/// <c>Aller</c> vers la cible.</item>
/// <item><c>Drink</c> → <c>Consommer</c> sur le bloc d'eau (payload par
/// coordonnée : PRISM boit à la source, sans identifiant d'objet).</item>
/// <item><c>Eat</c> → <c>Consommer</c> sur un <b>objet porté</b> : SYNE choisit le
/// plus petit identifiant comestible de l'inventaire observé (catalogue +
/// <c>self.inventory</c>), donc il ne demande que ce qu'il porte réellement.</item>
/// </list>
/// Rien d'autre : les actions collectives (Give/Trade/Work/Build) n'ont pas
/// d'équivalent dans le contrat, et <c>Rest</c>/<c>Attack</c>/<c>Defend</c> n'ont
/// aucune commande PRISM correspondante.
/// </para>
/// <para>
/// <c>ObtenirObjet</c> est une <b>macro-action</b> de l'hôte : PRISM cherche la
/// source la plus proche du point indiqué, la rejoint, y prélève la quantité
/// demandée. SYNE cesse donc d'avoir besoin de connaître la géométrie du gisement
/// pour se nourrir — c'est exactement ce que l'étape 3b doit déplacer — tout en
/// gardant son opinion sur <i>où</i> il croit qu'elle se trouve (l'indice).
/// </para>
/// <para>
/// Une décision <b>sans cible</b> n'est pas traduite : sans destination, une
/// commande <c>Aller</c> n'aurait pas de sens et un <c>Consommer</c> viserait le
/// néant. Ce n'est pas une erreur du couplage, c'est un agent qui ignore encore
/// où aller — l'appelant peut compter ces cas.
/// </para>
/// </remarks>
public static class PrismDecisionCommandMapper
{
    /// <summary>
    /// Rayon de l'indice transmis à <c>ObtenirObjet</c>, en blocs. Calibrage
    /// explicite : assez large pour que la source crue soit retrouvée après
    /// dérive de croyance, assez étroit pour que l'acquisition reste <i>cette</i>
    /// source et non la plus proche du monde entier.
    /// </summary>
    public const int AcquisitionHintRadius = 8;

    /// <summary>
    /// Kinds de décision que ce pont sait traduire en commande PRISM sans rien
    /// savoir du contenu : les poursuites et la consommation à la source.
    /// </summary>
    public static bool IsMappable(DecisionRecord decision) => IsMappable(decision, vocabulary: null);

    /// <summary>
    /// Kinds traduisibles <b>avec</b> le vocabulaire disponible. <c>Eat</c> n'est
    /// traduisible que si l'agent porte un objet comestible identifié — sinon la
    /// commande viserait un objet qu'il n'a pas.
    /// </summary>
    public static bool IsMappable(DecisionRecord decision, PrismCommandVocabulary? vocabulary)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Missing || decision.State == DecisionState.None)
        {
            return false;
        }

        return decision.Chosen switch
        {
            // Manger ne vise aucun lieu : c'est l'objet porté qui est consommé.
            DesireKind.Eat => vocabulary?.CarriedFoodItemId is not null,
            // SecureFood (SYNE-222) est une poursuite vers une source de nourriture
            // connue ; en mode couplé l'hôte sait l'exécuter jusqu'au prélèvement
            // (ObtenirObjet), sinon un simple déplacement.
            DesireKind.SeekFood or DesireKind.SeekWater or DesireKind.Explore or
                DesireKind.Socialize or DesireKind.Flee or DesireKind.SecureFood => decision.Target is not null,
            DesireKind.Drink => decision.Target is not null,
            _ => false,
        };
    }

    /// <summary>
    /// Traduit les décisions d'un tick. Une décision intraduisible est ignorée
    /// (voir <see cref="IsMappable(DecisionRecord, PrismCommandVocabulary?)"/>) ; deux décisions du même agent ou un tick
    /// incohérent restent des erreurs de programmation et lèvent.
    /// </summary>
    public static IReadOnlyList<CommandEnvelope> Map(
        long tick,
        IEnumerable<DecisionRecord> decisions,
        Func<Position, PrismBlockPosition> mapPosition,
        Func<GoalTarget, PrismBlockPosition?>? mapSource = null,
        PrismCommandVocabulary? vocabulary = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(mapPosition);

        var commands = new List<CommandEnvelope>();
        var agents = new HashSet<ulong>();
        foreach (var decision in decisions.OrderBy(static decision => decision.EntityId))
        {
            if (decision.Tick != (ulong)tick)
            {
                throw new ArgumentException("Every BDI decision must match the requested PRISM tick.", nameof(decisions));
            }

            if (!agents.Add(decision.EntityId))
            {
                throw new ArgumentException("At most one BDI decision per agent may be mapped per tick.", nameof(decisions));
            }

            var command = MapOne(tick, decision, mapPosition, mapSource, vocabulary);
            if (command is not null)
            {
                commands.Add(command);
            }
        }

        return commands;
    }

    /// <summary>
    /// Traduit une décision, ou <c>null</c> si elle n'a pas d'équivalent PRISM.
    /// <paramref name="mapSource"/> résout le bloc à viser pour une consommation
    /// (boire) quand la cible cognitive désigne une case d'approche et non le bloc
    /// source lui-même ; à défaut, la position de la cible sert de source.
    /// </summary>
    public static CommandEnvelope? MapOne(
        long tick,
        DecisionRecord decision,
        Func<Position, PrismBlockPosition> mapPosition,
        Func<GoalTarget, PrismBlockPosition?>? mapSource = null,
        PrismCommandVocabulary? vocabulary = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(mapPosition);

        if (!IsMappable(decision, vocabulary))
        {
            return null;
        }

        string kind;
        JsonElement payload;
        switch (decision.Chosen)
        {
            // Manger : consommer un objet **porté** (l'identifiant vient du
            // catalogue de contenu et de l'inventaire observé, jamais d'une
            // supposition sur le pack).
            case DesireKind.Eat:
                kind = "Consommer";
                payload = JsonSerializer.SerializeToElement(
                    new { item = vocabulary!.CarriedFoodItemId!.Value },
                    ProtocolJson.Options);
                break;

            // Nourriture : acquisition par tag. `hint` reste l'opinion de SYNE sur
            // l'emplacement de la source, `hintRadius` borne la recherche autour.
            case DesireKind.SeekFood or DesireKind.SecureFood when vocabulary?.Items.HasFood == true:
            {
                PrismBlockPosition hint = mapPosition(decision.Target!.Value.Position);
                kind = "ObtenirObjet";
                payload = JsonSerializer.SerializeToElement(
                    new
                    {
                        tag = PrismItemCatalog.FoodTag,
                        quantity = 1,
                        hint = new { x = hint.X, y = hint.Y, z = hint.Z },
                        hintRadius = AcquisitionHintRadius,
                    },
                    ProtocolJson.Options);
                break;
            }

            default:
            {
                // Le contrat PRISM attend la coordonnée **directement** sous `target`
                // (`CommandPayloadValidator.TryRequireCoordinate` : x/y/z numériques) ;
                // l'enveloppe `{ type, id, position }` que produisait ce pont n'était
                // jamais validée et toute commande aurait été refusée `invalid_payload`.
                GoalTarget target = decision.Target!.Value;
                bool consumes = decision.Chosen is DesireKind.Drink;
                kind = consumes ? "Consommer" : "Aller";
                PrismBlockPosition position = consumes && mapSource?.Invoke(target) is { } source
                    ? source
                    : mapPosition(target.Position);
                payload = JsonSerializer.SerializeToElement(
                    new { target = new { x = position.X, y = position.Y, z = position.Z } },
                    ProtocolJson.Options);
                break;
            }
        }

        var commandId = string.Concat(
            "syne:",
            tick.ToString(CultureInfo.InvariantCulture),
            ":",
            decision.EntityId.ToString(CultureInfo.InvariantCulture),
            ":",
            kind);
        return new CommandEnvelope(
            ProtocolVersion.Current.ToString(),
            tick,
            commandId,
            decision.EntityId,
            kind,
            payload);
    }
}
