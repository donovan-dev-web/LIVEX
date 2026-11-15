using System.Text.Json;
using Prism.Contracts;
using Prism.Content;
using Prism.Core.World;
using Prism.Host.Hosting;

namespace Prism.Host.Tests;

/// <summary>
/// Run R1 gate (PRISM-102): the host exposes the matter conservation audit so a bot campaign can
/// prove that nothing was created or destroyed behind the ledger's back. The audit counts what
/// agents, containers and the ground actually hold and compares it with the ledger's expected
/// balance; an empty violation list is the green verdict the gate requires.
/// </summary>
public sealed class ConservationAuditTests
{
    [Fact]
    public void AuditIsGreenBeforeAnyActionAndAfterAMinedLog()
    {
        var root = Path.Combine(FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var pack = compilation.Pack!;

        var runtime = new PrismHostRuntime(checkpointOnStop: false);
        runtime.Prepare(new PrepareWorldRequest(1UL, pack.ContentHash, root));
        runtime.Start(new StartWorldRequest());

        // A fresh world holds nothing, so the ledger has nothing to disagree with.
        Assert.Empty(runtime.AuditConservation());

        var world = runtime.WorldSnapshot!;
        var tree = world.Trees.OrderBy(candidate => candidate.Origin.X)
            .ThenBy(candidate => candidate.Origin.Y)
            .First();
        var cell = FindStandingCell(world, tree.Origin);
        Assert.NotNull(cell);

        var tick = 0L;
        TickResult Step(Func<long, IEnumerable<CommandEnvelope>>? commands = null)
        {
            var current = tick++;
            var batch = commands?.Invoke(current).ToArray() ?? [];
            return runtime.Step(new TickRequest(ProtocolVersion.Current.ToString(), current, batch));
        }

        using var incarnatePayload = JsonDocument.Parse(JsonSerializer.Serialize(
            new { position = new { x = cell.Value.X, y = cell.Value.Y, z = cell.Value.Z } },
            ProtocolJson.Options));
        TickResult incarnated = Step(current =>
        [
            new CommandEnvelope(
                ProtocolVersion.Current.ToString(), current, "incarnate", 7, "Incarner",
                incarnatePayload.RootElement.Clone()),
        ]);
        Assert.Contains(incarnated.Events, entry => entry.Kind == "action_completed");

        // Let perception run twice so the tree block is part of the observed set before mining.
        Step();
        Step();

        using var minePayload = JsonDocument.Parse(JsonSerializer.Serialize(
            new
            {
                target = new
                {
                    x = tree.Origin.X,
                    y = tree.Origin.Y,
                    z = tree.Origin.Z,
                },
            },
            ProtocolJson.Options));
        TickResult mined = Step(current =>
        [
            new CommandEnvelope(
                ProtocolVersion.Current.ToString(), current, "mine-tree", 7, "Miner",
                minePayload.RootElement.Clone()),
        ]);
        Assert.DoesNotContain(mined.Events, entry => entry.Kind == "action_failed");

        // Mining has a duration: let the deferred action resolve and drop its log.
        var completed = false;
        for (var i = 0; i < 60 && !completed; i++)
        {
            TickResult step = Step();
            completed = step.Events.Any(entry => entry.Kind is "item_dropped" or "action_completed");
        }

        Assert.True(completed, "The mining action never resolved.");
        var metrics = runtime.CurrentMetrics!;
        Assert.True(
            metrics.Stocks.Sum(stock => stock.Agents + stock.Containers + stock.GroundItems) > 0,
            "The mined log must be held somewhere.");
        Assert.Empty(runtime.AuditConservation());
    }

    private static BlockCoordinate? FindStandingCell(GeneratedWorldSnapshot world, BlockCoordinate origin)
    {
        (int Dx, int Dy)[] neighbours = [(-1, 0), (1, 0), (0, -1), (0, 1)];
        foreach (var (dx, dy) in neighbours)
        {
            var x = origin.X + dx;
            var y = origin.Y + dy;
            if (x < 0 || y < 0 || x >= world.Width || y >= world.Height)
            {
                continue;
            }

            var column = world.SurfaceColumns[x + world.Width * y];
            if (column.SurfaceZ == origin.Z - 1)
            {
                return new BlockCoordinate(x, y, origin.Z);
            }
        }

        return null;
    }

    private static string FindPrismRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Prism.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Prism.sln from the host test output directory.");
    }
}
