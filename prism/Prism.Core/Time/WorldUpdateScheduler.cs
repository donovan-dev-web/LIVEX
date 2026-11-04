using Prism.Core.Determinism;
using Prism.Core.World;

namespace Prism.Core.Time;

public static class WorldUpdateScheduler
{
    public static ScheduledWorldEvent ScheduleWithJitter(
        EventWheel wheel,
        ulong runSeed,
        long currentTick,
        BlockCoordinate target,
        string kind,
        int baseDelayTicks,
        int jitterTicks,
        int state = 0,
        long? jitterSeedTick = null)
    {
        ArgumentNullException.ThrowIfNull(wheel);
        if (currentTick < wheel.CurrentTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentTick), "An update cannot be scheduled from before the event wheel's current tick.");
        }

        if (baseDelayTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDelayTicks));
        }

        if (jitterTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(jitterTicks));
        }

        var randomTick = jitterSeedTick ?? currentTick;
        if (randomTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(jitterSeedTick));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        var key = DeterministicRandom.Mix64(
            ((ulong)(uint)target.X << 32) | (uint)target.Y) ^
            DeterministicRandom.Mix64((ulong)(uint)target.Z) ^
            DeterministicRandom.Mix64((ulong)(uint)state);
        var range = checked((ulong)jitterTicks * 2 + 1);
        var jitter = jitterTicks == 0
            ? 0
            : (int)(DeterministicRandom.CounterValue(
                runSeed,
                RandomStreamNames.GrowthJitter,
                (ulong)randomTick,
                key) % range) - jitterTicks;
        var delay = Math.Max(1L, (long)baseDelayTicks + jitter);
        var dueTick = checked(currentTick + delay);
        return wheel.Schedule(dueTick, kind, target, state, randomTick);
    }
}
