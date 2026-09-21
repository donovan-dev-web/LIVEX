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
        Needs = new BodyNeeds();
    }

    public Memory Memory { get; }

    public BeliefSet Beliefs { get; }

    public BodyNeeds Needs { get; }

    /// <summary>Facteurs de personnalité dérivés des traits (résolus à la première exécution).</summary>
    public AgentFactors? Factors { get; set; }

    /// <summary>Objectif courant (intention) — nul tant qu'aucune délibération n'a eu lieu.</summary>
    public Goal? Intention { get; set; }

    public UtilityScore? LastDecision { get; private set; }

    internal void RecordDecision(UtilityScore score) => LastDecision = score;

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