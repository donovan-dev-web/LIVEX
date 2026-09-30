using Simulation.Core.Actions;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.World;

namespace Simulation.Core.Cognition;

/// <summary>
/// Objectif collectif adopté par un groupe et propagé à ses membres (SYNE-076,
/// SYSTEM_SPEC.md §5 décision n°24) : l'intention dominante du groupe devient un
/// facteur d'alignement de l'utilité individuelle, pondéré par le consensus
/// atteint et la confiance du membre envers le leader émergent. La durée de vie
/// de l'objectif est l'intervalle de revue des groupes (TTL, ré-adopté à chaque
/// révision).
/// </summary>
public readonly record struct GroupObjective(
    ulong GroupId,
    DesireKind Kind,
    double Consensus,
    double LeaderTrust,
    ulong AdoptedTick,
    ulong ExpiresTick)
{
    /// <summary>Vrai tant que l'objectif collectif n'a pas expiré.</summary>
    public bool IsActive(ulong currentTick) => currentTick <= ExpiresTick;
}

/// <summary>
/// État cognitif d'une entité (DATA_MODEL.md §3.1 : perception, mémoire,
/// croyances, décision) : mémoire, croyances, besoins, intention et trace de
/// décision (DecisionRecord minimal — COGNITIVE_ARCHITECTURE.md §7).
/// <para>
/// Depuis engineVersion 0.14.0 : inventaire (D8, si <c>agents.inventory.enabled</c>),
/// engagements (D5, si <c>agents.commitments.enabled</c>) et état de saillance
/// (D2 : besoins déclenchés à la dernière délibération, tick de celle-ci).
/// </para>
/// </summary>
public sealed class MindState
{
    private readonly Dictionary<DesireKind, double> _successRates = new();

    public MindState(SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Memory = new Memory(options.Agents.Memory);
        Beliefs = new BeliefSet();
        Trust = new Relationships(options.Agents.Trust);
        Needs = new BodyNeeds();
        Communication = new CommunicationState();
        Inventory = options.Agents.Actions.Inventory.Enabled
            ? new Inventory(options.Agents.Actions.Inventory.CapacityWeight)
            : null;
    }

    private MindState(SimulationOptions options, Memory memory, BeliefSet beliefs, Relationships trust)
    {
        ArgumentNullException.ThrowIfNull(options);
        Memory = memory;
        Beliefs = beliefs;
        Trust = trust;
        Needs = new BodyNeeds();
        Communication = new CommunicationState();
        Inventory = options.Agents.Actions.Inventory.Enabled
            ? new Inventory(options.Agents.Actions.Inventory.CapacityWeight)
            : null;
    }

    public Memory Memory { get; }

    public BeliefSet Beliefs { get; }

    public Relationships Trust { get; }

    /// <summary>État de communication (files sortante/entrante, relais — SYNE-050 → 054).</summary>
    public CommunicationState Communication { get; }

    /// <summary>
    /// Inventaire de l'entité (D8, ADR « Système d'inventaire ») — <c>null</c> tant
    /// que <c>agents.inventory.enabled</c> est faux (comportement historique :
    /// Eat/Drink consomment directement les réserves mondiales).
    /// </summary>
    public Inventory? Inventory { get; }

    /// <summary>Engagements communicationnels actifs (D5, version minimale — au plus un par pair).</summary>
    public List<Commitment> Commitments { get; } = new();

    /// <summary>Besoins dont le seuil était franchi à la dernière délibération (contrôle de saillance D2).</summary>
    internal IReadOnlyList<DesireKind> TriggeredAtLastDeliberation { get; private set; } = [];

    /// <summary>Tick de la dernière délibération complète (D2 : filet de sécurité périodique).</summary>
    internal ulong LastDeliberationTick { get; private set; }

    /// <summary>Score de saillance du dernier contrôle (D2 : traçabilité ECHOS).</summary>
    public double LastSalienceScore { get; internal set; }

    /// <summary>Vrai si la dernière itération a sauté la délibération faute de saillance (D2 : traçabilité ECHOS).</summary>
    public bool SkippedBySalience { get; internal set; }

    public BodyNeeds Needs { get; }

