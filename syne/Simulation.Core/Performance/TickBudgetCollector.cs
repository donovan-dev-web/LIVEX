using System.Diagnostics;

namespace Simulation.Core.Performance;

/// <summary>Sous-systèmes mesurés par tick (PY (Monographie §7.4.3) — PERFORMANCE.md §3).</summary>
public enum TickPhase
{
    /// <summary>Grille spatiale + ligne de vue + tris (perception, budget 20 ms).</summary>
    Perception,

    /// <summary>Stockage mémoire + révision des croyances (budget 15 ms).</summary>
    MemoryBeliefs,

    /// <summary>Avance des besoins + génération d'objectifs (budget 10 ms).</summary>
    NeedsGoals,

    /// <summary>Utilité, délibération, interruption (budget 20 ms).</summary>
    UtilityDecision,

    /// <summary>Actions &amp; mouvement (budget 15 ms).</summary>
    ActionsMovement,

    /// <summary>Communication en une passe (budget 15 ms).</summary>
    Communication,

    /// <summary>Événements, groupes, naissances/mortalité, tick des croyances/confiance (budget 5 ms).</summary>
    EventsGroupsPopulation,
}

/// <summary>
/// Collecteur de budgets de tick (SYNE-090, PERFORMANCE.md §3) : mesure le temps
/// passé par sous-système (par phase) et le temps total de tick computationnel.
/// Opt-in (toujours détachable — aucun coût structurel sur la trajectoire, aucun
/// tirage de PRNG, déterminisme conservé : DETERMINISM.md §3). Le mode par défaut
/// de la boucle ne collecte rien.
/// </summary>
public sealed class TickBudgetCollector
{
    public const int PhaseCount = 8;

    private readonly double[] _totalMs = new double[PhaseCount];
    private readonly long[] _started = new long[PhaseCount];
    private readonly int[] _samples = new int[PhaseCount];
    private double _pipelineTotalMs;
    private int _pipelineSamples;

    /// <summary>Collecteur actif (mesure les phases).</summary>
    public static TickBudgetCollector CreateEnabled() => new();

    /// <summary>Commence la mesure d'une phase (imbrications interdites — même thread).</summary>
    public TickPhaseScope Begin(TickPhase phase)
    {
        Debug.Assert(_started[(int)phase] == 0, "Phase déjà en cours.");
        _started[(int)phase] = Stopwatch.GetTimestamp();
        return new TickPhaseScope(this, phase);
    }

    /// <summary>Enregistre l'écoulement mesuré pour la phase (appelé par le scope).</summary>
    public void Record(TickPhase phase, double elapsedMs)
    {
        _started[(int)phase] = 0;
        _totalMs[(int)phase] += elapsedMs;
        _samples[(int)phase]++;
    }

    /// <summary>Enregistre le temps total d'un tick computationnel (mesuré par la boucle).</summary>
    public void RecordPipelineTick(double elapsedMs)
    {
        _pipelineTotalMs += elapsedMs;
        _pipelineSamples++;
    }

    /// <summary>Photographie des mesures agrégées (temps moyen par tick par phase).</summary>
    public TickBudgetSnapshot Snapshot() => new(
        (double[])_totalMs.Clone(),
        (int[])_samples.Clone(),
        _pipelineTotalMs,
        _pipelineSamples);

    /// <summary>Repart à zéro (pour enchaîner des segments de mesure).</summary>
    public void Reset()
    {
        Array.Clear(_totalMs);
        Array.Clear(_samples);
        Array.Clear(_started);
        _pipelineTotalMs = 0.0;
        _pipelineSamples = 0;
    }
}

/// <summary>
/// Scope de mesure d'une phase : enregistre l'écoulement de la phase dans le
/// collecteur à la disposition. <see cref="Noop"/> est un scope inerte (collecteur
/// nul) — aucune collecte, aucun coût de branchement sur le chemin nominal.
/// </summary>
public readonly struct TickPhaseScope : IDisposable
{
    private readonly TickBudgetCollector? _collector;
    private readonly TickPhase _phase;
    private readonly long _start;

    internal TickPhaseScope(TickBudgetCollector collector, TickPhase phase)
    {
        _collector = collector;
        _phase = phase;
        _start = Stopwatch.GetTimestamp();
    }

    /// <summary>Scope inerte (aucune collecte) — chemin nominal de la boucle.</summary>
    public static TickPhaseScope Noop => default;

    public void Dispose()
    {
        if (_collector is null)
        {
            return;
        }

        double elapsedMs = (Stopwatch.GetTimestamp() - _start) * (1000.0 / Stopwatch.Frequency);
        _collector.Record(_phase, elapsedMs);
    }
}