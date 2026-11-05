using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class BucketFillSystemTests
{
    [Fact]
    public void FillFromStructureConvertsEmptyBucketInsideWaterAccess()
    {
        var world = new WorldGrid(4, 4, verticalSize: 16);
        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                world.SetSurfaceColumn(x, y, new SurfaceColumn(4, 1, 0, 0));

        var movementRules = new MovementRules([1], []);
        var lifecycle = new AgentLifecycleSystem(world, movementRules);
        var agent = lifecycle.Incarnate(1, AgentPosition.AtCell(1, 1, 5), new AgentBodyProfile(CarrySlots: 4));
        const ushort wood = 10, water = 11;
        Assert.True(agent.Inventory.TryAdd(wood, 1, 1, out _));

        var ok = BucketFillSystem.FillFromStructure(agent, wood, water, hasWaterAccess: true);
        Assert.True(ok.Succeeded);
        Assert.Equal(0, agent.Inventory.Count(wood));
        Assert.Equal(1, agent.Inventory.Count(water));

        // No bucket -> failure.
        var fail = BucketFillSystem.FillFromStructure(agent, wood, water, hasWaterAccess: true);
        Assert.False(fail.Succeeded);
        Assert.Equal("bucket_missing", fail.ErrorCode);
    }
}