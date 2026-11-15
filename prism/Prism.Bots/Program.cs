using System.Globalization;
using System.Text.Json;
using Prism.Bots;
using Prism.Content;
using Prism.Contracts;
using Prism.Host.Hosting;

// Runs a bot campaign and prints one JSON record per (seed, bot) pair. The same command on
// Windows and Linux must print the same fingerprints: that is the PRISM-079 evidence.
var options = CampaignOptions.Parse(args);
if (!options.Valid)
{
    Console.Error.WriteLine(options.Error);
    return 2;
}

var contentRoot = options.ContentDirectory ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "content", "minimal");
contentRoot = Path.GetFullPath(contentRoot);
var compilationTimer = System.Diagnostics.Stopwatch.StartNew();
var compilation = new ContentPackCompiler().CompileDirectory(contentRoot);
compilationTimer.Stop();
if (options.Timings)
{
    Console.Error.WriteLine($"[timings] content compile: {compilationTimer.ElapsedMilliseconds} ms");
}
if (!compilation.Succeeded)
{
    foreach (var error in compilation.Errors)
    {
        Console.Error.WriteLine($"{error.Code}: {error.Path}: {error.Message}");
    }

    return 3;
}

var pack = compilation.Pack!;
var content = BotContentMap.FromContent(pack);
var records = new List<object>();
foreach (var seed in options.Seeds)
{
    if (options.R1)
    {
        records.Add(RunR1(seed, pack, content, contentRoot, options));
        continue;
    }

    foreach (var botName in options.Bots)
    {
        var runtime = new PrismHostRuntime();
        var prepareTimer = System.Diagnostics.Stopwatch.StartNew();
        runtime.Prepare(new PrepareWorldRequest(seed, pack.ContentHash, contentRoot));
        prepareTimer.Stop();
        if (options.Timings)
        {
            Console.Error.WriteLine($"[timings] prepare seed {seed}: {prepareTimer.ElapsedMilliseconds} ms");
        }

        runtime.Start(new StartWorldRequest());
        var spawns = new List<BotSpawn>();
        var bots = new Dictionary<ulong, IPrismBot>();
        for (var index = 0; index < options.Agents; index++)
        {
            var site = runtime.WorldSnapshot!.SpawnSites[index % runtime.WorldSnapshot.SpawnSites.Length];
            var agentId = (ulong)(index + 1);
            spawns.Add(new BotSpawn(agentId, new BotCoordinate(site.X, site.Y, site.Z)));
            bots[agentId] = CreateBot(botName, seed, agentId, content);
        }

        var runner = new BotRunner(runtime, bots);
        var runTimer = System.Diagnostics.Stopwatch.StartNew();
        var result = runner.Run(0, spawns, options.Ticks);
        runTimer.Stop();
        if (options.Timings)
        {
            Console.Error.WriteLine($"[timings] {options.Ticks} ticks: {runTimer.ElapsedMilliseconds} ms");
        }

        if (options.Timings && runtime.PathfindingTotals is { } pathfinding)
        {
            Console.Error.WriteLine(
                $"[timings] pathfinding: {pathfinding.Searches} recherche(s), " +
                $"{pathfinding.CacheHits} cache, {pathfinding.CacheInvalidations} périmée(s), " +
                $"{pathfinding.ExpandedNodes} nœuds, {pathfinding.FailedSearches} échec(s), " +
                $"{pathfinding.BudgetExhaustedSearches} budget(s) épuisé(s), " +
                $"taux d'échec {pathfinding.FailureRatePermille} ‰");
        }
        records.Add(new
        {
            seed,
            bot = botName,
            ticks = options.Ticks,
            contentHash = pack.ContentHash,
            fingerprint = result.FingerprintHex,
            trajectoryFingerprint = result.TrajectoryFingerprintHex,
            trajectoryFrames = result.TrajectoryFrames,
            commandsAccepted = result.CommandsAccepted,
            commandsSent = result.CommandsSent,
            failures = result.FailureReasons,
        });
    }
}

