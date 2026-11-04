using Prism.Bots;
using Prism.Content;
using Prism.Host.Hosting;

namespace Prism.Bots.Tests;

/// <summary>
/// Reduced golden campaign for PRISM-077/079: 3 seeds x 200 ticks x the three reference bots,
/// one agent per run, against the reference pack and the real host. The full campaign (3 seeds x
/// 5 000 ticks) stays a manual release gate; this one walks the same code path and pins the run
/// fingerprints, so any behavioural drift in the tick, the perception or the bots fails the build.
/// <para>
/// Re-pinning is never silent: a changed fingerprint means the run changed, so update
/// <c>docs/prism/ISSUES.md</c> (PRISM-077) with the reason before touching the constants below.
/// </para>
/// </summary>
public sealed class DeterministicCampaignTests
{
    /// <summary>Ticks of the reduced campaign; the full campaign uses 5 000.</summary>
    public const long GoldenTicks = 200;

    [Theory]
    [InlineData(1UL, "random", "e7a7de87375d26bf")]
    [InlineData(1UL, "greedy", "e675629c8038b576")]
    [InlineData(1UL, "planner", "623df86b644b7eee")]
    [InlineData(2UL, "random", "fee18bde0b6f3d11")]
    [InlineData(2UL, "greedy", "e8de84376ea91739")]
    [InlineData(2UL, "planner", "d804c024ce703100")]
    [InlineData(3UL, "random", "70e3a4e10abbc6e7")]
    [InlineData(3UL, "greedy", "ae13168c5be4e1ca")]
    [InlineData(3UL, "planner", "26256c608a33d3f9")]
    public void ReducedGoldenCampaignKeepsItsFingerprint(ulong seed, string bot, string expected)
    {
        var root = Path.Combine(BotRunnerTests.FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var pack = compilation.Pack!;
        var content = BotContentMap.FromContent(pack);

        var runtime = new PrismHostRuntime();
        runtime.Prepare(new PrepareWorldRequest(seed, pack.ContentHash, root));
        runtime.Start(new StartWorldRequest());

        var site = runtime.WorldSnapshot!.SpawnSites[0];
        var runner = new BotRunner(runtime, new Dictionary<ulong, IPrismBot>
        {
            [1] = CreateBot(bot, seed, content),
        });

        var result = runner.Run(
            0,
            [new BotSpawn(1, new BotCoordinate(site.X, site.Y, site.Z))],
            GoldenTicks);

        Assert.Equal(GoldenTicks - 1, result.LastTick);
        Assert.Equal(expected, result.FingerprintHex);
    }

    private static IPrismBot CreateBot(string name, ulong seed, BotContentMap content) => name switch
    {
        "random" => new RandomBot(seed, 1, content),
        "greedy" => new GreedyBot(content),
        "planner" => new PlannerBot(content, [("plank", 4)]),
        _ => throw new ArgumentOutOfRangeException(nameof(name), $"Unknown bot '{name}'."),
    };
}
