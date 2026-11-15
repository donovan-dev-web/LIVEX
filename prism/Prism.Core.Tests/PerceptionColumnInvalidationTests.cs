using System.Text.Json;
using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Core.Tests;

/// <summary>
/// PRISM-119: a write in a region a window covers used to rebuild all 1 089 columns, even though a
/// column's contribution only depends on that column and its four cardinal neighbours. The window
/// is now patched column by column, and the reference is still a <see cref="PerceptionSystem"/>
/// built with <c>slideWindow: false</c>, which rebuilds on every step: the frames must be equal
/// byte for byte, otherwise the patch is a second definition of what an agent sees.
/// </summary>
public sealed class PerceptionColumnInvalidationTests
{
    private static readonly PerceptionSettings Settings =
        new(NearRadius: 3, MidRadiusRegions: 0, MaximumObservedPerAgent: 256);

    [Fact]
    public void APatchedWindowEmitsTheSameChangesAsAFullRebuildOverAWalkWithWrites()
    {
        var (world, movement) = CreateWorld();
        var rules = CreateRules();
        var groundItems = new GroundItemStore();
        var patched = new PerceptionSystem(slideWindow: true);
        var rebuilt = new PerceptionSystem(slideWindow: false);

        var path = WalkPath();
        for (var tick = 0; tick < path.Length; tick++)
        {
            var (x, y) = path[tick];

            // Writes in the agent's neighbourhood, one or two cells off the path so the walk stays
            // valid: raising a column changes what its neighbours hide, which is exactly the
            // second-order dependency the five-column bump has to cover.
            WriteNear(path, tick, world);

            var registry = Incarnate(movement, x, y);
            Assert.True(registry.TryGet(1, out var observer));

            var patchedFrame = patched.Observe(observer!, world, registry, groundItems, rules, Settings, tick: tick);
            var rebuiltFrame = rebuilt.Observe(observer!, world, registry, groundItems, rules, Settings, tick: tick);

            var rebuiltJson = JsonSerializer.Serialize(rebuiltFrame.Changes);
            var patchedJson = JsonSerializer.Serialize(patchedFrame.Changes);
            Assert.True(
                rebuiltJson == patchedJson,
                $"tick {tick} at ({x},{y})\nrebuilt: {rebuiltJson}\npatched: {patchedJson}");
        }

        // The walk must actually exercise the patch, otherwise the comparison is vacuous.
        Assert.True(patched.BlockWindowPatches > 0, "the walk never patched the window");
        Assert.Equal(1, patched.BlockWindowBuilds);
        Assert.True(rebuilt.BlockWindowBuilds > 1, "the reference never rebuilt");
    }

    [Fact]
    public void AWriteInsideTheWindowPatchesInsteadOfRebuildingEverything()
    {
        var (world, movement) = CreateWorld();
        var rules = CreateRules();
        var groundItems = new GroundItemStore();
        var system = new PerceptionSystem();

        var registry = Incarnate(movement, 8, 10);
        Assert.True(registry.TryGet(1, out var observer));
        system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 0);
        Assert.Equal(1, system.BlockWindowBuilds);

        // One column of the disc is raised: only it and its neighbours must be re-read.
        world.SetSurfaceColumn(7, 10, new SurfaceColumn(6, 2, 0, 0));

