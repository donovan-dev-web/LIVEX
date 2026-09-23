using System.Globalization;
using Simulation.Core.Actions;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Perception;
using Simulation.Core.Population;
using Simulation.Core.Social;

namespace Simulation.Core.Cognition;

/// <summary>
/// Pipeline cognitif BDI (SYNE-010, COGNITIVE_ARCHITECTURE.md §2) exécuté à
/// chaque tick dans l'ordre causal strict (DETERMINISM.md §5) :
/// perception → mémoire → croyances → besoins → objectifs → faisabilité →
/// utilité → délibération → intention → action (« boucle 10/15 étapes », jalon U1).
///
/// Depuis le jalon ph4, l'exécution des actions sort du pipeline : elle est
/// confiée au catalogue déclaratif + exécuteur atomique (SYNE-040/041/042) et
/// les interruptions au déclencheur centralisé (SYNE-043).
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
    private readonly World.ResourceStocks _stocks;
    private readonly ActionCatalog _catalog;
    private readonly ActionExecutor _executor;
    private readonly InterruptionTrigger _interruption;
    private readonly CommunicationSystem _communication;
    private readonly GroupSystem _groups;
    private readonly BirthSystem _birth;
    private readonly DeathSystem _death;
    private readonly Dictionary<ulong, MindState> _minds = new();

    public CognitionPipeline(
        Simulation.Core.World.World world,
        SimulationOptions options,
        World.ResourceStocks stocks)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(stocks);
        _world = world;
        _options = options;
        _perception = new PerceptionSystem(world, options.Agents.Perception);
        _stocks = stocks;
        _catalog = new ActionCatalog(options.Agents.Actions);
        _executor = new ActionExecutor(world, _catalog, stocks, options);
        _interruption = new InterruptionTrigger(_catalog, stocks);
        _communication = new CommunicationSystem(world, options.Communication);
        _groups = new GroupSystem(options.Groups);
        _birth = new BirthSystem(options.Reproduction);
        _death = new DeathSystem(options.Agents.Life);
    }

    public PerceptionSystem Perception => _perception;

    public ActionCatalog Catalog => _catalog;

    public World.ResourceStocks Stocks => _stocks;

    /// <summary>Sous-système de communication (une passe par tick — SYNE-050 → 054).</summary>
    public CommunicationSystem Communication => _communication;

    /// <summary>Sous-système de groupes émergents (SYNE-060/061, révision LOD configurable).</summary>
    public GroupSystem Groups => _groups;

    /// <summary>Sous-système de naissance par fusion consentie (SYNE-062).</summary>
    public BirthSystem Birth => _birth;

    /// <summary>Sous-système de mortalité par épuisement (SYNE-074).</summary>
    public DeathSystem Death => _death;

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

        // Communication de masse en une passe (performance.batchCommunication,
        // COMMUNICATION_PROTOCOL.md §7) : cartes après perception/décision/action
        // — l'ordre causal est agent par agent puis réseau de communication.
        _communication.Step(currentTick, _minds);

        foreach (MindState mind in _minds.Values)
        {
            mind.Beliefs.Tick(currentTick, _options.Agents.Beliefs);
            mind.Trust.Tick();
        }

        // Groupes émergents (SYNE-060/061) puis naissances (SYNE-062) : après la
        // boucle des entités, la communication de masse et les décréments de
        // croyance/confiance — la population ne mute qu'après l'itération complète
        // (ordre causal strict, DETERMINISM.md §5).
        _groups.Step(currentTick, _minds);
        _groups.PropagateObjectives(currentTick, (ulong)_options.Groups.ReviewIntervalTicks, _minds);
        _birth.Step(currentTick, _world, _minds, _options);
        foreach ((ulong childId, MindState childMind) in _birth.NewbornMinds)
        {
            _minds[childId] = childMind;
        }

        // Mortalité (SYNE-074) : après les naissances — les nouveau-nés naissent à
        // pleine énergie et ne meurent pas au tick de leur naissance. Retrait du
        // monde, puis purge des esprits et des groupes (dissolution/redimension si
        // passage sous la taille minimale). Tout se fait après l'itération complète.
        IReadOnlyList<ulong> deceased = _death.Step(currentTick, _world, _minds);
        if (deceased.Count > 0)
        {
            _groups.PurgeDeceased(deceased, currentTick, _minds);
            foreach (ulong deathId in deceased)
            {
                _minds.Remove(deathId);
            }
        }
    }

    private void StepEntity(Entity entity, ulong currentTick)
    {
        MindState mind = GetOrCreate(entity.Id.Value, entity);
        mind.Factors ??= new AgentFactors(entity.Traits);
        mind.DeliberatedThisTick = false;
        mind.InterruptedThisTick = false;

        int observed = 0;
        bool announced = false;
        if (_perception.ShouldPerceive(entity, currentTick))
        {
            IReadOnlyList<Observation> observations = _perception.Perceive(entity, currentTick);
            observed = observations.Count;
            foreach (Observation observation in observations)
            {
                StoreObservation(mind, entity, observation, currentTick);
                if (!announced && WantsToShare(entity, observation))
                {
                    EnqueueSharePulse(entity, observation, mind, currentTick);
                    announced = true;
                }
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
            IReadOnlyList<Goal> generated = DesireFactory.Generate(mind.Needs, currentTick, active.Select(goal => goal.Kind), _catalog, _stocks);
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

            // Holdover : une action terminale Eat/Drink dont la réserve s'est
            // épuisée retombe sur la poursuite SeekFood/SeekWater (SYNE-042).
            if (chosenKind == DesireKind.Eat && !_catalog.IsViable(DesireKind.Eat, _stocks))
            {
                chosenKind = DesireKind.SeekFood;
            }
            else if (chosenKind == DesireKind.Drink && !_catalog.IsViable(DesireKind.Drink, _stocks))
            {
                chosenKind = DesireKind.SeekWater;
            }
        }

        // Interruptions d'action (SYNE-043, décision n°15, COGNITIVE_ARCHITECTURE.md §6) :
        // déclencheur centralisé — un besoin critique dont l'utilité surpasse de >
        // utilityExcessMargin l'action en cours reprend la main, y compris entre
        // deux délibérations. Eat/Drink répondent à la faim critique si la réserve est disponible.
        if (_options.Agents.Actions.Interruption.Enabled)
        {
            (IReadOnlyList<UtilityScore> scores, UtilityScore? interruption) = _interruption.Evaluate(
                actions.Interruption,
                actions,
                mind.Needs,
                mind.Factors ?? new AgentFactors(TraitSet.NeutralAll),
                mind.SuccessRate,
                chosenKind,
                mind.Intention,
                currentTick,
                entity.Id.Value);

            if (interruption is { } chosen)
            {
                mind.RecordDecision(scores, chosen, deliberated: false, interrupted: true, currentTick, entity.Id.Value);
                mind.DeliberatedThisTick = false;
                mind.InterruptedThisTick = true;
                chosenKind = chosen.Kind;
            }
        }

        Goal? goal = chosenKind == DesireKind.Idle
            ? new Goal(DesireKind.Idle, currentTick)
            : mind.Intention is { } intention && intention.Kind == chosenKind
                ? intention
                : new Goal(chosenKind, currentTick);
        mind.Intention = goal;

        ActionResult result = _executor.Execute(entity, mind, chosenKind, currentTick);
        mind.RecordAction(result);
    }

    /// <summary>Fréquence de délibération configurable (décalée par entité pour lisser la charge).</summary>
    private bool ShouldDeliberate(Entity entity, ulong currentTick) =>
        (currentTick + entity.Id.Value) % (ulong)_options.Agents.Actions.Deliberation.IntervalTicks == 0;

    /// <summary>
    /// Objectifs encore actifs : besoin encore déclenché ou objectif récemment
    /// défini (poursuite). Les actions terminales Eat/Drink (SYNE-042) ne
    /// persistent pas : elles restent actives tant que le besoin est déclenché et
    /// que la réserve est disponible.
    /// </summary>
    private IReadOnlyList<Goal> ActiveGoals(MindState mind, ulong currentTick)
    {
        if (mind.Intention is not { } intention)
        {
            return [];
        }

        bool stillNeeded = mind.Needs.IsTriggered(intention.Kind, _options.Agents.Needs);
        bool recent = intention.Age(currentTick) < 100;
        bool viable = intention.Kind is not (DesireKind.Eat or DesireKind.Drink) ||
                      _catalog.IsViable(intention.Kind, _stocks);

        return (stillNeeded || (recent && !IsTerminal(intention.Kind))) && viable ? [intention] : [];
    }

    private static bool IsTerminal(DesireKind kind) => kind is DesireKind.Eat or DesireKind.Drink;

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
                mind.Intention?.Kind,
                mind.CollectiveObjective));
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
                actions,
                mind.CollectiveObjective);

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

    /// <summary>
    /// Partage social (SYNE-050, V0.1) : une entité sociable (trait ≥ 0.5) qui
    /// perçoit une entité vivante émet une pulsation <b>Information</b> publique
    /// — une au plus par tick (bien sous <c>maxSendsPerTick</c>). La décision de
    /// partage ne consomme aucun tirage du PRNG global (DETERMINISM.md §3).
    /// </summary>
    private bool WantsToShare(Entity entity, Observation observation)
    {
        if (observation.EntityType != "entity")
        {
            return false;
        }

        return entity.Traits["sociability"] >= 0.5;
    }

    private void EnqueueSharePulse(Entity entity, Observation observation, MindState mind, ulong currentTick)
    {
        string payload = $"perceived-{observation.EntityId}#{CanonicalPosition(observation.Position)}";
        Message share = CommunicationSystem.CreateMessage(
            entity.Id.Value,
            targetId: null,
            MessageType.Information,
            payload,
            currentTick,
            sequence: 1);
        mind.Communication.Enqueue(share);
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