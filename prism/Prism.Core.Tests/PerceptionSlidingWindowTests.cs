using System.Text.Json;
using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Core.Tests;

/// <summary>
/// PRISM-105: a one-cell step whose covered regions were not written slides the cached block
/// window instead of rescanning it. The optimisation is only acceptable if it is a *speed* change:
/// every change it emits must be the one the full rebuild would have emitted, byte for byte.
/// <para>
/// The reference is a <see cref="PerceptionSystem"/> built with <c>slideWindow: false</c>, which
/// rebuilds on every step exactly as before. Both systems are fed the same world and the same
/// agent position each tick, so any difference between their frames is the slide's fault.
/// </para>
/// </summary>
public sealed class PerceptionSlidingWindowTests
{
    private static readonly PerceptionSettings Settings =
        new(NearRadius: 3, MidRadiusRegions: 0, MaximumObservedPerAgent: 256);

    [Fact]
    public void ASlidWindowEmitsTheSameChangesAsAFullRebuildOverAWalk()
    {
        var (world, movement) = CreateWorld();
        var rules = CreateRules();
        var groundItems = new GroundItemStore();
        var slid = new PerceptionSystem(slideWindow: true);
        var rebuilt = new PerceptionSystem(slideWindow: false);

        var path = WalkPath();
        for (var tick = 0; tick < path.Length; tick++)
        {
            var (x, y) = path[tick];
            var registry = Incarnate(movement, x, y);
            Assert.True(registry.TryGet(1, out var observer));

            var slidFrame = slid.Observe(observer!, world, registry, groundItems, rules, Settings, tick: tick);
            var rebuiltFrame = rebuilt.Observe(observer!, world, registry, groundItems, rules, Settings, tick: tick);

            var rebuiltJson = JsonSerializer.Serialize(rebuiltFrame.Changes);
            var slidJson = JsonSerializer.Serialize(slidFrame.Changes);
            Assert.True(
                rebuiltJson == slidJson,
                $"tick {tick} at ({x},{y})\nrebuilt: {rebuiltJson}\nslid:    {slidJson}");
        }

        // The walk must actually exercise the slide, otherwise the comparison above is vacuous.
        Assert.True(slid.BlockWindowSlides > 0, "the walk never slid the window");
        Assert.Equal(1, slid.BlockWindowBuilds);
        Assert.True(rebuilt.BlockWindowBuilds > 1, "the reference never rebuilt");
    }

    [Fact]
    public void AOneCellStepWithUnchangedRegionsSlidesTheWindow()
    {
        var (world, movement) = CreateWorld();
        var rules = CreateRules();
        var groundItems = new GroundItemStore();
        var system = new PerceptionSystem();

        var registry = Incarnate(movement, 8, 10);
        Assert.True(registry.TryGet(1, out var observer));
        system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 0);
        Assert.Equal(1, system.BlockWindowBuilds);

        var stepped = Incarnate(movement, 9, 10);
        Assert.True(stepped.TryGet(1, out var moved));
        system.Observe(moved!, world, stepped, groundItems, rules, Settings, tick: 1);

