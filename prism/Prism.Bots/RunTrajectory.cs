using Prism.Contracts;
using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Bots;

/// <summary>Quantity of one item type an agent holds, aggregated for the coarse fingerprint.</summary>
public sealed record TrajectoryItemCount(ushort ItemTypeId, int Quantity);

/// <summary>
/// One agent as the trajectory level sees it: where it stands, whether it is sheltered and how
/// much of each item it holds. No stack split, no durability, no freshness: those belong to the
/// byte level, and a merge order must not be able to break the coarse pin.
/// </summary>
public sealed record TrajectoryAgent(
    ulong AgentId,
    int CellX,
    int CellY,
    int FeetZ,
    bool Sheltered,
    IReadOnlyList<TrajectoryItemCount> Inventory);

/// <summary>How many events of one kind the world published during a sampling window.</summary>
public sealed record TrajectoryCounter(string Kind, int Count);

/// <summary>A coarse picture of the world at the end of one sampling window.</summary>
public sealed record TrajectoryFrame(
    long Tick,
    IReadOnlyList<TrajectoryAgent> Agents,
    IReadOnlyList<TrajectoryCounter> Counters);

/// <summary>
/// Trajectory-level run fingerprint (PRISM-107): "did the simulation behave the same?", as opposed
/// to <see cref="RunFingerprint"/> which answers "is the byte stream identical?".
/// <para>
/// Every <see cref="DefaultIntervalTicks"/> ticks it samples what an observer of the world can
/// see — each agent's cell, its sheltered state and its inventory totals, plus the number of
/// events of each kind published in the window — and hashes the frames in a canonical order
/// (agents by id, items by type id, counters by kind). It therefore ignores the things a
/// performance change is allowed to touch: the order observations and events are emitted in, the
/// order of stacks inside an inventory, and the serialized shape of the frame. It catches the
/// things a performance change must not touch: an agent that ends up elsewhere, holds something
/// else, or a world that publishes a different number of events.
/// </para>
/// </summary>
public static class RunTrajectory
{
    /// <summary>Sampling period of the trajectory level, in ticks.</summary>
    public const int DefaultIntervalTicks = 100;

    /// <summary>
    /// Builds the canonical frame at <paramref name="tick"/> from the tick's observations and the
    /// event counters accumulated since the previous frame. Observations may arrive in any order:
    /// they are sorted by agent id here, and read through <see cref="SelfObservation"/> rather
    /// than the raw JSON, so a permuted emission cannot change the frame.
    /// </summary>
    public static TrajectoryFrame Frame(
        long tick,
        IReadOnlyList<ObservationEnvelope> observations,
        IReadOnlyDictionary<string, int> counters)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(counters);

        var agents = new List<TrajectoryAgent>(observations.Count);
        foreach (var observation in observations)
        {
            var self = System.Text.Json.JsonSerializer.Deserialize<SelfObservation>(
                           observation.Self,
                           ProtocolJson.Options)
                       ?? throw new System.Text.Json.JsonException(
                           "Observation self could not be read for the trajectory fingerprint.");
            agents.Add(new TrajectoryAgent(
                observation.AgentId,
                self.Position.CellX,
                self.Position.CellY,
                self.Position.FeetZ,
                self.Sheltered,
                AggregateInventory(self.Inventory)));
        }

        // One observation per agent per tick, but the contract does not say so: sort, so a doubled
        // or reordered emission cannot reorder the hashed bytes either.
        agents.Sort(static (left, right) => left.AgentId.CompareTo(right.AgentId));

        var counterList = new List<TrajectoryCounter>(counters.Count);
        foreach (var (kind, count) in counters)
        {
            if (count != 0)
            {
                counterList.Add(new TrajectoryCounter(kind, count));
            }
        }

