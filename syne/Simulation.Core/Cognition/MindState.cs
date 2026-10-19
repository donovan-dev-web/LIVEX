// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : État mental d'une entité : besoins, désirs, croyances, souvenirs et intentions.

using Simulation.Core.Actions;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.World;

namespace Simulation.Core.Cognition;

// Arbitrage A0-3 (17/10/2026) : le `GroupObjective` (SYNE-076) et
// `MindState.CollectiveObjective` ont été SUPPRIMÉS — l'objectif collectif
// d'un groupe ne s'injecte plus dans la cognition individuelle. La décision
// collective reste calculée par `GroupSystem` et exposée (état du groupe,
// `LastDecisions`, snapshot, événement `group_decision`).

/// <summary>
/// État cognitif d'une entité (perception, mémoire,
/// croyances, décision) : mémoire, croyances, besoins, intention et trace de
/// décision (DecisionRecord minimal).
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
    /// Inventaire de l'entité (D8) — <c>null</c> tant
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

    public UtilityScore? LastDecision { get; private set; }

    /// <summary>Trace complète de la dernière décision.</summary>
    public DecisionRecord? LastDecisionRecord { get; private set; }

    /// <summary>
    /// État de la décision <b>du tick courant</b> (<c>New</c>, <c>Holdover</c>,
    /// <c>Interrupted</c>, <c>None</c>). Permet à l'observabilité de ne jamais
    /// publier un score ancien comme décision courante (B-10 / D-03) : un report
    /// est un report, une absence n'est pas un zéro.
    /// </summary>
    public DecisionState DecisionStateThisTick { get; internal set; } = DecisionState.None;

    /// <summary>Tick de la dernière perception effective (épistémique, distinct du taux de succès).</summary>
    public ulong LastPerceptionTick { get; private set; }

    /// <summary>Nombre d'objets perçus au dernier tick de perception (0 = zéro <b>observé</b>).</summary>
    public int LastObservationCount { get; private set; }

    /// <summary>Scores d'utilité des candidats de la dernière décision.</summary>
    public IReadOnlyList<UtilityScore> LastDecisionScores { get; private set; } = [];

    /// <summary>Résultat de l'action atomique du tick courant (SYNE-040/041/042).</summary>
    public ActionResult? LastActionResult { get; private set; }

    /// <summary>Vrai si un tick a délibéré (fréquence configurable).</summary>
    internal bool DeliberatedThisTick { get; set; }

    /// <summary>Vrai si un tick a interrompu l'action en cours (besoin critique).</summary>
    internal bool InterruptedThisTick { get; set; }

    /// <summary>
    /// Vrai pendant le tick où l'entité exécute <see cref="DesireKind.Defend"/>
    /// (D7) : le dégât d'une attaque subie ce tick est réduit de moitié. Réinitialisé
    /// à chaque itération du pipeline (comme <c>DeliberatedThisTick</c>).
    /// </summary>
    public bool DefendingThisTick { get; internal set; }

    /// <summary>Vitesse d'apprentissage du taux de succès observé d'une action (EMA).</summary>
    private const double ActionSuccessLearningRate = 0.25;

    internal void RecordAction(Actions.ActionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        LastActionResult = result;

        // Le taux de succès d'une action décrit la <b>réussite observée</b> de
        // cette action (S-04 / D-07) : moyenne exponentielle des issues, et non
        // « présence récente d'une observation quelconque ». Un échec
        // (Blocked) fait baisser le taux de la seule action concernée.
        double current = _successRates.TryGetValue(result.Kind, out double rate) ? rate : 0.7;
        double observed = result.Outcome == Actions.ActionOutcome.Executed ? 1.0 : 0.0;
        _successRates[result.Kind] = Math.Clamp(
            current + ((observed - current) * ActionSuccessLearningRate),
            0.05,
            0.95);

        if (LastDecisionRecord is { } record)
        {
            LastDecisionRecord = record with
            {
                ResultOutcome = result.Outcome.ToString(),
                ResultReason = result.Reason,
            };
        }
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
        ulong entityId,
        GoalTarget? target = null,
        IReadOnlyList<DecisionEvidence>? evidence = null,
        IReadOnlyList<DesireKind>? rejected = null)
    {
        ArgumentNullException.ThrowIfNull(scores);

        LastDecision = chosen;
        LastDecisionScores = scores;
        DecisionStateThisTick = interrupted
            ? DecisionState.Interrupted
            : deliberated ? DecisionState.New : DecisionState.Holdover;
        LastDecisionRecord = new DecisionRecord(
            tick,
            entityId,
            deliberated,
            interrupted,
            chosen.Kind,
            chosen.Utility,
            scores,
            DecisionStateThisTick,
            Missing: false,
            Target: target,
            Evidence: evidence,
            Rejected: rejected);
    }

    /// <summary>
    /// Enregistre explicitement une <b>absence</b> de décision au tick courant
    /// (B-10) : l'absence n'est ni un zéro d'utilité ni la décision du tick
    /// précédent reportée — elle est nommée.
    /// </summary>
    internal void RecordMissingDecision(ulong tick, ulong entityId)
    {
        DecisionStateThisTick = DecisionState.None;
        LastDecision = null;
        LastDecisionScores = [];
        LastDecisionRecord = new DecisionRecord(
            tick,
            entityId,
            Deliberated: false,
            Interrupted: false,
            Chosen: DesireKind.Idle,
            Utility: null,
            Scores: [],
            State: DecisionState.None,
            Missing: true);
    }

    /// <summary>
    /// Naissance par fusion consentie (SYNE-020) : l'entité née
    /// hérite des traits (via l'Entity), de la **mémoire intergénérationnelle**
    /// et des **croyances** des deux parents. Les besoins sont
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
    /// Probabilité de succès du désir : base de
    /// capacité 0.7, renforcée par la conscience de l'environnement (perception
    /// récente). La calibration précise arrive avec le moteur d'actions (ph4).
    /// </summary>
    public double SuccessRate(DesireKind kind) => _successRates.TryGetValue(kind, out double rate) ? rate : 0.7;

    /// <summary>
    /// Constat des perceptions du tick courant. <b>N'alimente plus les taux de
    /// succès</b> (S-04 / D-07 : « voir quelque chose » n'est pas « réussir une
    /// action » — le taux de succès d'une action se mesure sur ses issues,
    /// <see cref="RecordAction"/>) : ce constat horodate la fraîcheur épistémique
    /// de l'agent, exploitable par l'analyse causale.
    /// </summary>
    internal void AcknowledgePerceptions(int observationCount, ulong tick)
    {
        LastObservationCount = Math.Max(0, observationCount);
        LastPerceptionTick = tick;
    }

    /// <summary>Taux de succès par désir (persistance bit-à-bit).</summary>
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

    /// <summary>Restauration des taux de succès par désir (persistance bit-à-bit).</summary>
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
    /// besoins, mémoire (avec séquence), croyances, relations,
    /// communication non-éphémère, taux de succès, intention
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
        if (kind is null)
        {
            LastDecisionRecord = null;
            DecisionStateThisTick = DecisionState.None;
        }
    }

    /// <summary>
    /// Restauration de la <b>trace diagnostique complète</b> (D-18) : sans elle, la
    /// trajectoire restait exacte après reprise mais l'explication repartait d'un
    /// score nul et d'une cible absente.
    /// </summary>
    internal void RestoreDecisionRecord(DecisionRecord? record)
    {
        LastDecisionRecord = record;
        if (record is null)
        {
            LastDecision = null;
            LastDecisionScores = [];
            DecisionStateThisTick = DecisionState.None;
            return;
        }

        // Une absence restaurée reste une <b>absence</b> : aucun score d'utilité
        // n'est reconstitué pour elle (B-10), mais la trace est conservée telle
        // quelle pour que l'explication continue après reprise (D-18).
        LastDecision = record.Missing
            ? null
            : new UtilityScore(
                record.Chosen,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                record.Utility ?? 0.0);
        LastDecisionScores = record.Scores;
        DecisionStateThisTick = record.State;
    }
}
