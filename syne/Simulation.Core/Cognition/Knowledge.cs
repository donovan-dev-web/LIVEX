// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// RÃ´le : Connaissance active d'un agent Ã  l'instant de la dÃ©cision : cibles concrÃ¨tes dÃ©rivÃ©es des croyances
//        (et, en repli, de la mÃ©moire rappelÃ©e). C'est le maillon Â« objectif â† besoin â† croyance â† mÃ©moire â†
//        perception Â» : la dÃ©cision ne consomme aucune collection globale du monde, seulement ce que l'agent sait.

using System.Globalization;
using Simulation.Core.Configuration;
using Simulation.Core.Prng;
using Simulation.Core.World;

namespace Simulation.Core.Cognition;

/// <summary>
/// Cible concrÃ¨te d'un objectif : <b>quel</b> objet, <b>oÃ¹</b>, <b>connu Ã  quel
/// tick</b> et <b>avec quelle confiance</b> â€” Â« de l'eau en P, connue Ã  t Â»
/// (B-02). Un <see cref="Goal"/> qui porte une cible est falsifiable : arrivÃ© Ã  la
/// position sans trouver la ressource, l'objectif n'est pas accompli et la cible
/// est rÃ©visÃ©e.
/// </summary>
/// <param name="TargetType">Â« node Â» (gisement), Â« entity Â» (pair), Â« point Â» (position dÃ©rivÃ©e).</param>
/// <param name="TargetId">IdentitÃ© numÃ©rique stable de l'objet visÃ©.</param>
/// <param name="Label">Identifiant canonique (Â« resource-food-1 Â», Â« entity-7 Â»).</param>
/// <param name="Position">Position visÃ©e <b>telle qu'observÃ©e</b> â€” jamais la position vraie actuelle.</param>
/// <param name="ObservedTick">Tick d'observation de cette position (Ã¢ge de la connaissance).</param>
/// <param name="Confidence">Confiance Ã©pistÃ©mique de la cible (0-1) issue de la croyance.</param>
public readonly record struct GoalTarget(
    string TargetType,
    ulong TargetId,
    string Label,
    Position Position,
    ulong ObservedTick,
    double Confidence)
{
    /// <summary>Ã‚ge de la connaissance en ticks.</summary>
    public ulong Age(ulong currentTick) => currentTick >= ObservedTick ? currentTick - ObservedTick : 0;

    /// <summary>IdentitÃ© canonique de la cible (Â« node:resource-food-1 Â») â€” traÃ§abilitÃ© et persistance.</summary>
    public string Canonical => string.Concat(TargetType, ":", Label);
}

