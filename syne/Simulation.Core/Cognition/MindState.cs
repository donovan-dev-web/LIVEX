using Simulation.Core.Actions;
using Simulation.Core.Configuration;

namespace Simulation.Core.Cognition;

/// <summary>
/// État cognitif d'une entité (DATA_MODEL.md §3.1 : perception, mémoire,
/// croyances, décision) : mémoire, croyances, besoins, intention et trace de
/// décision (DecisionRecord minimal — COGNITIVE_ARCHITECTURE.md §7).
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
    }

    private MindState(SimulationOptions options, Memory memory, BeliefSet beliefs, Relationships trust)
    {
        ArgumentNullException.ThrowIfNull(options);
        Memory = memory;
        Beliefs = beliefs;
        Trust = trust;
        Needs = new BodyNeeds();
    }

    public Memory Memory { get; }

    public BeliefSet Beliefs { get; }

    public Relationships Trust { get; }

    public BodyNeeds Needs { get; }

    /// <summary>Facteurs de personnalité dérivés des traits (résolus à la première exécution).</summary>
    public AgentFactors? Factors { get; set; }

    /// <summary>Objectif courant (intention) — nul tant qu'aucune délibération n'a eu lieu.</summary>
    public Goal? Intention { get; set; }

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

    internal void RecordAction(Actions.ActionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        LastActionResult = result;
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
            birthTick);

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
}