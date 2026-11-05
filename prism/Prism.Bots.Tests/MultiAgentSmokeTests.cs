using Prism.Content;
using Prism.Host.Hosting;

namespace Prism.Bots.Tests;

/// <summary>
/// Smoke test for PRISM-102 (run R1): several planner bots run concurrently on the minimal pack
/// through the real host for a bounded number of ticks without crashing and without losing the
/// tick cadence. This is a reduced stand-in for the 50-agent / 5-chain campaign, which stays a
/// manual release gate.
/// </summary>
public sealed class MultiAgentSmokeTests
{
    [Fact]
    public void ThreePlannerBotsRunConcurrentlyWithoutCrashing()
    {
        var root = Path.Combine(BotRunnerTests.FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var pack = compilation.Pack!;
        var content = BotContentMap.FromContent(pack);

        var runtime = new PrismHostRuntime();
        runtime.Prepare(new PrepareWorldRequest(1UL, pack.ContentHash, root));
        runtime.Start(new StartWorldRequest());

        var site = runtime.WorldSnapshot!.SpawnSites[0];
        var bots = new Dictionary<ulong, IPrismBot>
        {
            [1] = new PlannerBot(content, []),
            [2] = new PlannerBot(content, []),
            [3] = new PlannerBot(content, []),
        };
        var runner = new BotRunner(runtime, bots);

        var spawns = new[]
        {
            new BotSpawn(1, new BotCoordinate(site.X, site.Y, site.Z)),
            new BotSpawn(2, new BotCoordinate(site.X + 1, site.Y, site.Z)),
            new BotSpawn(3, new BotCoordinate(site.X + 2, site.Y, site.Z)),
        };

        var result = runner.Run(0, spawns, ticks: 50);

        Assert.Equal(49, result.LastTick);
        Assert.Equal(3, result.Agents.Count);
    }
}