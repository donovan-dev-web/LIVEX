using Prism.Contracts;
using Prism.Host.Ticking;
using System.Text.Json;

namespace Prism.Host.Tests;

public sealed class WorldTickHostTests
{
    [Fact]
    public void StepExecutesEveryPhaseInSpecificationOrderAndAdvancesOneTick()
    {
        var executedPhases = new List<WorldTickPhase>();
        var phases = Enum.GetValues<WorldTickPhase>()
            .Reverse()
            .Select(phase => new RecordingPhase(phase, executedPhases))
            .ToArray();
        var host = new OrderedWorldHost(phases);
        var request = new TickRequest(ProtocolVersion.Current.ToString(), 0, []);

        var result = host.Step(request);

        Assert.Equal(Enum.GetValues<WorldTickPhase>(), executedPhases);
        Assert.Equal(0, result.Tick);
        Assert.Equal(0, host.LastCompletedTick);
    }

    [Fact]
    public void StepRejectsUnexpectedTickWithoutAdvancingTheHost()
    {
        var host = CreateHost([]);
        var skippedTick = new TickRequest(ProtocolVersion.Current.ToString(), 1, []);

        Assert.Throws<ArgumentException>(() => host.Step(skippedTick));
        Assert.Equal(-1, host.LastCompletedTick);
        Assert.Equal(0, host.Step(new TickRequest(ProtocolVersion.Current.ToString(), 0, [])).Tick);
    }

    [Fact]
    public void FailedPhaseFaultsHostAndPreventsPartialWorldFromAdvancing()
    {
        var phases = Enum.GetValues<WorldTickPhase>()
            .Select(phase => new RecordingPhase(
                phase,
                [],
                phase == WorldTickPhase.ApplyWrites
                    ? _ => throw new InvalidOperationException("phase failed")
                    : null))
            .ToArray();
        var host = new OrderedWorldHost(phases);
        var request = new TickRequest(ProtocolVersion.Current.ToString(), 0, []);

        Assert.Throws<InvalidOperationException>(() => host.Step(request));
        Assert.Equal(-1, host.LastCompletedTick);
        Assert.Throws<InvalidOperationException>(() => host.Step(request));
    }

    [Fact]
    public void ConstructorRequiresExactlyOneHandlerPerPhase()
    {
        var phases = Enum.GetValues<WorldTickPhase>()
            .Select(phase => new RecordingPhase(phase, []))
            .ToArray();

        Assert.Throws<ArgumentException>(() => new OrderedWorldHost(phases[..^1]));
        Assert.Throws<ArgumentException>(() => new OrderedWorldHost([.. phases, phases[0]]));
        Assert.Throws<ArgumentException>(() => new OrderedWorldHost([.. phases, null!]));
    }

    [Fact]
    public void RestoreAcceptsCheckpointCreatedBeforeOptionalConstructionStateWasAdded()
    {
        var sourceDispatch = new CommandDispatchPhase([]);
        var source = new OrderedWorldHost(Enum.GetValues<WorldTickPhase>()
            .Select(phase => phase switch
            {
                WorldTickPhase.Receive => (IWorldTickPhaseHandler)sourceDispatch.ReceivePhase,
                WorldTickPhase.StartOrCancelActions => sourceDispatch,
                _ => new RecordingPhase(phase, []),
            }));
        _ = source.Step(new TickRequest(ProtocolVersion.Current.ToString(), 0, []));
        var checkpoint = source.CaptureCheckpoint();

        var targetDispatch = new CommandDispatchPhase([]);
        var target = new OrderedWorldHost(Enum.GetValues<WorldTickPhase>()
            .Select(phase => phase switch
            {
                WorldTickPhase.Receive => (IWorldTickPhaseHandler)targetDispatch.ReceivePhase,
                WorldTickPhase.StartOrCancelActions => targetDispatch,
                WorldTickPhase.WearExpiryAndConstruction => new StructureConstructionTickPhase(null),
                _ => new RecordingPhase(phase, []),
            }));

        target.RestoreCheckpoint(checkpoint);

        Assert.Equal(0, target.LastCompletedTick);
        Assert.Equal(1, target.Step(new TickRequest(ProtocolVersion.Current.ToString(), 1, [])).Tick);
    }

