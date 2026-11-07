// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : ActionExecutor - partie Movement.

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Prng;
using Simulation.Core.World;

namespace Simulation.Core.Actions;

public sealed partial class ActionExecutor
{

    /// <summary>
    /// Vrai si l'action peut appliquer sa satisfaction : action sans cible (effet
    /// immédiat) ou cible connue atteinte à portée d'interaction.
    /// </summary>
    private bool ArrivedAtTarget(Entity entity, MindState mind, DesireKind kind)
    {
        if (mind.Intention is not { } goal || goal.Kind != kind || goal.Target is not { } target)
        {
            return true;
        }

        return entity.Position.DistanceTo(target.Position) <= _options.Resources.InteractionRadius;
    }


    /// <summary>
    /// Pas de déplacement vers la <b>destination de l'intention</b> : cible connue
    /// (« eau en P, connue à t »), stable d'un tick à l'autre — c'est le contrat
    /// <c>B-01/B-02</c> : une destination persistante, pas un offset recalculé.
    ///
    /// <para>
    /// Si l'intention ne porte pas de cible (aucun repère connu), le pas retombe
    /// sur l'offset pseudo-aléatoire déterministe historique — <b>prospection
    /// déclarée</b>, et non « quête » : aucun nom d'action ne doit laisser croire à
    /// une navigation vers un objet que l'agent ignore.
    /// </para>
    ///
    /// Si le pas direct est bloqué par un obstacle, le chemin A* contournant
    /// l'obstacle est emprunté (SYNE-077) ; en l'absence de chemin, repli
    /// « sur place ».
    /// </summary>
    private void MoveTowardDestination(Entity entity, MindState mind, DesireKind kind, ulong currentTick)
    {
        // SYNE-220 étape 3c : le déplacement est désormais calculé et exécuté
        // côté PRISM (commande Aller). SYNE n'applique plus de pas local ; il
        // se contente de mettre à jour la réserve/les besoins. Aucune géométrie
        // locale n'est consultée.
        _ = entity;
        _ = mind;
        _ = kind;
        _ = currentTick;
    }


    /// <summary>Pas de déplacement vers la cible (au plus <paramref name="speed"/> unités).</summary>
    private static Position StepToward(Position from, Position target, double speed)
    {
        double dx = target.X - from.X;
        double dy = target.Y - from.Y;
        double distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance <= 1e-12)
        {
            return from;
        }

        double step = Math.Min(distance, Math.Max(0.0, speed));
        return new Position(
            from.X + ((dx / distance) * step),
            from.Y + ((dy / distance) * step));
    }

    /// <summary>
    /// Cible de déplacement pseudo-aléatoire déterminée par (id, tick, désir) —
    /// finaliseur SplitMix64/avalanche stable, aucune dépendance au PRNG global.
    /// </summary>
    private (double Dx, double Dy) DeterministicOffset(ulong id, ulong tick, DesireKind kind)
    {
        ulong h = id;
        h ^= tick * SplitMix64.Gamma;
        h ^= (ulong)kind * 0xBF58476D1CE4E5B9UL;
        h = SplitMix64.Avalanche(h);

        double angle = ((h % 10000) / 10000.0) * 2.0 * Math.PI;
        double radius = ((h >> 17) % 100) / 100.0 * _options.Agents.Perception.Radius * 0.5;
        return (Math.Cos(angle) * radius, Math.Sin(angle) * radius);
    }
}