Console.WriteLine(JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
return 0;

static object RunR1(
    ulong seed,
    CompiledContentPack pack,
    BotContentMap content,
    string contentRoot,
    CampaignOptions options)
{
    var runtime = new PrismHostRuntime();
    var prepareTimer = System.Diagnostics.Stopwatch.StartNew();
    runtime.Prepare(new PrepareWorldRequest(seed, pack.ContentHash, contentRoot));
    prepareTimer.Stop();
    if (options.Timings)
    {
        Console.Error.WriteLine($"[timings] prepare seed {seed}: {prepareTimer.ElapsedMilliseconds} ms");
    }

    runtime.Start(new StartWorldRequest());
    var runTimer = System.Diagnostics.Stopwatch.StartNew();
    R1RunResult result = R1Campaign.Run(runtime, content, options.Agents, options.Ticks);
    runTimer.Stop();
    if (options.Timings)
    {
        Console.Error.WriteLine($"[timings] R1 {options.Agents} agents x {options.Ticks} ticks: {runTimer.ElapsedMilliseconds} ms");
    }

    return new
    {
        seed,
        mode = "r1",
        ticks = options.Ticks,
        agents = options.Agents,
        contentHash = pack.ContentHash,
        fingerprint = result.Run.FingerprintHex,
        trajectoryFingerprint = result.Run.TrajectoryFingerprintHex,
        trajectoryFrames = result.Run.TrajectoryFrames,
        commandsSent = result.Run.CommandsSent,
        commandsAccepted = result.Run.CommandsAccepted,
        failures = result.Run.FailureReasons,
        agentFailures = result.Run.Agents.Select(static agent => new
        {
            agentId = agent.AgentId,
            commandsSent = agent.CommandsSent,
            commandsAccepted = agent.CommandsAccepted,
            failures = agent.FailureReasons,
        }),
        cropRipeEvents = result.Run.CropRipeEvents,
        cropWitheredEvents = result.Run.CropWitheredEvents,
        conservation = new
        {
            green = result.ConservationGreen,
            violations = result.Violations.Select(static violation => new
            {
                itemTypeId = violation.ItemTypeId,
                expected = violation.Expected,
                actual = violation.Actual,
            }),
        },
        chains = result.Agents.Select(static agent => new
        {
            agentId = agent.AgentId,
            chain = agent.Chain,
            goal = agent.GoalItem,
            quantity = agent.Quantity,
            planSteps = agent.PlanSteps,
            rejectedSteps = agent.RejectedSteps,
            completed = agent.Completed,
        }),
        chainsCompleted = result.Agents.Count(static agent => agent.Completed),
    };
}

static IPrismBot CreateBot(string name, ulong seed, ulong agentId, BotContentMap content) => name switch
{
    "random" => new RandomBot(seed, agentId, content),
    "greedy" => new GreedyBot(content),
    "planner" => new PlannerBot(content, [("plank", 4)]),
    "idle" => new NoOpBot(),
    _ => throw new ArgumentOutOfRangeException(nameof(name), $"Unknown bot '{name}'."),
};

internal sealed record CampaignOptions(
    long Ticks,
    IReadOnlyList<ulong> Seeds,
    IReadOnlyList<string> Bots,
    int Agents,
    string? ContentDirectory)
{
    public bool Valid => Error is null;

    public bool Timings { get; init; }

    /// <summary>Run the R1 chain campaign (PRISM-102) instead of the reference bots.</summary>
    public bool R1 { get; init; }

    public string? Error { get; init; }

    public static CampaignOptions Parse(string[] arguments)
    {
        var ticks = 500L;
        var seeds = new List<ulong> { 1, 2, 3 };
        var bots = new List<string> { "random", "greedy", "planner" };
        var agents = 2;
        string? content = null;
        var timings = false;
        var r1 = false;
        for (var index = 0; index < arguments.Length; index++)
        {
            var current = arguments[index];
            var next = index + 1 < arguments.Length ? arguments[index + 1] : null;
            switch (current)
            {
                case "--ticks" when next is not null && long.TryParse(next, CultureInfo.InvariantCulture, out var parsedTicks) && parsedTicks > 0:
                    ticks = parsedTicks;
                    index++;
                    break;
                case "--seeds" when next is not null:
                    seeds = [];
                    foreach (var value in next.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!ulong.TryParse(value, CultureInfo.InvariantCulture, out var seed))
                        {
                            return new CampaignOptions(ticks, seeds, bots, agents, content) { Error = $"Invalid seed '{value}'." };
                        }

                        seeds.Add(seed);
                    }

                    index++;
                    break;
                case "--bots" when next is not null:
                    bots = [.. next.Split(',', StringSplitOptions.RemoveEmptyEntries)];
                    index++;
                    break;
                case "--agents" when next is not null && int.TryParse(next, CultureInfo.InvariantCulture, out var parsedAgents) && parsedAgents >= 0:
                    agents = parsedAgents;
                    index++;
                    break;
                case "--content" when next is not null:
                    content = next;
                    index++;
                    break;
                case "--timings":
                    timings = true;
                    break;
                case "--r1":
                    r1 = true;
                    break;
                default:
                    return new CampaignOptions(ticks, seeds, bots, agents, content) { Error = $"Unexpected argument '{current}'." };
            }
        }

        return new CampaignOptions(ticks, seeds, bots, agents, content) { Timings = timings, R1 = r1 };
    }
}
