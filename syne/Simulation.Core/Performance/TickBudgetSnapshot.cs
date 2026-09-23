namespace Simulation.Core.Performance;

/// <summary>
/// Photographie agrégée des budgets de tick (SYNE-090) : moyennes par phase en
/// millisecondes par tick, part de computation (Σ phases / tick total) et temps
/// moyen d'un tick computationnel. Immutable.
/// </summary>
public sealed record TickBudgetSnapshot
{
    private readonly double[] _totalMs;
    private readonly int[] _samples;

    public TickBudgetSnapshot(double[] totalMs, int[] samples, double pipelineTotalMs, int pipelineSamples)
    {
        _totalMs = totalMs;
        _samples = samples;
        PipelineTotalMs = pipelineTotalMs;
        PipelineSamples = pipelineSamples;
    }

    public double PipelineTotalMs { get; }

    public int PipelineSamples { get; }

    public double MeanPipelineMs => PipelineSamples > 0 ? PipelineTotalMs / PipelineSamples : 0.0;

    /// <summary>Temps moyen par tick pour la phase (0 si jamais mesurée).</summary>
    public double MeanMs(TickPhase phase)
    {
        int index = (int)phase;
        return _samples[index] > 0 ? _totalMs[index] / _samples[index] : 0.0;
    }

    /// <summary>Nombre de captures de la phase.</summary>
    public int Samples(TickPhase phase) => _samples[(int)phase];

    /// <summary>
    /// Part de computation du tick : Σ du temps des sept phases rapporté au temps
    /// total du tick computationnel (cible ≥ 30 %, PERFORMANCE.md §9 — le reste
    /// est de l'allocation/GC/overhead).
    /// </summary>
    public double ComputationShare()
    {
        if (MeanPipelineMs <= 0.0)
        {
            return 0.0;
        }

        double phasesMs = 0.0;
        foreach (TickPhase phase in Enum.GetValues<TickPhase>())
        {
            phasesMs += MeanMs(phase);
        }

        return phasesMs / MeanPipelineMs;
    }
}