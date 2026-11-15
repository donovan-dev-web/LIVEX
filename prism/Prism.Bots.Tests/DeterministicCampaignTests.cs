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
/// These are <b>byte-level</b> fingerprints (PRISM-107): they hash the serialized stream in the
/// order it was published, so a pure emission-order change moves them too. They stay the
/// contract and replay pin; a performance change is judged on the trajectory level instead
/// (see <see cref="TwoLevelFingerprintTests"/> and <c>docs/prism/TESTING.md</c> §3.1).
/// </para>
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
    [InlineData(1UL, "random", "1d57d8c21416379a")]
    // 9 novembre 2026 : le graphe de déplacement accepte la cellule d'un trou miné (PRISM-102),
    // une station se pose sur le sol de la colonne voisine et le bot outils ne perd plus la
    // chaîne sur `path_not_found`. Greedy et planner bougent, donc leurs empreintes bougent.
    [InlineData(1UL, "greedy", "733e128aebc76c3d")]
    [InlineData(1UL, "planner", "ffde28674bbc6174")]
    [InlineData(2UL, "random", "2290cf914cbad44a")]
    [InlineData(2UL, "greedy", "6896049703c9b10a")]
    [InlineData(2UL, "planner", "5444a165b8b0992a")]
    [InlineData(3UL, "random", "435842c3fc778d52")]
    [InlineData(3UL, "greedy", "0181f2dc0e0c3dc5")]
    [InlineData(3UL, "planner", "fab40e8d19c72371")]
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
