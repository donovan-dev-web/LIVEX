using Prism.Core.World;

namespace Prism.Core.Time;

public sealed record CropGrowthRule(
    ushort CropMaterialId,
    ushort FarmlandMaterialId,
    ushort WaterMaterialId,
    int Stages,
    int StageTicks,
    int JitterTicks,
    int WitherAfterTicks,
    int WaterRadius,
    int SpringFactorPermille = 1200,
    int SummerFactorPermille = 1000,
    int AutumnFactorPermille = 800,
    int WinterFactorPermille = 0)
{
    public int GetSeasonFactor(WorldSeason season) => season switch
    {
        WorldSeason.Spring => SpringFactorPermille,
        WorldSeason.Summer => SummerFactorPermille,
        WorldSeason.Autumn => AutumnFactorPermille,
        WorldSeason.Winter => WinterFactorPermille,
        _ => throw new ArgumentOutOfRangeException(nameof(season)),
    };

    public void Validate()
    {
        if (Stages is < 2 or > byte.MaxValue + 1 ||
            StageTicks <= 0 ||
            JitterTicks < 0 ||
            WitherAfterTicks <= 0 ||
            WaterRadius < 0 ||
            SpringFactorPermille is < 0 or > 10_000 ||
            SummerFactorPermille is < 0 or > 10_000 ||
            AutumnFactorPermille is < 0 or > 10_000 ||
            WinterFactorPermille is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(CropGrowthRule));
        }
    }
}

public sealed record CropPlantResult(bool Succeeded, string? ErrorCode)
{
    public static CropPlantResult Success { get; } = new(true, null);
    public static CropPlantResult Failure(string errorCode) => new(false, errorCode);
}

public enum CropGrowthEventKind : byte
{
    Ripe,
    Withered,
}

public readonly record struct CropGrowthEvent(
    long Tick,
    CropGrowthEventKind Kind,
    BlockCoordinate Target,
    byte Stage);

public static class CropGrowthSystem
{
    public static CropPlantResult Plant(
        WorldGrid world,
        EventWheel wheel,
        ulong runSeed,
        long tick,
        BlockCoordinate target,
        WorldSeason season,
        CropGrowthRule rule)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(wheel);
        ArgumentNullException.ThrowIfNull(rule);
        rule.Validate();
        ValidateTick(wheel, tick);
        _ = world.GetSurfaceColumn(target.X, target.Y);
        if ((uint)target.Z >= (uint)world.VerticalSize)
        {
            throw new ArgumentOutOfRangeException(nameof(target));
        }

        var surface = world.GetSurfaceColumn(target.X, target.Y);
        if (target.Z != surface.SurfaceZ + 1 ||
            !HasMaterialAt(world, target.X, target.Y, target.Z - 1, rule.FarmlandMaterialId))
        {
            return CropPlantResult.Failure("crop.invalid_soil");
        }

        if (!HasWaterWithin(world, target, rule.WaterMaterialId, rule.WaterRadius))
        {
            return CropPlantResult.Failure("crop.water_required");
        }

        if (world.TryGetBlockOverride(target, out var existing) && !existing.IsAir)
        {
            return CropPlantResult.Failure("crop.block_occupied");
        }

