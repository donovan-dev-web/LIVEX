using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerDayNightCycleTests
{
    private static readonly ViewerCalendarConfig Calendar = new();

    [Fact]
    public void MidnightIsDarkAndNoonIsFullyLit()
    {
        var midnight = ViewerDayNightCycle.Evaluate(0, Calendar);
        var noon = ViewerDayNightCycle.Evaluate(240, Calendar);

        Assert.Equal(0, midnight.Hour);
        Assert.Equal(0, midnight.Minute);
        Assert.Equal(-1f, midnight.SunElevation, 3);
        Assert.True(midnight.Daylight < 0.01f);
        Assert.Equal(0.5f, noon.DayFraction, 3);
        Assert.Equal(1f, noon.SunElevation, 3);
        Assert.Equal(1f, noon.Daylight, 3);
        Assert.Equal(12, noon.Hour);
        Assert.Equal(0, noon.Minute);
    }

    [Fact]
    public void DawnAndDuskAreBetweenNightAndDay()
    {
        var dawn = ViewerDayNightCycle.Evaluate(120, Calendar);
        var noon = ViewerDayNightCycle.Evaluate(240, Calendar);
        var dusk = ViewerDayNightCycle.Evaluate(360, Calendar);

        Assert.InRange(dawn.Daylight, 0.01f, 0.99f);
        Assert.InRange(dusk.Daylight, 0.01f, 0.99f);
        Assert.True(dawn.Daylight < noon.Daylight);
        Assert.True(dusk.Daylight < noon.Daylight);
    }

    [Fact]
    public void DaysAndSeasonsAdvanceWithTheTick()
    {
        var firstDay = ViewerDayNightCycle.Evaluate(0, Calendar);
        var secondDay = ViewerDayNightCycle.Evaluate(480, Calendar);
        var summer = ViewerDayNightCycle.Evaluate(480 * 4, Calendar);
        var nextSpring = ViewerDayNightCycle.Evaluate(480 * 16, Calendar);
        var lateTick = ViewerDayNightCycle.Evaluate(479, Calendar);

        Assert.Equal(0, firstDay.DayIndex);
        Assert.Equal(1, firstDay.DayOfSeason);
        Assert.Equal("SPRING", firstDay.Season);
        Assert.Equal(1, secondDay.DayIndex);
        Assert.Equal(2, secondDay.DayOfSeason);
        Assert.Equal("SUMMER", summer.Season);
        Assert.Equal(1, summer.DayOfSeason);
        Assert.Equal("SPRING", nextSpring.Season);
        Assert.Equal(23, lateTick.Hour);
    }

    [Fact]
    public void EvaluationIsDeterministicAndColorsStayInRange()
    {
        var first = ViewerDayNightCycle.Evaluate(12_345, Calendar);
        var repeated = ViewerDayNightCycle.Evaluate(12_345, Calendar);

        Assert.Equal(first, repeated);
        Assert.InRange(first.SkyR, 0f, 1f);
        Assert.InRange(first.SkyG, 0f, 1f);
        Assert.InRange(first.SkyB, 0f, 1f);
        Assert.InRange(first.LightR, 0f, 1f);
        Assert.InRange(first.LightG, 0f, 1f);
        Assert.InRange(first.LightB, 0f, 1f);
        Assert.InRange(first.SunAzimuth, 0f, MathF.Tau);
    }

    [Fact]
    public void HonoursCustomCalendarLengths()
    {
        var config = new ViewerCalendarConfig(DayLengthTicks: 100, SeasonLengthDays: 2);

        var beforeRollover = ViewerDayNightCycle.Evaluate(99, config);
        var rollover = ViewerDayNightCycle.Evaluate(100, config);
        var thirdDay = ViewerDayNightCycle.Evaluate(200, config);

        Assert.Equal(0, beforeRollover.DayIndex);
        Assert.Equal(23, beforeRollover.Hour);
        Assert.Equal(1, rollover.DayIndex);
        Assert.Equal(2, rollover.DayOfSeason);
        Assert.Equal(2, thirdDay.DayIndex);
        Assert.Equal(1, thirdDay.DayOfSeason);
        Assert.Equal("SUMMER", thirdDay.Season);
    }

    [Fact]
    public void RejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ViewerDayNightCycle.Evaluate(-1, Calendar));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ViewerDayNightCycle.Evaluate(0, new ViewerCalendarConfig(0, 4)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ViewerDayNightCycle.Evaluate(0, new ViewerCalendarConfig(480, 0)));
        Assert.Throws<ArgumentNullException>(
            () => ViewerDayNightCycle.Evaluate(0, null!));
    }
}