        var frame = system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 1);

        Assert.Equal(1, system.BlockWindowBuilds);
        Assert.Equal(1, system.BlockWindowPatches);
        Assert.Equal(0, system.BlockWindowSlides);
        Assert.Equal(0, system.BlockWindowMoveRebuilds);
        Assert.Contains(frame.Changes, change =>
            change.Entry.Kind == PerceptionEntityKind.Block &&
            change.Entry.X == 7 && change.Entry.Y == 10 && change.Entry.MaterialId == 2);
    }

    [Fact]
    public void RemovingABlockerOnANeighbourColumnRevealsABlockTheWindowAlreadyKnew()
    {
        var (world, movement) = CreateWorld();
        var rules = CreateRules();
        var groundItems = new GroundItemStore();
        var system = new PerceptionSystem();

        // A block floating one cell above the ground is buried behind four solid sides and a
        // solid top, so the first observation must not see it.
        world.SetBlockOverride(new BlockCoordinate(6, 10, 6), new BlockOverride(2, 0, IsAir: false));
        world.SetBlockOverride(new BlockCoordinate(5, 10, 6), new BlockOverride(2, 0, IsAir: false));
        world.SetBlockOverride(new BlockCoordinate(6, 9, 6), new BlockOverride(2, 0, IsAir: false));
        world.SetBlockOverride(new BlockCoordinate(6, 11, 6), new BlockOverride(2, 0, IsAir: false));
        world.SetBlockOverride(new BlockCoordinate(6, 10, 7), new BlockOverride(2, 0, IsAir: false));
        world.SetBlockOverride(new BlockCoordinate(7, 10, 6), new BlockOverride(2, 0, IsAir: false));

        var registry = Incarnate(movement, 8, 10);
        Assert.True(registry.TryGet(1, out var observer));
        var first = system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 0);
        Assert.DoesNotContain(first.Changes, change =>
            change.Entry.Kind == PerceptionEntityKind.Block &&
            change.Entry.X == 6 && change.Entry.Y == 10 && change.Entry.Z == 6);

        // The east blocker is two columns away from the agent's own cell: the column it hides is
        // (6,10), whose own counter the write bumps because it is a cardinal neighbour.
        Assert.True(world.RemoveBlockOverride(new BlockCoordinate(7, 10, 6)));

        var second = system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 1);

        Assert.Equal(1, system.BlockWindowPatches);
        Assert.Equal(1, system.BlockWindowBuilds);
        Assert.Contains(second.Changes, change =>
            change.Change == PerceptionChangeKind.Appeared &&
            change.Entry.Kind == PerceptionEntityKind.Block &&
            change.Entry.X == 6 && change.Entry.Y == 10 && change.Entry.Z == 6);
    }

    [Fact]
    public void AWriteInACoveredRegionButOutsideTheDiscPatchesNothing()
    {
        var (world, movement) = CreateWorld();
        var rules = CreateRules();
        var groundItems = new GroundItemStore();
        var system = new PerceptionSystem();

        var registry = Incarnate(movement, 8, 10);
        Assert.True(registry.TryGet(1, out var observer));
        system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 0);

        // (5,7) is inside a region the window covers (the residency rim that decides exposure)
        // but its five-column neighbourhood is outside the disc, so nothing the agent sees moved.
        world.SetSurfaceColumn(5, 7, new SurfaceColumn(6, 2, 0, 0));

        var frame = system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 1);

        Assert.Equal(1, system.BlockWindowBuilds);
        Assert.Equal(1, system.BlockWindowPatches);
        Assert.DoesNotContain(frame.Changes, change =>
            change.Entry.Kind == PerceptionEntityKind.Block && change.Entry.X == 5 && change.Entry.Y == 7);
    }

    [Fact]
    public void AFloodOfChangedColumnsIsStillPatchedAndStillMatchesARebuild()
    {
        var (world, movement) = CreateWorld();
        var rules = CreateRules();
        var groundItems = new GroundItemStore();
        var system = new PerceptionSystem();
        var reference = new PerceptionSystem(slideWindow: false);

        var registry = Incarnate(movement, 8, 10);
        Assert.True(registry.TryGet(1, out var observer));
        system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 0);
        reference.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 0);

        // Every other column of the disc changes: re-reading a column costs the same in a patch
        // as in a rebuild, so the patch stays the cheaper path even at this density.
        for (var x = 5; x <= 11; x += 2)
        {
            for (var y = 7; y <= 13; y += 2)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(6, 2, 0, 0));
            }
        }

        var frame = system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 1);
        var referenceFrame = reference.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 1);

        Assert.Equal(1, system.BlockWindowBuilds);
        Assert.Equal(1, system.BlockWindowPatches);
        Assert.Equal(
            JsonSerializer.Serialize(referenceFrame.Changes),
            JsonSerializer.Serialize(frame.Changes));
        Assert.Contains(frame.Changes, change =>
            change.Entry.Kind == PerceptionEntityKind.Block &&
            change.Entry.X == 7 && change.Entry.Y == 9 && change.Entry.MaterialId == 2);
    }

    [Fact]
    public void ThePatchStaysSilentInVerificationMode()
    {
        var (world, movement) = CreateWorld();
        var rules = CreateRules();
        var groundItems = new GroundItemStore();
        var system = new PerceptionSystem(verifyCaches: true);

        var path = WalkPath();
        var emitted = 0;
        for (var tick = 0; tick < path.Length; tick++)
        {
            var (x, y) = path[tick];
            WriteNear(path, tick, world);

            var registry = Incarnate(movement, x, y);
            Assert.True(registry.TryGet(1, out var observer));
            emitted += system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: tick)
                .Changes.Count;
        }

        // PRISM-108's mode recomputes a patched window from the world and compares it entry by
        // entry: no exception means the patch is the world's content, not an approximation of it.
        Assert.True(system.BlockWindowPatches > 0);
        Assert.True(emitted > 0);
    }

    [Fact]
    public void AWriteMarksItsColumnAndItsCardinalNeighboursOnly()
    {
        var world = new WorldGrid(width: 8, height: 8, regionSize: 4, verticalSize: 16);
        world.SetSurfaceColumn(3, 3, new SurfaceColumn(4, 1, 0, 0));

        int Before(int x, int y) => world.GetColumnRevision(x, y);
        var far = Before(0, 0);
        var diagonal = Before(4, 4);

        world.SetSurfaceColumn(3, 3, new SurfaceColumn(5, 1, 0, 0));

        Assert.True(Before(3, 3) > 0);
        Assert.True(Before(2, 3) > 0);
        Assert.True(Before(4, 3) > 0);
        Assert.True(Before(3, 2) > 0);
        Assert.True(Before(3, 4) > 0);
        Assert.Equal(diagonal, Before(4, 4));
        Assert.Equal(far, Before(0, 0));

        // Rows are exposed for a bulk compare, including the empty case outside the world.
        Assert.Equal(8, world.GetColumnRevisionRow(3).Length);
        Assert.True(world.GetColumnRevisionRow(8).IsEmpty);
        Assert.Equal(0, world.GetColumnRevision(-1, 3));
    }

    private static void WriteNear((int X, int Y)[] path, int tick, WorldGrid world)
    {
        // Two cells off the walk, so the raised column never lands under the agent (a surface
        // above the feet would make the incarnation invalid): close enough to be inside the disc
        // of most path cells, far enough never to be one of them.
        if (tick == 0)
        {
            return;
        }

        var writeX = 8 + (tick % 11);
        const int writeY = 8;
        world.SetSurfaceColumn(writeX, writeY, new SurfaceColumn((byte)(5 + tick % 3), 2, 0, 0));
    }

    private static (int X, int Y)[] WalkPath()
    {
        var path = new List<(int X, int Y)>();
        for (var x = 8; x <= 15; x++)
        {
            path.Add((x, 10));
        }

        for (var y = 11; y <= 14; y++)
        {
            path.Add((15, y));
        }

        var cursorX = 15;
        var cursorY = 14;
        for (var step = 0; step < 5; step++)
        {
            cursorX--;
            cursorY++;
            path.Add((cursorX, cursorY));
        }

        for (var step = 0; step < 5; step++)
        {
            cursorX--;
            cursorY--;
            path.Add((cursorX, cursorY));
        }

        return path.ToArray();
    }

    private static (WorldGrid World, MovementGraph Movement) CreateWorld()
    {
        var world = new WorldGrid(width: 32, height: 32, regionSize: 4, verticalSize: 16);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(4, 1, 0, 0));
            }
        }

        world.SetBlockOverride(new BlockCoordinate(9, 10, 5), new BlockOverride(2, 0, IsAir: false));
        world.SetBlockOverride(new BlockCoordinate(12, 10, 6), new BlockOverride(2, 0, IsAir: false));
        world.SetBlockOverride(new BlockCoordinate(14, 12, 5), new BlockOverride(2, 0, IsAir: false));

        var movement = new MovementGraph(world, new MovementRules([1], []));
        return (world, movement);
    }

    private static AgentRegistry Incarnate(MovementGraph movement, int x, int y)
    {
        var registry = new AgentRegistry();
        registry.Incarnate(1, AgentPosition.AtCell(x, y, 5), new AgentBodyProfile(), movement);
        return registry;
    }

    private static PerceptionRules CreateRules() => new(
        new Dictionary<ushort, PerceptionMaterialRule>
        {
            [0] = new(Solid: false, Liquid: false, Perceivable: false, TagMask: 0),
            [1] = new(Solid: true, Liquid: false, Perceivable: true, TagMask: 1),
            [2] = new(Solid: true, Liquid: false, Perceivable: true, TagMask: 2),
        },
        airMaterialId: 0);
}