        Assert.Equal(1, system.BlockWindowBuilds);
        Assert.Equal(1, system.BlockWindowSlides);
        Assert.Equal(0, system.BlockWindowMoveRebuilds);
    }

    [Fact]
    public void AWriteInACoveredRegionPatchesTheWrittenColumnInsteadOfRebuilding()
    {
        var (world, movement) = CreateWorld();
        var rules = CreateRules();
        var groundItems = new GroundItemStore();
        var system = new PerceptionSystem();

        var registry = Incarnate(movement, 8, 10);
        Assert.True(registry.TryGet(1, out var observer));
        system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 0);

        // The window covers cells 5..11 around (8,10): a column in that disc changes what the
        // agent sees. Since PRISM-119 it is re-read column by column rather than thrown away: the
        // step cannot slide (the regions moved) but it does not need a full rebuild either.
        // (7,10) is written rather than the agent's destination, which stays standable.
        world.SetSurfaceColumn(7, 10, new SurfaceColumn(6, 2, 0, 0));

        var stepped = Incarnate(movement, 9, 10);
        Assert.True(stepped.TryGet(1, out var moved));
        var frame = system.Observe(moved!, world, stepped, groundItems, rules, Settings, tick: 1);

        Assert.Equal(0, system.BlockWindowSlides);
        Assert.Equal(1, system.BlockWindowPatches);
        Assert.Equal(0, system.BlockWindowMoveRebuilds);
        Assert.Equal(1, system.BlockWindowBuilds);
        Assert.Contains(frame.Changes, change =>
            change.Entry.Kind == PerceptionEntityKind.Block &&
            change.Entry.X == 7 && change.Entry.Y == 10 && change.Entry.MaterialId == 2);
    }

    [Fact]
    public void ADiagonalStepSlidesTheWindow()
    {
        var (world, movement) = CreateWorld();
        var rules = CreateRules();
        var groundItems = new GroundItemStore();
        var system = new PerceptionSystem();

        var registry = Incarnate(movement, 8, 10);
        Assert.True(registry.TryGet(1, out var observer));
        system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 0);

        var stepped = Incarnate(movement, 9, 11);
        Assert.True(stepped.TryGet(1, out var moved));
        system.Observe(moved!, world, stepped, groundItems, rules, Settings, tick: 1);

        Assert.Equal(1, system.BlockWindowSlides);
        Assert.Equal(0, system.BlockWindowMoveRebuilds);
        Assert.Equal(1, system.BlockWindowBuilds);
    }

    [Fact]
    public void AJumpWiderThanOneCellRebuildsTheWindow()
    {
        var (world, movement) = CreateWorld();
        var rules = CreateRules();
        var groundItems = new GroundItemStore();
        var system = new PerceptionSystem();

        var registry = Incarnate(movement, 8, 10);
        Assert.True(registry.TryGet(1, out var observer));
        system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: 0);

        // A teleport (incarnation elsewhere) has no band to read: the whole window is rebuilt.
        var stepped = Incarnate(movement, 10, 10);
        Assert.True(stepped.TryGet(1, out var moved));
        system.Observe(moved!, world, stepped, groundItems, rules, Settings, tick: 1);

        Assert.Equal(0, system.BlockWindowSlides);
        Assert.Equal(1, system.BlockWindowMoveRebuilds);
        Assert.Equal(2, system.BlockWindowBuilds);
    }

    [Fact]
    public void TheSlideStaysSilentInVerificationMode()
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
            var registry = Incarnate(movement, x, y);
            Assert.True(registry.TryGet(1, out var observer));
            emitted += system.Observe(observer!, world, registry, groundItems, rules, Settings, tick: tick)
                .Changes.Count;
        }

        // PRISM-108's mode recomputes the window without its memo on every hit, slide included:
        // no exception means the slid window is exactly the world's.
        Assert.True(system.BlockWindowSlides > 0);
        Assert.True(emitted > 0);
    }

    [Fact]
    public void TheColumnIndexOfTheGridMatchesTheRegionIndex()
    {
        var world = new WorldGrid(width: 8, height: 8, regionSize: 4, verticalSize: 16);
        world.SetSurfaceColumn(2, 2, new SurfaceColumn(3, 1, 0, 0));
        world.SetBlockOverride(new BlockCoordinate(2, 2, 9), new BlockOverride(2, 0, IsAir: false));
        world.SetBlockOverride(new BlockCoordinate(2, 2, 5), new BlockOverride(2, 0, IsAir: false));
        world.SetBlockOverride(new BlockCoordinate(2, 2, 7), new BlockOverride(2, 0, IsAir: false));
        world.SetBlockOverride(new BlockCoordinate(3, 2, 5), new BlockOverride(2, 0, IsAir: false));

        Assert.Equal(3, world.GetColumnOverrideCount(2, 2));
        Assert.Equal(5, world.GetColumnOverrideAt(2, 2, 0).Z);
        Assert.Equal(7, world.GetColumnOverrideAt(2, 2, 1).Z);
        Assert.Equal(9, world.GetColumnOverrideAt(2, 2, 2).Z);

        // An update of an existing coordinate keeps the column indexed once.
        world.SetBlockOverride(new BlockCoordinate(2, 2, 7), new BlockOverride(3, 0, IsAir: false));
        Assert.Equal(3, world.GetColumnOverrideCount(2, 2));

        // A removal keeps the Z order of what remains.
        Assert.True(world.RemoveBlockOverride(new BlockCoordinate(2, 2, 5)));
        Assert.Equal(2, world.GetColumnOverrideCount(2, 2));
        Assert.Equal(7, world.GetColumnOverrideAt(2, 2, 0).Z);
        Assert.Equal(9, world.GetColumnOverrideAt(2, 2, 1).Z);

        // The column index must describe exactly what the region index holds for the column.
        Assert.True(world.TryGetRegionId(2, 2, out var regionId));
        var fromRegion = world
            .EnumerateRegionOverrides(regionId)
            .Select(entry => entry.Key)
            .Where(coordinate => coordinate.X == 2 && coordinate.Y == 2)
            .ToArray();
        var fromColumn = Enumerable.Range(0, world.GetColumnOverrideCount(2, 2))
            .Select(index => world.GetColumnOverrideAt(2, 2, index))
            .ToArray();
        Assert.Equal(fromRegion, fromColumn);
    }

    private static (int X, int Y)[] WalkPath()
    {
        var path = new List<(int X, int Y)>();
        for (var x = 8; x <= 18; x++)
        {
            path.Add((x, 10));
        }

        for (var y = 11; y <= 15; y++)
        {
            path.Add((18, y));
        }

        // Diagonals: the same slide must hold when both axes move, including rows that just
        // entered the disc at the poles of the circle.
        var cursorX = 18;
        var cursorY = 15;
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

        // Trees and a placed block: the slide must read the entering column's overrides, not only
        // its surface, or a block would enter the disc without ever being perceived.
        world.SetBlockOverride(new BlockCoordinate(9, 10, 5), new BlockOverride(2, 0, IsAir: false));
        world.SetBlockOverride(new BlockCoordinate(12, 10, 6), new BlockOverride(2, 0, IsAir: false));
        world.SetBlockOverride(new BlockCoordinate(16, 10, 5), new BlockOverride(2, 0, IsAir: false));
        world.SetBlockOverride(new BlockCoordinate(18, 12, 5), new BlockOverride(2, 0, IsAir: false));

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