    /// <summary>Facteurs de personnalité dérivés des traits (résolus à la première exécution).</summary>
    public AgentFactors? Factors { get; set; }

    /// <summary>Objectif courant (intention) — nul tant qu'aucune délibération n'a eu lieu.</summary>
    public Goal? Intention { get; set; }

    /// <summary>
    /// Objectif collectif adopté par le groupe du membre (SYNE-076) — nul hors
    /// groupe ou sans décision collective. Contraint l'utilité via un bonus
    /// d'alignement pondéré par le consensus et la confiance leader (TTL =
    /// intervalle de revue).
    /// </summary>
    public GroupObjective? CollectiveObjective { get; set; }

    public UtilityScore? LastDecision { get; private set; }

    /// <summary>Trace complète de la dernière décision (COGNITIVE_ARCHITECTURE.md §7).</summary>
    public DecisionRecord? LastDecisionRecord { get; private set; }

    /// <summary>Scores d'utilité des candidats de la dernière décision.</summary>
    public IReadOnlyList<UtilityScore> LastDecisionScores { get; private set; } = [];

    /// <summary>Résultat de l'action atomique du tick courant (SYNE-040/041/042).</summary>
    public ActionResult? LastActionResult { get; private set; }

    /// <summary>Vrai si un tick a délibéré (fréquence configurable, décision n°14).</summary>
    internal bool DeliberatedThisTick { get; set; }

    /// <summary>Vrai si un tick a interrompu l'action en cours (besoin critique, décision n°15).</summary>
    internal bool InterruptedThisTick { get; set; }

    /// <summary>
    /// Vrai pendant le tick où l'entité exécute <see cref="DesireKind.Defend"/>
    /// (D7) : le dégât d'une attaque subie ce tick est réduit de moitié. Réinitialisé
    /// à chaque itération du pipeline (comme <c>DeliberatedThisTick</c>).
    /// </summary>
    public bool DefendingThisTick { get; internal set; }

    internal void RecordAction(Actions.ActionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        LastActionResult = result;
    }

    /// <summary>Enregistre l'état des besoins à la délibération (D2 : base de comparaison de saillance).</summary>
    internal void RecordDeliberationState(ulong tick, IReadOnlyList<DesireKind> triggered)
    {
        LastDeliberationTick = tick;
        TriggeredAtLastDeliberation = triggered;
    }

    internal void RecordDecision(
        IReadOnlyList<UtilityScore> scores,
        UtilityScore chosen,
        bool deliberated,
        bool interrupted,
        ulong tick,
        ulong entityId)
    {
        ArgumentNullException.ThrowIfNull(scores);

        LastDecision = chosen;
        LastDecisionScores = scores;
        LastDecisionRecord = new DecisionRecord(
            tick,
            entityId,
            deliberated,
            interrupted,
            chosen.Kind,
            chosen.Utility,
            scores);
    }

    /// <summary>
    /// Naissance par fusion consentie (SYNE-020, décision n°16) : l'entité née
    /// hérite des traits (via l'Entity), de la **mémoire intergénérationnelle**
    /// et des **croyances** des deux parents (§6.6.2/§6.6.3). Les besoins sont
    /// vierges. La diffusion de cet état dans la simulation (action de
    /// reproduction) est câblée au moteur d'actions (ph4).
    /// </summary>
    public static MindState Born(
        SimulationOptions options,
        MindState parentA,
        MindState parentB,
        ulong birthTick)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(parentA);
        ArgumentNullException.ThrowIfNull(parentB);

        Memory memory = Inheritance.InheritMemory(
            parentA.Memory.AllEntries.Concat(parentB.Memory.AllEntries),
            options.Agents.Memory,
            birthTick,
            options.Agents.Inheritance.SalienceThreshold);

        BeliefSet beliefs = Inheritance.InheritBeliefs(
            parentA.Beliefs.All.Concat(parentB.Beliefs.All),
            options.Agents.Beliefs,
            birthTick);

