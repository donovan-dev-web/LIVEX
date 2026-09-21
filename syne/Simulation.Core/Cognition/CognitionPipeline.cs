using System.Globalization;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Perception;

namespace Simulation.Core.Cognition;

/// <summary>
/// Pipeline cognitif BDI (SYNE-010, COGNITIVE_ARCHITECTURE.md §2) exécuté à
/// chaque tick dans l'ordre causal strict (DETERMINISM.md §5) :
/// perception → mémoire → croyances → besoins → objectifs → faisabilité →
/// utilité → délibération → intention → action (« boucle 10/15 étapes », jalon U1).
///
/// Déterminisme : aucune consommation du PRNG (l'avance du générateur reste à
/// un tirage par tick), ordre d'itération par identifiant croissant, cibles de
/// déplacement pseudo-aléatoires déterministes (hash stable), agrégats en
/// représentation invariante à la culture.
/// </summary>
public sealed class CognitionPipeline
{
    private readonly Simulation.Core.World.World _world;
    private readonly SimulationOptions _options;
    private readonly PerceptionSystem _perception;
    private readonly Dictionary<ulong, MindState> _minds = new();

    public CognitionPipeline(Simulation.Core.World.World world, SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(options);
        _world = world;
        _options = options;
        _perception = new PerceptionSystem(world, options.Agents.Perception);
    }

    public PerceptionSystem Perception => _perception;

    public IReadOnlyCollection<MindState> Minds => _minds.Values;

    public MindState MindOf(ulong entityId) => _minds[entityId];

    public bool HasMind(ulong entityId) => _minds.ContainsKey(entityId);

    /// <summary>Exécute une itération complète du pipeline pour toutes les entités.</summary>
    public void Step(ulong currentTick)
    {
        List<Entity> ordered = _world.Entities
            .OrderBy(entity => entity.Id.Value)
            .ToList();

        foreach (Entity entity in ordered)
        {
            StepEntity(entity, currentTick);
        }

        foreach (MindState mind in _minds.Values)
        {
            mind.Beliefs.Tick(currentTick, _options.Agents.Beliefs);
        }
    }

    private void StepEntity(Entity entity, ulong currentTick)
    {
        MindState mind = GetOrCreate(entity.Id.Value, entity);
        mind.Factors ??= new AgentFactors(entity.Traits);

        int observed = 0;
        if (_perception.ShouldPerceive(entity, currentTick))
        {
            IReadOnlyList<Observation> observations = _perception.Perceive(entity, currentTick);
            observed = observations.Count;
            foreach (Observation observation in observations)
            {
                StoreObservation(mind, entity, observation, currentTick);
            }
        }

        mind.AcknowledgePerceptions(observed);
        mind.Needs.Advance(_options.Agents.Needs);

        // Délibération : objectifs actifs + candidats, évalués par l'utilité.
        IReadOnlyList<Goal> active = ActiveGoals(mind, currentTick);
        IReadOnlyList<Goal> generated = DesireFactory.Generate(mind.Needs, currentTick, active.Select(goal => goal.Kind));
        var candidates = new List<Goal>();
        candidates.AddRange(active);
        candidates.AddRange(generated);

        UtilityScore best = Deliberate(mind, candidates, currentTick);
        mind.RecordDecision(best);
        Goal? chosen = candidates.FirstOrDefault(candidate => candidate.Kind == best.Kind);
        mind.Intention = best.Kind == DesireKind.Idle || chosen is null
            ? new Goal(DesireKind.Idle, currentTick)
            : chosen.Value;

        ExecuteIntention(entity, mind, best.Kind, currentTick);
    }

    /// <summary>Objectifs encore actifs : besoin encore déclenché ou objectif récemment défini.</summary>
    private static IReadOnlyList<Goal> ActiveGoals(MindState mind, ulong currentTick)
    {
        if (mind.Intention is not { } intention)
        {
            return [];
        }

        bool stillNeeded = mind.Needs.IsTriggered(intention.Kind);
        bool recent = intention.Age(currentTick) < 100;
        return stillNeeded || recent ? [intention] : [];
    }

    private UtilityScore Deliberate(MindState mind, IReadOnlyList<Goal> candidates, ulong currentTick)
    {
        AgentFactors factors;
        if (mind.Factors is { } resolved)
        {
            factors = resolved;
        }
        else
        {
            factors = new AgentFactors(TraitSet.NeutralAll);
        }

        ActionSettings actions = _options.Agents.Actions;
        var scores = new List<UtilityScore>
        {
            UtilityEvaluator.Evaluate(
                DesireKind.Idle,
                mind.Needs,
                factors,
                mind.SuccessRate(DesireKind.Idle),
                goalAge: 0,
                actions),
        };

        foreach (Goal goal in candidates)
        {
            if (goal.Kind == DesireKind.Idle)
            {
                continue;
            }

            scores.Add(UtilityEvaluator.Evaluate(
                goal.Kind,
                mind.Needs,
                factors,
                mind.SuccessRate(goal.Kind),
                goal.Age(currentTick),
                actions));
        }

        return UtilityEvaluator.Best(scores);
    }

