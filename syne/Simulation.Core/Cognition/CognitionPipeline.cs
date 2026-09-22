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
            mind.Trust.Tick();
        }
    }

    private void StepEntity(Entity entity, ulong currentTick)
    {
        MindState mind = GetOrCreate(entity.Id.Value, entity);
        mind.Factors ??= new AgentFactors(entity.Traits);
        mind.DeliberatedThisTick = false;
        mind.InterruptedThisTick = false;

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

        ActionSettings actions = _options.Agents.Actions;
        DesireKind chosenKind;

        // Délibération à fréquence configurable (décision n°14) : entre deux
        // délibérations, l'intention est conservée (holdover) telle quelle.
        if (ShouldDeliberate(entity, currentTick))
        {
            IReadOnlyList<Goal> active = ActiveGoals(mind, currentTick);
            IReadOnlyList<Goal> generated = DesireFactory.Generate(mind.Needs, currentTick, active.Select(goal => goal.Kind));
            var candidates = new List<Goal>(active.Count + generated.Count);
            candidates.AddRange(active);
            candidates.AddRange(generated);

            (IReadOnlyList<UtilityScore> scores, UtilityScore best) = Deliberate(mind, candidates, currentTick, entity.Id.Value);
            mind.RecordDecision(scores, best, deliberated: true, interrupted: false, currentTick, entity.Id.Value);
            mind.DeliberatedThisTick = true;
            chosenKind = best.Kind;
        }
        else
        {
            chosenKind = mind.LastDecision is { } held ? held.Kind : DesireKind.Idle;
        }

        // Interruptions d'action (décision n°15, COGNITIVE_ARCHITECTURE.md §6) :
        // un besoin critique dont l'utilité surpasse de > utilityExcessMargin
        // l'action en cours reprend la main, y compris entre deux délibérations.
        DesireKind? interruptedKind = _options.Agents.Actions.Interruption.Enabled
            ? TryInterrupt(mind, chosenKind, entity.Id.Value, currentTick)
            : null;
        if (interruptedKind is { } interruption)
        {
            chosenKind = interruption;
        }

        Goal? goal = chosenKind == DesireKind.Idle
            ? new Goal(DesireKind.Idle, currentTick)
            : mind.Intention is { } intention && intention.Kind == chosenKind
                ? intention
                : new Goal(chosenKind, currentTick);
        mind.Intention = goal;

        ExecuteIntention(entity, mind, chosenKind, currentTick);
    }

    /// <summary>Fréquence de délibération configurable (décalée par entité pour lisser la charge).</summary>
    private bool ShouldDeliberate(Entity entity, ulong currentTick) =>
        (currentTick + entity.Id.Value) % (ulong)_options.Agents.Actions.Deliberation.IntervalTicks == 0;

    /// <summary>
    /// Interruption d'action (SYNE-032) : besoin critique (faim &gt; 85 ou
    /// énergie &lt; 10) dont l'utilité excède de <c>utilityExcessMargin</c> celle
    /// de l'action en cours. Enregistre une décision interrompue et renvoie le
    /// désir qui reprend la main (ou <c>null</c> si rien ne justifie d'interrompre).
    /// </summary>
    private DesireKind? TryInterrupt(MindState mind, DesireKind currentKind, ulong entityId, ulong currentTick)
    {
        InterruptionSettings interruption = _options.Agents.Actions.Interruption;
        var urgents = new List<DesireKind>(2);
        if (mind.Needs.Energy < interruption.CriticalEnergy && currentKind != DesireKind.Rest)
        {
            urgents.Add(DesireKind.Rest);
        }

        if (mind.Needs.Hunger > interruption.CriticalHunger && currentKind != DesireKind.SeekFood)
        {
            urgents.Add(DesireKind.SeekFood);
        }

        if (urgents.Count == 0)
        {
            return null;
        }

        AgentFactors factors = mind.Factors ?? new AgentFactors(TraitSet.NeutralAll);
        ActionSettings actions = _options.Agents.Actions;
        ulong goalAge = mind.Intention is { } intention ? intention.Age(currentTick) : 0;

        UtilityScore currentScore = UtilityEvaluator.Evaluate(
            currentKind,
            mind.Needs,
            factors,
            mind.SuccessRate(currentKind),
            goalAge,
            actions,
            currentKind);

        UtilityScore? best = null;
        foreach (DesireKind kind in urgents)
        {
            UtilityScore candidate = UtilityEvaluator.Evaluate(
                kind,
                mind.Needs,
                factors,
                mind.SuccessRate(kind),
                goalAge: 0,
                actions,
                kind);

            if (candidate.Utility > currentScore.Utility + interruption.UtilityExcessMargin &&
                (best is null ||
                 candidate.Utility > best.Value.Utility ||
                 (candidate.Utility == best.Value.Utility && candidate.Kind < best.Value.Kind)))
            {
                best = candidate;
            }
        }

        if (best is null)
        {
            return null;
        }

        mind.RecordDecision(
            [currentScore, best.Value],
            best.Value,
            deliberated: false,
            interrupted: true,
            currentTick,
            entityId);
        mind.InterruptedThisTick = true;
        return best.Value.Kind;
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

    /// <summary>
    /// Évalue toutes les actions candidates et sélectionne (SYNE-030/031/033) :
    /// utilité maximale ; à conflit (candidats à moins de <c>conflictTieMargin</c>
    /// du maximum), résolution probabiliste force × confiance (décision n°22) ;
    /// puis hystérésis anti-oscillation (<c>actionSwitchMargin</c>).
    /// </summary>
    private (IReadOnlyList<UtilityScore> Scores, UtilityScore Best) Deliberate(
        MindState mind,
        IReadOnlyList<Goal> candidates,
        ulong currentTick,
        ulong entityId)
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
        var scores = new List<UtilityScore>(candidates.Count + 1)
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
                actions,
                mind.Intention?.Kind));
        }

        double maximum = scores.Max(score => score.Utility);
        var contested = scores.Where(score => maximum - score.Utility <= actions.Deliberation.ConflictTieMargin).ToList();
        UtilityScore selected = contested.Count > 1
            ? PriorityConflictResolver.Resolve(contested, mind.Needs, entityId, currentTick)
            : contested[0];

        if (mind.Intention is { } current)
        {
            UtilityScore adjusted = UtilityEvaluator.ApplyActionSwitchMargin(
                selected,
                current.Kind,
                mind.Needs,
                factors,
                mind.SuccessRate(current.Kind),
                current.Age(currentTick),
                actions);

            if (adjusted.Kind != selected.Kind)
            {
                scores.Add(adjusted);
                selected = adjusted;
            }
        }

        return (scores, selected);
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