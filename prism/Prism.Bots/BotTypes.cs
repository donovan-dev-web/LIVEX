using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Bots;

/// <summary>
/// Block coordinate in the perception frame (no floating point, deterministic).
/// </summary>
public readonly record struct BotCoordinate(int X, int Y, int Z)
{
    public int ManhattanDistanceTo(BotCoordinate other)
    {
        var distance = Math.Abs((long)X - other.X) +
                       Math.Abs((long)Y - other.Y) +
                       Math.Abs((long)Z - other.Z);
        return checked((int)distance);
    }

    /// <summary>Mirrors the world rule for actions: Manhattan distance of at most one.</summary>
    public bool IsAdjacentTo(BotCoordinate other) => ManhattanDistanceTo(other) <= 1;

    public override string ToString() => $"({X},{Y},{Z})";
}

/// <summary>A command a bot refused by the world on the previous tick.</summary>
public sealed record BotCommandFailure(string Kind, string Reason);

/// <summary>
/// Everything a bot is allowed to know: its own body and its perception frame. A bot never
/// reads the world directly, so its decisions stay valid for a remote SYNE cognition.
/// </summary>
public sealed record BotSenses(
    long Tick,
    AgentPosition Position,
    IReadOnlyList<AgentItemStack> Inventory,
    IReadOnlyList<PerceptionEntry> Entries,
    IReadOnlyList<BotCommandFailure> Failures)
{
    public int CountItems(ushort itemTypeId)
    {
        var total = 0;
        foreach (var stack in Inventory)
        {
            if (stack.ItemTypeId == itemTypeId)
            {
                total += stack.Quantity;
            }
        }

        return total;
    }

    public BotCoordinate Cell => new(Position.CellX, Position.CellY, Position.FeetZ);

    public IReadOnlyList<PerceptionEntry> Perceived(PerceptionEntityKind kind)
    {
        var entries = new List<PerceptionEntry>();
        foreach (var entry in Entries)
        {
            if (entry.Kind == kind)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }
}

/// <summary>
/// Belief state built by applying the differential perception frames the host publishes.
/// Perception only sends appeared/changed/disappeared entries, so every bot keeps its own
/// memory: a bot that forgot to merge would act on a partial world.
/// </summary>
public sealed class BotPerceptionMemory
{
    private readonly Dictionary<ObservationKey, PerceptionEntry> _entries = [];

    public long LastMergedTick { get; private set; } = -1;

    public int Count => _entries.Count;

    public IReadOnlyList<PerceptionEntry> Entries
    {
        get
        {
            var entries = new List<PerceptionEntry>(_entries.Count);
            foreach (var key in _entries.Keys.Order())
            {
                entries.Add(_entries[key]);
            }

            return entries;
        }
    }

    public void Merge(long tick, IReadOnlyList<PerceptionChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        foreach (var change in changes)
        {
            var key = ObservationKey.From(change.Entry);
            switch (change.Change)
            {
                case PerceptionChangeKind.Disappeared:
                    _entries.Remove(key);
                    break;
                case PerceptionChangeKind.Appeared:
                case PerceptionChangeKind.Changed:
                    _entries[key] = change.Entry;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(changes), "Unknown perception change kind.");
            }
        }

        LastMergedTick = tick;
    }

    public void ForgetAll() => _entries.Clear();

    private readonly record struct ObservationKey(PerceptionEntityKind Kind, int X, int Y, int Z, ulong Id)
        : IComparable<ObservationKey>
    {
        public static ObservationKey From(PerceptionEntry entry) =>
            new(entry.Kind, entry.X, entry.Y, entry.Z, entry.Id);

        public int CompareTo(ObservationKey other)
        {
            var comparison = Kind.CompareTo(other.Kind);
            if (comparison != 0) return comparison;
            comparison = Z.CompareTo(other.Z);
            if (comparison != 0) return comparison;
            comparison = Y.CompareTo(other.Y);
            if (comparison != 0) return comparison;
            comparison = X.CompareTo(other.X);
            return comparison != 0 ? comparison : Id.CompareTo(other.Id);
        }
    }
}
