using System.Text.Json;
using Prism.Core.Actions;
using Prism.Host.Ticking;

namespace Prism.Host.Tests;

public sealed class ActionDurationCheckpointTests
{
    [Fact]
    public void DispatchCheckpointRoundTripsActiveActionDurations()
    {
        var tracker = new ActionDurationTracker();
        Assert.True(tracker.TryBegin(7, "Miner", "cmd-1", 4, 20));
        var source = new CommandDispatchPhase([], durations: tracker);
        var state = source.CaptureState();

        var restoredTracker = new ActionDurationTracker();
        var target = new CommandDispatchPhase([], durations: restoredTracker);
        target.RestoreState(state);

        Assert.True(restoredTracker.IsBusy(7, 5));
        Assert.True(restoredTracker.IsBusy(7, 23));
        Assert.False(restoredTracker.IsBusy(7, 24));
        Assert.False(restoredTracker.IsBusy(8, 5));
    }

    [Fact]
    public void LegacyArrayCheckpointStillRestoresWithoutDurations()
    {
        var tracker = new ActionDurationTracker();
        var dispatch = new CommandDispatchPhase([], durations: tracker);

        dispatch.RestoreState(JsonSerializer.SerializeToElement(Array.Empty<object>()));

        Assert.Equal(0, tracker.Count);
        Assert.True(dispatch.CaptureState().TryGetProperty("durations", out var durations));
        Assert.NotEqual(JsonValueKind.Undefined, durations.ValueKind);
    }

    [Fact]
    public void CheckpointWithDurationsRequiresAConfiguredTracker()
    {
        var tracker = new ActionDurationTracker();
        Assert.True(tracker.TryBegin(1, "Fabriquer", "cmd-9", 2, 30));
        var state = new CommandDispatchPhase([], durations: tracker).CaptureState();
        var dispatch = new CommandDispatchPhase([]);

        Assert.Throws<JsonException>(() => dispatch.RestoreState(state));
    }
}
