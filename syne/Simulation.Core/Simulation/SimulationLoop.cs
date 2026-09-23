using System.Diagnostics;
using Simulation.Core.Performance;
using Simulation.Core.Prng;

namespace Simulation.Core.Loop;

/// <summary>
/// Boucle de simulation (SYNE-002, SIMULATION_LOOP.md §1).
/// <list type="bullet">
/// <item>1 tick = 1 minute simulée (défaut) — <see cref="SimulationTime"/>.</item>
/// <item>Respect de <c>maxTicks</c> : la boucle s'arrête après le tick n° <c>maxTicks</c>.</item>
/// <item>L'état du PRNG avance d'un tirage par tick (flux ancré au tick index).</item>
/// <item>Depuis U1 : pipeline cognitif BDI (perception, mémoire, croyances, besoins,
///  objectifs, utilité, intention, action) exécuté à chaque tick (SYNE-010).</item>
/// </list>
/// </summary>
public sealed class SimulationLoop
{
    private readonly Simulation.Core.Cognition.CognitionPipeline _cognition;
    private readonly TickBudgetCollector? _budget;
    private Xoshiro256StarStar _rng;

    public SimulationLoop(World.World world, Xoshiro256StarStar initialRng)
        : this(world, initialRng, new Simulation.Core.Configuration.SimulationOptions())
    {
    }

    public SimulationLoop(World.World world, Xoshiro256StarStar initialRng, Simulation.Core.Configuration.SimulationOptions options)
        : this(world, initialRng, options, null)
    {
    }

    public SimulationLoop(
        World.World world,
        Xoshiro256StarStar initialRng,
        Simulation.Core.Configuration.SimulationOptions options,
        TickBudgetCollector? budget)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(options);
        World = world;
        _rng = initialRng;
        _budget = budget;
        Resources = new World.ResourceStocks(options.Resources);
        _cognition = new Simulation.Core.Cognition.CognitionPipeline(world, options, Resources, budget);
    }

    public World.World World { get; }

    /// <summary>Réserves globales de ressources (SYNE-042) : consommées par Eat/Drink, exposées dans le snapshot.</summary>
    public World.ResourceStocks Resources { get; }

    public ulong CurrentTick { get; private set; }

    public Xoshiro256StarStar Rng => _rng;

    public Simulation.Core.Cognition.CognitionPipeline Cognition => _cognition;

    /// <summary>
    /// Collecteur de budgets de tick (SYNE-090) si la boucle en a été munie
    /// (sinon <c>null</c> — aucun coût sur le chemin nominal).
    /// </summary>
    public TickBudgetCollector? Budgets => _budget;

    /// <summary>
    /// Avance d'un tick (1 minute simulée, SIMULATION_LOOP.md §1) puis exécute le
    /// pipeline cognitif BDI (U1, SYNE-010) dans l'ordre causal strict. Le PRNG
    /// n'avance que d'un tirage par tick (contrat DETERMINISM.md §3).
    /// </summary>
    public void AdvanceOneTick()
    {
        CurrentTick += 1;
        _rng = _rng.NextUInt64(out _);
        if (_budget is null)
        {
            _cognition.Step(CurrentTick);
            return;
        }

        long start = Stopwatch.GetTimestamp();
        _cognition.Step(CurrentTick);
        double elapsedMs = (Stopwatch.GetTimestamp() - start) * (1000.0 / Stopwatch.Frequency);
        _budget.RecordPipelineTick(elapsedMs);
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