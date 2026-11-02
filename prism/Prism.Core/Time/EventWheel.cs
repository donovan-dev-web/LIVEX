using Prism.Core.World;

namespace Prism.Core.Time;

public sealed class EventWheel
{
    private readonly List<ScheduledWorldEvent>[] _buckets;
    private ulong _nextSequence;

    public EventWheel(int bucketCount = 256, long initialTick = 0)
    {
        if (bucketCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCount));
        }

        if (initialTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialTick));
        }

        _buckets = Enumerable.Range(0, bucketCount)
            .Select(_ => new List<ScheduledWorldEvent>())
            .ToArray();
        CurrentTick = initialTick;
    }

    public long CurrentTick { get; private set; }
    public int BucketCount => _buckets.Length;
    public int Count { get; private set; }

    public bool HasScheduledEvent(string kind, BlockCoordinate target, int state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        return _buckets.Any(bucket => bucket.Any(scheduledEvent =>
            scheduledEvent.Kind == kind &&
            scheduledEvent.Target == target &&
            scheduledEvent.State == state));
    }

    public ScheduledWorldEvent Schedule(
        long dueTick, string kind, BlockCoordinate target, int state = 0, long contextTick = 0)
    {
        if (dueTick < CurrentTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dueTick), "An event cannot be scheduled before the current tick.");
        }

        if (contextTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contextTick));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (_nextSequence == ulong.MaxValue)
        {
            throw new InvalidOperationException("The event sequence space is exhausted.");
        }

        var scheduledEvent = new ScheduledWorldEvent(
            dueTick, _nextSequence++, kind, target, state, contextTick);
        GetBucket(dueTick).Add(scheduledEvent);
        Count++;
        return scheduledEvent;
    }

    public bool Cancel(ulong sequence)
    {
        foreach (var bucket in _buckets)
        {
            var index = bucket.FindIndex(scheduledEvent => scheduledEvent.Sequence == sequence);
            if (index < 0)
            {
                continue;
            }

            bucket.RemoveAt(index);
            Count--;
            return true;
        }

        return false;
    }

    public IReadOnlyList<ScheduledWorldEvent> AdvanceTo(long tick)
    {
        if (tick < CurrentTick)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), "The event wheel cannot move backwards.");
        }

        var dueEvents = new List<ScheduledWorldEvent>();
        foreach (var bucket in _buckets)
        {
            for (var index = bucket.Count - 1; index >= 0; index--)
            {
                if (bucket[index].DueTick > tick)
                {
                    continue;
                }

                dueEvents.Add(bucket[index]);
                bucket.RemoveAt(index);
            }
        }

        dueEvents.Sort(static (left, right) =>
        {
            var tickComparison = left.DueTick.CompareTo(right.DueTick);
            return tickComparison != 0 ? tickComparison : left.Sequence.CompareTo(right.Sequence);
        });
        Count -= dueEvents.Count;
        CurrentTick = tick;
        return dueEvents;
    }

    private List<ScheduledWorldEvent> GetBucket(long dueTick) =>
        _buckets[(int)(dueTick % _buckets.Length)];
}
