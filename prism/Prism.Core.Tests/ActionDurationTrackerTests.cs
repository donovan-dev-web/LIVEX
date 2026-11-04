using Prism.Core.Actions;

namespace Prism.Core.Tests;

public sealed class ActionDurationTrackerTests
{
    [Fact]
    public void OccupancyStartsAtTheActionTickAndEndsAfterTheDuration()
    {
        var tracker = new ActionDurationTracker();

        Assert.True(tracker.TryBegin(7, "Miner", "cmd-1", 4, 20));
        Assert.True(tracker.IsBusy(7, 4));
        Assert.True(tracker.IsBusy(7, 23));
        Assert.False(tracker.IsBusy(7, 24));
        Assert.False(tracker.TryBegin(7, "Miner", "cmd-2", 10, 5));
        Assert.True(tracker.TryBegin(7, "Miner", "cmd-3", 24, 5));

        Assert.True(tracker.TryGetActive(7, 24, out var active));
        Assert.Equal("cmd-3", active.CommandId);
        Assert.Equal(29, active.BusyUntilTick);
        Assert.False(tracker.TryGetActive(7, 29, out _));
    }

    [Fact]
    public void ZeroDurationActionsDoNotOccupyTheAgent()
    {
        var tracker = new ActionDurationTracker();

        Assert.True(tracker.TryBegin(3, "Poser", "cmd-1", 9, 0));
        Assert.False(tracker.IsBusy(3, 9));
        Assert.Equal(0, tracker.Count);
        Assert.Empty(tracker.Snapshot(9));
    }

    [Fact]
    public void SnapshotKeepsOnlyActiveOccupanciesOrderedByAgent()
    {
        var tracker = new ActionDurationTracker();
        Assert.True(tracker.TryBegin(9, "Miner", "b", 0, 5));
        Assert.True(tracker.TryBegin(2, "Fabriquer", "a", 0, 50));

        Assert.Equal([2UL, 9UL], tracker.Snapshot(1).Select(action => action.AgentId));
        Assert.Equal([2UL], tracker.Snapshot(6).Select(action => action.AgentId));
        Assert.Empty(tracker.Snapshot(50));
        Assert.True(tracker.Release(2));
        Assert.False(tracker.Release(2));
        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void CheckpointRoundTripsAndRejectsTamperedEntries()
    {
        var source = new ActionDurationTracker();
        Assert.True(source.TryBegin(5, "Miner", "cmd-5", 3, 18));
        Assert.True(source.TryBegin(2, "Recolter", "cmd-2", 1, 4));

        var restored = new ActionDurationTracker();
        restored.RestoreState(source.CaptureState());

        Assert.True(restored.IsBusy(5, 20));
        Assert.False(restored.IsBusy(5, 21));
        Assert.False(restored.IsBusy(2, 5));
        Assert.Equal(2, restored.Count);

        var invalid = new ActionDurationTracker();
        Assert.Throws<InvalidDataException>(() => invalid.RestoreState(
            """{"version":1,"actions":[{"agentId":1,"kind":"Miner","commandId":"x","startedTick":5,"busyUntilTick":5}]}"""u8));
        Assert.ThrowsAny<InvalidDataException>(() => invalid.RestoreState("""{"version":2,"actions":[]}"""u8));
        Assert.ThrowsAny<InvalidDataException>(() => invalid.RestoreState("""{"version":1}"""u8));
        Assert.Equal(0, invalid.Count);
        Assert.Throws<InvalidOperationException>(() => restored.RestoreState(source.CaptureState()));
    }
}