    [Fact]
    public void CommandDispatchRunsHandlersOnceAndReplaysPriorOutcomeOnRetry()
    {
        using var payload = JsonDocument.Parse("""{"amount":3}""");
        var handler = new CountingCommandHandler("Transferer");
        var dispatch = new CommandDispatchPhase([handler]);
        var phases = Enum.GetValues<WorldTickPhase>()
            .Select(phase => phase switch
            {
                WorldTickPhase.Receive => (IWorldTickPhaseHandler)dispatch.ReceivePhase,
                WorldTickPhase.StartOrCancelActions => dispatch,
                _ => new RecordingPhase(phase, []),
            })
            .ToArray();
        var host = new OrderedWorldHost(phases);
        var firstRequest = new TickRequest(
            ProtocolVersion.Current.ToString(),
            0,
            [
                new CommandEnvelope("1.0.0", 0, "transfer-1", 42, "Transferer", payload.RootElement.Clone()),
                new CommandEnvelope("1.0.0", 0, "transfer-1", 42, "Transferer", payload.RootElement.Clone()),
            ]);
        var retryRequest = firstRequest with
        {
            Tick = 1,
            Commands = [firstRequest.Commands[0] with { Tick = 1 }],
        };

        var firstResult = host.Step(firstRequest);
        var retryResult = host.Step(retryRequest);

        Assert.Equal(1, handler.ExecutionCount);
        Assert.Equal("action_completed", firstResult.Events.Single(eventEnvelope => eventEnvelope.Kind == "action_completed").Kind);
        Assert.Equal("action_completed", retryResult.Events.Single(eventEnvelope => eventEnvelope.Kind == "action_completed").Kind);
        Assert.Equal(1, retryResult.Events.Single(eventEnvelope => eventEnvelope.Kind == "action_completed").Tick);
    }

    [Fact]
    public void CommandDispatchRejectsCommandIdReuseWithDifferentPayload()
    {
        using var firstPayload = JsonDocument.Parse("""{"amount":3}""");
        using var conflictingPayload = JsonDocument.Parse("""{"amount":4}""");
        var handler = new CountingCommandHandler("Transferer");
        var dispatch = new CommandDispatchPhase([handler]);
        var phases = Enum.GetValues<WorldTickPhase>()
            .Select(phase => phase switch
            {
                WorldTickPhase.Receive => (IWorldTickPhaseHandler)dispatch.ReceivePhase,
                WorldTickPhase.StartOrCancelActions => dispatch,
                _ => new RecordingPhase(phase, []),
            })
            .ToArray();
        var host = new OrderedWorldHost(phases);
        _ = host.Step(new TickRequest(
            "1.0.0",
            0,
            [new CommandEnvelope("1.0.0", 0, "transfer-1", 42, "Transferer", firstPayload.RootElement.Clone())]));

        var conflict = new TickRequest(
            "1.0.0",
            1,
            [new CommandEnvelope("1.0.0", 1, "transfer-1", 42, "Transferer", conflictingPayload.RootElement.Clone())]);

        Assert.Throws<CommandIdConflictException>(() => host.Step(conflict));
        Assert.Equal(0, host.LastCompletedTick);
        Assert.Equal(
            1,
            host.Step(new TickRequest(
                "1.0.0",
                1,
                [new CommandEnvelope("1.0.0", 1, "transfer-2", 42, "Transferer", firstPayload.RootElement.Clone())]))
                .Events.Count(eventEnvelope => eventEnvelope.Kind == "action_completed"));
        Assert.Equal(2, handler.ExecutionCount);
    }

    [Fact]
    public void ReceivePhaseRejectsConflictingDuplicateIdsBeforeOtherPhasesRun()
    {
        using var firstPayload = JsonDocument.Parse("""{"amount":3}""");
        using var conflictingPayload = JsonDocument.Parse("""{"amount":4}""");
        var handler = new CountingCommandHandler("Transferer");
        var dispatch = new CommandDispatchPhase([handler]);
        var executedPhases = new List<WorldTickPhase>();
        var phases = Enum.GetValues<WorldTickPhase>()
            .Select(phase => phase switch
            {
                WorldTickPhase.Receive => (IWorldTickPhaseHandler)dispatch.ReceivePhase,
                WorldTickPhase.StartOrCancelActions => dispatch,
                _ => new RecordingPhase(phase, executedPhases),
            })
            .ToArray();
        var host = new OrderedWorldHost(phases);
        var request = new TickRequest(
            "1.0.0",
            0,
            [
                new CommandEnvelope("1.0.0", 0, "duplicate", 42, "Transferer", firstPayload.RootElement.Clone()),
                new CommandEnvelope("1.0.0", 0, "duplicate", 42, "Transferer", conflictingPayload.RootElement.Clone()),
            ]);

        Assert.Throws<CommandIdConflictException>(() => host.Step(request));
        Assert.Empty(executedPhases);
        Assert.Equal(-1, host.LastCompletedTick);
        Assert.Equal(
            0,
            host.Step(new TickRequest(
                "1.0.0",
                0,
                [new CommandEnvelope("1.0.0", 0, "valid", 42, "Transferer", firstPayload.RootElement.Clone())])).Tick);
        Assert.Equal(1, handler.ExecutionCount);
    }

