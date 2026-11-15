using Prism.Contracts;
using Prism.Host.Hosting;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

/// <summary>
/// PRISM-117 : le rendu ne peut pas changer le run. Un host consommé par une session de
/// viewer et un host laissé sans lecteur doivent produire exactement le même monde, la même
/// empreinte et le même état d'entités.
/// </summary>
public sealed class ViewerRenderIndependenceTests
{
    private const ulong Seed = 1;
    private const int Ticks = 20;

    [Fact]
    public async Task ViewerSessionLeavesTheRunBitIdentical()
    {
        var withoutViewer = await RunAsync(withViewer: false);
        var withViewer = await RunAsync(withViewer: true);

        Assert.NotEqual(0UL, withoutViewer.WorldHash);
        Assert.Equal(withoutViewer.WorldHash, withViewer.WorldHash);
        Assert.Equal(withoutViewer.Tick, withViewer.Tick);
        Assert.Equal(withoutViewer.Agents, withViewer.Agents);
        Assert.True(withViewer.Envelopes > 0);
        Assert.Equal(0, withViewer.Resynchronisations);
        Assert.Equal(withoutViewer.WorldHash, withViewer.SessionWorldHash);
    }

    private static async Task<RunResult> RunAsync(bool withViewer)
    {
        var contentRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "content", "minimal"));
        var compilation = new Prism.Content.ContentPackCompiler().CompileDirectory(contentRoot);
        Assert.True(compilation.Succeeded);
        var pack = compilation.Pack!;

        var runtime = new PrismHostRuntime(checkpointOnStop: false);
        runtime.Prepare(new PrepareWorldRequest(Seed, pack.ContentHash, contentRoot));
        runtime.Start(new StartWorldRequest());

        var session = new ViewerLiveSession();
        await using var subscription = runtime.SubscribeReadStream();
        var reader = withViewer ? ReadAsync(subscription, session, Ticks - 1) : null;
        long envelopes = 0;

        for (var tick = 0; tick < Ticks; tick++)
        {
            runtime.Step(new TickRequest(ProtocolVersion.Current.ToString(), tick, []));
        }

        if (reader is not null)
        {
            // Lecture bornée : le flux ne se ferme qu'à la fin du run, on s'arrête au dernier tick.
            envelopes = await reader.WaitAsync(TimeSpan.FromSeconds(60));
        }
        else
        {
            // Sans viewer, personne ne lit le flux : le host ne doit pas s'en apercevoir.
            envelopes = 0;
        }

        var world = runtime.WorldSnapshot!;
        return new RunResult(
            world.WorldHash,
            runtime.Status.LastCompletedTick,
            withViewer ? session.Store.AgentCount : world.Agents?.Length ?? 0,
            envelopes,
            session.Resynchronisations,
            session.World?.WorldHash ?? 0);
    }

    private static async Task<long> ReadAsync(
        ReadStreamSubscription subscription,
        ViewerLiveSession session,
        long lastTick)
    {
        long count = 0;
        var reader = subscription.ReadAllAsync().GetAsyncEnumerator();
        try
        {
            while (await reader.MoveNextAsync())
            {
                var envelope = reader.Current;
                session.Apply(new ViewerStreamEnvelope(
                    envelope.ProtocolVersion,
                    envelope.Sequence,
                    envelope.Tick,
                    envelope.Kind,
                    envelope.Payload));
                count++;
                if (envelope.Kind == "tick_result" && envelope.Tick >= lastTick)
                {
                    break;
                }
            }
        }
        finally
        {
            await reader.DisposeAsync();
        }

        return count;
    }

    private sealed record RunResult(
        ulong WorldHash,
        long Tick,
        int Agents,
        long Envelopes,
        int Resynchronisations,
        ulong SessionWorldHash);
}