        ScheduleNextStage(wheel, runSeed, tick, tick, target, 1, season, rule);
        world.SetBlockOverride(target, new BlockOverride(rule.CropMaterialId, 0, IsAir: false));
        return CropPlantResult.Success;
    }

    public static int ResumeGrowingCrops(
        WorldGrid world,
        EventWheel wheel,
        ulong runSeed,
        long tick,
        WorldSeason season,
        CropGrowthRule rule)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(wheel);
        ArgumentNullException.ThrowIfNull(rule);
        rule.Validate();
        ValidateTick(wheel, tick);

        var scheduled = 0;
        foreach (var entry in world.EnumerateOverrides())
        {
            if (entry.Value.IsAir ||
                entry.Value.MaterialId != rule.CropMaterialId ||
                entry.Value.State >= rule.Stages - 1)
            {
                continue;
            }

            var nextStage = entry.Value.State + 1;
            if (wheel.HasScheduledEvent(GetGrowthEventName(rule), entry.Key, nextStage) ||
                !HasGrowingConditions(world, entry.Key, rule))
            {
                continue;
            }

            if (ScheduleNextStage(
                    wheel, runSeed, tick, tick, entry.Key, nextStage, season, rule))
            {
                scheduled++;
            }
        }

        return scheduled;
    }

    public static IReadOnlyList<CropGrowthEvent> ProcessDueEvents(
        WorldGrid world,
        EventWheel wheel,
        ulong runSeed,
        long tick,
        WorldSeason season,
        CropGrowthRule rule,
        IReadOnlyList<ScheduledWorldEvent> dueEvents)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(wheel);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(dueEvents);
        rule.Validate();
        if (wheel.CurrentTick != tick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tick), "The event wheel must be advanced to the tick being processed.");
        }

        var results = new List<CropGrowthEvent>();
        var growthEventName = GetGrowthEventName(rule);
        var witherEventName = GetWitherEventName(rule);
        foreach (var scheduledEvent in dueEvents)
        {
            if (scheduledEvent.Kind != growthEventName && scheduledEvent.Kind != witherEventName)
            {
                continue;
            }

            if (scheduledEvent.DueTick > tick)
            {
                throw new ArgumentException("A future event cannot be processed as due.", nameof(dueEvents));
            }

            if (scheduledEvent.Kind == growthEventName)
            {
                ProcessGrowthEvent(
                    world, wheel, runSeed, tick, season, rule, scheduledEvent, results);
            }
            else
            {
                ProcessWitherEvent(world, rule, scheduledEvent, results);
            }
        }

        return results;
    }

    private static void ProcessGrowthEvent(
        WorldGrid world,
        EventWheel wheel,
        ulong runSeed,
        long tick,
        WorldSeason season,
        CropGrowthRule rule,
        ScheduledWorldEvent scheduledEvent,
        List<CropGrowthEvent> results)
    {
        if (scheduledEvent.State is < 1 || scheduledEvent.State >= rule.Stages)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scheduledEvent), "The scheduled crop stage is outside the configured growth rule.");
        }

        if (!world.TryGetBlockOverride(scheduledEvent.Target, out var crop) ||
            crop.IsAir ||
            crop.MaterialId != rule.CropMaterialId ||
            crop.State + 1 != scheduledEvent.State ||
            rule.GetSeasonFactor(season) == 0 ||
            !HasGrowingConditions(world, scheduledEvent.Target, rule))
        {
            return;
        }

        var stage = checked((byte)scheduledEvent.State);
        if (stage == rule.Stages - 1)
        {
            var witherTick = checked(tick + rule.WitherAfterTicks);
            wheel.Schedule(witherTick, GetWitherEventName(rule), scheduledEvent.Target, stage);
            world.SetBlockOverride(
                scheduledEvent.Target,
                crop with { State = stage });
            results.Add(new CropGrowthEvent(tick, CropGrowthEventKind.Ripe, scheduledEvent.Target, stage));
            return;
        }

        ScheduleNextStage(
            wheel,
            runSeed,
            tick,
            scheduledEvent.ContextTick,
            scheduledEvent.Target,
            stage + 1,
            season,
            rule);
        world.SetBlockOverride(
            scheduledEvent.Target,
            crop with { State = stage });
    }

    private static void ProcessWitherEvent(
        WorldGrid world,
        CropGrowthRule rule,
        ScheduledWorldEvent scheduledEvent,
        List<CropGrowthEvent> results)
    {
        if (!world.TryGetBlockOverride(scheduledEvent.Target, out var crop) ||
            crop.IsAir ||
            crop.MaterialId != rule.CropMaterialId ||
            crop.State != scheduledEvent.State ||
            crop.State != rule.Stages - 1)
        {
            return;
        }

        world.SetBlockOverride(
            scheduledEvent.Target,
            new BlockOverride(MaterialId: 0, State: 0, IsAir: true));
        results.Add(new CropGrowthEvent(
            scheduledEvent.DueTick,
            CropGrowthEventKind.Withered,
            scheduledEvent.Target,
            checked((byte)crop.State)));
    }

    private static bool ScheduleNextStage(
        EventWheel wheel,
        ulong runSeed,
        long tick,
        long jitterSeedTick,
        BlockCoordinate target,
        int nextStage,
        WorldSeason season,
        CropGrowthRule rule)
    {
        var seasonFactor = rule.GetSeasonFactor(season);
        if (seasonFactor == 0)
        {
            return false;
        }

        var adjustedDelay = checked((int)(((long)rule.StageTicks * 1000 + seasonFactor - 1) / seasonFactor));
        WorldUpdateScheduler.ScheduleWithJitter(
            wheel,
            runSeed,
            tick,
            target,
            GetGrowthEventName(rule),
            adjustedDelay,
            rule.JitterTicks,
            nextStage,
            jitterSeedTick);
        return true;
    }

    private static bool HasGrowingConditions(WorldGrid world, BlockCoordinate crop, CropGrowthRule rule) =>
        crop.Z > 0 &&
        HasMaterialAt(world, crop.X, crop.Y, crop.Z - 1, rule.FarmlandMaterialId) &&
        HasWaterWithin(world, crop, rule.WaterMaterialId, rule.WaterRadius);

    private static bool HasMaterialAt(WorldGrid world, int x, int y, int z, ushort materialId)
    {
        if (!world.TryGetSurfaceColumn(x, y, out var surface))
        {
            return false;
        }

        if (world.TryGetBlockOverride(new BlockCoordinate(x, y, z), out var block))
        {
            return !block.IsAir && block.MaterialId == materialId;
        }

        if (z == surface.SurfaceZ)
        {
            return surface.MaterialId == materialId;
        }

        return false;
    }

    private static bool HasWaterWithin(
        WorldGrid world,
        BlockCoordinate crop,
        ushort waterMaterialId,
        int radius)
    {
        foreach (var entry in world.EnumerateOverrides())
        {
            if (Math.Abs(entry.Key.X - crop.X) + Math.Abs(entry.Key.Y - crop.Y) <= radius &&
                !entry.Value.IsAir &&
                entry.Value.MaterialId == waterMaterialId)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetGrowthEventName(CropGrowthRule rule) =>
        $"crop.grow.{rule.CropMaterialId}";

    private static string GetWitherEventName(CropGrowthRule rule) =>
        $"crop.wither.{rule.CropMaterialId}";

    private static void ValidateTick(EventWheel wheel, long tick)
    {
        if (tick < wheel.CurrentTick)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }
    }
}