    [Fact]
    public void UnknownCommandProducesMotivatedFailureEvent()
    {
        using var payload = JsonDocument.Parse("{}");
        var dispatch = new CommandDispatchPhase([]);
        var phases = Enum.GetValues<WorldTickPhase>()
            .Select(phase => phase switch
            {
                WorldTickPhase.Receive => (IWorldTickPhaseHandler)dispatch.ReceivePhase,
                WorldTickPhase.StartOrCancelActions => dispatch,
                _ => new RecordingPhase(phase, []),
            })
            .ToArray();
        var host = new OrderedWorldHost(phases);
        var request = new TickRequest(
            "1.0.0",
            0,
            [new CommandEnvelope("1.0.0", 0, "unknown", 42, "NotRegistered", payload.RootElement.Clone())]);

        var result = host.Step(request);
        var failureEvent = result.Events.Single(eventEnvelope => eventEnvelope.Kind == "action_failed");

        Assert.Equal("unknown", failureEvent.Payload.GetProperty("commandId").GetString());
        Assert.Equal("unknown_command", failureEvent.Payload.GetProperty("reason").GetString());
    }

    [Fact]
    public void StepSnapshotsRequestBeforeExecutingPhaseHandlers()
    {
        using var payload = JsonDocument.Parse("""{"amount":3}""");
        var commands = new List<CommandEnvelope>
        {
            new("1.0.0", 0, "snapshot", 42, "Transferer", payload.RootElement.Clone()),
        };
        var dispatch = new CommandDispatchPhase([new CountingCommandHandler("Transferer")]);
        var phases = Enum.GetValues<WorldTickPhase>()
            .Select(phase => phase switch
            {
                WorldTickPhase.Receive => (IWorldTickPhaseHandler)dispatch.ReceivePhase,
                WorldTickPhase.Lifecycle => new RecordingPhase(
                    phase,
                    [],
                    _ => commands.Clear()),
                WorldTickPhase.StartOrCancelActions => dispatch,
                _ => new RecordingPhase(phase, []),
            })
            .ToArray();
        var host = new OrderedWorldHost(phases);

        var result = host.Step(new TickRequest("1.0.0", 0, commands));

        Assert.Empty(commands);
        Assert.Contains(result.Events, eventEnvelope => eventEnvelope.Kind == "action_completed");
    }

    private static OrderedWorldHost CreateHost(List<WorldTickPhase> executed) =>
        new(Enum.GetValues<WorldTickPhase>().Select(phase => new RecordingPhase(phase, executed)));

    private sealed class CountingCommandHandler(string kind) : IWorldCommandHandler
    {
        public string Kind => kind;
        public int ExecutionCount { get; private set; }

        public CommandExecutionResult Execute(CommandEnvelope command, WorldTickContext context)
        {
            ExecutionCount++;
            return CommandExecutionResult.Success(command.Payload);
        }
    }

    private sealed class RecordingPhase(
        WorldTickPhase phase,
        List<WorldTickPhase> executedPhases,
        Action<WorldTickContext>? action = null) : IWorldTickPhaseHandler
    {
        public WorldTickPhase Phase => phase;

        public void Execute(WorldTickContext context)
        {
            executedPhases.Add(phase);
            action?.Invoke(context);
            if (phase == WorldTickPhase.Output)
            {
                using var payload = JsonDocument.Parse("""{"ok":true}""");
                context.AddEvent(new EventEnvelope(
                    ProtocolVersion.Current.ToString(),
                    context.Tick,
                    $"tick-complete-{context.Tick}",
                    "tick_completed",
                    null,
                    payload.RootElement.Clone()));
            }
        }
    }
}
