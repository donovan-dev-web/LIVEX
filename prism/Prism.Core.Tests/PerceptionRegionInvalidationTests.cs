using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Core.Tests;

/// <summary>
/// PRISM-109: the perception caches are keyed by the revision of the regions they read, never by
/// the global grid revision. These tests pin the exact dependency down — a write in a region the
/// cache read must invalidate it, a write one region further away must not — because a dependency
/// that is merely "big enough" brings back the defect the ticket exists to remove, and one that is
/// too narrow lies silently (which is what <c>PRISM_VERIFY_CACHES=1</c> is for).
/// </summary>
public sealed class PerceptionRegionInvalidationTests
{
    private static readonly PerceptionSettings WindowSettings =
        new(NearRadius: 3, MidRadiusRegions: 0, MaximumObservedPerAgent: 64);

    private static readonly PerceptionSettings MaskSettings =
        new(NearRadius: 0, MidRadiusRegions: 0, MaximumObservedPerAgent: 64);

    [Fact]
    public void AWriteInAnUnrelatedRegionDoesNotRebuildTheBlockWindow()
    {
        var (world, registry, observer) = CreateScene();
        var system = new PerceptionSystem();
        var groundItems = new GroundItemStore();
        var rules = CreateRules();

        system.Observe(observer, world, registry, groundItems, rules, WindowSettings, tick: 0);
        Assert.Equal(1, system.BlockWindowBuilds);

        // The observer stands at (10,10) with radius 3: the window reads cells 7..13 and the
        // one-cell rim 6..14, i.e. regions 1..3. A write at (22,22) is in region 5 and cannot
        // change a single cell that was read, so the window must be reused as is.
        world.SetSurfaceColumn(22, 22, new SurfaceColumn(6, 1, 0, 0));
        system.Observe(observer, world, registry, groundItems, rules, WindowSettings, tick: 1);
        Assert.Equal(1, system.BlockWindowBuilds);

        // A write on a cell the window did read is re-read column by column (PRISM-119): the
        // window is neither reused nor rebuilt whole, and the change reaches the agent.
        world.SetSurfaceColumn(10, 10, new SurfaceColumn(6, 1, 0, 0));
        var frame = system.Observe(observer, world, registry, groundItems, rules, WindowSettings, tick: 2);
        Assert.Equal(1, system.BlockWindowBuilds);
        Assert.Equal(1, system.BlockWindowPatches);
        Assert.Contains(frame.Changes, change =>
            change.Entry.Kind == PerceptionEntityKind.Block &&
            change.Entry.X == 10 && change.Entry.Y == 10 && change.Entry.MaterialId == 1);
    }

    [Fact]
    public void TheWindowDependencyStopsAfterTheOneCellRimItReads()
    {
        var (world, registry, observer) = CreateScene();
        var system = new PerceptionSystem();
        var groundItems = new GroundItemStore();
        var rules = CreateRules();

        system.Observe(observer, world, registry, groundItems, rules, WindowSettings, tick: 0);
        Assert.Equal(1, system.BlockWindowBuilds);

        // (15,10) is two columns past the window, which ends at 13: no column of the disc depends
        // on it, so the write costs nothing beyond a patch that finds no dirty column. The region
        // key alone would have rebuilt the window — that is the conservatism PRISM-119 narrows.
        world.SetSurfaceColumn(15, 10, new SurfaceColumn(6, 1, 0, 0));
        system.Observe(observer, world, registry, groundItems, rules, WindowSettings, tick: 1);
        Assert.Equal(1, system.BlockWindowBuilds);
        Assert.Equal(1, system.BlockWindowPatches);

        // (14,10) is the one-cell rim the exposure test reads: column 13 depends on it, so the
        // window is re-read — again without a rebuild.
        world.SetSurfaceColumn(14, 10, new SurfaceColumn(6, 1, 0, 0));
        system.Observe(observer, world, registry, groundItems, rules, WindowSettings, tick: 2);
        Assert.Equal(1, system.BlockWindowBuilds);
        Assert.Equal(2, system.BlockWindowPatches);
        Assert.Equal(0, system.BlockWindowMoveRebuilds);
    }

    [Fact]
    public void ARegionMaskIsRebuiltOnlyWhenARegionItReadsIsWritten()
    {
        var (world, registry, observer) = CreateScene();
        var system = new PerceptionSystem();
        var groundItems = new GroundItemStore();
        var rules = CreateRules();

        // MidRadiusRegions 0: the observer's own region is the only mask scanned.
        system.Observe(observer, world, registry, groundItems, rules, MaskSettings, tick: 0);
        Assert.Equal(1, system.RegionMaskRebuilds);

        // Region (5,5), unrelated to region (2,2): the mask is reused.
        world.SetSurfaceColumn(22, 22, new SurfaceColumn(6, 1, 0, 0));
        system.Observe(observer, world, registry, groundItems, rules, MaskSettings, tick: 1);
        Assert.Equal(1, system.RegionMaskRebuilds);

        // Region (3,3), the diagonal neighbour of (2,2): the mask never reads it, because the
        // exposure test only asks the four side-neighbours.
        world.SetSurfaceColumn(13, 13, new SurfaceColumn(6, 1, 0, 0));
        system.Observe(observer, world, registry, groundItems, rules, MaskSettings, tick: 2);
        Assert.Equal(1, system.RegionMaskRebuilds);

        // Region (3,2), the east neighbour: a block on the last column of the region is exposed
        // by open air in that neighbour, so its mask must be recomputed.
        world.SetSurfaceColumn(13, 10, new SurfaceColumn(6, 1, 0, 0));
        system.Observe(observer, world, registry, groundItems, rules, MaskSettings, tick: 3);
        Assert.Equal(2, system.RegionMaskRebuilds);
    }

    [Fact]
    public void VerificationStaysSilentAcrossUnrelatedWrites()
    {
        var (world, registry, observer) = CreateScene();
        var system = new PerceptionSystem(verifyCaches: true);
        var groundItems = new GroundItemStore();
        var rules = CreateRules();

        system.Observe(observer, world, registry, groundItems, rules, WindowSettings, tick: 0);
        world.SetSurfaceColumn(22, 22, new SurfaceColumn(6, 1, 0, 0));
        system.Observe(observer, world, registry, groundItems, rules, WindowSettings, tick: 1);
        world.SetSurfaceColumn(9, 9, new SurfaceColumn(6, 1, 0, 0));
        var frame = system.Observe(
            observer, world, registry, groundItems, rules, WindowSettings, tick: 2);

        // The verification recomputed the window on both hits and found the same content the
        // cache held; a change inside the window still reaches the agent normally.
        Assert.Contains(frame.Changes, change =>
            change.Entry.Kind == PerceptionEntityKind.Block &&
            change.Entry.X == 9 && change.Entry.Y == 9);
    }

    private static (WorldGrid World, AgentRegistry Registry, AgentBody Observer) CreateScene()
    {
        var world = new WorldGrid(width: 24, height: 24, regionSize: 4, verticalSize: 16);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(4, 1, 0, 0));
            }
        }

        var registry = new AgentRegistry();
        var movement = new MovementGraph(world, new MovementRules([1], []));
        var observer = registry.Incarnate(
            1,
            AgentPosition.AtCell(10, 10, 5),
            new AgentBodyProfile(),
            movement);
        return (world, registry, observer);
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
