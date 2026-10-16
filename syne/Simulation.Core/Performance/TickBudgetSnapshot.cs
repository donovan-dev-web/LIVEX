// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Photographie agrégée des budgets de tick (SYNE-090) : moyennes par phase en millisecondes par tick, part
//        de computation (Σ phases / tick total) et temps moyen d'un tick computationnel.

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

    /// <summary>
    /// Temps moyen <b>par tick</b> de la phase (0 si jamais mesurée) — lecture
    /// dimensionnellement cohérente avec <see cref="MeanPipelineMs"/> et
    /// <see cref="ComputationShare"/>.
    /// </summary>
    /// <remarks>
    /// <b>Correction de mesure (réalignement)</b> : la moyenne divisait par le
    /// nombre d'<i>échantillons</i> de la phase. Les phases intra-entité
    /// (perception, mémoire, besoins, décision, action) sont échantillonnées une fois
    /// par entité et par tick, les phases monde/réseau une fois par tick : la colonne
    /// affichée rendait donc le temps <i>par entité</i> pour les premières (0,03 ms) et
    /// le temps <i>par tick</i> pour les secondes (6 ms) — deux unités dans le même
    /// tableau, ce qui masquait la phase réellement dominante. La lecture par tick est
    /// conservée (par échantillon explicitement, <see cref="MeanPerSampleMs"/>).
    /// </remarks>
    public double MeanMs(TickPhase phase)
    {
        int index = (int)phase;
        return PipelineSamples > 0 ? _totalMs[index] / PipelineSamples : 0.0;
    }

    /// <summary>Temps moyen par échantillon de la phase (temps par entité pour les phases intra-entité).</summary>
    public double MeanPerSampleMs(TickPhase phase)
    {
        int index = (int)phase;
        return _samples[index] > 0 ? _totalMs[index] / _samples[index] : 0.0;
    }

    /// <summary>Nombre de captures de la phase.</summary>
    public int Samples(TickPhase phase) => _samples[(int)phase];

    /// <summary>
    /// Part de computation du tick : Σ du temps des sept phases rapporté au temps
    /// total du tick computationnel (cible ≥ 30 %, — le reste
    /// est de l'allocation/GC/overhead).
    /// </summary>
    /// <remarks>
    /// <b>Correction calibration B1 </b> : la somme se faisait sur
    /// <see cref="MeanMs(TickPhase)"/>, c'est-à-dire du temps <i>par échantillon</i>
    /// — or les phases intra-entité (perception, mémoire, besoins, actions) sont
    /// échantillonnées à chaque entité × tick quand les phases réseau/monde le sont
    /// une fois par tick. Mélanger les deux sous-estimait les phases intra-entité
    /// d'un facteur ≈ population : la part affichée revenait à
    /// (communication + événements) / tick. On rapporte désormais, comme le dit la
    /// documentation, le temps **par tick** de chaque phase (Σ des temps totaux ÷
    /// nombre de ticks), seule lecture dimensionnellement cohérente.
    /// </remarks>
    public double ComputationShare()
    {
        if (MeanPipelineMs <= 0.0 || PipelineSamples <= 0)
        {
            return 0.0;
        }

        double phasesMs = 0.0;
        for (int index = 0; index < _totalMs.Length; index++)
        {
            phasesMs += _totalMs[index] / PipelineSamples;
        }

        return phasesMs / MeanPipelineMs;
    }
}