        return new MindState(options, memory, beliefs, new Relationships(options.Agents.Trust));
    }

    /// <summary>
    /// Probabilité de succès du désir (COGNITIVE_ARCHITECTURE.md §5) : base de
    /// capacité 0.7, renforcée par la conscience de l'environnement (perception
    /// récente). La calibration précise arrive avec le moteur d'actions (ph4).
    /// </summary>
    public double SuccessRate(DesireKind kind) => _successRates.TryGetValue(kind, out double rate) ? rate : 0.7;

    /// <summary>Rend l'entité « consciente » : constate les perceptions du tick courant.</summary>
    internal void AcknowledgePerceptions(int observationCount)
    {
        foreach (DesireKind kind in Enum.GetValues<DesireKind>())
        {
            double rate = _successRates.TryGetValue(kind, out double current) ? current : 0.7;
            _successRates[kind] = Math.Clamp(rate + (observationCount > 0 ? 0.15 : -0.01), 0.1, 1.0);
        }
    }

    /// <summary>Taux de succès par désir (persistance bit-à-bit, PERSISTENCE.md §4).</summary>
    internal IReadOnlyDictionary<DesireKind, double> SuccessRatesSnapshot()
    {
        var copy = new Dictionary<DesireKind, double>(_successRates);
        foreach (DesireKind kind in Enum.GetValues<DesireKind>())
        {
            if (!copy.ContainsKey(kind))
            {
                copy[kind] = 0.7;
            }
        }

        return copy;
    }

    /// <summary>Restauration des taux de succès par désir (persistance bit-à-bit, PERSISTENCE.md §4).</summary>
    internal void RestoreSuccessRates(IReadOnlyDictionary<DesireKind, double> rates)
    {
        ArgumentNullException.ThrowIfNull(rates);
        _successRates.Clear();
        foreach ((DesireKind kind, double rate) in rates)
        {
            _successRates[kind] = rate;
        }
    }

    /// <summary>
    /// Restaure l'état cognitif complet d'une entité (persistance bit-à-bit,
    /// PERSISTENCE.md §4) : besoins, mémoire (avec séquence), croyances, relations,
    /// communication non-éphémère, taux de succès, intention, objectif collectif
    /// et facteurs de personnalité.
    /// </summary>
    internal void RestoreState(
        BodyNeeds needs,
        Memory memory,
        BeliefSet beliefs,
        Relationships trust,
        CommunicationState communication,
        IReadOnlyDictionary<DesireKind, double> successRates,
        AgentFactors? factors,
        Goal? intention,
        GroupObjective? collectiveObjective,
        IReadOnlyDictionary<World.ResourceKind, double>? inventoryAmounts = null,
        IReadOnlyList<Commitment>? commitments = null,
        ulong lastDeliberationTick = 0,
        IReadOnlyList<DesireKind>? triggeredAtLastDeliberation = null)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(beliefs);
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(communication);
        ArgumentNullException.ThrowIfNull(successRates);

        Needs.RestoreState(
            needs.Hunger, needs.Thirst, needs.Fatigue, needs.Safety,
            needs.Social, needs.Curiosity, needs.Energy);
        Memory.RestoreState(memory.AllEntries, memory.NextSequence);
        Beliefs.RestoreState(beliefs.All);
        Trust.RestoreState(trust.Snapshot());
        Communication.RestoreState(communication.OutgoingSnapshot, communication.RelayedIdsSnapshot);
        RestoreSuccessRates(successRates);
        Factors = factors;
        Intention = intention;
        CollectiveObjective = collectiveObjective;
        if (inventoryAmounts is { } amounts && Inventory is { } inventory)
        {
            inventory.RestoreState(amounts);
        }

        if (commitments is { } restoredCommitments)
        {
            Commitments.Clear();
            Commitments.AddRange(restoredCommitments);
        }

        LastDeliberationTick = lastDeliberationTick;
        TriggeredAtLastDeliberation = triggeredAtLastDeliberation ?? [];
    }

    /// <summary>
    /// Restaure le holdover de délibération (décision du dernier tick délibéré).
    /// Un <c>null</c> en entrée efface la décision : laisser l'ancienne décision en
    /// place après restauration feraitChooser à l'esprit une intention absente du
    /// snapshot — divergence silencieuse avec l'état sauvegardé.
    /// </summary>
    internal void RestoreLastDecision(DesireKind? kind)
    {
        LastDecision = kind is { } resolved
            ? new UtilityScore(resolved, 0, 0, 0, 0, 0, 0, 0)
            : null;
    }
}