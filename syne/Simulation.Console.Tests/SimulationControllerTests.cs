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
        await controller.PrepareAsync(42, ConfigLoader.ToJson(new SimulationOptions()));
        Assert.Equal(SimulationControlState.Ready, controller.State);
        Assert.False(controller.Status().WorldReadyAcknowledged);
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.StartAsync(null, null, 1));
        Assert.True(controller.AcknowledgeReady(Simulation.Core.World.WorldDescriptionBuilder.Version));
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

        var world = await controller.PrepareAsync(42, ConfigLoader.ToJson(config), ticksPerSecond: 2);
        Assert.Equal(2, world.TicksPerSecond);
        Assert.True(controller.AcknowledgeReady(Simulation.Core.World.WorldDescriptionBuilder.Version));
        DateTime startedAt = DateTime.UtcNow;
        await controller.StartAsync(42, null, 2);
        await WaitUntilAsync(() => controller.State == SimulationControlState.Finished);
        AssertRunTookAtLeastTwoTickIntervals(startedAt);

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
        await controller.PrepareAsync(42, ConfigLoader.ToJson(new SimulationOptions()), ticksPerSecond: 3);
        Assert.True(controller.AcknowledgeReady(Simulation.Core.World.WorldDescriptionBuilder.Version));

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

        string runId = await controller.StartAsync(seed: 42, configJson: ConfigLoader.ToJson(config), maxTicks: 2);
        DateTime startedAt = DateTime.UtcNow;

        await WaitUntilAsync(() => controller.State == SimulationControlState.Finished);
        AssertRunTookAtLeastTwoTickIntervals(startedAt);
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

    /// <summary>
    /// La cadence demandée (2 t/s) a bien été celle du run : deux ticks valent
    /// deux intervalles de 500 ms, soit ≈ 1 s — un run à la cadence par défaut
    /// (10 t/s) finirait en ≈ 200 ms.
    /// </summary>
    /// <remarks>
    /// Mesuré sur le run entier, ancré sur <c>start</c>, et non plus par une
    /// fenêtre fixe de 150 ms après l'observation du tick 1 : sous charge (runner
    /// CI), la reprise du fil de test ou du <c>Task.Delay</c> peut être tardive,
    /// et le tick 2 — légitimement arrivé 500 ms après le premier — tombait alors
    /// dans la fenêtre et faisait échouer le banc sans anomalie de cadence
    /// (constaté sur `windows-latest`, job SYNE .NET).
    /// </remarks>
    private static void AssertRunTookAtLeastTwoTickIntervals(DateTime startedAt)
    {
        TimeSpan elapsed = DateTime.UtcNow - startedAt;
        Assert.True(
            elapsed >= TimeSpan.FromMilliseconds(900),
            $"cadence trop rapide : {elapsed.TotalMilliseconds:F0} ms pour 2 ticks à 2 t/s " +
            "(au moins deux intervalles de 500 ms attendus)");
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
