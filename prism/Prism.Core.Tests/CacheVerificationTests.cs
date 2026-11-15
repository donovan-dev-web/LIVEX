using System.Reflection;
using Prism.Core.Diagnostics;
using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Core.Tests;

/// <summary>
/// PRISM-108: the caches of the 10 November 2026 optimisations are only safe while every world
/// mutation bumps <see cref="WorldGrid.Revision"/>. These tests forge the opposite — a write the
/// revision counter never saw — and show the two behaviours the mode exists for: without
/// verification the cache lies silently, with <c>PRISM_VERIFY_CACHES=1</c> the first divergence
/// fails the run and names the cache, the tick, the agent and the cell.
/// </summary>
public sealed class CacheVerificationTests
{
    private static readonly PerceptionSettings Settings =
        new(NearRadius: 4, MidRadiusRegions: 0, MaximumObservedPerAgent: 64);

    /// <summary>A block placed one level above the ground is visible; the pit of the fault is that nobody sees it.</summary>
    private static readonly BlockCoordinate Placed = new(3, 3, 5);

    [Fact]
    public void PerceptionWindowLiesSilentlyWhenTheWorldMovesWithoutARevisionBump()
    {
        var (world, registry, observer) = CreateScene(materialId: 3);
        var system = new PerceptionSystem();

        // Tick 0 builds the window; tick 1 must reuse it, since neither the grid revision nor the
        // agent's cell moved.
        system.Observe(observer, world, registry, new GroundItemStore(), CreateRules(), Settings, tick: 0);
        WriteWithoutRevisionBump(world, Placed, new BlockOverride(2, 0, false));
        var frame = system.Observe(
            observer, world, registry, new GroundItemStore(), CreateRules(), Settings, tick: 1);

        // This is the debt the issue names: no exception, no change event, the block the world now
        // holds is simply absent from what the agent believes.
        Assert.DoesNotContain(frame.Changes, change =>
            change.Entry.Kind == PerceptionEntityKind.Block &&
            change.Entry.X == Placed.X &&
            change.Entry.Y == Placed.Y &&
            change.Entry.Z == Placed.Z);
        Assert.DoesNotContain(
            system.GetPerceivedEntries(observer.AgentId),
            entry => entry.Kind == PerceptionEntityKind.Block &&
                     entry.X == Placed.X && entry.Y == Placed.Y && entry.Z == Placed.Z);
    }

