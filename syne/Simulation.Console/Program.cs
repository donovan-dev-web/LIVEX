using System.Globalization;
using System.Net;
using System.Text;
using Simulation.Console.Batch;
using Simulation.Console.Control;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Prng;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
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

            if (cli.Help)
            {
                System.Console.Out.WriteLine(CliOptions.Usage);
                return 0;
            }

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

            if (cli.ControlPort is not null)
            {
                return await RunSupervisedAsync(options, cli);
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
                await RunBatchAsync(options, cli);
            }

            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException
            or DirectoryNotFoundException or InvalidDataException or FormatException or OverflowException
            or IOException or UnauthorizedAccessException or InvalidOperationException or HttpListenerException)
        {
            // FormatException et OverflowException proviennent de l'analyse des
            // arguments : sans ce filtre, « --seed -1 » ou « --max-ticks abc »
            // sortaient en trace d'appels au lieu d'un message exploitable.
            System.Console.Error.WriteLine($"Erreur : {ex.Message}");
            System.Console.Error.WriteLine("Lancez --help pour la liste des options.");
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

        if (cli.Ticks is { } ticks)
        {
            options.Simulation.MaxTicks = ticks;
        }

        if (cli.Seed is { } seed)
        {
            options.Random.Seed = seed;
        }

        return options;
    }

    private static async Task RunBatchAsync(SimulationOptions options, CliOptions cli)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        (_, var world, SimulationLoop loop, EntityTemplate template) = BuildSimulation(options);
        int initialEntityCount = world.Entities.Count;

        // L'export du flux passe par l'émetteur d'observabilité : les trames
        // écrites sont exactement celles du WebSocket, et l'export n'ajoute
        // aucun tirage PRNG (DETERMINISM.md §3).
        string? runId = null;
        if (cli.ExportStream)
        {
            string streamDirectory = cli.ExportDirectory
                ?? throw new ArgumentException("--export-stream exige --export-dir.");
            runId = string.IsNullOrWhiteSpace(cli.RunId)
                ? ObservabilityContract.RunIdFor(options.Random.Seed)
                : cli.RunId;
            await using var stream = new BatchStreamFile(
                Path.Combine(streamDirectory, BatchStreamFile.DefaultFileName));
            var emitter = new Observability.ObservabilityTickEmitter(loop, options.Random.Seed, stream, runId);
            await emitter.RunAsync(options.Simulation.MaxTicks);
            System.Console.WriteLine($"  flux d'observabilité : {stream.Path} ({stream.FramesWritten} trame(s))");
        }
        else
        {
            loop.Run(options.Simulation.MaxTicks);
        }

        sw.Stop();

        if (cli.ExportDirectory is not null)
        {
            string resultPath = await BatchRunExporter.WriteResultAsync(
                loop,
                cli.Simulation ?? "reference",
                options.Random.Seed,
                (ulong)options.Agents.InitialCount,
                cli.ExportDirectory,
                cli.RunId ?? runId,
                cli.ExportStream ? BatchStreamFile.DefaultFileName : null);
            System.Console.WriteLine($"  résultat batch : {resultPath}");
        }

        if (cli.Headless != true)
        {
            PrintSummary(options, loop, template, sw.ElapsedMilliseconds, initialEntityCount);
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
        int initialEntityCount = world.Entities.Count;

        int port = cli.ObservePort ?? Observability.ObservabilityServer.DefaultPort;
        await using var server = new Observability.ObservabilityServer(port);
        server.Start();
        var emitter = new Observability.ObservabilityTickEmitter(loop, options.Random.Seed, server);
        await emitter.RunAsync(options.Simulation.MaxTicks);

        sw.Stop();

        if (cli.Headless != true)
        {
            PrintSummary(options, loop, template, sw.ElapsedMilliseconds, initialEntityCount);
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
        var observability = new Observability.ObservabilityServer(
            cli.ObservePort ?? Observability.ObservabilityServer.DefaultPort);
        await using var server = new Control.ControlServer(port, observability: observability);
        server.Start();

        System.Console.WriteLine($"SYNE — serveur de contrôle HTTP :{server.Port}/ (API_CONTRACTS.md §3)");
        System.Console.WriteLine($"  observabilité WebSocket : ws://127.0.0.1:{observability.Port}/");
        System.Console.WriteLine($"  POST /api/control/start|pause|resume|stop|reset | GET /api/control/status");
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

    private static async Task<int> RunSupervisedAsync(SimulationOptions options, CliOptions cli)
    {
        if (!cli.AutoStart
            && (cli.Simulation is not null || cli.Ticks is not null || cli.ExportDirectory is not null
                || cli.Seed is not null || cli.MaxTicks is not null || cli.ConfigPath is not null
                || cli.RunId is not null || cli.ExportStream))
        {
            throw new ArgumentException("Les options de batch (--simulation, --seed, --ticks, --config, --export-dir, --run-id, --export-stream) exigent --autostart en mode supervisé.");
        }

        string sessionToken = Environment.GetEnvironmentVariable("LIVEX_SESSION_TOKEN")
            ?? throw new ArgumentException("LIVEX_SESSION_TOKEN est obligatoire avec --control-port.");
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new ArgumentException("LIVEX_SESSION_TOKEN ne peut pas être vide.");
        }

        string correlationId = cli.CorrelationId!;
        if (!string.Equals(Environment.GetEnvironmentVariable("LIVEX_CORRELATION_ID"), correlationId, StringComparison.Ordinal))
        {
            throw new ArgumentException("--correlation-id doit correspondre à LIVEX_CORRELATION_ID.");
        }

        string workDirectory = Path.GetFullPath(cli.WorkDirectory!);
        string logDirectory = Path.GetFullPath(cli.LogDirectory!);
        Directory.CreateDirectory(workDirectory);
        Directory.CreateDirectory(logDirectory);

        string? exportDirectory = null;
        if (cli.AutoStart)
        {
            exportDirectory = ResolveExportDirectory(cli.ExportDirectory, workDirectory);
            Directory.CreateDirectory(exportDirectory);
        }
        else if (cli.ExportDirectory is not null)
        {
            throw new ArgumentException("--export-dir exige --autostart en mode service.");
        }

        string configJson = cli.ConfigPath is null
            ? ConfigLoader.ToJson(options)
            : await File.ReadAllTextAsync(cli.ConfigPath);
        Observability.ObservabilityServer? observability = cli.AutoStart && cli.ObservePort is null
            ? null
            : new Observability.ObservabilityServer(cli.ObservePort ?? Observability.ObservabilityServer.DefaultPort);
        await using var streamFile = cli.ExportStream
            ? new BatchStreamFile(Path.Combine(exportDirectory!, BatchStreamFile.DefaultFileName))
            : null;
        Observability.IObservabilitySink? sink = (Observability.IObservabilitySink?)observability ?? streamFile;
        if (observability is not null && streamFile is not null)
        {
            sink = new Observability.CompositeObservabilitySink(observability, streamFile);
        }

        await using var server = new Control.ControlServer(
            cli.ControlPort!.Value,
            controller: sink is null ? null : new Control.SimulationController(sink),
            observability: observability,
            sessionToken: sessionToken,
            instanceId: cli.InstanceId!,
            version: ObservabilityContract.EngineVersion,
            initiallyReady: false);

        if (cli.AutoStart)
        {
            // Listener (contrôle + observabilité) ouvert AVANT la préparation :
            // la trame `world_initialized` n'est diffusée qu'une fois, et un
            // consommateur live (analyse temps réel) doit pouvoir se connecter
            // avant elle — sinon le mode supervisé ne livre jamais la description
            // du monde à personne. Le contrôle reste fermé tant que MarkReady
            // n'a pas été appelé (initiallyReady = false).
            server.Start();
            var world = await server.Controller.PrepareAsync(options.Random.Seed, configJson);
            server.MarkReady();
            server.Controller.AcknowledgeReady(world.Version);
            await server.Controller.StartAsync(
                options.Random.Seed,
                configJson: null,
                cli.Ticks ?? cli.MaxTicks ?? options.Simulation.MaxTicks,
                requestedRunId: cli.RunId);
            await WriteComponentLogAsync(logDirectory, correlationId, "batch_started");
            System.Console.WriteLine($"SYNE — instance {cli.InstanceId} prête sur 127.0.0.1:{server.Port}; batch lancé.");

            Task finished = WaitUntilRunFinishedAsync(server.Controller);
            Task ended = await WaitForTerminationAsync(server.ShutdownRequested, finished);
            if (ended != finished)
            {
                await server.Controller.StopAsync();
                await WriteComponentLogAsync(logDirectory, correlationId, "batch_cancelled");
                return 130;
            }

            SimulationLoop loop = server.Controller.CurrentLoop
                ?? throw new InvalidOperationException("La boucle batch terminée est indisponible.");
            string resultPath = await BatchRunExporter.WriteResultAsync(
                loop,
                cli.Simulation ?? "reference",
                options.Random.Seed,
                (ulong)options.Agents.InitialCount,
                exportDirectory!,
                server.Controller.RunId,
                streamFile is null ? null : BatchStreamFile.DefaultFileName);
            await WriteComponentLogAsync(logDirectory, correlationId, "batch_completed");
            System.Console.WriteLine($"SYNE — batch terminé; résultat : {resultPath}");
            return 0;
        }

        server.Start();
        server.MarkReady();
        await WriteComponentLogAsync(logDirectory, correlationId, "service_ready");
        System.Console.WriteLine($"SYNE — instance {cli.InstanceId} prête sur 127.0.0.1:{server.Port}.");
        await WaitForTerminationAsync(server.ShutdownRequested);
        await WriteComponentLogAsync(logDirectory, correlationId, "service_stopping");
        return 0;
    }

    private static string ResolveExportDirectory(string? configuredDirectory, string workDirectory)
    {
        string configured = configuredDirectory ?? Path.Combine(workDirectory, "data");
        string exportDirectory = Path.GetFullPath(configured);
        string relative = Path.GetRelativePath(workDirectory, exportDirectory);
        if (Path.IsPathRooted(relative)
            || relative == ".."
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException("--export-dir doit être situé dans --work-dir.");
        }

        return exportDirectory;
    }

    private static async Task WriteComponentLogAsync(string logDirectory, string correlationId, string eventName)
    {
        string line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.UtcNow:O} correlationId={correlationId} event={eventName}{Environment.NewLine}");
        await File.AppendAllTextAsync(Path.Combine(logDirectory, "syne.log"), line);
    }

    private static async Task WaitUntilRunFinishedAsync(SimulationController controller)
    {
        while (controller.State != SimulationControlState.Finished)
        {
            await Task.Delay(20);
        }
    }

    private static async Task<Task> WaitForTerminationAsync(params Task[] tasks)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            signal.TrySetResult();
        };
        System.Console.CancelKeyPress += handler;
        try
        {
            return await Task.WhenAny(tasks.Append(signal.Task));
        }
        finally
        {
            System.Console.CancelKeyPress -= handler;
        }
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
            options.Agents.Perception.SpatialCellSize);

        EntityTemplate template = EntityTemplate.DefaultA;
        for (ulong i = 0; i < population; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(template, world, rng, i + 1, bornAt: 0);
            world.AddEntity(entity);
        }

        world.ApplyConfiguredLayout(options.World);

        var loop = new SimulationLoop(world, rng, options, budgets);
        return (rng, world, loop, template);
    }

    private static void PrintSummary(
        SimulationOptions options,
        SimulationLoop loop,
        EntityTemplate template,
        long elapsedMs,
        int initialEntityCount)
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"SYNE — boucle minimale ({template.Species}) :");
        summary.AppendLine($"  monde : {options.Simulation.WorldWidth} x {options.Simulation.WorldHeight} | grille : {loop.World.Grid.CellCountX}x{loop.World.Grid.CellCountY}");
        summary.AppendLine($"  PRNG : {options.Random.Engine}, seed : {options.Random.Seed}");

        if (loop.CurrentTick > 0)
        {
            // La ligne « (tête) » décrit le tick 1 : les entités ne meurent qu'en
            // cours de run, son compte est donc le compte initial capturé avant la
            // boucle — pas le compteur final, qui faisait lire « tick 1 … 42 entités »
            // après une extinction.
            var head = BuildTickLine(1UL, initialEntityCount);
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