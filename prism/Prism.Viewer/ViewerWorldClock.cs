namespace Prism.Viewer;

public sealed record ViewerCalendarConfig(int DayLengthTicks = 480, int SeasonLengthDays = 4)
{
    public void Validate()
    {
        if (DayLengthTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DayLengthTicks),
                "Day length must be a positive number of ticks.");
        }

        if (SeasonLengthDays <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SeasonLengthDays),
                "Season length must be a positive number of days.");
        }
    }
}

public readonly record struct ViewerDayNightState(
    long Tick,
    int DayIndex,
    int DayOfSeason,
    string Season,
    float DayFraction,
    float SunElevation,
    float SunAzimuth,
    float Daylight,
    float SkyR,
    float SkyG,
    float SkyB,
    float LightR,
    float LightG,
    float LightB,
    int Hour,
    int Minute);

public static class ViewerDayNightCycle
{
    private static readonly string[] Seasons = ["SPRING", "SUMMER", "AUTUMN", "WINTER"];

    public static ViewerDayNightState Evaluate(long tick, ViewerCalendarConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(tick);

        var dayIndex = tick / config.DayLengthTicks;
        var tickOfDay = tick % config.DayLengthTicks;
        var fraction = tickOfDay / (float)config.DayLengthTicks;
        var angle = MathF.Tau * (fraction - 0.25f);
        var elevation = float.Sin(angle);
        var daylight = SmoothStep(Clamp01((elevation + 0.12f) / 0.35f));
        var horizon = Clamp01(1f - MathF.Abs(elevation) / 0.30f);

        var sky = Blend(
            (0.035f, 0.055f, 0.110f),
            (0.780f, 0.840f, 0.880f),
            daylight);
        sky = Blend(sky, (0.800f, 0.500f, 0.360f), horizon * 0.55f);

        var light = Blend(
            (0.550f, 0.640f, 0.860f),
            (1.000f, 0.970f, 0.900f),
            daylight);
        light = Blend(light, (1.000f, 0.780f, 0.600f), horizon * 0.45f);

        var hours = fraction * 24f;
        var hour = (int)MathF.Floor(hours);
        var minute = (int)MathF.Floor((hours - hour) * 60f);
        var seasonIndex = (int)(dayIndex / config.SeasonLengthDays % Seasons.Length);
        var dayOfSeason = (int)(dayIndex % config.SeasonLengthDays) + 1;

        return new ViewerDayNightState(
            tick,
            (int)Math.Min(dayIndex, int.MaxValue),
            dayOfSeason,
            Seasons[seasonIndex],
            fraction,
            elevation,
            NormalizeAngle(angle),
            daylight,
            sky.Item1,
            sky.Item2,
            sky.Item3,
            light.Item1,
            light.Item2,
            light.Item3,
            hour,
            minute);
    }

    private static float NormalizeAngle(float angle)
    {
        var normalized = angle % MathF.Tau;
        return normalized < 0f ? normalized + MathF.Tau : normalized;
    }

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

    private static float SmoothStep(float value) => value * value * (3f - 2f * value);

    private static (float R, float G, float B) Blend(
        (float R, float G, float B) from,
        (float R, float G, float B) to,
        float amount) =>
        (from.R + (to.R - from.R) * amount,
            from.G + (to.G - from.G) * amount,
            from.B + (to.B - from.B) * amount);
}