    [Fact]
    public void VerificationNamesTheCacheTheTickTheAgentAndTheDivergentCell()
    {
        var (world, registry, observer) = CreateScene(materialId: 3);
        var system = new PerceptionSystem(verifyCaches: true);

        system.Observe(observer, world, registry, new GroundItemStore(), CreateRules(), Settings, tick: 0);
        WriteWithoutRevisionBump(world, Placed, new BlockOverride(2, 0, false));

        var exception = Assert.Throws<CacheVerificationException>(() =>
            system.Observe(observer, world, registry, new GroundItemStore(), CreateRules(), Settings, tick: 1));

        Assert.Equal("perception.window", exception.Cache);
        Assert.Equal(1, exception.Tick);
        Assert.Equal(observer.AgentId, exception.AgentId);
        // The block placed above the ground hides the surface block under it, and canonical key
        // order (kind, then Z, then Y, then X) meets the hidden cell first: the mode reports the
        // first divergence, not the most convenient one.
        Assert.Equal($"({Placed.X},{Placed.Y},{Placed.Z - 1})", exception.Location);
        Assert.Contains("tick 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("agent 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"({Placed.X},{Placed.Y},{Placed.Z - 1})", exception.Message, StringComparison.Ordinal);
        Assert.Contains("without a grid revision bump", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StandMemoLiesSilentlyWhenTheWorldMovesWithoutARevisionBump()
    {
        var (world, _, _) = CreateScene(materialId: 3);
        var graph = new MovementGraph(world, new MovementRules([1, 2], []));

        Assert.True(graph.CanStandAt(2, 2, out var feetZ));

        // A solid block at the agent's feet makes the cell unstandable, but the memo keeps the old
        // answer: this is the silent wrong path a real effect could produce.
        WriteWithoutRevisionBump(world, new BlockCoordinate(2, 2, feetZ), new BlockOverride(1, 0, false));

        Assert.True(graph.CanStandAt(2, 2, out var memoFeetZ));
        Assert.Equal(feetZ, memoFeetZ);
    }

    [Fact]
    public void VerificationCatchesTheStandMemoAtTheDivergentCell()
    {
        var (world, _, _) = CreateScene(materialId: 3);
        var graph = new MovementGraph(world, new MovementRules([1, 2], []), verifyCaches: true)
        {
            Tick = 7,
        };

        Assert.True(graph.CanStandAt(2, 2, out var feetZ));
        WriteWithoutRevisionBump(world, new BlockCoordinate(2, 2, feetZ), new BlockOverride(1, 0, false));

        var exception = Assert.Throws<CacheVerificationException>(() => graph.CanStandAt(2, 2, out _));

        Assert.Equal("movement.standMemo", exception.Cache);
        Assert.Equal(7, exception.Tick);
        Assert.Equal("(2,2)", exception.Location);
        Assert.Contains("tick 7", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerificationStaysQuietWhenEveryWriteBumpsTheRevision()
    {
        var (world, registry, observer) = CreateScene(materialId: 3);
        var system = new PerceptionSystem(verifyCaches: true);
        var graph = new MovementGraph(world, new MovementRules([1, 2], []), verifyCaches: true);

        system.Observe(observer, world, registry, new GroundItemStore(), CreateRules(), Settings, tick: 0);
        Assert.True(graph.CanStandAt(2, 2, out _));

        // Ordinarily the world moves between two ticks: a block appears, one is removed, the agent
        // moves. Every one of those goes through the grid, so the revision moves with them and the
        // verification has nothing to report — the mode must not cry wolf on a healthy run.
        world.SetBlockOverride(Placed, new BlockOverride(2, 0, false));
        var afterPlace = system.Observe(
            observer, world, registry, new GroundItemStore(), CreateRules(), Settings, tick: 1);
        Assert.Contains(afterPlace.Changes, change =>
            change.Change == PerceptionChangeKind.Appeared &&
            change.Entry.Kind == PerceptionEntityKind.Block &&
            change.Entry.X == Placed.X && change.Entry.Y == Placed.Y && change.Entry.Z == Placed.Z);

        world.RemoveBlockOverride(Placed);
        system.Observe(observer, world, registry, new GroundItemStore(), CreateRules(), Settings, tick: 2);

        // And a legitimate write does move the memo: the revision bump clears it, the new answer
        // is right, and the verification has nothing to report.
        Assert.True(graph.CanStandAt(1, 1, out var feetZ));
        world.SetBlockOverride(new BlockCoordinate(1, 1, feetZ), new BlockOverride(1, 0, false));
        Assert.False(graph.CanStandAt(1, 1, out _));
    }

    [Fact]
    public void TheModeIsOffUnlessTheEnvironmentAsksForIt()
    {
        var previous = Environment.GetEnvironmentVariable(CacheVerification.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(CacheVerification.EnvironmentVariable, null);
            Assert.False(CacheVerification.FromEnvironment());

            Environment.SetEnvironmentVariable(CacheVerification.EnvironmentVariable, "0");
            Assert.False(CacheVerification.FromEnvironment());

            Environment.SetEnvironmentVariable(CacheVerification.EnvironmentVariable, "1");
            Assert.True(CacheVerification.FromEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable(CacheVerification.EnvironmentVariable, previous);
        }
    }

    private static (WorldGrid World, AgentRegistry Registry, AgentBody Observer) CreateScene(ushort materialId)
    {
        var world = new WorldGrid(6, 6, regionSize: 2, verticalSize: 16);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(4, 1, 0, 0));
            }
        }

        var registry = new AgentRegistry();
        var movement = new MovementGraph(world, new MovementRules([1, 2], []));
        var observer = registry.Incarnate(
            1,
            AgentPosition.AtCell(2, 2, 5),
            new AgentBodyProfile(),
            movement);
        Assert.True(observer.Inventory.TryAdd(materialId, 1, 64, out var remaining));
        Assert.Equal(0, remaining);
        return (world, registry, observer);
    }

    /// <summary>
    /// Forges the fault the mode exists to catch: a world mutation the revision counters never saw.
    /// The public API always bumps, so the injection writes through it and then rewinds the global
    /// revision <b>and the written region's revision</b> — both keys a cache may compare against.
    /// A mutation that only slipped past one of them is exactly what the verification is for.
    /// </summary>
    private static void WriteWithoutRevisionBump(WorldGrid world, BlockCoordinate coordinate, BlockOverride block)
    {
        Assert.True(world.TryGetRegionId(coordinate.X, coordinate.Y, out var regionId));
        var revisionBefore = world.Revision;
        var regionRevisionBefore = world.GetRegionRevision(regionId);
        world.SetBlockOverride(coordinate, block);
        Assert.NotEqual(revisionBefore, world.Revision);
        Assert.NotEqual(regionRevisionBefore, world.GetRegionRevision(regionId));

        var field = typeof(WorldGrid).GetField(
                        "<Revision>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException(
                        "WorldGrid.Revision must stay an auto-property for the fault injection to work.");
        field.SetValue(world, revisionBefore);
        Assert.Equal(revisionBefore, world.Revision);

        var regionRevisions = typeof(WorldGrid)
                                  .GetField("_regionRevisions", BindingFlags.Instance | BindingFlags.NonPublic)
                                  ?.GetValue(world) as long[]
                              ?? throw new InvalidOperationException(
                                  "WorldGrid must keep its per-region revisions in a long[] array.");
        regionRevisions[regionId] = regionRevisionBefore;
        Assert.Equal(regionRevisionBefore, world.GetRegionRevision(regionId));
    }

    private static PerceptionRules CreateRules() => new(
        new Dictionary<ushort, PerceptionMaterialRule>
        {
            [0] = new(Solid: false, Liquid: false, Perceivable: false, TagMask: 0),
            [1] = new(Solid: true, Liquid: false, Perceivable: true, TagMask: 0),
            [2] = new(Solid: true, Liquid: false, Perceivable: true, TagMask: 2),
        },
        airMaterialId: 0);
}
