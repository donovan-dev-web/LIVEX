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
