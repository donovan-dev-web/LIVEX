namespace Prism.Core.Time;

public enum WorldSeason : byte
{
    Spring,
    Summer,
    Autumn,
    Winter,
}

public enum CalendarEventKind : byte
{
    DayChanged,
    SeasonChanged,
}

public readonly record struct CalendarSnapshot(
    long Tick,
    long Day,
    int TickOfDay,
    int DayOfSeason,
    WorldSeason Season)
{
    public bool IsNight(int dayLengthTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dayLengthTicks);
        return TickOfDay < dayLengthTicks / 4 || TickOfDay >= (long)dayLengthTicks * 3 / 4;
    }
}

public readonly record struct CalendarEvent(
    long Tick,
    CalendarEventKind Kind,
    long Day,
    WorldSeason Season);

public sealed class WorldCalendar
{
    private static readonly WorldSeason[] Seasons =
    [
        WorldSeason.Spring,
        WorldSeason.Summer,
        WorldSeason.Autumn,
        WorldSeason.Winter,
    ];

    public WorldCalendar(int dayLengthTicks = 480, int seasonLengthDays = 4, long initialTick = 0)
    {
        if (dayLengthTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dayLengthTicks));
        }

        if (seasonLengthDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seasonLengthDays));
        }

        if (initialTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialTick));
        }

        DayLengthTicks = dayLengthTicks;
        SeasonLengthDays = seasonLengthDays;
        CurrentTick = initialTick;
    }

    public int DayLengthTicks { get; }
    public int SeasonLengthDays { get; }
    public long CurrentTick { get; private set; }

    public CalendarSnapshot GetSnapshot(long tick)
    {
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }

        var dayIndex = tick / DayLengthTicks;
        var dayInSeason = dayIndex % SeasonLengthDays;
        var seasonIndex = (dayIndex / SeasonLengthDays) % Seasons.Length;
        return new CalendarSnapshot(
            tick,
            checked(dayIndex + 1),
            (int)(tick % DayLengthTicks),
            (int)dayInSeason + 1,
            Seasons[seasonIndex]);
    }

    public IReadOnlyList<CalendarEvent> AdvanceTo(long tick)
    {
        if (tick < CurrentTick)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), "The calendar cannot move backwards.");
        }

        var events = new List<CalendarEvent>();
        var ticksUntilNextDay = DayLengthTicks - CurrentTick % DayLengthTicks;
        if (CurrentTick > long.MaxValue - ticksUntilNextDay)
        {
            CurrentTick = tick;
            return events;
        }

        var nextDayBoundary = CurrentTick + ticksUntilNextDay;
        while (nextDayBoundary <= tick)
        {
            var snapshot = GetSnapshot(nextDayBoundary);
            events.Add(new CalendarEvent(
                nextDayBoundary,
                CalendarEventKind.DayChanged,
                snapshot.Day,
                snapshot.Season));
            if ((snapshot.Day - 1) % SeasonLengthDays == 0)
            {
                events.Add(new CalendarEvent(
                    nextDayBoundary,
                    CalendarEventKind.SeasonChanged,
                    snapshot.Day,
                    snapshot.Season));
            }

            if (nextDayBoundary > long.MaxValue - DayLengthTicks)
            {
                break;
            }

            nextDayBoundary += DayLengthTicks;
        }

        CurrentTick = tick;
        return events;
    }
}
