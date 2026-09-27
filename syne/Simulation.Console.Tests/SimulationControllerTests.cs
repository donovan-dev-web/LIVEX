using System.Collections.Concurrent;
using System.Text.Json;
using Simulation.Console.Control;
using Simulation.Console.Observability;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class SimulationControllerTests
{
    [Fact]
    public async Task ExplicitPreparationRequiresReadyAcknowledgement()
    {
        await using var controller = new SimulationController();
        await controller.PrepareAsync(42, new SimulationOptions());
        Assert.Equal(SimulationControlState.Ready, controller.State);
        Assert.False(controller.Status().WorldReadyAcknowledged);
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.StartAsync(null, null, 1));
        Assert.True(controller.AcknowledgeReady("1.0"));
        Assert.True(controller.Status().WorldReadyAcknowledged);
        await controller.StartAsync(null, null, 1);
    }

    [Fact]
    public async Task PrepareStoresRequestedTickRateForTheFollowingRun()
    {
        var sink = new RecordingSink();
        await using var controller = new SimulationController(sink);
        var config = new SimulationOptions();
        config.Agents.InitialCount = 0;

        var world = await controller.PrepareAsync(42, config, ticksPerSecond: 2);
        Assert.Equal(2, world.TicksPerSecond);
        Assert.True(controller.AcknowledgeReady("1.0"));
        await controller.StartAsync(42, null, 2);
        await WaitUntilAsync(() => controller.Status().Tick == 1);
        await Task.Delay(150);
        Assert.Equal(1ul, controller.Status().Tick);
        await WaitUntilAsync(() => controller.State == SimulationControlState.Finished);

        var snapshotTicks = new List<int>();
        foreach (string message in sink.Messages.Where(message => message.Contains("\"type\":\"snapshot\"")))
        {
            using JsonDocument document = JsonDocument.Parse(message);
            snapshotTicks.Add(document.RootElement.GetProperty("tick").GetInt32());
        }
        snapshotTicks.Sort();
        Assert.Equal([1, 2], snapshotTicks);
    }

    [Fact]
    public async Task ExplicitPreparationRejectsDifferentStartSeedWithoutReplacingWorld()
    {
        await using var controller = new SimulationController();
        await controller.PrepareAsync(42, new SimulationOptions(), ticksPerSecond: 3);
        Assert.True(controller.AcknowledgeReady("1.0"));

        var exception = await Assert.ThrowsAsync<PreparedWorldMismatchException>(
            () => controller.StartAsync(43, null, 1));
        Assert.Equal("prepared_seed_mismatch", exception.Code);
        Assert.Equal(42ul, controller.Status().Seed);
        Assert.Equal(3, controller.WorldDescription?.TicksPerSecond);

        await controller.StartAsync(42, null, 1);
    }

    [Fact]
    public async Task UsesConfiguredTicksPerSecondAndKeepsRunIdentityInSnapshots()
    {
        var sink = new RecordingSink();
        await using var controller = new SimulationController(sink);
        var config = new SimulationOptions();
        config.Agents.InitialCount = 1;
        config.Simulation.TicksPerSecond = 2;

        string runId = await controller.StartAsync(seed: 42, config: config, maxTicks: 2);

        await Task.Delay(150);
        Assert.InRange(controller.Status().Tick, 1ul, 1ul);

        await WaitUntilAsync(() => controller.State == SimulationControlState.Finished);
        Assert.Equal(runId, controller.Status().RunId);

        string snapshot = sink.Messages.First(message => message.Contains("\"type\":\"snapshot\""));
        using JsonDocument document = JsonDocument.Parse(snapshot);
        Assert.Equal(runId, document.RootElement.GetProperty("runId").GetString());
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "La condition n'a pas été satisfaite dans le délai imparti.");
    }

    private sealed class RecordingSink : IObservabilitySink
    {
        public ConcurrentBag<string> Messages { get; } = new();

        public Task BroadcastAsync(string text)
        {
            Messages.Add(text);
            return Task.CompletedTask;
        }
    }
}
