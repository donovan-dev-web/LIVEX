using Prism.Bots;
using Prism.Content;
using Prism.Contracts;
using Prism.Host.Hosting;

namespace Prism.Bots.Tests;

/// <summary>
/// End-to-end proof that the bots speak the real contract: they incarnate through the host,
/// receive differential observations and get their commands dispatched. Kept deliberately short
/// because world generation dominates the runtime on the reference pack.
/// </summary>
public sealed class BotHostIntegrationTests
{
    [Fact]
    public void GreedyBotRunsAgainstTheRealHost()
    {
        var root = Path.Combine(BotRunnerTests.FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var pack = compilation.Pack!;
        var content = BotContentMap.FromContent(pack);

        var runtime = new PrismHostRuntime();
        runtime.Prepare(new PrepareWorldRequest(123, pack.ContentHash, root));
        runtime.Start(new StartWorldRequest());

        var site = runtime.WorldSnapshot!.SpawnSites[0];
        var spawn = new BotSpawn(1, new BotCoordinate(site.X, site.Y, site.Z));
        var runner = new BotRunner(runtime, new Dictionary<ulong, IPrismBot>
        {
            [1] = new GreedyBot(content),
        });

        var result = runner.Run(0, [spawn], ticks: 3);

        Assert.Equal(0, result.FirstTick);
        Assert.Equal(2, result.LastTick);
        Assert.True(result.CommandsSent >= 2, "The incarnation and at least one decision are sent.");
        Assert.True(result.CommandsAccepted >= 1, "The host must dispatch at least one bot command.");
        Assert.NotEqual(RunFingerprint.Empty, result.Fingerprint);
        Assert.Equal("greedy", Assert.Single(result.Agents).BotKind);
        Assert.NotNull(runtime.WorldSnapshot!.Agents);
        Assert.Equal(1UL, Assert.Single(runtime.WorldSnapshot!.Agents!).AgentId);
    }
}
