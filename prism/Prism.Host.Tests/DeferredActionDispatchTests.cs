using System.Text.Json;
using Prism.Contracts;
using Prism.Host.Ticking;

namespace Prism.Host.Tests;

public sealed class DeferredActionDispatchTests
{
    private static string Protocol => ProtocolVersion.Current.ToString();

    [Fact]
    public void DeferredActionEmitsStartedThenCompletesAtItsResolveTick()
    {
        var handler = new DeferredHandler("Fabriquer");
        var host = CreateHost(handler);
        using var payload = JsonDocument.Parse("""{"recipeId":1}""");

        var started = host.Step(new TickRequest(
            Protocol,
            0,
            [new CommandEnvelope(Protocol, 0, "craft-1", 42, "Fabriquer", payload.RootElement.Clone())]));
        Assert.Contains(started.Events, entry => entry.Kind == "action_started" && CommandId(entry) == "craft-1");
        Assert.DoesNotContain(started.Events, entry => entry.Kind == "action_completed");
        Assert.Equal(1, handler.Scheduled);
        Assert.Equal(0, handler.Resolved);

        var waiting = host.Step(new TickRequest(Protocol, 1, []));
        Assert.Empty(waiting.Events);
        Assert.Equal(0, handler.Resolved);

        var completed = host.Step(new TickRequest(Protocol, 2, []));
        var completion = Assert.Single(completed.Events);
        Assert.Equal("action_completed", completion.Kind);
        Assert.Equal("craft-1", CommandId(completion));
        Assert.Equal(1, handler.Resolved);
        Assert.Equal(1, handler.Scheduled);
    }

    [Fact]
    public void PendingDeferredActionSurvivesCheckpointRestore()
    {
        var handler = new DeferredHandler("Fabriquer");
        var host = CreateHost(handler);
        using var payload = JsonDocument.Parse("""{"recipeId":1}""");
        host.Step(new TickRequest(
            Protocol,
            0,
            [new CommandEnvelope(Protocol, 0, "craft-1", 42, "Fabriquer", payload.RootElement.Clone())]));
        var checkpoint = host.CaptureCheckpoint();

        var restoredHandler = new DeferredHandler("Fabriquer");
        var restored = CreateHost(restoredHandler);
        restored.RestoreCheckpoint(checkpoint);
        restored.Step(new TickRequest(Protocol, 1, []));
        var completed = restored.Step(new TickRequest(Protocol, 2, []));

        Assert.Contains(completed.Events, entry => entry.Kind == "action_completed" && CommandId(entry) == "craft-1");
        Assert.Equal(1, restoredHandler.Resolved);
    }

    [Fact]
    public void AnnulerCancelsAPendingDeferredAction()
    {
        var craft = new DeferredHandler("Fabriquer");
        var cancel = new CancelHandler("Annuler");
        var host = CreateHost(craft, cancel);
        using var payload = JsonDocument.Parse("""{"recipeId":1}""");
        using var empty = JsonDocument.Parse("{}");
        host.Step(new TickRequest(
            Protocol,
            0,
            [new CommandEnvelope(Protocol, 0, "craft-1", 42, "Fabriquer", payload.RootElement.Clone())]));

        var cancelled = host.Step(new TickRequest(
            Protocol,
            1,
            [new CommandEnvelope(Protocol, 1, "cancel-1", 42, "Annuler", empty.RootElement.Clone())]));

        Assert.Contains(cancelled.Events, entry => entry.Kind == "action_completed" && CommandId(entry) == "cancel-1");
        var later = host.Step(new TickRequest(Protocol, 2, []));
        Assert.DoesNotContain(later.Events, entry => CommandId(entry) == "craft-1");
        Assert.Equal(0, craft.Resolved);
    }

    private static OrderedWorldHost CreateHost(params IWorldCommandHandler[] handlers) =>
        new(Enum.GetValues<WorldTickPhase>()
            .Select(phase => (IWorldTickPhaseHandler)(phase == WorldTickPhase.StartOrCancelActions
                ? new CommandDispatchPhase(handlers)
                : new NoOpPhase(phase))));

    private static string? CommandId(EventEnvelope entry) =>
        entry.Payload.TryGetProperty("commandId", out var commandId)
            ? commandId.GetString()
            : null;

    private sealed class NoOpPhase(WorldTickPhase phase) : IWorldTickPhaseHandler
    {
        public WorldTickPhase Phase => phase;

        public void Execute(WorldTickContext context) => ArgumentNullException.ThrowIfNull(context);
    }

    private sealed class DeferredHandler(string kind) : IWorldCommandHandler
    {
        public string Kind { get; } = kind;
        public int Scheduled { get; private set; }
        public int Resolved { get; private set; }

        public CommandExecutionResult Execute(CommandEnvelope command, WorldTickContext context)
        {
            Scheduled++;
            return CommandExecutionResult.Scheduled(JsonSerializer.SerializeToElement(
                new { accepted = true, durationTicks = 2, busyUntilTick = command.Tick + 2 },
                ProtocolJson.Options));
        }

        public CommandExecutionResult ResolveDeferred(CommandEnvelope command, WorldTickContext context)
        {
            Resolved++;
            return CommandExecutionResult.Success(
                JsonSerializer.SerializeToElement(new { crafted = true }, ProtocolJson.Options));
        }
    }

    private sealed class CancelHandler(string kind) : IWorldCommandHandler
    {
        public string Kind { get; } = kind;

        public CommandExecutionResult Execute(CommandEnvelope command, WorldTickContext context) =>
            CommandExecutionResult.Failure("nothing_to_cancel");
    }
}
