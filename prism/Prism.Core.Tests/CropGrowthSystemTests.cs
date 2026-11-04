using Prism.Core.Time;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class CropGrowthSystemTests
{
    [Fact]
    public void CropAdvancesThroughStagesAndWithersAfterRipening()
    {
        var (world, target, _) = CreateFarm();
        var wheel = new EventWheel();
        var rule = CreateRule(stageTicks: 2, witherAfterTicks: 5);

        Assert.True(CropGrowthSystem.Plant(world, wheel, 42, 0, target, WorldSeason.Spring, rule).Succeeded);

        var events = AdvanceCropToRipe(world, wheel, target, rule);
        Assert.Equal(
            new CropGrowthEvent(6, CropGrowthEventKind.Ripe, target, 3),
            Assert.Single(events));
        Assert.True(world.TryGetBlockOverride(target, out var ripeCrop));
        Assert.Equal((byte)3, ripeCrop.State);

        var witherEvents = wheel.AdvanceTo(11);
        var withered = CropGrowthSystem.ProcessDueEvents(
            world, wheel, 42, 11, WorldSeason.Spring, rule, witherEvents);

        Assert.Equal(
            new CropGrowthEvent(11, CropGrowthEventKind.Withered, target, 3),
            Assert.Single(withered));
        Assert.True(world.TryGetBlockOverride(target, out var air));
        Assert.True(air.IsAir);
    }

    [Fact]
    public void PlantingRequiresFarmableSoilAndNearbyWater()
    {
        var (world, target, water) = CreateFarm();
        var dryWheel = new EventWheel();
        var rule = CreateRule();
        world.RemoveBlockOverride(water);

        var dryResult = CropGrowthSystem.Plant(
            world, dryWheel, 42, 0, target, WorldSeason.Spring, rule);

        Assert.False(dryResult.Succeeded);
        Assert.Equal("crop.water_required", dryResult.ErrorCode);
        Assert.Equal(0, dryWheel.Count);

        var invalidSoilTarget = new BlockCoordinate(target.X, target.Y, target.Z + 1);
        var soilResult = CropGrowthSystem.Plant(
            world, new EventWheel(), 42, 0, invalidSoilTarget, WorldSeason.Spring, rule);
        Assert.Equal("crop.invalid_soil", soilResult.ErrorCode);
    }

    [Fact]
    public void DormantWinterCropResumesOnceWhenSpringReturns()
    {
        var (world, target, _) = CreateFarm();
        var wheel = new EventWheel();
        var rule = CreateRule(winterFactorPermille: 0);

        Assert.True(CropGrowthSystem.Plant(world, wheel, 42, 0, target, WorldSeason.Winter, rule).Succeeded);
        Assert.Equal(0, wheel.Count);

        Assert.Equal(
            1,
            CropGrowthSystem.ResumeGrowingCrops(
                world, wheel, 42, 10, WorldSeason.Spring, rule));
        Assert.Equal(
            0,
            CropGrowthSystem.ResumeGrowingCrops(
                world, wheel, 42, 10, WorldSeason.Spring, rule));
        Assert.Equal(1, wheel.Count);
    }

    [Fact]
    public void CropGrowthStopsWhenWaterConditionDisappears()
    {
        var (world, target, water) = CreateFarm();
        var wheel = new EventWheel();
        var rule = CreateRule(stageTicks: 2);

        Assert.True(CropGrowthSystem.Plant(world, wheel, 42, 0, target, WorldSeason.Spring, rule).Succeeded);
        world.RemoveBlockOverride(water);
        var dueEvents = wheel.AdvanceTo(2);
        var result = CropGrowthSystem.ProcessDueEvents(
            world, wheel, 42, 2, WorldSeason.Spring, rule, dueEvents);

        Assert.Empty(result);
        Assert.True(world.TryGetBlockOverride(target, out var crop));
        Assert.Equal((byte)0, crop.State);
        Assert.Equal(0, wheel.Count);
    }

    [Fact]
    public void GrowthDueDuringWinterWaitsUntilTheCropCanResume()
    {
        var (world, target, _) = CreateFarm();
        var wheel = new EventWheel();
        var rule = CreateRule(stageTicks: 2);

        Assert.True(CropGrowthSystem.Plant(world, wheel, 42, 0, target, WorldSeason.Spring, rule).Succeeded);
        var winterEvents = wheel.AdvanceTo(2);
        var winterResult = CropGrowthSystem.ProcessDueEvents(
            world, wheel, 42, 2, WorldSeason.Winter, rule, winterEvents);

        Assert.Empty(winterResult);
        Assert.True(world.TryGetBlockOverride(target, out var dormantCrop));
        Assert.Equal((byte)0, dormantCrop.State);
        Assert.Equal(
            1,
            CropGrowthSystem.ResumeGrowingCrops(world, wheel, 42, 2, WorldSeason.Spring, rule));
        var springEvents = wheel.AdvanceTo(4);
        Assert.Equal(2, Assert.Single(springEvents).ContextTick);
        CropGrowthSystem.ProcessDueEvents(
            world, wheel, 42, 4, WorldSeason.Spring, rule, springEvents);
        Assert.True(world.TryGetBlockOverride(target, out var growingCrop));
        Assert.Equal((byte)1, growingCrop.State);
    }

    private static IReadOnlyList<CropGrowthEvent> AdvanceCropToRipe(
        WorldGrid world,
        EventWheel wheel,
        BlockCoordinate target,
        CropGrowthRule rule)
    {
        IReadOnlyList<CropGrowthEvent> result = [];
        for (var tick = 2L; tick <= 6; tick += 2)
        {
            var dueEvents = wheel.AdvanceTo(tick);
            Assert.Equal(0, Assert.Single(dueEvents).ContextTick);
            result = CropGrowthSystem.ProcessDueEvents(
                world, wheel, 42, tick, WorldSeason.Spring, rule, dueEvents);
        }

        return result;
    }

    private static (WorldGrid World, BlockCoordinate Crop, BlockCoordinate Water) CreateFarm()
    {
        var world = new WorldGrid(5, 5, regionSize: 2, verticalSize: 16);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(3, 10, 0, 0));
            }
        }

        var water = new BlockCoordinate(3, 2, 4);
        world.SetBlockOverride(water, new BlockOverride(12, 0, false));
        return (world, new BlockCoordinate(2, 2, 4), water);
    }

    private static CropGrowthRule CreateRule(
        int stageTicks = 10,
        int witherAfterTicks = 20,
        int winterFactorPermille = 0) =>
        new(
            CropMaterialId: 20,
            FarmlandMaterialId: 10,
            WaterMaterialId: 12,
            Stages: 4,
            StageTicks: stageTicks,
            JitterTicks: 0,
            WitherAfterTicks: witherAfterTicks,
            WaterRadius: 4,
            SpringFactorPermille: 1000,
            SummerFactorPermille: 1000,
            AutumnFactorPermille: 1000,
            WinterFactorPermille: winterFactorPermille);
}
