using System.Text.Json;
using Prism.Contracts;
using Prism.Core.Actions;
using Prism.Core.World;
using Prism.Host.Ticking;

namespace Prism.Host.Tests;

public sealed class DeferredActionResolutionsTests
{
    private static string Protocol => ProtocolVersion.Current.ToString();

    [Fact]
    public void ResolutionIsHandedBackToTheStepThatRequestedIt()
    {
        var channel = new DeferredActionResolutions();
        channel.Expect(42, "macro:42:0:Miner", Step(MacroStepKind.Mine, itemTypeId: 10, quantity: 1));
        Assert.True(channel.IsPending(42));

        channel.Record("macro:42:0:Miner", Success());

        Assert.True(channel.TryTake(42, out var resolved));
        Assert.True(resolved.Succeeded);
        Assert.Equal(MacroStepKind.Mine, resolved.Step.Kind);
        Assert.Equal(10, resolved.Step.ItemTypeId);
        Assert.Equal(1, resolved.Step.Quantity);
        Assert.False(channel.TryTake(42, out _));
        Assert.False(channel.IsPending(42));
        Assert.Equal(0, channel.PendingCount);
    }

    [Fact]
    public void ResolutionsNobodyWaitsForAreIgnored()
    {
        var channel = new DeferredActionResolutions();

        channel.Record("host-craft-1", Success());

        Assert.Equal(0, channel.ResolvedCount);
        Assert.Equal(0, channel.PendingCount);
    }

    [Fact]
    public void CancellingAPendingDeferredActionFailsTheStep()
    {
        var channel = new DeferredActionResolutions();
        channel.Expect(42, "macro:42:0:Miner", Step(MacroStepKind.Mine, itemTypeId: 10, quantity: 1));
        var host = CreateHost(channel, new DeferredHandler("Miner"), new CancelHandler("Annuler"));
        using var payload = JsonDocument.Parse("""{"target":{"x":1,"y":2,"z":3}}""");
        using var empty = JsonDocument.Parse("{}");

        host.Step(new TickRequest(
            Protocol,
            0,
            [new CommandEnvelope(Protocol, 0, "macro:42:0:Miner", 42, "Miner", payload.RootElement.Clone())]));
        host.Step(new TickRequest(
            Protocol,
            1,
            [new CommandEnvelope(Protocol, 1, "cancel-1", 42, "Annuler", empty.RootElement.Clone())]));

        Assert.True(channel.TryTake(42, out var resolved));
        Assert.False(resolved.Succeeded);
        Assert.Equal("cancelled", resolved.ErrorCode);
    }

    [Fact]
    public void PruningDropsStateForAgentsWithoutAMacro()
    {
        var channel = new DeferredActionResolutions();
        channel.Expect(1, "macro:1:0:Miner", Step(MacroStepKind.Mine, itemTypeId: 10, quantity: 1));
        channel.Expect(2, "macro:2:0:Mine", Step(MacroStepKind.Mine, itemTypeId: 10, quantity: 1));
        channel.Record("macro:2:0:Mine", Success());

        Assert.Equal(2, channel.Prune(new ulong[] { 1 }));

        Assert.True(channel.IsPending(1));
        Assert.False(channel.IsPending(2));
        Assert.False(channel.TryTake(2, out _));
    }

    [Fact]
    public void ChannelCheckpointPreservesUnconsumedResolutions()
    {
        var channel = new DeferredActionResolutions();
        channel.Expect(42, "macro:42:0:Miner", Step(MacroStepKind.Mine, itemTypeId: 10, quantity: 1));
        channel.Record("macro:42:0:Miner", Success());
        var state = channel.CaptureState();

        var restored = new DeferredActionResolutions();
        restored.RestoreState(state);

        Assert.True(restored.TryTake(42, out var resolved));
        Assert.True(resolved.Succeeded);
        Assert.Equal(10, resolved.Step.ItemTypeId);
    }

    [Fact]
    public void DispatchCheckpointCarriesPendingMacroSteps()
    {
        var channel = new DeferredActionResolutions();
        channel.Expect(9, "macro:9:0:Fabriquer", Step(MacroStepKind.Craft, itemTypeId: 3, quantity: 1));
        var state = new CommandDispatchPhase([], resolutions: channel).CaptureState();

        var restoredChannel = new DeferredActionResolutions();
        new CommandDispatchPhase([], resolutions: restoredChannel).RestoreState(state);

        Assert.True(restoredChannel.IsPending(9));
    }

    [Fact]
    public void CheckpointWithPendingStepsRequiresAConfiguredChannel()
    {
        var channel = new DeferredActionResolutions();
        channel.Expect(9, "macro:9:0:Fabriquer", Step(MacroStepKind.Craft, itemTypeId: 3, quantity: 1));
        var state = new CommandDispatchPhase([], resolutions: channel).CaptureState();

        Assert.Throws<JsonException>(() => new CommandDispatchPhase([]).RestoreState(state));
    }

    private static MacroActionStep Step(MacroStepKind kind, ushort itemTypeId, int quantity) =>
        new(kind, default, 0, itemTypeId, quantity);

    private static CommandExecutionResult Success() =>
        CommandExecutionResult.Success(JsonSerializer.SerializeToElement(
            new { produced = new[] { new { itemTypeId = (ushort)10, quantity = 1 } } },
            ProtocolJson.Options));

    private static OrderedWorldHost CreateHost(
        DeferredActionResolutions channel,
        params IWorldCommandHandler[] handlers) =>
        new(Enum.GetValues<WorldTickPhase>()
            .Select(phase => (IWorldTickPhaseHandler)(phase == WorldTickPhase.StartOrCancelActions
                ? new CommandDispatchPhase(handlers, resolutions: channel)
                : new NoOpPhase(phase))));

    private sealed class NoOpPhase(WorldTickPhase phase) : IWorldTickPhaseHandler
    {
        public WorldTickPhase Phase => phase;

        public void Execute(WorldTickContext context) => ArgumentNullException.ThrowIfNull(context);
    }

    private sealed class DeferredHandler(string kind) : IWorldCommandHandler
    {
        public string Kind { get; } = kind;

        public CommandExecutionResult Execute(CommandEnvelope command, WorldTickContext context) =>
            CommandExecutionResult.Scheduled(JsonSerializer.SerializeToElement(
                new
                {
                    accepted = true,
                    durationTicks = 2,
                    busyUntilTick = command.Tick + 2,
                },
                ProtocolJson.Options));
    }

    private sealed class CancelHandler(string kind) : IWorldCommandHandler
    {
        public string Kind { get; } = kind;

        public CommandExecutionResult Execute(CommandEnvelope command, WorldTickContext context) =>
            CommandExecutionResult.Failure("nothing_to_cancel");
    }
}