    private void StoreObservation(MindState mind, Entity self, Observation observation, ulong currentTick)
    {
        string content = FormatContent(observation);
        mind.Memory.Store(
            MemoryCategory.Observation,
            $"entity-{self.Id.Value}",
            content,
            observation.Confidence,
            currentTick);

        if (observation.EntityType == "entity")
        {
            Fact position = new($"entity-{observation.EntityId}", "position", CanonicalPosition(observation.Position));
            mind.Beliefs.ApplyEvidence(
                position,
                observation.Confidence,
                $"perception-{self.Id.Value}",
                _options.Agents.Beliefs,
                currentTick);
        }
        else if (observation.EntityType == "obstacle")
        {
            Fact obstacle = new($"obstacle-{observation.EntityId}", "radius", observation.Attributes["radius"]);
            mind.Beliefs.ApplyEvidence(
                obstacle,
                observation.Confidence,
                $"perception-{self.Id.Value}",
                _options.Agents.Beliefs,
                currentTick);
        }
    }

    private static string FormatContent(Observation observation) =>
        string.Join(
            "|",
            observation.EntityType,
            observation.EntityId.ToString(CultureInfo.InvariantCulture),
            observation.Position.X.ToString("0.00", CultureInfo.InvariantCulture),
            observation.Position.Y.ToString("0.00", CultureInfo.InvariantCulture),
            observation.Confidence.ToString("0.000", CultureInfo.InvariantCulture));

    /// <summary>Position canonique d'un fait (DATA_MODEL.md §6) : « X,Y » invariant à la culture.</summary>
    private static string CanonicalPosition(Simulation.Core.World.Position position) =>
        string.Concat(
            position.X.ToString("0.00", CultureInfo.InvariantCulture),
            ",",
            position.Y.ToString("0.00", CultureInfo.InvariantCulture));

    /// <summary>Exécute l'intention minimale (SYNE-010) : repos ou déplacement déterministe.</summary>
    private void ExecuteIntention(Entity entity, MindState mind, DesireKind kind, ulong currentTick)
    {
        switch (kind)
        {
            case DesireKind.Rest:
                mind.Needs.RecoverFatigue(_options.Agents.Actions.RestFatigueRecovery);
                mind.Needs.RecoverEnergy(_options.Agents.Actions.RestEnergyGain);
                break;

            case DesireKind.Idle:
                break;

            default:
                MoveTowardDeterministicTarget(entity, mind, kind, currentTick);
                mind.Needs.ExertEnergy(_options.Agents.Actions.MoveEnergyCost);
                break;
        }
    }

    private void MoveTowardDeterministicTarget(Entity entity, MindState mind, DesireKind kind, ulong currentTick)
    {
        (double dx, double dy) = DeterministicOffset(entity.Id.Value, currentTick, kind);
        double targetX = entity.Position.X + dx;
        double targetY = entity.Position.Y + dy;

        var target = Simulation.Core.World.Position.Clamp(
            new Simulation.Core.World.Position(targetX, targetY),
            _world.Size);

        double speed = Math.Max(0.0, entity.Traits["speed"]);
        Simulation.Core.World.Position step = StepToward(entity.Position, target, speed);
        if (IsBlocked(step))
        {
            return;
        }

        _world.Grid.Move(entity, step);
    }

    /// <summary>Pas de déplacement vers la cible (au plus <paramref name="speed"/> unités).</summary>
    private static Simulation.Core.World.Position StepToward(
        Simulation.Core.World.Position from,
        Simulation.Core.World.Position target,
        double speed)
    {
        double dx = target.X - from.X;
        double dy = target.Y - from.Y;
        double distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance <= 1e-12)
        {
            return from;
        }

        double step = Math.Min(distance, Math.Max(0.0, speed));
        return new Simulation.Core.World.Position(
            from.X + ((dx / distance) * step),
            from.Y + ((dy / distance) * step));
    }

    private bool IsBlocked(Simulation.Core.World.Position position) =>
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

    private MindState GetOrCreate(ulong entityId, Entity entity)
    {
        if (!_minds.TryGetValue(entityId, out MindState? mind))
        {
            mind = new MindState(_options);
            _minds[entityId] = mind;
        }

        return mind;
    }
}