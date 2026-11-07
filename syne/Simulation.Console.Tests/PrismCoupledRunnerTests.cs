using System.Text.Json;
using Prism.Contracts;
using Simulation.Console.Integration;
using Simulation.Core.Perception;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class PrismCoupledRunnerTests
{
    [Fact]
    public async Task RunsAllTicksAndMapsObservations()
    {
        var observed = new List<(ulong AgentId, int Count)>();
        Func<TickRequest, CancellationToken, Task<TickResult>> step = (request, _) =>
        {
            var envelope = new ObservationEnvelope(
                ProtocolVersion.Current.ToString(),
                request.Tick,
                7,
                JsonSerializer.SerializeToElement(new { changes = Array.Empty<object>() }, ProtocolJson.Options),
                JsonSerializer.SerializeToElement(new { position = new { x = 1, y = 2, z = 3 } }, ProtocolJson.Options));
            var result = new TickResult(ProtocolVersion.Current.ToString(), request.Tick, [envelope], []);
            return Task.FromResult(result);
        };

        var runner = new PrismCoupledRunner(step, onObservations: (agentId, obs, _) => observed.Add((agentId, obs.Count)));
        var run = await runner.RunAsync(5);

        Assert.True(run.Succeeded);
        Assert.Equal(5, run.TicksCompleted);
        Assert.Equal(5, observed.Count);
        Assert.All(observed, o => Assert.Equal(7UL, o.AgentId));
    }

    [Fact]
    public async Task StopsOnFailureAndReportsTick()
    {
        Func<TickRequest, CancellationToken, Task<TickResult>> step = (request, _) =>
        {
            if (request.Tick == 2)
            {
                throw new InvalidOperationException("boom");
            }

            var result = new TickResult(ProtocolVersion.Current.ToString(), request.Tick, [], []);
            return Task.FromResult(result);
        };

        var runner = new PrismCoupledRunner(step);
        var run = await runner.RunAsync(5);

        Assert.False(run.Succeeded);
        Assert.Equal(2, run.TicksCompleted);
        Assert.Equal(2, run.FailedAtTick);
        Assert.IsType<InvalidOperationException>(run.Failure);
    }

    [Fact]
    public async Task RejectsMismatchedTick()
    {
        Func<TickRequest, CancellationToken, Task<TickResult>> step = (request, _) =>
        {
            var result = new TickResult(ProtocolVersion.Current.ToString(), request.Tick + 1, [], []);
            return Task.FromResult(result);
        };

        var runner = new PrismCoupledRunner(step);
        var run = await runner.RunAsync(1);

        Assert.False(run.Succeeded);
        Assert.Equal(0, run.TicksCompleted);
    }

    [Fact]
    public async Task ResumesFromStartTick()
    {
        Func<TickRequest, CancellationToken, Task<TickResult>> step = (request, _) =>
        {
            var result = new TickResult(ProtocolVersion.Current.ToString(), request.Tick, [], []);
            return Task.FromResult(result);
        };

        var runner = new PrismCoupledRunner(step);
        var run = await runner.RunAsync(3, startTick: 7);

        Assert.True(run.Succeeded);
        Assert.Equal(7, run.StartTick);
        Assert.Equal(3, run.TicksCompleted);
    }

    [Fact]
    public async Task HandlesLargeTickHorizonWithoutCrash()
    {
        Func<TickRequest, CancellationToken, Task<TickResult>> step = (request, _) =>
        {
            var result = new TickResult(ProtocolVersion.Current.ToString(), request.Tick, [], []);
            return Task.FromResult(result);
        };

        var runner = new PrismCoupledRunner(step);
        var run = await runner.RunAsync(5000);

        Assert.True(run.Succeeded);
        Assert.Equal(5000, run.TicksCompleted);
    }

    [Fact]
    public async Task AttributesARefusalToTheCommandThatCausedIt()
    {
        // Le refus PRISM ne porte qu'un `commandId` : sans attribution, SYNE sait
        // qu'une commande a échoué mais pas laquelle, donc ne peut pas cesser de
        // redemander cette cible.
        var failures = new List<(ulong AgentId, string Kind, string Reason, string Payload)>();
        Func<long, IReadOnlyList<ObservationEnvelope>, IReadOnlyList<CommandEnvelope>> decide =
            (tick, _) =>
            [
                new CommandEnvelope(
                    ProtocolVersion.Current.ToString(),
                    tick,
                    "walk-42",
                    42,
                    "Aller",
                    JsonSerializer.SerializeToElement(new { target = new { x = 5, y = 6, z = 57 } }, ProtocolJson.Options)),
            ];
        Func<TickRequest, CancellationToken, Task<TickResult>> step = (request, _) =>
        {
            EventEnvelope refused = new(
                ProtocolVersion.Current.ToString(),
                request.Tick,
                "walk-42:0",
                "action_failed",
                42,
                JsonSerializer.SerializeToElement(new { commandId = "walk-42", reason = "path_not_found" }, ProtocolJson.Options));
            return Task.FromResult(new TickResult(ProtocolVersion.Current.ToString(), request.Tick, [], [refused]));
        };

        var runner = new PrismCoupledRunner(
            step,
            decide: decide,
            onCommandFailed: (agentId, kind, payload, reason, _) => failures.Add((agentId, kind, reason, payload)));
        var run = await runner.RunAsync(1);

        Assert.True(run.Succeeded);
        var failure = Assert.Single(failures);
        Assert.Equal(42UL, failure.AgentId);
        Assert.Equal("Aller", failure.Kind);
        Assert.Equal("path_not_found", failure.Reason);
        Assert.Contains("\"x\":5", failure.Payload, StringComparison.Ordinal);
    }
}
