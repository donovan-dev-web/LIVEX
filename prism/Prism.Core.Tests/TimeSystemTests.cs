using Prism.Core.Time;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class WorldCalendarTests
{
    [Fact]
    public void CalendarMapsTicksToDaySeasonAndNight()
    {
        var calendar = new WorldCalendar(dayLengthTicks: 8, seasonLengthDays: 2);

        var firstDay = calendar.GetSnapshot(0);
        var daytime = calendar.GetSnapshot(2);
        var nighttime = calendar.GetSnapshot(6);
        var nextSeason = calendar.GetSnapshot(16);

        Assert.Equal(1, firstDay.Day);
        Assert.Equal(WorldSeason.Spring, firstDay.Season);
        Assert.Equal(2, daytime.TickOfDay);
        Assert.False(daytime.IsNight(calendar.DayLengthTicks));
        Assert.True(nighttime.IsNight(calendar.DayLengthTicks));
        Assert.Equal(3, nextSeason.Day);
        Assert.Equal(WorldSeason.Summer, nextSeason.Season);
        Assert.Equal(1, nextSeason.DayOfSeason);
    }

    [Fact]
    public void AdvancingCalendarEmitsOrderedDayAndSeasonEvents()
    {
        var calendar = new WorldCalendar(dayLengthTicks: 4, seasonLengthDays: 2);

        var events = calendar.AdvanceTo(9);

        Assert.Equal(
            new[]
            {
                new CalendarEvent(4, CalendarEventKind.DayChanged, 2, WorldSeason.Spring),
                new CalendarEvent(8, CalendarEventKind.DayChanged, 3, WorldSeason.Summer),
                new CalendarEvent(8, CalendarEventKind.SeasonChanged, 3, WorldSeason.Summer),
            },
            events);
        Assert.Equal(9, calendar.CurrentTick);
        Assert.Empty(calendar.AdvanceTo(9));
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.AdvanceTo(8));
    }

    [Fact]
    public void CalendarDoesNotOverflowAtMaximumTick()
    {
        var calendar = new WorldCalendar(dayLengthTicks: 7, initialTick: long.MaxValue - 1);

        Assert.Equal(
            new CalendarEvent(
                long.MaxValue,
                CalendarEventKind.DayChanged,
                long.MaxValue / 7 + 1,
                calendar.GetSnapshot(long.MaxValue).Season),
            Assert.Single(calendar.AdvanceTo(long.MaxValue)));
        Assert.Equal(long.MaxValue, calendar.CurrentTick);
    }
}

public sealed class WorldUpdateSchedulerTests
{
    [Fact]
    public void GrowthJitterIsRepeatableAndSchedulesAfterCurrentTick()
    {
        var firstWheel = new EventWheel(initialTick: 10);
        var secondWheel = new EventWheel(initialTick: 10);
        var target = new BlockCoordinate(3, 5, 7);

        var first = WorldUpdateScheduler.ScheduleWithJitter(
            firstWheel, 123, 10, target, "crop.grow", 5, 2, state: 1);
        var second = WorldUpdateScheduler.ScheduleWithJitter(
            secondWheel, 123, 10, target, "crop.grow", 5, 2, state: 1);

        Assert.Equal(first, second);
        Assert.InRange(first.DueTick, 13, 17);
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldUpdateScheduler.ScheduleWithJitter(
            firstWheel, 123, 9, target, "crop.grow", 5, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldUpdateScheduler.ScheduleWithJitter(
            firstWheel, 123, 10, target, "crop.grow", 0, 0));
    }

    [Fact]
    public void GrowthJitterNeverSchedulesInThePastOrAtTheCurrentTick()
    {
        var wheel = new EventWheel(initialTick: 20);

        var scheduled = WorldUpdateScheduler.ScheduleWithJitter(
            wheel, 5, 20, new BlockCoordinate(0, 0, 0), "crop.grow", 1, 25);

        Assert.InRange(scheduled.DueTick, 21, 46);
    }
}

public sealed class RandomTickProcessorTests
{
    [Fact]
    public void RandomTicksProduceIdenticalWorldsAcrossParallelismLevels()
    {
        var sequentialWorld = CreateWorld();
        var parallelWorld = CreateWorld();
        var sequentialSamples = new List<RandomTickSample>();
        var parallelSamples = new List<RandomTickSample>();

        var sequentialResult = RandomTickProcessor.Execute(
            sequentialWorld,
            9876,
            42,
            2,
            (_, sample) =>
            {
                sequentialSamples.Add(sample);
                return new BlockOverride((ushort)(100 + sample.RegionId), (byte)sample.Ordinal, false);
            },
            maxDegreeOfParallelism: 1);
        var parallelResult = RandomTickProcessor.Execute(
            parallelWorld,
            9876,
            42,
            2,
            (_, sample) =>
            {
                lock (parallelSamples)
                {
                    parallelSamples.Add(sample);
                }

                return new BlockOverride((ushort)(100 + sample.RegionId), (byte)sample.Ordinal, false);
            },
            maxDegreeOfParallelism: 8);

        Assert.Equal(sequentialResult, parallelResult);
        Assert.Equal(12, sequentialResult.SamplesProcessed);
        Assert.Equal(12, sequentialResult.BlocksChanged);
        Assert.Equal(sequentialSamples.OrderBy(sample => sample.RegionId).ThenBy(sample => sample.Ordinal),
            parallelSamples.OrderBy(sample => sample.RegionId).ThenBy(sample => sample.Ordinal));
        Assert.Equal(
            WorldGridHasher.ComputeWorldHash(sequentialWorld),
            WorldGridHasher.ComputeWorldHash(parallelWorld));
    }

    [Fact]
    public void RandomTicksAreUniquePerRegionAndValidateCountsBeforeProcessing()
    {
        var world = CreateWorld();
        var samples = new List<RandomTickSample>();

        RandomTickProcessor.Execute(
            world,
            11,
            3,
            2,
            (_, sample) =>
            {
                samples.Add(sample);
                return null;
            });

        Assert.Equal(12, samples.Count);
        Assert.Equal(12, samples.Select(sample => (sample.X, sample.Y)).Distinct().Count());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RandomTickProcessor.Execute(world, 11, 3, 5, (_, _) => null));
    }

    private static WorldGrid CreateWorld()
    {
        var world = new WorldGrid(6, 4, regionSize: 2, verticalSize: 16);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn((byte)(4 + x % 2), 1, 0, 0));
            }
        }

        return world;
    }
}
