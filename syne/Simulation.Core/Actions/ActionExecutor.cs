using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;

namespace Simulation.Core.Actions;

/// <summary>
/// Exécuteur d'actions (SYNE-040) : applique atomiquement chaque tick l'action
/// choisie par la délibération — une action par entité par tick. Les effets
/// (coût d'énergie, récupérations, consommation de réserve) proviennent du
/// catalogue déclaratif (SYNE-040). Le déplacement (SYNE-041) respecte les
/// obstacles et le coût énergie ; Eat/Drink mettent à jour les réserves (SYNE-042).
///
/// Déterminisme : aucune consommation du PRNG — cible pseudo-aléatoire stable
/// (hash SplitMix64 de (id, tick, désir)), itération par identifiant croissant,
/// pas de déplacement dans un obstacle (DETERMINISM.md §5).
/// </summary>
public sealed class ActionExecutor
{
    private readonly World.World _world;
    private readonly ActionCatalog _catalog;
    private readonly ResourceStocks _stocks;
    private readonly SimulationOptions _options;

    public ActionExecutor(World.World world, ActionCatalog catalog, ResourceStocks stocks, SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(stocks);
        ArgumentNullException.ThrowIfNull(options);
        _world = world;
        _catalog = catalog;
        _stocks = stocks;
        _options = options;
    }

    /// <summary>
    /// Exécute l'action <paramref name="kind"/> pour l'entité : applique les effets
    /// du catalogue et renvoie le résultat. Les actions à réserve bloquent quand la
    /// réserve est vide (outcome <see cref="ActionOutcome.Blocked"/>, aucun effet).
    /// </summary>
    public ActionResult Execute(Entity entity, MindState mind, DesireKind kind, ulong currentTick)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(mind);

        ActionDefinition definition = _catalog[kind];
        if (definition.RequiresReserve is { } reserve && _stocks.IsEmpty(reserve))
        {
            return ActionResult.Blocked(kind, $"réserve {reserve} vide");
        }

        double energyDelta = 0.0;
        double hungerDelta = 0.0;
        double thirstDelta = 0.0;
        double fatigueDelta = 0.0;

        if (definition.Movement)
        {
            MoveTowardDeterministicTarget(entity, kind, currentTick);
            energyDelta = -definition.EnergyCost;
            if (energyDelta != 0.0)
            {
                mind.Needs.ExertEnergy(definition.EnergyCost);
            }
        }
        else if (definition.EnergyCost > 0.0)
        {
            energyDelta = -definition.EnergyCost;
            mind.Needs.ExertEnergy(definition.EnergyCost);
        }

        if (definition.EnergyRecovery > 0.0)
        {
            energyDelta += definition.EnergyRecovery;
            mind.Needs.RecoverEnergy(definition.EnergyRecovery);
        }

        if (definition.FatigueRecovery > 0.0)
        {
            fatigueDelta = -definition.FatigueRecovery;
            mind.Needs.RecoverFatigue(definition.FatigueRecovery);
        }

        if (definition.HungerRecovery > 0.0)
        {
            hungerDelta = -definition.HungerRecovery;
            mind.Needs.RecoverHunger(definition.HungerRecovery);
        }

        if (definition.ThirstRecovery > 0.0)
        {
            thirstDelta = -definition.ThirstRecovery;
            mind.Needs.RecoverThirst(definition.ThirstRecovery);
        }

        ResourceKind? reserveConsumed = null;
        double consumed = 0.0;
        if (definition.RequiresReserve is { } reserveKind)
        {
            reserveConsumed = reserveKind;
            consumed = definition.ReserveConsumption;
            _stocks.TryConsume(reserveKind, consumed);
        }

        return new ActionResult(
            kind,
            ActionOutcome.Executed,
            null,
            energyDelta,
            hungerDelta,
            thirstDelta,
            fatigueDelta,
            reserveConsumed,
            consumed);
    }

    /// <summary>Pas de déplacement vers la cible déterministe, sans entrer dans un obstacle.</summary>
    private void MoveTowardDeterministicTarget(Entity entity, DesireKind kind, ulong currentTick)
    {
        ActionDefinition definition = _catalog[kind];
        (double dx, double dy) = DeterministicOffset(entity.Id.Value, currentTick, kind);
        double targetX = entity.Position.X + dx;
        double targetY = entity.Position.Y + dy;

        Position target = Position.Clamp(
            new Position(targetX, targetY),
            _world.Size);

        double speed = Math.Max(0.0, entity.Traits["speed"]);
        Position step = StepToward(entity.Position, target, speed);
        if (IsBlocked(step))
        {
            return;
        }

        _world.Grid.Move(entity, step);
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
        h ^= tick * 0x9E3779B97F4A7C15UL;
        h ^= (ulong)kind * 0xBF58476D1CE4E5B9UL;
        h ^= h >> 30;
        h *= 0xBF58476D1CE4E5B9UL;
        h ^= h >> 27;
        h *= 0x94D049BB133111EBUL;
        h ^= h >> 31;

        double angle = ((h % 10000) / 10000.0) * 2.0 * Math.PI;
        double radius = ((h >> 17) % 100) / 100.0 * _options.Agents.Perception.Radius * 0.5;
        return (Math.Cos(angle) * radius, Math.Sin(angle) * radius);
    }
}