        counterList.Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(left.Kind, right.Kind));

        return new TrajectoryFrame(tick, agents, counterList);
    }

    /// <summary>Hashes one frame into <paramref name="hash"/>, in canonical order.</summary>
    public static ulong Append(ulong hash, TrajectoryFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        hash = RunFingerprint.Append(hash, "trajectory");
        hash = RunFingerprint.Append(hash, frame.Tick);
        hash = RunFingerprint.Append(hash, (long)frame.Agents.Count);
        foreach (var agent in frame.Agents)
        {
            hash = RunFingerprint.Append(hash, agent.AgentId);
            hash = RunFingerprint.Append(hash, (long)agent.CellX);
            hash = RunFingerprint.Append(hash, (long)agent.CellY);
            hash = RunFingerprint.Append(hash, (long)agent.FeetZ);
            hash = RunFingerprint.Append(hash, agent.Sheltered ? "sheltered" : "exposed");
            hash = RunFingerprint.Append(hash, (long)agent.Inventory.Count);
            foreach (var item in agent.Inventory)
            {
                hash = RunFingerprint.Append(hash, (long)item.ItemTypeId);
                hash = RunFingerprint.Append(hash, (long)item.Quantity);
            }
        }

        hash = RunFingerprint.Append(hash, (long)frame.Counters.Count);
        foreach (var counter in frame.Counters)
        {
            hash = RunFingerprint.Append(hash, counter.Kind);
            hash = RunFingerprint.Append(hash, (long)counter.Count);
        }

        return hash;
    }

    /// <summary>
    /// Trajectory fingerprint of an already recorded tick sequence, for comparing two runs (or two
    /// orderings of the same run) after the fact.
    /// </summary>
    public static ulong Compute(
        IEnumerable<TickResult> results,
        int intervalTicks = DefaultIntervalTicks)
    {
        ArgumentNullException.ThrowIfNull(results);
        var accumulator = new TrajectoryAccumulator(intervalTicks);
        foreach (var result in results)
        {
            accumulator.Add(result);
        }

        return accumulator.Fingerprint;
    }

    private static IReadOnlyList<TrajectoryItemCount> AggregateInventory(
        IReadOnlyList<AgentItemStack> inventory)
    {
        if (inventory.Count == 0)
        {
            return [];
        }

        var totals = new SortedDictionary<ushort, int>();
        foreach (var stack in inventory)
        {
            totals[stack.ItemTypeId] = totals.GetValueOrDefault(stack.ItemTypeId) + stack.Quantity;
        }

        var counts = new List<TrajectoryItemCount>(totals.Count);
        foreach (var (itemTypeId, quantity) in totals)
        {
            counts.Add(new TrajectoryItemCount(itemTypeId, quantity));
        }

        return counts;
    }
}

/// <summary>
/// Accumulates the trajectory fingerprint tick by tick, the way <see cref="BotRunner"/> feeds it.
/// A frame is taken every <c>intervalTicks</c> ticks; a trailing partial window is not sampled,
/// so the fingerprint depends on whole windows only.
/// </summary>
public sealed class TrajectoryAccumulator
{
    private readonly int _intervalTicks;
    private readonly SortedDictionary<string, int> _counters = new(StringComparer.Ordinal);
    private int _ticksSinceFrame;

    public TrajectoryAccumulator(int intervalTicks = RunTrajectory.DefaultIntervalTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalTicks);
        _intervalTicks = intervalTicks;
    }

    /// <summary>Trajectory fingerprint of the frames taken so far.</summary>
    public ulong Fingerprint { get; private set; } = RunFingerprint.Empty;

    /// <summary>Number of frames hashed; zero means the run was shorter than one window.</summary>
    public int Frames { get; private set; }

    public void Add(TickResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        foreach (var eventEnvelope in result.Events)
        {
            _counters[eventEnvelope.Kind] = _counters.GetValueOrDefault(eventEnvelope.Kind) + 1;
        }

        if (++_ticksSinceFrame < _intervalTicks)
        {
            return;
        }

        var frame = RunTrajectory.Frame(result.Tick, result.Observations, _counters);
        Fingerprint = RunTrajectory.Append(Fingerprint, frame);
        Frames++;
        _ticksSinceFrame = 0;
        _counters.Clear();
    }
}
