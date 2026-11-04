using Prism.Core.Time;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class GrassSpreadSystemTests
{
    [Fact]
    public void GrassSpreadsOnlyFromAdjacentGrassToExposedSoil()
    {
        var world = CreateFlatWorld(3, 3);
        var source = new BlockCoordinate(0, 1, 5);
        world.SetBlockOverride(source, new BlockOverride(20, 0, false));
        var rule = new GrassSpreadRule(GrassMaterialId: 20, SoilMaterialId: 10);

        var result = GrassSpreadSystem.TrySpread(
            world, 42, new RandomTickSample(8, 0, 0, 1, 1), rule);
        var isolated = GrassSpreadSystem.TrySpread(
            world, 42, new RandomTickSample(8, 0, 1, 2, 2), rule);

        Assert.Equal(new BlockOverride(20, 0, false), result);
        Assert.Null(isolated);
    }

    [Fact]
    public void GrassSpreadHonorsChanceAndBlockingOverrides()
    {
        var world = CreateFlatWorld(2, 1);
        world.SetBlockOverride(new BlockCoordinate(0, 0, 5), new BlockOverride(20, 0, false));
        var sample = new RandomTickSample(1, 0, 0, 1, 0);

        Assert.Null(GrassSpreadSystem.TrySpread(
            world, 1, sample, new GrassSpreadRule(20, 10, ChancePermille: 0)));
        world.SetBlockOverride(new BlockCoordinate(1, 0, 5), new BlockOverride(30, 0, false));
        Assert.Null(GrassSpreadSystem.TrySpread(
            world, 1, sample, new GrassSpreadRule(20, 10)));
    }

    [Fact]
    public void GrassSpreadThroughRandomTicksIsRepeatableAcrossWorkerCounts()
    {
        var sequential = CreateFlatWorld(8, 8, regionSize: 4);
        var parallel = CreateFlatWorld(8, 8, regionSize: 4);
        foreach (var world in new[] { sequential, parallel })
        {
            world.SetSurfaceColumn(3, 3, new SurfaceColumn(4, 20, 0, 0));
            world.SetBlockOverride(new BlockCoordinate(3, 3, 5), new BlockOverride(20, 0, false));
        }

        var rule = new GrassSpreadRule(GrassMaterialId: 20, SoilMaterialId: 10, ChancePermille: 1000);
        var sequentialResult = RandomTickProcessor.Execute(
            sequential,
            19,
            7,
            4,
            (world, sample) => GrassSpreadSystem.TrySpread(world, 19, sample, rule),
            maxDegreeOfParallelism: 1);
        var parallelResult = RandomTickProcessor.Execute(
            parallel,
            19,
            7,
            4,
            (world, sample) => GrassSpreadSystem.TrySpread(world, 19, sample, rule),
            maxDegreeOfParallelism: 8);

        Assert.Equal(sequentialResult, parallelResult);
        Assert.Equal(
            WorldGridHasher.ComputeWorldHash(sequential),
            WorldGridHasher.ComputeWorldHash(parallel));
    }

    private static WorldGrid CreateFlatWorld(int width, int height, int regionSize = 4)
    {
        var world = new WorldGrid(width, height, regionSize, verticalSize: 16);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(4, 10, 0, 0));
            }
        }

        return world;
    }
}
