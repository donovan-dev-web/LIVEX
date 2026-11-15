using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Core.Tests;

/// <summary>
/// L'index spatial (PRISM-141) ne doit changer que le coût de la recherche du voisinage, jamais
/// les entrées observées. Ces tests comparent, agent par agent et tick par tick, la perception
/// indexée à une perception sans index — la seconde est la référence exhaustive.
/// </summary>
public sealed class PerceptionSpatialIndexTests
{
    [Fact]
    public void IndexedObservationEmitsTheSameChangesAsAFullScanAcrossTicksAndPopulationChanges()
    {
        var world = CreateFlatWorld(64, 64);
        var registry = new AgentRegistry();
        var groundItems = new GroundItemStore();
        for (var index = 0; index < 60; index++)
        {
            Incarnate(registry, world, index * 7 % 64, index * 13 % 64, (ulong)(index + 1));
        }

        for (var index = 0; index < 60; index++)
        {
            groundItems.AddStack(10, 1, 64, new BlockCoordinate(index * 11 % 64, index * 5 % 64, 5));
        }

        var indexed = new PerceptionSystem();
        var reference = new PerceptionSystem();
        var rules = CreateRules();
        var settings = new PerceptionSettings(
            NearRadius: 12,
            MidRadiusRegions: 1,
            MaximumObservedPerAgent: 4096);
        var spatialIndex = new PerceptionSpatialIndex();

        for (var tick = 0; tick < 3; tick++)
        {
            spatialIndex.Rebuild(registry.Agents.ToArray(), groundItems);
            AssertAgreesWithFullScan(
                registry, world, groundItems, rules, settings, indexed, reference, spatialIndex, tick);
        }

        // Un agent part (son inventaire tombe au sol) et un autre arrive : l'index doit suivre la
        // population, pas seulement les positions.
        Assert.True(registry.Retire(5, groundItems));
        Incarnate(registry, world, 40, 41, 500);
        spatialIndex.Rebuild(registry.Agents.ToArray(), groundItems);
        AssertAgreesWithFullScan(
            registry, world, groundItems, rules, settings, indexed, reference, spatialIndex, tick: 3);

        // Sans seaux distincts, le test ne prouverait rien : l'index a bien réparti la population.
        Assert.True(spatialIndex.AgentBucketCount > 1);
        Assert.True(spatialIndex.ItemBucketCount > 1);
    }

    [Fact]
    public void NeighboursAcrossABucketBoundaryAreObserved()
    {
        var world = CreateFlatWorld(64, 64);
        var registry = new AgentRegistry();
        // 15 et 16 tombent dans deux seaux différents (frontière des 16 cases) : un index qui
        // n'élargirait pas sa fenêtre de seaux manquerait ce voisin.
        var observer = Incarnate(registry, world, 15, 15, 1);
        var beyond = Incarnate(registry, world, 16, 16, 2);
        var groundItems = new GroundItemStore();
        groundItems.AddStack(10, 1, 64, new BlockCoordinate(14, 16, 5));
        var spatialIndex = new PerceptionSpatialIndex();
        spatialIndex.Rebuild(registry.Agents.ToArray(), groundItems);
        Assert.True(spatialIndex.AgentBucketCount >= 2);

        var frame = new PerceptionSystem().Observe(
            observer,
            world,
            registry,
            groundItems,
            CreateRules(),
            new PerceptionSettings(NearRadius: 2, MidRadiusRegions: 0, MaximumObservedPerAgent: 4096),
            constructionSites: null,
            shelter: null,
            tick: 0,
            spatialIndex: spatialIndex);

        Assert.Contains(
            frame.Changes,
            change => change.Entry.Kind == PerceptionEntityKind.Agent &&
                      change.Entry.Id == beyond.AgentId);
        Assert.Contains(
            frame.Changes,
            change => change.Entry.Kind == PerceptionEntityKind.GroundItem);
    }

    [Fact]
    public void EmptyIndexStillFallsBackToTheFullScanWhenNoneIsProvided()
    {
        var world = CreateFlatWorld(8, 8);
        var registry = new AgentRegistry();
        var observer = Incarnate(registry, world, 1, 1, 1);
        var neighbour = Incarnate(registry, world, 2, 1, 2);
        var groundItems = new GroundItemStore();
        groundItems.AddStack(10, 1, 64, new BlockCoordinate(1, 2, 5));

        var frame = new PerceptionSystem().Observe(
            observer,
            world,
            registry,
            groundItems,
            CreateRules(),
            new PerceptionSettings(NearRadius: 2, MidRadiusRegions: 0, MaximumObservedPerAgent: 4096));

        Assert.Contains(
            frame.Changes,
            change => change.Entry.Kind == PerceptionEntityKind.Agent &&
                      change.Entry.Id == neighbour.AgentId);
        Assert.Contains(
            frame.Changes,
            change => change.Entry.Kind == PerceptionEntityKind.GroundItem);
    }

    private static void AssertAgreesWithFullScan(
        AgentRegistry registry,
        WorldGrid world,
        GroundItemStore groundItems,
        PerceptionRules rules,
        PerceptionSettings settings,
        PerceptionSystem indexed,
        PerceptionSystem reference,
        PerceptionSpatialIndex spatialIndex,
        long tick)
    {
        foreach (var agent in registry.Agents)
        {
            var indexedFrame = indexed.Observe(
                agent,
                world,
                registry,
                groundItems,
                rules,
                settings,
                constructionSites: null,
                shelter: null,
                tick: tick,
                spatialIndex: spatialIndex);
            var referenceFrame = reference.Observe(
                agent,
                world,
                registry,
                groundItems,
                rules,
                settings,
                constructionSites: null,
                shelter: null,
                tick: tick);

            // SelfObservation porte une collection en lecture seule : deux appels produisent deux
            // instances, donc on compare la position et la séquence d'inventaire, pas la référence.
            Assert.Equal(referenceFrame.Self.Position, indexedFrame.Self.Position);
            Assert.Equal(referenceFrame.Self.Inventory, indexedFrame.Self.Inventory);
            Assert.Equal(referenceFrame.Self.Sheltered, indexedFrame.Self.Sheltered);
            Assert.Equal(referenceFrame.Self.ShelterSiteId, indexedFrame.Self.ShelterSiteId);
            Assert.Equal(
                referenceFrame.Self.FatigueRecoveryBonusPermille,
                indexedFrame.Self.FatigueRecoveryBonusPermille);
            Assert.Equal(referenceFrame.Changes, indexedFrame.Changes);
        }
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
