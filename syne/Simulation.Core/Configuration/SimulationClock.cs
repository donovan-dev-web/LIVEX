namespace Simulation.Core.Configuration;

/// <summary>
/// Horloge du temps simulé, instanciée à partir de la configuration
/// (ADR-017 « échelle temporelle configurable ») : 1 tick vaut
/// <c>simulatedSecondsPerTick</c> secondes simulées (défaut 60 — soit
/// 1 tick = 1 minute, temporalité historique ADR-005).
///
/// <para>
/// <b>Neutre par défaut</b> : à <c>simulatedSecondsPerTick = 60</c>, les
/// conversions redonnent exactement les valeurs de l'ancienne constante
/// statique <c>SimulationTime.TicksPerSimulationMinute = 1</c> (multiplication
/// par 60 puis division entière par 60 est exacte pour les ticks praticables).
/// </para>
///
/// <para>
/// Le ratio temps réel ⇄ temps simulé du run est
/// <c>R = ticksPerSecond × simulatedSecondsPerTick</c> : 1 seconde réelle
/// équivaut à <c>R</c> secondes simulées (SCALE_AND_CADENCE_SPEC.md §2).
/// </para>
/// </summary>
public sealed class SimulationClock
{
    /// <summary>Valeur par défaut : 1 tick = 60 s simulées = 1 minute simulée (ADR-005).</summary>
    public const int DefaultSimulatedSecondsPerTick = 60;

    /// <summary>Plage validée de <c>simulation.simulatedSecondsPerTick</c> (ADR-017 §4.1).</summary>
    public const int MinSimulatedSecondsPerTick = 1;
    public const int MaxSimulatedSecondsPerTick = 3600;

    /// <summary>Horloge du profil par défaut / <c>reference</c> (1 tick = 1 minute simulée).</summary>
    public static SimulationClock Default { get; } = new(DefaultSimulatedSecondsPerTick);

    public SimulationClock(int simulatedSecondsPerTick)
    {
        if (simulatedSecondsPerTick is < MinSimulatedSecondsPerTick or > MaxSimulatedSecondsPerTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(simulatedSecondsPerTick),
                simulatedSecondsPerTick,
                $"simulatedSecondsPerTick doit être dans [{MinSimulatedSecondsPerTick}, {MaxSimulatedSecondsPerTick}].");
        }

        SimulatedSecondsPerTick = simulatedSecondsPerTick;
    }

    /// <summary>Secondes simulées par tick (entier, ADR-017 §4.1).</summary>
    public int SimulatedSecondsPerTick { get; }

    /// <summary>
    /// Minutes simulées par tick (<c>dt</c>, ADR-017 §3) : 1,0 au défaut ;
    /// 1/12 pour le profil <c>prism</c> (5 s simulées par tick). Les taux de
    /// classe A sont multipliés par cette valeur.
    /// </summary>
    public double SimulatedMinutesPerTick => SimulatedSecondsPerTick / 60.0;

    /// <summary>Ratio « 1 seconde réelle = R secondes simulées » (R = TPS × spt).</summary>
    public double RealTimeRatio(int ticksPerSecond) => ticksPerSecond * (double)SimulatedSecondsPerTick;

    /// <summary>Temps simulé en secondes au tick <paramref name="tick"/> (entier 64 bits, ADR-017 §4.2).</summary>
    public long ToSimulatedSeconds(ulong tick) => (long)tick * SimulatedSecondsPerTick;

    /// <summary>Temps simulé en minutes au tick <paramref name="tick"/> (division entière — plancher, ADR-017 §4.2).</summary>
    public long ToSimulatedMinutes(ulong tick) => ToSimulatedSeconds(tick) / 60;

    /// <summary>Horloge instanciée depuis <c>simulation.simulatedSecondsPerTick</c>.</summary>
    public static SimulationClock From(SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new SimulationClock(settings.SimulatedSecondsPerTick);
    }

    /// <summary>Format horloge simulée « T+h:mm » depuis le tick 0 (ADR-005).</summary>
    public string FormatClock(ulong tick)
    {
        long minutes = ToSimulatedMinutes(tick);
        long hours = minutes / 60;
        int minutesOfHour = (int)(minutes % 60);
        return $"T+{hours}:{minutesOfHour:D2}";
    }
}
