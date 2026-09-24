using System.Text.Json.Nodes;
using Simulation.Console.Observability;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Prng;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Console.Tests;

/// <summary>
/// Cycle de saisons à travers l'émetteur (SYNE-072, jalon U8) : quand
/// <c>world.seasons.enabled</c>, un changement de saison est drainé en événement
/// <c>world.season_changed</c> au tick exact du basculement et le snapshot porte
/// les champs <c>season</c>/<c>seasonIndex</c> — sans rompre le déterminisme.
/// Cycle désactivé (défaut) : aucun événement saisonnier.
/// </summary>
public class ObservabilitySeasonTests
{
    private sealed class RecordingSink : IObservabilitySink
    {
        public List<string> Frames { get; } = new();

        public Task BroadcastAsync(string text)
        {
            Frames.Add(text);
            return Task.CompletedTask;
        }
    }

    private static SimulationLoop BuildScenario(ulong seed, SimulationOptions options)
    {
        var world = new WorldType(new WorldSize(250, 250), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);

        for (ulong i = 1; i <= 3; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, options);
    }

    private static SimulationOptions OptionsWithShortCycle(int length)
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.World.Seasons.Enabled = true;
        options.World.Seasons.SeasonLengthTicks = length;
        return options;
    }

    private static JsonNode ParseFrame(string frame) =>
        JsonNode.Parse(frame) ?? throw new InvalidOperationException("trame JSON invalide");

    [Fact]
    public async Task SeasonChange_IsEmittedExactlyAtTheBoundaryTick_AndDrained()
    {
        var sink = new RecordingSink();
        SimulationLoop loop = BuildScenario(seed: 7, options: OptionsWithShortCycle(length: 4));
        var emitter = new ObservabilityTickEmitter(loop, seed: 7, sink);

        for (int tick = 1; tick <= 3; tick++)
        {
            loop.AdvanceOneTick();
            await emitter.EmitCurrentTickAsync();
        }

        Assert.Empty(loop.LastSeasonChanges);

        // Au tick 4, on bascule spring → summer (tick 4 / 4 = 1) : l'événement
        // world.season_changed est émis exactement au tick du basculement.
        loop.AdvanceOneTick();
        await emitter.EmitCurrentTickAsync();

        var seasonEvents = sink.Frames.Select(ParseFrame)
            .Where(node => node!["type"]!.GetValue<string>() == ObservabilityContract.SeasonChanged)
            .ToList();
        JsonNode seasonEvent = Assert.Single(seasonEvents);
        Assert.Equal(4UL, seasonEvent["tick"]!.GetValue<ulong>());
        Assert.Equal("spring", (string?)seasonEvent["value"]!["previous"]);
        Assert.Equal("summer", (string?)seasonEvent["value"]!["current"]);
        Assert.Equal("summer", (string?)seasonEvent["targetId"]);

        Assert.Empty(loop.LastSeasonChanges);
    }

    [Fact]
    public async Task Snapshot_CarriesSeasonThroughTheEmitter()
    {
        var sink = new RecordingSink();
        SimulationLoop loop = BuildScenario(seed: 7, options: OptionsWithShortCycle(length: 3));
        var emitter = new ObservabilityTickEmitter(loop, seed: 7, sink);

        loop.AdvanceOneTick();
        await emitter.EmitCurrentTickAsync();

        var snapshots = sink.Frames.Select(ParseFrame)
            .Where(node => node!["type"]!.GetValue<string>() == ObservabilityContract.SnapshotType)
            .ToList();
        JsonNode snapshot = Assert.Single(snapshots);
        Assert.Equal("spring", (string?)snapshot["season"]);
        Assert.Equal(0, snapshot["seasonIndex"]!.GetValue<int>());
        Assert.Equal("0.10.0", (string?)snapshot["engineVersion"]);
    }

    [Fact]
    public async Task SeasonsDisabled_EmitNoSeasonEvent()
    {
        var sink = new RecordingSink();
        SimulationLoop loop = BuildScenario(seed: 7, options: ConfigLoader.LoadDefaults());
        var emitter = new ObservabilityTickEmitter(loop, seed: 7, sink);

        for (int tick = 1; tick <= 30; tick++)
        {
            loop.AdvanceOneTick();
            await emitter.EmitCurrentTickAsync();
        }

        Assert.DoesNotContain(sink.Frames.Select(ParseFrame),
            node => node!["type"]!.GetValue<string>() == ObservabilityContract.SeasonChanged);
    }
}