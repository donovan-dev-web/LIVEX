using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class PerceptionSystemTests
{
    [Fact]
    public void PerceptionDoesNotRevealBuriedBlocksAndSummarizesExposedTags()
    {
        var world = CreateFlatWorld(3, 3);
        var buried = new BlockCoordinate(1, 1, 2);
        world.SetBlockOverride(buried, new BlockOverride(2, 0, false));
        var registry = new AgentRegistry();
        var observer = Incarnate(registry, world, 1, 1, 1);
        var system = new PerceptionSystem();

        var frame = system.Observe(
            observer,
            world,
            registry,
            new GroundItemStore(),
            CreateRules(),
            new PerceptionSettings(NearRadius: 0, MidRadiusRegions: 0, MaximumObservedPerAgent: 32));

        Assert.DoesNotContain(
            frame.Changes,
            change => change.Entry.Kind == PerceptionEntityKind.Block &&
                      change.Entry.X == buried.X &&
                      change.Entry.Y == buried.Y &&
                      change.Entry.Z == buried.Z);
        Assert.DoesNotContain(
            frame.Changes,
            change => change.Entry.Kind == PerceptionEntityKind.RegionSummary &&
                      change.Entry.TagMask == 2);
    }

    [Fact]
    public void PerceptionReportsAppearChangedAndDisappearExactlyOnce()
    {
        var world = CreateFlatWorld(3, 1);
        var registry = new AgentRegistry();
        var observer = Incarnate(registry, world, 0, 0, 1);
        var system = new PerceptionSystem();
        var groundItems = new GroundItemStore();
        var settings = new PerceptionSettings(NearRadius: 1, MidRadiusRegions: 0, MaximumObservedPerAgent: 32);

        var appeared = system.Observe(observer, world, registry, groundItems, CreateRules(), settings);
        Assert.Contains(appeared.Changes, change =>
            change.Change == PerceptionChangeKind.Appeared &&
            change.Entry.Kind == PerceptionEntityKind.Block);
        Assert.Empty(system.Observe(observer, world, registry, groundItems, CreateRules(), settings).Changes);

        var target = new BlockCoordinate(1, 0, 4);
        world.SetBlockOverride(target, new BlockOverride(2, 3, false));
        var changed = system.Observe(observer, world, registry, groundItems, CreateRules(), settings);
        Assert.Contains(changed.Changes, change =>
            change.Change == PerceptionChangeKind.Changed &&
            change.Entry.Kind == PerceptionEntityKind.Block &&
            change.Entry.X == target.X &&
            change.Entry.State == 3);
        Assert.Contains(changed.Changes, change =>
            change.Change == PerceptionChangeKind.Appeared &&
            change.Entry.Kind == PerceptionEntityKind.RegionSummary &&
            change.Entry.TagMask == 2);

        world.SetBlockOverride(target, new BlockOverride(0, 0, true));
        var disappeared = system.Observe(observer, world, registry, groundItems, CreateRules(), settings);
        Assert.Contains(disappeared.Changes, change =>
            change.Change == PerceptionChangeKind.Disappeared &&
            change.Entry.Kind == PerceptionEntityKind.Block &&
            change.Entry.X == target.X);
        Assert.Contains(disappeared.Changes, change =>
            change.Change == PerceptionChangeKind.Disappeared &&
            change.Entry.Kind == PerceptionEntityKind.RegionSummary);
        Assert.DoesNotContain(
            system.Observe(observer, world, registry, groundItems, CreateRules(), settings).Changes,
            change => change.Change == PerceptionChangeKind.Disappeared);
    }

    [Fact]
    public void PerceptionHonorsObservationCapAndPrioritizesAgents()
    {
        var world = CreateFlatWorld(5, 5);
        var registry = new AgentRegistry();
        var observer = Incarnate(registry, world, 2, 2, 1);
        _ = Incarnate(registry, world, 3, 2, 2);
        var system = new PerceptionSystem();
        var settings = new PerceptionSettings(NearRadius: 2, MidRadiusRegions: 0, MaximumObservedPerAgent: 1);

        var frame = system.Observe(
            observer, world, registry, new GroundItemStore(), CreateRules(), settings);

        Assert.Single(frame.Changes);
        Assert.Equal(PerceptionEntityKind.Agent, frame.Changes[0].Entry.Kind);
        Assert.Equal(PerceptionChangeKind.Appeared, frame.Changes[0].Change);
        Assert.Single(frame.Self.Inventory);
    }

    [Fact]
    public void PerceptionDoesNotSeeThroughWaterOrAClosedSolidBlock()
    {
        var world = CreateFlatWorld(3, 3);
        var target = new BlockCoordinate(1, 1, 4);
        world.SetBlockOverride(
            new BlockCoordinate(1, 1, 5),
            new BlockOverride(1, 0, false));
        world.SetSurfaceColumn(2, 1, new SurfaceColumn(4, 3, 0, 0));
        var registry = new AgentRegistry();
        var observer = Incarnate(registry, world, 0, 0, 1);
        var rules = new PerceptionRules(
            new Dictionary<ushort, PerceptionMaterialRule>
            {
                [0] = new(Solid: false, Liquid: false, Perceivable: false, TagMask: 0),
                [1] = new(Solid: true, Liquid: false, Perceivable: true, TagMask: 0),
                [3] = new(Solid: false, Liquid: true, Perceivable: false, TagMask: 0),
            },
            airMaterialId: 0);

        var frame = new PerceptionSystem().Observe(
            observer,
            world,
            registry,
            new GroundItemStore(),
            rules,
            new PerceptionSettings(NearRadius: 2, MidRadiusRegions: 0, MaximumObservedPerAgent: 64));

        Assert.DoesNotContain(
            frame.Changes,
            change => change.Entry.Kind == PerceptionEntityKind.Block &&
                      change.Entry.X == target.X &&
                      change.Entry.Y == target.Y &&
                      change.Entry.Z == target.Z);
    }

    private static AgentBody Incarnate(AgentRegistry registry, WorldGrid world, int x, int y, ulong id)
    {
        var movement = new MovementGraph(
            world,
            new MovementRules(new HashSet<ushort> { 1, 2 }, new HashSet<ushort>()));
        var body = registry.Incarnate(
            id,
            AgentPosition.AtCell(x, y, 5),
            new AgentBodyProfile(),
            movement);
        Assert.True(body.Inventory.TryAdd(10, 1, 64, out var remaining));
        Assert.Equal(0, remaining);
        return body;
    }

    private static WorldGrid CreateFlatWorld(int width, int height)
    {
        var world = new WorldGrid(width, height, regionSize: 2, verticalSize: 16);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(4, 1, 0, 0));
            }
        }

        return world;
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