/// <summary>
/// Connaissance active d'un agent au moment de dÃ©libÃ©rer : les cibles concrÃ¨tes
/// qu'il peut viser, dÃ©rivÃ©es de ses <b>croyances</b> et â€” en repli â€” de sa
/// <b>mÃ©moire rappelÃ©e</b>. Aucune de ces valeurs ne lit le monde : une cible
/// inconnue de l'agent n'existe pas ici, mÃªme si l'objet existe dans le monde.
/// </summary>
public readonly record struct CognitionKnowledge(
    GoalTarget? FoodSource,
    GoalTarget? WaterSource,
    GoalTarget? Peer,
    GoalTarget? Landmark,
    bool AtFoodSource,
    bool AtWaterSource)
{
    /// <summary>Connaissance vide (agent sans aucune observation utile).</summary>
    public static readonly CognitionKnowledge Empty = new(null, null, null, null, false, false);

    public bool KnowsFood => AtFoodSource || FoodSource is not null;

    public bool KnowsWater => AtWaterSource || WaterSource is not null;

    public bool KnowsPeer => Peer is not null;

    public bool KnowsLandmark => Landmark is not null;

    /// <summary>Cible d'une action : la source pour un besoin physiologique, le pair pour un besoin social.</summary>
    public GoalTarget? TargetFor(DesireKind kind) => kind switch
    {
        DesireKind.Eat or DesireKind.SeekFood => FoodSource,
        DesireKind.Drink or DesireKind.SeekWater => WaterSource,
        DesireKind.Socialize or DesireKind.Give or DesireKind.Trade => Peer,
        DesireKind.Explore => Landmark,
        DesireKind.Flee => Peer,
        _ => null,
    };

    /// <summary>
    /// Confiance Ã©pistÃ©mique Ã  faire entrer dans l'utilitÃ© du candidat
    /// (<c>null</c> = aucune connaissance spÃ©cifique : terme neutre, formule
    /// inchangÃ©e par rapport Ã  l'historique). Â« ÃŠtre sur place Â» est une
    /// connaissance certaine : elle vaut 1.
    /// </summary>
    public double? EpistemicFor(DesireKind kind) => kind switch
    {
        DesireKind.Eat => AtFoodSource ? 1.0 : null,
        DesireKind.Drink => AtWaterSource ? 1.0 : null,
        DesireKind.SeekFood => FoodSource?.Confidence,
        DesireKind.SeekWater => WaterSource?.Confidence,
        DesireKind.Socialize or DesireKind.Give or DesireKind.Trade => Peer?.Confidence,
        DesireKind.Explore => Landmark?.Confidence,
        DesireKind.Flee => Peer?.Confidence,
        _ => null,
    };

    /// <summary>
    /// Destination de fuite : point opposÃ© Ã  la menace connue, Ã 
    /// <paramref name="distance"/> â€” l'agent s'Ã©loigne d'une <b>menace crue</b>,
    /// jamais d'une entitÃ© qu'il ne sait pas lÃ .
    /// </summary>
    public GoalTarget? FleeDestination(Position self, double distance, WorldSize size)
    {
        if (Peer is not { } threat || !double.IsFinite(distance) || distance <= 0.0)
        {
            return null;
        }

        double dx = self.X - threat.Position.X;
        double dy = self.Y - threat.Position.Y;
        double norm = Math.Sqrt((dx * dx) + (dy * dy));
        if (norm <= 1e-9)
        {
            // Menace confondue avec soi (observation datÃ©e) : fuite dans une
            // direction stable dÃ©rivÃ©e de l'identitÃ© de la menace.
            double angle = ((threat.TargetId % 360) / 360.0) * 2.0 * Math.PI;
            dx = Math.Cos(angle);
            dy = Math.Sin(angle);
            norm = 1.0;
        }

        Position destination = Position.Clamp(
            new Position(self.X + ((dx / norm) * distance), self.Y + ((dy / norm) * distance)),
            size);
        return new GoalTarget("point", threat.TargetId, $"away-from-{threat.Label}", destination, threat.ObservedTick, threat.Confidence);
    }
}

/// <summary>
/// Construit la connaissance active d'un agent Ã  partir de ses croyances
/// (<b>source primaire</b>) et, en repli, du <b>rappel mÃ©moire</b> : la mÃ©moire
/// n'est plus un stockage inerte, elle alimente la cible quand la croyance a
/// expirÃ© ou a Ã©tÃ© Ã©vincÃ©e (S-07 / B-05).
///
/// <b>DÃ©terminisme</b> : aucune consommation du PRNG, itÃ©rations sur des
/// collections triÃ©es (<c>BeliefSet.All</c> suit l'ordre du dictionnaire des
/// faits, <c>Memory.Recall</c> est triÃ© par (StoredAt, Sequence)), dÃ©partages par
/// identifiant canonique.
/// </summary>
public static partial class KnowledgeBuilder
{
    /// <summary>Confiance minimale d'une croyance pour servir de cible (sous ce seuil : connaissance nÃ©gligeable).</summary>
    public const double MinConfidence = 0.2;

    /// <summary>Nom du fait d'Ã©tat d'un gisement (Â« x,y,amount Â»), sujet <c>node-&lt;label&gt;</c>.</summary>
    public const string NodeStatePredicate = "state";

    /// <summary>PrÃ©dicat de position d'une entitÃ© perÃ§ue (<c>entity-&lt;id&gt;</c>).</summary>
    public const string EntityPositionPredicate = "position";
}
