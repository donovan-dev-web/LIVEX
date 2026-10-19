// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : ActionExecutor - partie Movement.

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Navigation;
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
        // SYNE-071 : une construction posée/retirée en cours de run re-rasterise la
        // grille A* et purge le cache (no-op si aucune modification — déterminisme).
        _pathfinder.Refresh();

        GoalTarget? destination = mind.Intention is { Kind: var intentionKind, Target: { } intended } && intentionKind == kind
            ? intended
            : null;

        Position target;
        if (destination is { } aimed)
        {
            if (entity.Position.DistanceTo(aimed.Position) <= _options.Resources.InteractionRadius)
            {
                return; // arrivé : plus de pas (la satisfaction est décidée par le catalogue)
            }

            target = Position.Clamp(aimed.Position, _world.Size);
        }
        else
        {
            (double dx, double dy) = DeterministicOffset(entity.Id.Value, currentTick, kind);
            target = Position.Clamp(
                new Position(entity.Position.X + dx, entity.Position.Y + dy),
                _world.Size);
        }

        double speed = Math.Max(0.0, entity.Traits["speed"]);
        Position step = StepToward(entity.Position, target, speed);
        if (IsBlocked(step))
        {
            step = PathStep(entity, target, speed);
        }

        if (step == entity.Position)
        {
            return;
        }

        if (_commit is { } buffer)
        {
            buffer.RequestMove(entity, step);
        }
        else
        {
            _world.Grid.Move(entity, step);
        }
    }


    /// <summary>
    /// Prend le pas suivant sur le chemin A* (SYNE-077) quand le pas direct vers
    /// la cible est bloqué. Dépasse la cellule cible si elle est la dernière.
    /// </summary>
    private Position PathStep(Entity entity, Position target, double speed)
    {
        IReadOnlyList<Position> path = _pathfinder.FindPath(entity.Position, target);
        if (path.Count == 0)
        {
            return entity.Position; // repli « sur place »
        }

        Position waypoint = path[0];
        Position step = StepToward(entity.Position, waypoint, speed);
        return IsBlocked(step) ? entity.Position : step;
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


    private bool IsBlocked(Position position) =>
        _world.Obstacles.Any(obstacle => obstacle.Position.DistanceTo(position) <= obstacle.Radius);

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
