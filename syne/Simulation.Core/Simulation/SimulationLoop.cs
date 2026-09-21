using Simulation.Core.Prng;

namespace Simulation.Core.Loop;

/// <summary>
/// Boucle de simulation minimale (SYNE-002, SIMULATION_LOOP.md §1.
/// <list type="bullet">
/// <item>1 tick = 1 minute simulée (défaut) — <see cref="SimulationTime"/>.</item>
/// <item>Respect de <c>maxTicks</c> : la boucle s'arrête après le tick n° <c>maxTicks</c>.</item>
/// <item>L'état du PRNG avance d'un tirage par tick (flux ancré au tick index).</item>
/// </list>
/// Corps de tick volontairement minimal (pas de sous-systèmes) :
/// les étapes Percevoir…Modifier le monde (boucle 15 étapes de référence) seront
/// introduites avec l'exécution des sous-systèmes aux jalons U1+.
/// </summary>
public sealed class SimulationLoop
{
    private Xoshiro256StarStar _rng;

    public SimulationLoop(World.World world, Xoshiro256StarStar initialRng)
    {
        World = world;
        _rng = initialRng;
    }

    public World.World World { get; }

    public ulong CurrentTick { get; private set; }

    public Xoshiro256StarStar Rng => _rng;

    /// <summary>Avance d'un tick (1 minute simulée, SIMULATION_LOOP.md §1).</summary>
    public void AdvanceOneTick()
    {
        CurrentTick += 1;
        _rng = _rng.NextUInt64(out _);
    }

    /// <summary>Exécute la boucle jusqu'au tick n° <paramref name="maxTicks"/> inclus.</summary>
    public void Run(int maxTicks)
    {
        if (maxTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTicks), "maxTicks doit être &gt; 0.");
        }

        while (CurrentTick < (ulong)maxTicks)
        {
            AdvanceOneTick();
        }
    }
}

/// <summary>Correspondance tick ↔ temps simulé (1 tick = 1 minute, SIMULATION_LOOP.md §1).</summary>
public static class SimulationTime
{
    public static readonly int TicksPerSimulationMinute = 1;

    public static long ToSimulatedMinutes(ulong tick) => (long)tick * TicksPerSimulationMinute;

    /// <summary>Format horloge simulée « T+h:mm » depuis le tick 0.</summary>
    public static string FormatClock(ulong tick)
    {
        long minutes = ToSimulatedMinutes(tick);
        long hours = minutes / 60;
        int minutesOfHour = (int)(minutes % 60);
        return $"T+{hours}:{minutesOfHour:D2}";
    }
}