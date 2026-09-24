using System.Globalization;
using System.Text;
using Simulation.Console.Control;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Prng;
using Simulation.Core.Loop;
using Simulation.Core.Performance;

namespace Simulation.Console;

/// <summary>
/// Point d'entrée SYNE — mode CLI/batch (ADR-002).
/// Priorité de configuration : défauts → --config → flags CLI (CONFIGURATION.md §5).
/// Boucle minimale : monde + entités + grille spatiale + tick (SYNE-002/003/004).
/// Mode --observe : diffusion WebSocket des snapshots/événements par tick (SYNE-080).
/// Mode --benchmark : mesure débit/budgets/checksum aux échelles 50/500/1000 (SYNE-090…093).
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            CliOptions cli = CliOptions.Parse(args);

            SimulationOptions options = ConfigLoader.LoadDefaults();
            if (cli.ConfigPath is not null)
            {
                options = ConfigLoader.LoadFile(cli.ConfigPath);
            }

            options = ApplyCliOverrides(options, cli);

            IReadOnlyList<string> errors = SimulationOptionsValidator.Validate(options);
            if (errors.Count > 0)
            {
                System.Console.Error.WriteLine("Configuration invalide :");
                foreach (string error in errors)
                {
                    System.Console.Error.WriteLine($"  - {error}");
                }

                return 2;
            }

            if (cli.Observe == true)
            {
                await RunObservedAsync(options, cli);
            }
            else if (cli.Benchmark == true)
            {
                RunBenchmark(options, cli);
            }
            else if (cli.Serve == true)
            {
                await RunServeAsync(options, cli);
            }
            else
            {
                RunBatch(options, cli.Headless == true);
            }

            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or InvalidDataException)
        {
            System.Console.Error.WriteLine($"Erreur : {ex.Message}");
            return 2;
        }
    }

    private static SimulationOptions ApplyCliOverrides(SimulationOptions options, CliOptions cli)
    {
        if (cli.WorldSize is { } size)
        {
            options.Simulation.WorldWidth = size.Width;
            options.Simulation.WorldHeight = size.Height;
        }

        if (cli.MaxTicks is { } maxTicks)
        {
            options.Simulation.MaxTicks = maxTicks;
        }

        if (cli.Seed is { } seed)
        {
            options.Random.Seed = seed;
        }

        return options;
    }

    private static void RunBatch(SimulationOptions options, bool headless)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        (_, var world, SimulationLoop loop, EntityTemplate template) = BuildSimulation(options);
        loop.Run(options.Simulation.MaxTicks);

        sw.Stop();

        if (!headless)
        {
            PrintSummary(options, loop, template, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Mode --benchmark (SYNE-090…093, PERFORMANCE.md §7) : pour chaque population
    /// ∈ {50, 500, 1000} × chaque seed ∈ {12345, 999, 7} (config §7) exécute
    /// <c>ticks</c> (défaut 300) et mesure le débit (t/s), les budgets par
    /// sous-système et le checksum d'état bit-à-bit. Rien de parallèle — la
    /// trajectoire reste déterministe.
    /// </summary>
    private static void RunBenchmark(SimulationOptions options, CliOptions cli)
    {
        ulong[] populations = cli.BenchmarkPopulations is { Length: > 0 } raw
            ? raw.Split(',').Select(ulong.Parse).ToArray()
            : [50, 500, 1000];
        int ticks = cli.BenchmarkTicks ?? 300;
        ulong[] seeds = cli.Seed is { } one
            ? [one]
            : [12_345, 999, 7];

        System.Console.WriteLine($"SYNE — benchmark PERFORMANCE.md §7 ({populations.Length} populations × {seeds.Length} seeds × {ticks} ticks, monde {options.Simulation.WorldWidth}×{options.Simulation.WorldHeight})");
        System.Console.WriteLine("Population | seed  | ticks | t/s    | tick moyen | part computation | perc. | mémo. | bes.  | déc.  | comm. | act.  | évén.");

        for (int p = 0; p < populations.Length; p++)
        {
            ulong population = populations[p];
            for (int s = 0; s < seeds.Length; s++)
            {
                ulong seed = seeds[s];
                var budgets = TickBudgetCollector.CreateEnabled();
                (_, Simulation.Core.World.World world, SimulationLoop loop, _) = BuildSimulation(options, population, seed, budgets);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                loop.Run(ticks);
                sw.Stop();

                TickBudgetSnapshot snap = budgets.Snapshot();
                double ticksPerSecond = ticks / (sw.Elapsed.TotalMilliseconds / 1000.0);
                System.Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{population,-10} | {seed,-5} | {ticks,-5} | {ticksPerSecond,6:F1} | {snap.MeanPipelineMs,10:F2} ms | {snap.ComputationShare() * 100,10:F1} % | {snap.MeanMs(TickPhase.Perception),6:F2} | {snap.MeanMs(TickPhase.MemoryBeliefs),5:F2} | {snap.MeanMs(TickPhase.NeedsGoals),5:F2} | {snap.MeanMs(TickPhase.UtilityDecision),5:F2} | {snap.MeanMs(TickPhase.Communication),5:F2} | {snap.MeanMs(TickPhase.ActionsMovement),5:F2} | {snap.MeanMs(TickPhase.EventsGroupsPopulation),5:F2}"));
            }
        }

        System.Console.WriteLine("Checksum d'état (FNV-1a, id;x;y;énergie — reproductible à seed égale) :");
        foreach (ulong population in populations)
        {
            foreach (ulong seed in seeds)
            {
                (_, Simulation.Core.World.World world, SimulationLoop loop, _) = BuildSimulation(options, population, seed);
                loop.Run(ticks);
                System.Console.WriteLine($"  N={population,-5} seed={seed,-5} → 0x{ChecksumState(loop, population, ticks):x16}");
            }
        }
    }

    /// <summary>
/// FNV-1a canonique de l'état du monde (DETERMINISM.md §6) : préfixe
/// « elément=population;ticks » puis une ligne par entité vivante
/// (id;x;y;énergie trié par id). Reprodéterminé bit-à-bit à seed égale.
/// </summary>
    private static ulong ChecksumState(SimulationLoop loop, ulong population, int ticks)
    {
        var text = new StringBuilder();
        text.Append("population=").Append(population.ToString(CultureInfo.InvariantCulture));
        text.Append(";ticks=").Append(ticks.ToString(CultureInfo.InvariantCulture));
        text.AppendLine();
        foreach (Entity entity in loop.World.Entities.OrderBy(e => e.Id.Value))
        {
            text.Append(entity.Id.Value.ToString(CultureInfo.InvariantCulture));
            text.Append(';');
            text.Append(entity.Position.X.ToString("0.00", CultureInfo.InvariantCulture));
            text.Append(';');
            text.Append(entity.Position.Y.ToString("0.00", CultureInfo.InvariantCulture));
            text.Append(';');
            double energy = loop.Cognition.HasMind(entity.Id.Value)
                ? loop.Cognition.MindOf(entity.Id.Value).Needs.Energy
                : 0.0;
            text.Append(energy.ToString("0.00", CultureInfo.InvariantCulture));
            text.AppendLine();
        }

        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        foreach (byte value in Encoding.UTF8.GetBytes(text.ToString()))
        {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }

    /// <summary>
    /// Mode --observe (SYNE-080) : boucle pilotée par l'émetteur, chaque tick
    /// diffusé en WebSocket (snapshot + événements) puis résumé final.
    /// </summary>
    private static async Task RunObservedAsync(SimulationOptions options, CliOptions cli)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        (_, var world, SimulationLoop loop, EntityTemplate template) = BuildSimulation(options);

        int port = cli.ObservePort ?? Observability.ObservabilityServer.DefaultPort;
        await using var server = new Observability.ObservabilityServer(port);
        server.Start();
        var emitter = new Observability.ObservabilityTickEmitter(loop, options.Random.Seed, server);
        await emitter.RunAsync(options.Simulation.MaxTicks);

        sw.Stop();

        if (cli.Headless != true)
        {
            PrintSummary(options, loop, template, sw.ElapsedMilliseconds);
            System.Console.WriteLine($"  observabilité : ws://127.0.0.1:{server.Port}/ | {emitter.TicksEmitted} ticks diffusés | {server.ClientCount} client(s)");
        }
    }

    /// <summary>
    /// Mode --serve (SYNE-113) : expose l'API HTTP REST de contrôle sur
    /// 127.0.0.1:[port] (défaut 5181, ADR-003, API_CONTRACTS.md §3). Le process
    /// reste en vie jusqu'à Ctrl+C ; aucun run n'est démarré automatiquement —
    /// il est lancé via POST /api/control/start.
    /// </summary>
    private static async Task RunServeAsync(SimulationOptions options, CliOptions cli)
    {
        int port = cli.ServePort ?? Control.ControlServer.DefaultPort;
        await using var server = new Control.ControlServer(port);
        server.Start();

        System.Console.WriteLine($"SYNE — serveur de contrôle HTTP :{server.Port}/ (API_CONTRACTS.md §3)");
        System.Console.WriteLine($"  POST /api/control/start|pause|resume|reset | GET /api/control/status");
        System.Console.WriteLine($"  seed par défaut : {options.Random.Seed} | population : {options.Agents.InitialCount}");
        System.Console.WriteLine("  Ctrl+C pour arrêter.");

        var exit = new TaskCompletionSource();
        System.Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            exit.TrySetResult();
        };

        await exit.Task;
    }

    private static (Xoshiro256StarStar Rng, Simulation.Core.World.World World, SimulationLoop Loop, EntityTemplate Template) BuildSimulation(SimulationOptions options)
    {
        return BuildSimulation(options, (ulong)options.Agents.InitialCount, options.Random.Seed, budgets: null);
    }

    private static (Xoshiro256StarStar Rng, Simulation.Core.World.World World, SimulationLoop Loop, EntityTemplate Template) BuildSimulation(
        SimulationOptions options,
        ulong population,
        ulong seed)
    {
        return BuildSimulation(options, population, seed, budgets: null);
    }

    private static (Xoshiro256StarStar Rng, Simulation.Core.World.World World, SimulationLoop Loop, EntityTemplate Template) BuildSimulation(
        SimulationOptions options,
        ulong population,
        ulong seed,
        TickBudgetCollector? budgets)
    {
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        var world = new Simulation.Core.World.World(
            new Simulation.Core.World.WorldSize(options.Simulation.WorldWidth, options.Simulation.WorldHeight),
            options.Agents.Perception.Radius);

        EntityTemplate template = EntityTemplate.DefaultA;
        for (ulong i = 0; i < population; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(template, world, rng, i + 1, bornAt: 0);
            world.AddEntity(entity);
        }

        var loop = new SimulationLoop(world, rng, options, budgets);
        return (rng, world, loop, template);
    }

    private static void PrintSummary(SimulationOptions options, SimulationLoop loop, EntityTemplate template, long elapsedMs)
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"SYNE — boucle minimale ({template.Species}) :");
        summary.AppendLine($"  monde : {options.Simulation.WorldWidth} x {options.Simulation.WorldHeight} | grille : {loop.World.Grid.CellCountX}x{loop.World.Grid.CellCountY}");
        summary.AppendLine($"  PRNG : {options.Random.Engine}, seed : {options.Random.Seed}");

        if (loop.CurrentTick > 0)
        {
            var head = BuildTickLine(1UL, loop.World.Entities.Count);
            var tail = BuildTickLine(loop.CurrentTick, loop.World.Entities.Count);
            summary.AppendLine("(tête) " + head);
            summary.AppendLine("  …    …");
            summary.AppendLine("(queue) " + tail);
        }

        summary.AppendLine($"  exécuté en {elapsedMs} ms | 1 tick = 1 minute simulée");
        System.Console.WriteLine(summary.ToString());
    }

    private static string BuildTickLine(ulong tick, int entityCount)
    {
        return $"tick {tick:D8}  {SimulationTime.FormatClock(tick)}  entités : {entityCount}";
    }
}