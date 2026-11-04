using Prism.Contracts;
using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.World;
using Prism.Host.Ticking;

namespace Prism.Host.Tests;

public sealed class ContainerDecayTickPhaseTests
{
    [Fact]
    public void ContainersInsideAStorageStructureSpoilSlowerAndPublishItemSpoiled()
    {
        var world = CreateWorld(7, 7);
        var construction = new StructureConstructionSystem(world);
        Assert.True(construction.CreateSite(
            "granary-1",
            GranaryTemplate(),
            new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.True(construction.ContributeWork("granary-1", "builder", 0).Succeeded);
        var containers = new WorldContainerStore();
        var inside = Container(new BlockCoordinate(2, 2, 6));
        var outside = Container(new BlockCoordinate(6, 6, 6));
        Assert.True(containers.TryAdd(inside));
        Assert.True(containers.TryAdd(outside));
        var phase = new StructureConstructionTickPhase(
            construction,
            containers,
            new MatterLedger(),
            new StructureEffectConsumer(construction),
            new ContainerDecayRules(BasePermillePerTick: 1_000, StorageSlowdownPermille: 500));
        var host = new OrderedWorldHost(Enum.GetValues<WorldTickPhase>()
            .Select(phaseKind => phaseKind == WorldTickPhase.WearExpiryAndConstruction
                ? (IWorldTickPhaseHandler)phase
                : new NoOpPhase(phaseKind)));

        _ = host.Step(new TickRequest(ProtocolVersion.Current.ToString(), 0, []));
        var result = host.Step(new TickRequest(ProtocolVersion.Current.ToString(), 1, []));

        Assert.Equal(500, Assert.Single(inside.Snapshot()).FreshnessPermilleTicks);
        Assert.Empty(outside.Snapshot());
        var spoiled = Assert.Single(
            result.Events,
            static entry => entry.Kind == "item_spoiled");
        Assert.Equal(10, spoiled.Payload.GetProperty("itemTypeId").GetInt32());
        Assert.Equal(2, spoiled.Payload.GetProperty("quantity").GetInt32());
    }

    private static ContainerInventory Container(BlockCoordinate position)
    {
        var container = new ContainerInventory(4, 1_000, position, AccessPrincipal.ForAgent(1));
        Assert.True(container.TryAdd(new AgentItemStack(
            10,
            2,
            8,
            DurabilityRemaining: null,
            FreshnessPermilleTicks: 1_000,
            FreshnessUpdatedTick: 0)));
        return container;
    }

    private static StructureTemplateRule GranaryTemplate() =>
        new(
            "granary",
            3,
            3,
            3,
            [new StructureBlockRule(1, 1, 0, 1)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 1,
            IntegrityThresholdPermille: 500,
            Effect: new StructureEffectRule(
                StructureEffectConsumer.StorageEffectType,
                new BlockCoordinate(0, 0, 0),
                new BlockCoordinate(2, 2, 2),
                FatigueRecoveryBonusPermille: 0));

    private static WorldGrid CreateWorld(int width, int height)
    {
        var world = new WorldGrid(width, height, verticalSize: 16);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(4, 1, 0, 0));
            }
        }

        return world;
    }

    private sealed class NoOpPhase(WorldTickPhase phase) : IWorldTickPhaseHandler
    {
        public WorldTickPhase Phase => phase;

        public void Execute(WorldTickContext context) => ArgumentNullException.ThrowIfNull(context);
    }
}
