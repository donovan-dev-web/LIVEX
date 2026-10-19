// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Pipeline cognitif BDI (SYNE-010) exécuté à chaque tick dans l'ordre causal strict : perception → mémoire →
//        croyances → besoins → objectifs → faisabilité → utilité → délibération → intention → action (« boucle
//        10/15 étapes », jalon U1).
//
//        Réalignement (Phases 2-4 du plan) :
//        — la génération des candidats et l'évaluation d'utilité consomment la CONNAISSANCE active de l'agent
//          (croyances de gisement et de pair, repli mémoire) et non plus seulement les seuils de besoin ;
//        — les objectifs portent une cible concrète (« eau en P, connue à t ») et le déplacement la poursuit ;
//        — les conséquences d'action sont différées à la fin du tick (fenêtre causale fermée, chapitre III) ;
//        — un message compris alimente une croyance nommée, et la trace décisionnelle porte cible, preuves et
//          candidats rejetés (absence de décision ≠ zéro).

using System.Globalization;
using Simulation.Core.Actions;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Perception;
using Simulation.Core.Performance;
using Simulation.Core.Population;
using Simulation.Core.Social;

namespace Simulation.Core.Cognition;

/// <summary>
/// Pipeline cognitif BDI (SYNE-010) exécuté à
/// chaque tick dans l'ordre causal strict :
/// perception → mémoire → croyances → besoins → objectifs → faisabilité →
/// utilité → délibération → intention → action (« boucle 10/15 étapes », jalon U1).
///
/// Depuis le jalon ph4, l'exécution des actions sort du pipeline : elle est
/// confiée au catalogue déclaratif + exécuteur atomique (SYNE-040/041/042) et
/// les interruptions au déclencheur centralisé (SYNE-043).
///
/// <para>
/// <b>Fenêtre causale (chapitre III §1)</b> : pendant l'itération des entités,
/// aucune mutation de monde n'est appliquée — déplacements et effets portés à un
/// pair sont mis en file dans un <see cref="TickCommitBuffer"/> et appliqués après
/// la boucle, dans l'ordre des identifiants. Ce qu'un agent perçoit à un tick est
/// donc l'état figé en début de tick, et non l'état à moitié mis à jour par un
/// agent traité plus tôt (S-15/S-31/D-11/B-06).
/// </para>
///
/// <b>Déterminisme</b> : aucune consommation du PRNG (l'avance du générateur reste à
/// un tirage par tick), ordre d'itération par identifiant croissant, agrégats en
/// représentation invariante à la culture. La prospection sans repère connu reste
/// un offset pseudo-aléatoire déterministe (hash stable) — déclarée comme telle.
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
    private readonly TickBudgetCollector? _budget;
    private readonly Dictionary<ulong, MindState> _minds = new();

    // Mémoïsation par tick des fragments canoniques d'observation (sujet
    // « entity-N », position « X,Y »). Pendant l'itération des entités, aucune
    // position ne change (fenêtre causale fermée : les déplacements sont appliqués
    // au commit de fin de tick), donc la même chaîne est valable pour tous les
    // observateurs du tick. Purement identique au résultat : mêmes chaînes, mêmes
    // croyances — seule l'allocation répétée disparaît (à forte densité, un agent
    // perçoit des dizaines de voisins et chaque observation format toutes ses
    // clés).
    private readonly Dictionary<ulong, string> _entitySubjectCache = new();
    private readonly Dictionary<ulong, string> _positionCache = new();
    private ulong _cacheTick = ulong.MaxValue;

    public CognitionPipeline(
        Simulation.Core.World.World world,
        SimulationOptions options,
        World.ResourceStocks stocks,
        TickBudgetCollector? budget = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(stocks);
        _world = world;

        // Échelle temporelle : les quantités de classe A (taux de
        // besoins, décroissances mémoire/croyances/confiance, coûts énergétiques
        // de mouvement/repos) sont mises à l'échelle **une fois** ici, de façon
        // déterministe, à partir de `dt = simulatedSecondsPerTick / 60` — le
        // sous-arbre agents des options « effectives » porte ces valeurs par
        // tick effectif. À `dt == 1` (défaut, reference), TemporalScale retourne
        // les instances d'entrée : le run est bit-à-bit identique (pin).
        double dt = Configuration.SimulationClock.From(options.Simulation).SimulatedMinutesPerTick;
        _options = Configuration.TemporalScale.WithScaledAgentSettings(
            options,
            Configuration.TemporalScale.ScaleNeeds(options.Agents.Needs, dt),
            Configuration.TemporalScale.ScaleMemory(options.Agents.Memory, dt),
            Configuration.TemporalScale.ScaleBeliefs(options.Agents.Beliefs, dt),
            Configuration.TemporalScale.ScaleTrust(options.Agents.Trust, dt),
            Configuration.TemporalScale.ScaleActions(options.Agents.Actions, dt));

        _perception = new PerceptionSystem(world, _options.Agents.Perception);
        _stocks = stocks;
        _catalog = new ActionCatalog(_options.Agents.Actions);
        _executor = new ActionExecutor(world, _catalog, stocks, _options);
        _interruption = new InterruptionTrigger(_catalog, stocks);
        _communication = new CommunicationSystem(world, _options.Communication);
        _groups = new GroupSystem(_options.Groups);
        _birth = new BirthSystem(_options.Reproduction);
        _death = new DeathSystem(_options.Agents.Life);
        _budget = budget;
    }

    /// <summary>
    /// Options effectives du run : sous-arbre <c>agents</c> mis à
    /// l'échelle temporelle (classe A par tick effectif), autres sections
    /// partagées avec la config d'origine. La restauration bit-à-bit reconstruit
    /// les esprits avec ces réglages pour rester cohérente avec le run sauvé.
    /// </summary>
    internal SimulationOptions EffectiveOptions => _options;

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

    /// <summary>Paires (identifiant, esprit) triées par identifiant croissant — persistance bit-à-bit.</summary>
    internal IReadOnlyList<(ulong Id, MindState Mind)> MindPairsSorted() =>
        _minds
            .Select(pair => (pair.Key, pair.Value))
            .OrderBy(pair => pair.Key)
            .ToList();

    public MindState MindOf(ulong entityId) => _minds[entityId];

    public bool HasMind(ulong entityId) => _minds.ContainsKey(entityId);

    /// <summary>
    /// Exécute une itération complète du pipeline pour toutes les entités.
    /// <list type="number">
    /// <item><b>Itération des entités</b> (identifiants croissants) : perception sur
    /// l'état figé, cognition, décision, action — les mutations de monde sont mises
    /// en file.</item>
    /// <item><b>Commit des conséquences</b> : déplacements et effets inter-entités
    /// appliqués en une fois.</item>
    /// <item><b>Communication</b> puis réception → <b>croyance</b> ; engagements si
    /// activés.</item>
    /// <item><b>Décroissances</b> (croyances, confiance), <b>groupes</b> (sur les
    /// positions finales), <b>naissances</b>, <b>mortalité</b>.</item>
    /// </list>
    /// </summary>
    public void Step(ulong currentTick)
    {
        if (_cacheTick != currentTick)
        {
            _cacheTick = currentTick;
            _entitySubjectCache.Clear();
            _positionCache.Clear();
        }

        List<Entity> ordered = _world.Entities
            .OrderBy(entity => entity.Id.Value)
            .ToList();

        var commit = new TickCommitBuffer();
        _executor.BeginTick(commit);
        try
        {
            foreach (Entity entity in ordered)
            {
                StepEntity(entity, currentTick);
            }


            // Fenêtre causale fermée (ch. III) : les conséquences d'action —
            // déplacements et dégâts — ne rejoignent le monde qu'ici. Aucun agent
            // n'a donc perçu, décidé ni agi sur un état modifié par un autre agent
            // du même tick, et l'ordre des identifiants n'est plus une loi causale.
            commit.Commit(_world);
        }
        finally
        {
            _executor.BeginTick(null);
            commit.Clear();
        }

        // Communication de masse en une passe (performance.batchCommunication,
        // cartes après perception/décision/action
        // — l'ordre causal est agent par agent puis réseau de communication.
        using (TickPhaseScope communicationScope = _budget?.Begin(TickPhase.Communication) ?? TickPhaseScope.Noop)
        {
            _communication.Step(currentTick, _minds);

            // Réception → croyance (D-13) : un message compris alimente une
            // croyance nommée (provenance = émetteur, confiance = fiabilité de
            // transmission × confiance relationnelle × confiance du témoignage).
            ApplyReceivedMessages(currentTick, ordered);

            // Engagements (D5) : après la
            // diffusion — création d'engagement à la réception d'une Response
            // positive, réponse des entités très sociables aux Request comprises.
            // Désactivé par défaut (trajectoire de référence).
            if (_options.Agents.Actions.Commitments.Enabled)
            {
                ProcessCommitmentMessages(currentTick);
            }
        }

        using (TickPhaseScope eventsScope = _budget?.Begin(TickPhase.EventsGroupsPopulation) ?? TickPhaseScope.Noop)
        {
            foreach (MindState mind in _minds.Values)
            {
                mind.Beliefs.Tick(currentTick, _options.Agents.Beliefs);
                mind.Trust.Tick();
            }

            // Engagements (D5) : résolution du cycle — honororation (le prometteur
            // a porté aide/échange/socialisation ce tick) ou expiration (pénalité
            // de confiance, distincte du mensonge factuel). Ordre : identifiants
            // croissants.
            if (_options.Agents.Actions.Commitments.Enabled)
            {
                ResolveCommitments(currentTick);
            }

            // Groupes émergents (SYNE-060/061) puis naissances (SYNE-062) : après la
            // boucle des entités, la communication de masse et les décréments de
            // croyance/confiance — la population ne mute qu'après l'itération complète
            // (ordre causal strict). La cohésion est évaluée sur les positions
            // <b>finales</b> du tick (post-commit) : elles servent à INSTRUMENTER la
            // distance des paires liées (arbitrage A0-3), jamais à filtrer la liaison.
            // Positions construites seulement quand la revue de groupes a lieu :
            // hors revue (LOD `reviewIntervalTicks`), la table serait un coût par
            // tick pour un système qui n'itère même pas.
            bool groupsReview = _options.Groups.Enabled
                && currentTick > 0
                && currentTick % (ulong)_options.Groups.ReviewIntervalTicks == 0;
            Dictionary<ulong, World.Position>? positions = groupsReview ? Positions() : null;
            _groups.Step(currentTick, _minds, positions);
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
                _groups.PurgeDeceased(deceased, currentTick, _minds, positions ?? Positions());
                foreach (ulong deathId in deceased)
                {
                    _minds.Remove(deathId);
                }
            }
        }
    }

    /// <summary>Positions courantes des esprits (post-commit) — cohésion de groupe.</summary>
    private Dictionary<ulong, World.Position> Positions()
    {
        var positions = new Dictionary<ulong, World.Position>(_minds.Count);
        foreach (Entity entity in _world.Entities)
        {
            if (_minds.ContainsKey(entity.Id.Value))
            {
                positions[entity.Id.Value] = entity.Position;
            }
        }

        return positions;
    }

    /// <summary>
    /// Réception d'un message compris → <b>croyance</b> (D-13/S-21). Le contenu
    /// (position d'entité ou état de gisement) devient un fait cru, avec une
    /// provenance explicite (<c>message-&lt;émetteur&gt;</c>) et une confiance
    /// dégradée par la fiabilité de transmission et la confiance relationnelle.
    /// Un message non compris, de soi-même, ou au contenu non interprétable ne
    /// produit <b>aucune</b> croyance.
    /// </summary>
    private void ApplyReceivedMessages(ulong currentTick, IReadOnlyList<Entity> ordered)
    {
        // Ordre d'itération par identifiant croissant, réutilisé de la boucle
        // d'entités (déjà trié) : retrier le dictionnaire des esprits à chaque tick
        // allouait et comparait N éléments pour un ordre identique.
        foreach (Entity entity in ordered)
        {
            ulong entityId = entity.Id.Value;
            MindState mind = _minds[entityId];
            foreach ((Message message, bool understood) in mind.Communication.ReceivedThisTick())
            {
                if (!understood || message.SenderId == entityId)
                {
                    continue;
                }

                if (!KnowledgeBuilder.TryParseSharedObservation(message.Payload, out Fact fact, out double payloadConfidence))
                {
                    continue;
                }

                double reliability = Math.Clamp(
                    message.Confidence * mind.Trust.TrustWith(message.SenderId) * payloadConfidence,
                    0.0,
                    1.0);
                if (reliability <= 0.0)
                {
                    continue;
                }

                mind.Beliefs.ApplyObservation(
                    fact,
                    reliability,
                    $"message-{message.SenderId}",
                    _options.Agents.Beliefs,
                    currentTick);
                mind.Memory.Store(
                    MemoryCategory.Interaction,
                    $"message-{message.SenderId}",
                    message.Payload,
                    reliability,
                    currentTick);
            }
        }
    }

    private void StepEntity(Entity entity, ulong currentTick)
    {
        MindState mind = GetOrCreate(entity.Id.Value, entity);
        mind.Factors ??= new AgentFactors(entity.Traits);
        mind.DeliberatedThisTick = false;
        mind.InterruptedThisTick = false;
        mind.DecisionStateThisTick = DecisionState.None;

        int observed = 0;
        bool announced = false;
        using (TickPhaseScope perceptionScope = _budget?.Begin(TickPhase.Perception) ?? TickPhaseScope.Noop)
        {
            if (_perception.ShouldPerceive(entity, currentTick))
            {
                IReadOnlyList<Observation> observations = _perception.Perceive(entity, currentTick);
                observed = observations.Count;
                AgentFactors shareFactors = mind.Factors ?? new AgentFactors(entity.Traits);

                // Source de provenance calculée <b>une fois par entité et par tick</b> :
                // elle ne dépend que de l'observateur, alors que le chemin de stockage
                // l'exigeait par observation — à densité (des dizaines de voisins
                // perçus), c'était une chaîne interpolée par observation pour une
                // valeur identique. Contenu inchangé (mêmes empreintes, mêmes
                // croyances : la source est comparée par valeur).
                string source = $"perception-{entity.Id.Value}";
                foreach (Observation observation in observations)
                {
                    StoreObservation(mind, entity, observation, currentTick, source);
                    if (!announced && WantsToShare(shareFactors, observation))
                    {
                        EnqueueSharePulse(entity, observation, mind, currentTick);
                        announced = true;
                    }
                }
            }
        }

        using (TickPhaseScope memoryScope = _budget?.Begin(TickPhase.MemoryBeliefs) ?? TickPhaseScope.Noop)
        {
            mind.AcknowledgePerceptions(observed, currentTick);
            mind.Needs.Advance(_options.Agents.Needs);
        }

        ActionSettings actions = _options.Agents.Actions;
        DesireKind chosenKind;
        mind.DefendingThisTick = false;
        mind.SkippedBySalience = false;

        // Connaissance active (Phase 2) : cibles crues issues des croyances puis,
        // en repli, de la mémoire rappelée. Modèle localisé uniquement : hors
        // sources localisées, la décision garde le modèle global historique.
        //
        // Construction <b>paresseuse</b> : elle n'a lieu qu'aux ticks qui en ont
        // besoin (délibération, état critique), pas à chaque tick de chaque agent —
        // la construction parcourt les croyances et peut interroger la mémoire.
        CognitionKnowledge? knowledge = null;
        bool knowledgeFresh = false;

        CognitionKnowledge? Knowledge()
        {
            if (!knowledgeFresh)
            {
                knowledge = _options.Resources.Localized
                    ? KnowledgeBuilder.Build(
                        mind,
                        entity.Position,
                        currentTick,
                        _options.Agents.Beliefs,
                        _options.Resources.InteractionRadius)
                    : null;
                knowledgeFresh = true;
            }

            return knowledge;
        }

        // Demandes d'aide (D5) : émission d'une Request quand la faim est critique
        // — la réponse des pairs crée l'engagement (voir ProcessCommitmentMessages).
        if (_options.Agents.Actions.Commitments.Enabled)
        {
            EnqueueHelpRequest(entity, mind, currentTick);
        }

        using (TickPhaseScope decisionScope = _budget?.Begin(TickPhase.NeedsGoals) ?? TickPhaseScope.Noop)
        {
            // Contrôle de saillance (D2, étape
            // 3bis) : entre deux délibérations planifiées, l'intention est poursuivie
            // SAUF saillance (besoin franchi / condition d'interruption) ou filet de
            // sécurité périodique. Désactivé par défaut (trajectoire de référence).
            bool deliberate = ShouldDeliberate(entity, currentTick);
            if (!deliberate && actions.Salience.Enabled)
            {
                double salience = ComputeSalience(mind, actions);
                mind.LastSalienceScore = salience;
                bool forced = actions.Salience.ForcedReconsiderationTicks > 0
                    && currentTick - mind.LastDeliberationTick >= (ulong)actions.Salience.ForcedReconsiderationTicks;
                if (salience < actions.Salience.ReconsiderThreshold && !forced)
                {
                    mind.SkippedBySalience = true;
                    deliberate = false;
                }
                else
                {
                    mind.SkippedBySalience = false;
                    deliberate = true;
                }
            }

            // Délibération à fréquence configurable : entre deux
            // délibérations, l'intention est conservée (holdover) telle quelle.
            if (deliberate)
            {
                CognitionKnowledge? current = Knowledge();
                IReadOnlyList<Goal> active = ActiveGoals(mind, entity, currentTick, current);
                IReadOnlyList<Goal> generated = DesireFactory.Generate(
                    mind.Needs,
                    currentTick,
                    active.Select(goal => goal.Kind),
                    _catalog,
                    _stocks,
                    current);
                var candidates = new List<Goal>(active.Count + generated.Count);
                candidates.AddRange(active);
                candidates.AddRange(generated);

                // Bibliothèque de plans (D3) : ajoute
                // des candidats de manipulation (Take/Trade) pour les objectifs de
                // besoin — désactivée par défaut (trajectoire de référence).
                IReadOnlyList<Goal> planCandidates = PlanLibrary.GenerateCandidates(
                    candidates, mind, entity, _catalog, _stocks,
                    actions.Inventory, actions.Plans, currentTick);
                candidates.AddRange(planCandidates);

                // Référence d'hystérésis : l'intention courante, <b>sauf</b> si son
                // objectif vient d'être accompli (cible atteinte) — comparer le
                // nouveau choix à un objectif terminé bloquerait l'action terminale.
                DesireKind? hysteresisReference = TargetReached(entity, mind) ? null : mind.Intention?.Kind;
                (IReadOnlyList<UtilityScore> scores, UtilityScore best) = Deliberate(
                    mind, candidates, currentTick, entity.Id.Value, current, hysteresisReference);
                GoalTarget? target = ResolveTarget(best.Kind, current, entity.Position);
                mind.RecordDecision(
                    scores,
                    best,
                    deliberated: true,
                    interrupted: false,
                    currentTick,
                    entity.Id.Value,
                    target,
                    EvidenceFor(mind, target));
                mind.DeliberatedThisTick = true;
                mind.RecordDeliberationState(currentTick, TriggeredNeeds(mind, _options.Agents.Needs));
                chosenKind = best.Kind;
            }
            else
            {
                chosenKind = mind.LastDecision is { } held ? held.Kind : DesireKind.Idle;

                // Holdover : une action terminale Eat/Drink dont la source n'est
                // plus à portée (ou dont la réserve globale s'est épuisée) retombe
                // sur la poursuite ciblée, puis sur la prospection. Le holdover ne
                // reconstruit pas la connaissance (coût) : la cible de l'intention
                // est conservée telle quelle et l'action échouera explicitement
                // (Blocked motivée) si elle n'est plus applicable — la prochaine
                // délibération (≤ interval) ré-ancre l'agent.
                chosenKind = HoldoverFallback(chosenKind, knowledgeFresh ? knowledge : null);

                if (mind.LastDecision is null)
                {
                    // Aucune décision antérieure : le tick est une ABSENCE de
                    // décision (jamais un zéro d'utilité, jamais un score ancien).
                    mind.RecordMissingDecision(currentTick, entity.Id.Value);
                }
                else
                {
                    mind.DecisionStateThisTick = DecisionState.Holdover;
                }
            }

            // Interruptions d'action (SYNE-043) :
            // déclencheur centralisé — un besoin critique dont l'utilité surpasse de >
            // utilityExcessMargin l'action en cours reprend la main, y compris entre
            // deux délibérations. Eat/Drink répondent à la faim critique si la source
            // est connue (gisement à portée, modèle localisé) ou disponible (réserve globale).
            bool criticalNeeds = _options.Agents.Actions.Interruption.Enabled
                && mind.Needs.IsCriticalFor(actions.Interruption);
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
                    entity.Id.Value,
                    knowledge: criticalNeeds ? Knowledge() : null);

                if (interruption is { } chosen)
                {
                    GoalTarget? target = ResolveTarget(chosen.Kind, criticalNeeds ? knowledge : null, entity.Position);
                    mind.RecordDecision(
                        scores,
                        chosen,
                        deliberated: false,
                        interrupted: true,
                        currentTick,
                        entity.Id.Value,
                        target,
                        EvidenceFor(mind, target));
                    mind.DeliberatedThisTick = false;
                    mind.InterruptedThisTick = true;
                    chosenKind = chosen.Kind;
                }
            }
        }

        mind.Intention = BuildGoal(chosenKind, mind, knowledge, knowledgeFresh, currentTick, entity.Position);

        using (TickPhaseScope actionScope = _budget?.Begin(TickPhase.ActionsMovement) ?? TickPhaseScope.Noop)
        {
            ActionResult result = _executor.Execute(entity, mind, chosenKind, currentTick, MindOf);
            mind.RecordAction(result);
        }
    }

    /// <summary>
    /// Objectif du tick : conserve l'intention courante (et donc sa
    /// <b>destination persistante</b>) tant que le type est identique et que la
    /// cible reste cru ; sinon l'objectif est ré-ancré sur la cible dérivée de la
    /// connaissance — ou sans cible si l'agent ne sait rien (B-01/B-02).
    /// </summary>
    private Goal BuildGoal(
        DesireKind kind,
        MindState mind,
        CognitionKnowledge? knowledge,
        bool knowledgeFresh,
        ulong currentTick,
        World.Position self)
    {
        // Holdover sans connaissance rafraîchie : l'intention est conservée
        // <b>telle quelle</b>, cible comprise — on ne « perd » pas une destination
        // simplement parce qu'on n'a pas relu le monde ce tick.
        if (!knowledgeFresh && mind.Intention is { } held && held.Kind == kind)
        {
            return held;
        }

        GoalTarget? target = ResolveTarget(kind, knowledge, self);

        if (mind.Intention is { } current && current.Kind == kind)
        {
            if (current.Target is { } kept && target is { } derived &&
                string.Equals(kept.Label, derived.Label, StringComparison.Ordinal))
            {
                return current;
            }

            if (current.Target is null && target is null)
            {
                return current;
            }
        }

        return new Goal(kind, currentTick, target);
    }

    /// <summary>Cible concrète d'une action, dérivée de la connaissance active.</summary>
    private GoalTarget? ResolveTarget(DesireKind kind, CognitionKnowledge? knowledge, World.Position self) =>
        kind switch
        {
            DesireKind.Flee => knowledge?.FleeDestination(self, _options.Agents.Perception.Radius * 0.5, _world.Size),
            _ => knowledge?.TargetFor(kind),
        };

    /// <summary>
    /// Faits justificatifs d'un choix : provenance, sujet, confiance et âge de la
    /// croyance qui porte la cible (ou mention explicite d'une cible <b>mémorée</b>
    /// faute de croyance vive). C'est ce qui permet de remonter la décision jusqu'à
    /// ses entrées (D-06).
    /// </summary>
    internal static IReadOnlyList<DecisionEvidence> EvidenceFor(MindState mind, GoalTarget? target)
    {
        if (target is not { } aimed)
        {
            return [];
        }

        string subject = aimed.TargetType == "node" ? $"node-{aimed.Label}" : aimed.Label;
        var evidence = new List<DecisionEvidence>();
        foreach (Belief belief in mind.Beliefs.AllInPlace)
        {
            if (!string.Equals(belief.Fact.Subject, subject, StringComparison.Ordinal))
            {
                continue;
            }

            evidence.Add(new DecisionEvidence(
                belief.Source,
                $"{belief.Fact.Subject}.{belief.Fact.Predicate}",
                belief.Confidence,
                belief.UpdatedTick));
        }

        if (evidence.Count == 0)
        {
            evidence.Add(new DecisionEvidence("memory", subject, aimed.Confidence, aimed.ObservedTick));
        }

        return evidence;
    }

    /// <summary>
    /// Score de saillance (D2) : généralisation O(k) des conditions d'interruption
    /// — lit les deltas déjà produits par les étapes 1-3 sans recalculer la boucle
    /// cognitive. Une condition d'interruption remplie déclenche la reconsidération
    /// indépendamment du seuil (RETOURNER SEUIL_MAX, invalidation incluse).
    /// </summary>
    private double ComputeSalience(MindState mind, ActionSettings actions)
    {
        double score = 0.0;
        IReadOnlyList<DesireKind> triggered = TriggeredNeeds(mind, _options.Agents.Needs);
        foreach (DesireKind kind in triggered)
        {
            if (!mind.TriggeredAtLastDeliberation.Contains(kind))
            {
                score += actions.Salience.NeedThresholdWeight;
            }
        }

        // Condition d'interruption remplie (critique) : reconsidération forcée.
        if (mind.Needs.IsCriticalFor(actions.Interruption))
        {
            return double.MaxValue;
        }

        return score;
    }

    private IReadOnlyList<DesireKind> TriggeredNeeds(MindState mind, NeedsSettings needs)
    {
        var triggered = new List<DesireKind>();
        foreach (DesireKind kind in Enum.GetValues<DesireKind>())
        {
            if (kind is DesireKind.Idle or DesireKind.Take or DesireKind.Give or DesireKind.Trade
                or DesireKind.Attack or DesireKind.Defend)
            {
                continue;
            }

            if (mind.Needs.IsTriggered(kind, needs))
            {
                triggered.Add(kind);
            }
        }

        return triggered;
    }

    /// <summary>Fréquence de délibération configurable (décalée par entité pour lisser la charge).</summary>
    private bool ShouldDeliberate(Entity entity, ulong currentTick) =>
        (currentTick + entity.Id.Value) % (ulong)_options.Agents.Actions.Deliberation.IntervalTicks == 0;

    /// <summary>
    /// Demandes d'aide (D5, version minimale) : une entité en faim critique, sans
    /// engagement en attente, émet une pulsation <c>Request</c> publique « help-food »
    /// au plus une fois par 20 ticks (cooldown déterministe par identifiant — la
    /// production réelle de Request remplace la consommation passive de la file).
    /// </summary>
    private void EnqueueHelpRequest(Entity entity, MindState mind, ulong currentTick)
    {
        if (mind.Needs.Hunger <= _options.Agents.Actions.Interruption.CriticalHunger
            || mind.Commitments.Any(commitment => commitment.Status == CommitmentStatus.Pending)
            || (currentTick + entity.Id.Value) % 20 != 0)
        {
            return;
        }

        Message request = CommunicationSystem.CreateMessage(
            entity.Id.Value,
            targetId: null,
            MessageType.Request,
            "help-food",
            currentTick,
            sequence: 2);
        mind.Communication.Enqueue(request, _options.Communication.MaxSendsPerTick);
    }

    /// <summary>
    /// Cycle des messages d'engagement (D5) : création d'un <see cref="Commitment"/>
    /// chez le demandeur qui reçoit une <c>Response</c> positive (borné : au plus un
    /// engagement actif par pair, au plus 3 engagements actifs — impact mémoire
    /// borné), puis réponse positive des entités très sociables
    /// (facteur ≥ 1.0) à la première <c>Request</c> comprise du tick.
    /// </summary>
    private void ProcessCommitmentMessages(ulong currentTick)
    {
        foreach ((ulong entityId, MindState mind) in _minds.OrderBy(pair => pair.Key))
        {
            foreach ((Message message, bool understood) in mind.Communication.ReceivedThisTick())
            {
                if (!understood || message.SenderId == entityId)
                {
                    continue;
                }

                if (message.Type == MessageType.Response
                    && !mind.Commitments.Any(commitment =>
                        commitment.ToEntityId == message.SenderId && commitment.Status == CommitmentStatus.Pending)
                    && mind.Commitments.Count < 3)
                {
                    mind.Commitments.Add(new Commitment(
                        message.SenderId,
                        "survival",
                        currentTick,
                        currentTick + _options.Agents.Actions.Commitments.ExpiryTicks));
                }
            }
        }

        foreach ((ulong entityId, MindState mind) in _minds.OrderBy(pair => pair.Key))
        {
            if ((mind.Factors?.Sociability ?? 0.0) < 1.0)
            {
                continue;
            }

            foreach ((Message message, bool understood) in mind.Communication.ReceivedThisTick())
            {
                if (!understood || message.SenderId == entityId || message.Type != MessageType.Request)
                {
                    continue;
                }

                Message response = CommunicationSystem.CreateMessage(
                    entityId,
                    message.SenderId,
                    MessageType.Response,
                    "yes",
                    currentTick,
                    sequence: 3);
                mind.Communication.Enqueue(response, _options.Communication.MaxSendsPerTick);
                break;
            }
        }
    }

    /// <summary>
    /// Résolution des engagements (D5) : honoré si le prometteur a exécuté
    /// Give/Trade/Socialize ce tick (l'aide promises s'est matérialisée) →
    /// <c>TrustLevel += commitmentBonus</c> ; expiré sans honororation →
    /// <c>TrustLevel -= commitmentPenalty</c> (traité comme rompu).
    /// Les engagements résolus sont purgés (suivi borné). Identifiants croissants.
    /// </summary>
    private void ResolveCommitments(ulong currentTick)
    {
        foreach ((ulong _, MindState mind) in _minds.OrderBy(pair => pair.Key))
        {
            for (int i = mind.Commitments.Count - 1; i >= 0; i--)
            {
                Commitment commitment = mind.Commitments[i];
                if (commitment.Status != CommitmentStatus.Pending)
                {
                    mind.Commitments.RemoveAt(i);
                    continue;
                }

                if (currentTick >= commitment.ExpiryTick)
                {
                    commitment.Expire(currentTick);
                    mind.Trust.Penalize(commitment.ToEntityId, _options.Agents.Trust.CommitmentPenalty);
                    mind.Commitments.RemoveAt(i);
                    continue;
                }

                if (_minds.TryGetValue(commitment.ToEntityId, out MindState? responder)
                    && responder.LastActionResult is { } action
                    && action.Outcome == ActionOutcome.Executed
                    && action.Kind is DesireKind.Give or DesireKind.Trade or DesireKind.Socialize)
                {
                    commitment.Fulfill(currentTick);
                    mind.Trust.Reward(commitment.ToEntityId, _options.Agents.Trust.CommitmentBonus);
                    mind.Commitments.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Objectifs encore actifs : besoin encore déclenché ou objectif récemment
    /// défini (poursuite), <b>et cible encore crue</b>. Une cible dont la croyance a
    /// expiré n'est plus un objectif : il est abandonné et la délibération ré-ancre
    /// l'agent (B-01 : « abandon / révision » d'une cible devenue invalide). Les
    /// actions terminales Eat/Drink ne persistent pas et exigent la source à portée.
    /// </summary>
    private IReadOnlyList<Goal> ActiveGoals(MindState mind, Entity self, ulong currentTick, CognitionKnowledge? knowledge)
    {
        if (mind.Intention is not { } intention)
        {
            return [];
        }

        // Objectif <b>accompli</b> (B-01/B-02) : une poursuite dont la cible connue
        // est atteinte cesse d'être un objectif — sans quoi elle serait rejouée à
        // chaque délibération (le déplacement vers un point qu'on occupe), et
        // l'hystérésis anti-oscillation comparerait l'action terminale au
        // <i>même</i> objectif sous sa forme « poursuite » (à utilité égale, la
        // poursuite l'emportait : l'agent campait sur la source sans jamais
        // consommer). La satisfaction d'arrivée est appliquée par l'exécuteur ; la
        // délibération suivante repart de la connaissance et choisit l'action
        // terminale (Eat/Drink) ou un nouvel objectif.
        if (IsPursuit(intention.Kind) && TargetReached(self, mind))
        {
            return [];
        }

        bool stillNeeded = mind.Needs.IsTriggered(intention.Kind, _options.Agents.Needs);
        bool recent = intention.Age(currentTick) < 100;
        bool viable = intention.Kind switch
        {
            DesireKind.Eat => knowledge is { } knownFood ? knownFood.AtFoodSource : _catalog.IsViable(DesireKind.Eat, _stocks),
            DesireKind.Drink => knowledge is { } knownWater ? knownWater.AtWaterSource : _catalog.IsViable(DesireKind.Drink, _stocks),
            _ => true,
        };
        bool targetKnown = knowledge is null ||
                           intention.Target is not { } aimed ||
                           TargetStillKnown(aimed, knowledge.Value);

        return (stillNeeded || (recent && !IsTerminal(intention.Kind))) && viable && targetKnown ? [intention] : [];
    }

    /// <summary>
    /// La cible connue de l'intention est-elle <b>atteinte</b> (à portée
    /// d'interaction) ? C'est la condition d'accomplissement d'une poursuite : le
    /// but cesse alors d'être poursuivi, l'action terminale peut être choisie.
    /// </summary>
    private bool TargetReached(Entity self, MindState mind) =>
        mind.Intention is { Target: { } target } &&
        self.Position.DistanceTo(target.Position) <= _options.Resources.InteractionRadius;

    /// <summary>Une poursuite (déplacement vers une cible connue) s'oppose aux actions terminales, qui ne persistent pas.</summary>
    private static bool IsPursuit(DesireKind kind) => kind switch
    {
        DesireKind.SeekFood or DesireKind.SeekWater or DesireKind.Explore
            or DesireKind.Socialize or DesireKind.Flee => true,
        _ => false,
    };

    /// <summary>La cible crue de l'intention est-elle encore présente dans la connaissance active ?</summary>
    private static bool TargetStillKnown(GoalTarget target, CognitionKnowledge knowledge) =>
        string.Equals(knowledge.FoodSource?.Label, target.Label, StringComparison.Ordinal) ||
        string.Equals(knowledge.WaterSource?.Label, target.Label, StringComparison.Ordinal) ||
        string.Equals(knowledge.Landmark?.Label, target.Label, StringComparison.Ordinal) ||
        string.Equals(knowledge.Peer?.Label, target.Label, StringComparison.Ordinal);

    private static bool IsTerminal(DesireKind kind) => kind is DesireKind.Eat or DesireKind.Drink;

    /// <summary>
    /// Repli d'une action terminale poursuivie (holdover) : la source n'est plus à
    /// portée → poursuite ciblée si l'agent connaît un gisement, prospection sinon.
    /// </summary>
    private DesireKind HoldoverFallback(DesireKind kind, CognitionKnowledge? knowledge)
    {
        if (knowledge is { } known)
        {
            if (kind == DesireKind.Eat && !known.AtFoodSource)
            {
                return known.KnowsFood ? DesireKind.SeekFood : DesireKind.Explore;
            }

            if (kind == DesireKind.Drink && !known.AtWaterSource)
            {
                return known.KnowsWater ? DesireKind.SeekWater : DesireKind.Explore;
            }

            return kind;
        }


        if (kind == DesireKind.Eat && !_catalog.IsViable(DesireKind.Eat, _stocks))
        {
            return DesireKind.SeekFood;
        }

        if (kind == DesireKind.Drink && !_catalog.IsViable(DesireKind.Drink, _stocks))
        {
            return DesireKind.SeekWater;
        }

        return kind;
    }

    /// <summary>
    /// Évalue toutes les actions candidates et sélectionne (SYNE-030/031/033) :
    /// utilité maximale ; à conflit (candidats à moins de <c>conflictTieMargin</c>
    /// du maximum), résolution probabiliste force × confiance ;
    /// puis hystérésis anti-oscillation (<c>actionSwitchMargin</c>).
    /// Chaque candidat reçoit la confiance épistémique de sa cible
    /// (<see cref="CognitionKnowledge.EpistemicFor"/>) : deux agents à état
    /// physiologique identique mais à connaissances différentes évaluent donc
    /// différemment — c'est la porte du gate de la Phase 2.
    /// </summary>
    private (IReadOnlyList<UtilityScore> Scores, UtilityScore Best) Deliberate(
        MindState mind,
        IReadOnlyList<Goal> candidates,
        ulong currentTick,
        ulong entityId,
        CognitionKnowledge? knowledge,
        DesireKind? currentIntention)
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
        double? Epistemic(DesireKind kind) => knowledge?.EpistemicFor(kind);

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
                currentIntention,
                knowledgeConfidence: Epistemic(goal.Kind)));
        }

        // Engagements (D5) : chaque engagement actif ajoute un candidat d'aide
        // (Socialize) dont le bénéfice dérive de la confiance envers le demandeur
        // (40 × confiance — jusqu'à 40, comparable à Socialize saturé) au lieu d'un
        // besoin physiologique. La formule d'utilité est inchangée (benefitOverride).
        if (_options.Agents.Actions.Commitments.Enabled)
        {
            foreach (Commitment commitment in mind.Commitments)
            {
                if (!commitment.IsActive(currentTick))
                {
                    continue;
                }

                double trust = mind.Trust.TrustWith(commitment.ToEntityId);
                scores.Add(UtilityEvaluator.Evaluate(
                    DesireKind.Socialize,
                    mind.Needs,
                    factors,
                    mind.SuccessRate(DesireKind.Socialize),
                    goalAge: 0,
                    actions,
                    mind.Intention?.Kind,
                    benefitOverride: 40.0 * trust));
            }
        }

        double maximum = scores.Max(score => score.Utility);
        var contested = scores.Where(score => maximum - score.Utility <= actions.Deliberation.ConflictTieMargin).ToList();
        UtilityScore selected = contested.Count > 1
            ? PriorityConflictResolver.Resolve(contested, mind.Needs, entityId, currentTick)
            : contested[0];

        if (currentIntention is { } current)
        {
            UtilityScore adjusted = UtilityEvaluator.ApplyActionSwitchMargin(
                selected,
                current,
                mind.Needs,
                factors,
                mind.SuccessRate(current),
                mind.Intention?.Age(currentTick) ?? 0UL,
                actions,
                Epistemic(current));

            if (adjusted.Kind != selected.Kind)
            {
                scores.Add(adjusted);
                selected = adjusted;
            }
        }

        return (scores, selected);
    }

    /// <summary>
    /// Stockage d'une observation : <b>mémoire</b> (récit canonique, source) puis
    /// <b>croyance</b> révisée par la formule du chapitre XI
    /// (<see cref="BeliefSet.ApplyObservation"/>) — croyance de <b>gisement</b>
    /// (<c>node-&lt;label&gt;</c> → « x,y,amount ») ou de <b>position d'entité</b>
    /// (<c>entity-&lt;id&gt;</c>). C'est la chaîne
    /// <c>perception → mémoire → croyance</c> rendue effective (S-02/S-07/S-08).
    /// </summary>
    private void StoreObservation(MindState mind, Entity self, Observation observation, ulong currentTick, string source)
    {
        // Le récit canonique n'est <b>formaté qu'à l'écriture effective</b> : la
        // déduplication ci-dessous écarte la plupart des réobservations d'un
        // voisinage stable, et formater d'abord payait une chaîne par observation
        // perçue pour un contenu aussitôt jeté (le formatage est pur : le déplacer
        // ne change ni la mémoire ni les croyances).
        bool storeInMemory = true;

        if (observation.EntityType == "entity")
        {
            Fact position = new(EntitySubject(observation.EntityId), KnowledgeBuilder.EntityPositionPredicate, CachedPosition(observation));

            // Déduplication par la croyance (même règle que les gisements) : une
            // position <b>déjà crue et non expirée</b> n'est pas réécrite en mémoire.
            // Sans cela, un voisinage stable réobservé à chaque rotation écrirait un
            // souvenir par entité et par passage — remplissait la mémoire et payait
            // une éviction de capacité à chaque écriture — pour une information déjà
            // connue. La croyance est, elle, rafraîchie à chaque passage (confiance,
            // âge) : c'est le fait qui fait foi.
            storeInMemory = !(mind.Beliefs.TryGet(position, out Belief? known) && !known.IsExpired(currentTick));
            mind.Beliefs.ApplyObservation(position, observation.Confidence, source, _options.Agents.Beliefs, currentTick);
        }
        else if (observation.EntityType == "node")
        {
            string label = observation.Attributes.TryGetValue("label", out string? resolved)
                ? resolved
                : observation.EntityId.ToString(CultureInfo.InvariantCulture);
            string amount = observation.Attributes.TryGetValue("amount", out string? quantity) ? quantity : "0";
            Fact state = new(
                $"node-{label}",
                KnowledgeBuilder.NodeStatePredicate,
                string.Concat(CanonicalPosition(observation.Position), ",", amount));

            // Déduplication par la croyance (O(1), sans état caché : la croyance est
            // persistée) : un gisement dont l'état est <b>déjà cru et non expiré</b>
            // n'est pas réécrit en mémoire. Sans cela, un repère statique réobservé à
            // chaque passage écrit un souvenir par agent et par rotation, remplit la
            // mémoire et paie une éviction de capacité — pour une information déjà
            // connue. La croyance, elle, est rafraîchie à chaque passage (confiance, âge).
            storeInMemory = !(mind.Beliefs.TryGet(state, out Belief? known) && !known.IsExpired(currentTick));
            mind.Beliefs.ApplyObservation(state, observation.Confidence, source, _options.Agents.Beliefs, currentTick);
        }
        else if (observation.EntityType == "obstacle")
        {
            Fact obstacle = new($"obstacle-{observation.EntityId}", "radius", observation.Attributes["radius"]);
            mind.Beliefs.ApplyObservation(obstacle, observation.Confidence, source, _options.Agents.Beliefs, currentTick);
        }

        if (storeInMemory)
        {
            mind.Memory.Store(MemoryCategory.Observation, EntitySubject(self.Id.Value), FormatContent(observation), observation.Confidence, currentTick);
        }
    }

    /// <summary>
    /// Partage social (SYNE-050, V0.1) : une entité sociable (trait ≥ 0.5) qui
    /// perçoit une entité vivante ou un gisement émet une pulsation
    /// <b>Information</b> publique — une au plus par tick (bien sous
    /// <c>maxSendsPerTick</c>). La décision de partage ne consomme aucun tirage du
    /// PRNG global. Le partage automatique est une <b>loi assumée</b> (arbitrage
    /// Phase 0) : son contenu est un témoignage interprétable par le récepteur, qui
    /// en fait une croyance dégradée (<see cref="ApplyReceivedMessages"/>).
    /// </summary>
    private static bool WantsToShare(AgentFactors factors, Observation observation)
    {
        if (observation.EntityType is not ("entity" or "node"))
        {
            return false;
        }

        // Facteurs déjà résolus pour l'entité (aucune recherche de trait par clé
        // textuelle par observation perçue).
        return factors.Sociability >= 0.5;
    }

    private void EnqueueSharePulse(Entity entity, Observation observation, MindState mind, ulong currentTick)
    {
        // Le payload porte le contenu canonique de l'observation : le récepteur
        // peut en faire une croyance de plein droit (provenance = émetteur).
        Message share = CommunicationSystem.CreateMessage(
            entity.Id.Value,
            targetId: null,
            MessageType.Information,
            FormatContent(observation),
            currentTick,
            sequence: 1);

        // File sortante bornée : au-delà de maxSendsPerTick en attente, la pulsance
        // la plus ancienne est abandonnée au profit de la plus récente (une
        // perception périmée n'a plus d'intérêt et la file ne doit pas croître).
        mind.Communication.Enqueue(share, _options.Communication.MaxSendsPerTick);
    }

    /// <summary>
    /// Récit canonique d'une observation, invariant à la culture — partagé par la
    /// mémoire et les messages : <c>entity|id|x.xx|y.yy|conf</c> ou
    /// <c>node|label|x.xx|y.yy|amount.0|conf</c>.
    /// </summary>
    private static string FormatContent(Observation observation)
    {
        // Interpolation à gestionnaire (une seule allocation, format invariant à la
        // culture) au lieu d'un tableau de fragments + 
        // <c>string.Join</c> : ce chemin est parcouru par observation perçue.
        if (observation.EntityType == "node")
        {
            string label = observation.Attributes.TryGetValue("label", out string? resolved) ? resolved : observation.EntityId.ToString(CultureInfo.InvariantCulture);
            string amount = observation.Attributes.TryGetValue("amount", out string? quantity) ? quantity : "0";
            return string.Create(
                CultureInfo.InvariantCulture,
                $"node|{label}|{observation.Position.X:0.00}|{observation.Position.Y:0.00}|{amount}|{observation.Confidence:0.000}");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{observation.EntityType}|{observation.EntityId}|{observation.Position.X:0.00}|{observation.Position.Y:0.00}|{observation.Confidence:0.000}");
    }

    /// <summary>Position canonique d'un fait : « X,Y » invariant à la culture.</summary>
    private static string CanonicalPosition(Simulation.Core.World.Position position) =>
        string.Create(CultureInfo.InvariantCulture, $"{position.X:0.00},{position.Y:0.00}");

    /// <summary>Sujet canonique « entity-N » d'une observation d'entité (mémoïsé par tick).</summary>
    private string EntitySubject(ulong entityId)
    {
        if (_entitySubjectCache.TryGetValue(entityId, out string? cached))
        {
            return cached;
        }

        string subject = string.Create(CultureInfo.InvariantCulture, $"entity-{entityId}");
        _entitySubjectCache[entityId] = subject;
        return subject;
    }

    /// <summary>Position canonique « X,Y » d'une observation d'entité (mémoïsée par tick).</summary>
    private string CachedPosition(Observation observation)
    {
        if (_positionCache.TryGetValue(observation.EntityId, out string? cached))
        {
            return cached;
        }

        string position = CanonicalPosition(observation.Position);
        _positionCache[observation.EntityId] = position;
        return position;
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

    /// <summary>
    /// Restauration de l'état cognitif complet (persistance bit-à-bit,
    /// remplace le dictionnaire d'esprits et ré-importe les
    /// groupes émergents. Aucun tirage du PRNG — la suite reste bit-à-bit.
    /// </summary>
    internal void RestoreState(IReadOnlyDictionary<ulong, MindState> minds, IEnumerable<Social.Group> groups, ulong nextGroupId)
    {
        ArgumentNullException.ThrowIfNull(minds);
        ArgumentNullException.ThrowIfNull(groups);

        _minds.Clear();
        foreach ((ulong id, MindState mind) in minds)
        {
            _minds[id] = mind;
        }

        // Le GroupSystem est readonly — restauration en place des groupes actifs
        // et du compteur d'identifiants (persistance bit-à-bit).
        _groups.RestoreState(groups, nextGroupId);
    }
}
