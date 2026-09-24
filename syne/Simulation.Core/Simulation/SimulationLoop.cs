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
    private readonly int _autoSaveEveryNTicks;
    private readonly Action<SimulationLoop>? _autosaveHandler;
    private readonly Simulation.Core.Configuration.SimulationOptions _options;
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
        _options = options;
        Resources = new World.ResourceStocks(options.Resources);
        _cognition = new Simulation.Core.Cognition.CognitionPipeline(world, options, Resources, budget);
        _autoSaveEveryNTicks = options.Simulation.AutoSaveEveryNTicks;
        _autosaveHandler = null;
    }

    /// <summary>
    /// Boucle avec autosave branche : <paramref name="autosaveHandler"/> est appelé
    /// après chaque <c>n</c>-ième tick (PERSISTENCE.md §5). Le handler ne doit
    /// consommer aucun tirage du PRNG — le déterminisme bit-à-bit reste inchangé.
    /// </summary>
    public SimulationLoop(
        World.World world,
        Xoshiro256StarStar initialRng,
        Simulation.Core.Configuration.SimulationOptions options,
        TickBudgetCollector? budget,
        int autoSaveEveryNTicks,
        Action<SimulationLoop>? autosaveHandler)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(options);
        World = world;
        _rng = initialRng;
        _budget = budget;
        _options = options;
        Resources = new World.ResourceStocks(options.Resources);
        _cognition = new Simulation.Core.Cognition.CognitionPipeline(world, options, Resources, budget);
        _autoSaveEveryNTicks = Math.Max(1, autoSaveEveryNTicks);
        _autosaveHandler = autosaveHandler;
    }

    public World.World World { get; }

    /// <summary>Réserves globales de ressources (SYNE-042) : consommées par Eat/Drink, exposées dans le snapshot.</summary>
    public World.ResourceStocks Resources { get; internal set; }

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
            Resources.ApplyLifecycle(CurrentTick, _options.Resources);
        }
        else
        {
            long start = Stopwatch.GetTimestamp();
            _cognition.Step(CurrentTick);
            using (TickPhaseScope resourcesScope = _budget.Begin(TickPhase.EventsGroupsPopulation))
            {
                Resources.ApplyLifecycle(CurrentTick, _options.Resources);
            }

            double elapsedMs = (Stopwatch.GetTimestamp() - start) * (1000.0 / Stopwatch.Frequency);
            _budget.RecordPipelineTick(elapsedMs);
        }

        if (_autosaveHandler is not null && CurrentTick % (ulong)_autoSaveEveryNTicks == 0)
        {
            _autosaveHandler(this);
        }
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

    /// <summary>
    /// Restauration bit-à-bit (SYNE-111, PERSISTENCE.md §4) : pointe la boucle sur
    /// le tick <paramref name="tick"/> et remplace l'état du PRNG par les 4 × 64 bits
    /// sauvegardés. Aucun tirage supplémentaire — la suite est identique à une
    /// exécution ininterrompue.
    /// </summary>
    internal void RestoreState(ulong tick, Xoshiro256StarStar rng)
    {
        CurrentTick = tick;
        _rng = rng;
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