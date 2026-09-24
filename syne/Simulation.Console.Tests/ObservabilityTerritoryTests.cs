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
/// Territoire à travers l'émetteur (SYNE-073, jalon U8) : quand
/// <c>world.territories.enabled</c>, l'appartenance aux zones (« points de
/// survie », décision n°21) est drainée en <c>world.territory_membership_changed</c>
/// et le snapshot porte le champ <c>territories[]</c> — sans rompre le déterminisme.
/// Suivi désactivé (défaut) : aucun événement de territoire.
/// </summary>
public class ObservabilityTerritoryTests
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
        var world = new WorldType(new WorldSize(500, 500), spatialCellSize: 50);
        world.ApplyConfiguredLayout(options.World);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);

        for (ulong i = 1; i <= 3; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, options);
    }

    private static SimulationOptions OptionsWithOneGiantZone()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.World.Territories.Enabled = true;
        options.World.Territories.Zones.Add(new TerritoryZoneDefinition
        {
            Id = "camp",
            CenterX = 250,
            CenterY = 250,
            Radius = 800,
        });
        return options;
    }

    private static JsonNode ParseFrame(string frame) =>
        JsonNode.Parse(frame) ?? throw new InvalidOperationException("trame JSON invalide");

    private static IEnumerable<JsonNode> EventsOf(IEnumerable<string> frames, string type) =>
        frames.Select(ParseFrame)
            .Where(node => node!["type"]!.GetValue<string>() == type);

    [Fact]
    public async Task MembershipChanges_AreEmittedThroughTheEmitter_AndSnapshotCarriesTerritories()
    {
        var sink = new RecordingSink();
        SimulationLoop loop = BuildScenario(seed: 7, options: OptionsWithOneGiantZone());
        var emitter = new ObservabilityTickEmitter(loop, seed: 7, sink);

        loop.AdvanceOneTick();
        await emitter.EmitCurrentTickAsync();

        // Les 3 entités sont nées dans le disque du « camp » : elles entrent au
        // tick 1 (appartenance précédente vide) — un événement par franchissement,
        // drainé par l'émetteur.
        var membershipEvents = EventsOf(sink.Frames, ObservabilityContract.TerritoryMembershipChanged).ToList();
        Assert.Equal(3, membershipEvents.Count);
        Assert.All(membershipEvents, node =>
        {
            Assert.Equal(1UL, node!["tick"]!.GetValue<ulong>());
            Assert.Equal("camp", (string?)node["targetId"]);
            Assert.Equal("entered", (string?)node["value"]!["kind"]);
        });
        Assert.Equal(new[] { "1", "2", "3" }, membershipEvents.Select(node => (string?)node!["agentId"]).OrderBy(id => id));

        Assert.Empty(loop.LastTerritoryChanges);

        // Le snapshot du même tick porte le territoire effectif (décision n°21 :
        // la présence délimite le territoire).
        var snapshots = EventsOf(sink.Frames, ObservabilityContract.SnapshotType).ToList();
        JsonNode snapshot = Assert.Single(snapshots);
        JsonArray territories = (JsonArray)snapshot["territories"]!;
        JsonObject zone = (JsonObject)territories[0]!;
        Assert.Equal("camp", (string?)zone["id"]);
        Assert.Equal(3, zone["memberCount"]!.GetValue<int>());
        var members = (JsonArray)zone["members"]!;
        Assert.Equal([1UL, 2UL, 3UL], members.Select(member => member!.GetValue<ulong>()));
        Assert.Equal("0.11.0", (string?)snapshot["engineVersion"]);
    }

    [Fact]
    public async Task Disabled_NoTerritoryEventAndEmptySnapshot()
    {
        var sink = new RecordingSink();
        SimulationLoop loop = BuildScenario(seed: 7, options: ConfigLoader.LoadDefaults());
        var emitter = new ObservabilityTickEmitter(loop, seed: 7, sink);

        for (int tick = 1; tick <= 30; tick++)
        {
            loop.AdvanceOneTick();
            await emitter.EmitCurrentTickAsync();
        }

        Assert.DoesNotContain(EventsOf(sink.Frames, ObservabilityContract.TerritoryMembershipChanged), _ => true);

        var snapshots = EventsOf(sink.Frames, ObservabilityContract.SnapshotType).ToList();
        JsonArray territories = (JsonArray)snapshots[0]["territories"]!;
        Assert.Empty(territories);
    }

    [Fact]
    public async Task BookChanges_AreEmittedThroughTheEmitter_AndSnapshotCarriesBooks()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.World.Books.Enabled = true;
        options.World.Books.WriteCostEnergy = 4.0;
        options.World.Books.ReadBenefit = 1.5;
        var sink = new RecordingSink();
        SimulationLoop loop = BuildScenario(seed: 11, options);
        loop.AdvanceOneTick();
        var book = new Simulation.Core.World.Book("book-1", 1, "Field notes", "A discovery.");
        loop.WriteBook(book);
        loop.ReadBook(book, 2);

        await new ObservabilityTickEmitter(loop, seed: 11, sink).EmitCurrentTickAsync();

        JsonNode snapshot = Assert.Single(EventsOf(sink.Frames, ObservabilityContract.SnapshotType));
        JsonObject serializedBook = (JsonObject)snapshot["books"]![0]!;
        Assert.Equal("book-1", (string?)serializedBook["id"]);
        Assert.Equal("A discovery.", (string?)serializedBook["content"]);
        Assert.Equal(1UL, (ulong?)serializedBook["writtenTick"]);
        Assert.Equal(new ulong[] { 2 }, serializedBook["readers"]!.AsArray().Select(n => n!.GetValue<ulong>()));

        JsonNode written = Assert.Single(EventsOf(sink.Frames, ObservabilityContract.BookWritten));
        Assert.Equal("1", (string?)written["agentId"]);
        Assert.Equal("book-1", (string?)written["targetId"]);
        Assert.Equal(4.0, (double?)written["value"]!["cost"]);
        JsonNode read = Assert.Single(EventsOf(sink.Frames, ObservabilityContract.BookRead));
        Assert.Equal("2", (string?)read["agentId"]);
        Assert.Equal(1.5, (double?)read["value"]!["readBenefit"]);
        Assert.Empty(loop.LastBookChanges);
        Assert.Equal("0.11.0", (string?)snapshot["engineVersion"]);
    }

}