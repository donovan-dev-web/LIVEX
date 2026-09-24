using System.Text;
using System.Text.Json.Nodes;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Prng;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Territoire (SYNE-073, décision n°21, jalon U8) : zones « point de survie »
/// (disques) posées à l'init — la **présence** d'une entité dans le disque délimite
/// le territoire effectif. L'appartenance est suivie en fin de tick par la boucle
/// (0 tirage PRNG, DETERMINISM.md §3), tracée en <c>world.territory_membership_changed</c>
/// et exposée dans le snapshot <c>territories[]</c> (additif). Suivi désactivé par
/// défaut ⇒ trajectoire du scénario de référence inchangée.
/// </summary>
public class TerritoryTests
{
    private static EntityTemplate InertTemplate() => new(
        "Inerte",
        TraitSet.TraitNames.ToDictionary(name => name, _ => (Min: 0.0, Max: 0.0), StringComparer.Ordinal));

    private static (string Id, double X, double Y, double Radius) Zone(string id, double x, double y, double radius) =>
        (id, x, y, radius);

    private static SimulationOptions OptionsWithTerritories(params (string Id, double X, double Y, double Radius)[] zones)
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.World.Territories.Enabled = zones.Length > 0;
        foreach ((string id, double x, double y, double radius) in zones)
        {
            options.World.Territories.Zones.Add(new TerritoryZoneDefinition
            {
                Id = id,
                CenterX = x,
                CenterY = y,
                Radius = radius,
            });
        }

        return options;
    }

    private static SimulationLoop Build(
        ulong seed,
        SimulationOptions options,
        int count = 2,
        bool inert = true)
    {
        var world = new WorldType(new WorldSize(500, 500), spatialCellSize: 50);
        world.ApplyConfiguredLayout(options.World);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= (ulong)count; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(inert ? InertTemplate() : EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, options);
    }

    private static string MembershipLog(SimulationLoop loop) =>
        string.Join(",", loop.World.Territories.Select(zone => $"{zone.Id}:[{string.Join(";", loop.MembersOfTerritory(zone.Id))}]"));

    // ------------------------------------------------------------------
    // Layout des zones (init, ordre de pose stable)
    // ------------------------------------------------------------------

    [Fact]
    public void Layout_AppliesZonesFromConfig_InStableOrder()
    {
        SimulationLoop loop = Build(
            seed: 7,
            options: OptionsWithTerritories(Zone("camp", 250, 250, 40), Zone("foret", 100, 400, 25)),
            count: 1);

        Assert.Equal(["camp", "foret"], loop.World.Territories.Select(zone => zone.Id));
        Assert.Equal(250.0, loop.World.Territories[0].Center.X);
        Assert.Equal(40.0, loop.World.Territories[0].Radius);
        Assert.True(loop.TerritoriesEnabled);
    }

    [Fact]
    public void Layout_Disabled_NoZones()
    {
        SimulationLoop loop = Build(seed: 7, options: ConfigLoader.LoadDefaults(), count: 1);

        Assert.Empty(loop.World.Territories);
        Assert.False(loop.TerritoriesEnabled);
    }

    [Fact]
    public void AddTerritory_RejectsOutOfWorldCenter_AndDuplicateId()
    {
        var world = new WorldType(new WorldSize(500, 500), spatialCellSize: 50);

        ArgumentOutOfRangeException outOfWorld = Assert.Throws<ArgumentOutOfRangeException>(() =>
            world.AddTerritory(new Territory("z", new World.Position(6000, 10), 5)));
        Assert.Contains("sort du monde", outOfWorld.Message);

        world.AddTerritory(new Territory("z", new World.Position(10, 10), 5));
        ArgumentException duplicate = Assert.Throws<ArgumentException>(() =>
            world.AddTerritory(new Territory("z", new World.Position(30, 30), 5)));
        Assert.Contains("existe déjà", duplicate.Message);
    }

    // ------------------------------------------------------------------
    // Appartenance = présence (0 PRNG)
    // ------------------------------------------------------------------

    [Fact]
    public void Membership_ReflectsPresence_WithOrderedIds()
    {
        // Zone « camp » : centre (250, 250), rayon 50. e1 (260, 260) dedans,
        // e2 (100, 100) dehors, e3 (250, 300) sur le bord (inclus, <= rayon).
        SimulationLoop loop = Build(seed: 7, options: OptionsWithTerritories(Zone("camp", 250, 250, 50)), count: 3);
        loop.World.Entities[0].SetPosition(new World.Position(260, 260));
        loop.World.Entities[1].SetPosition(new World.Position(100, 100));
        loop.World.Entities[2].SetPosition(new World.Position(250, 300));

        loop.Run(3);

        Assert.Equal([1UL, 3UL], loop.MembersOfTerritory("camp"));
        Assert.Empty(loop.MembersOfTerritory("inconnu"));
    }

    [Fact]
    public void MembershipChanges_TracedEnterThenLeft_DeterministicOrder()
    {
        SimulationLoop loop = Build(seed: 7, options: OptionsWithTerritories(Zone("camp", 250, 250, 50)), count: 1);
        loop.World.Entities[0].SetPosition(new World.Position(100, 100));

        loop.AdvanceOneTick();
        Assert.Empty(loop.LastTerritoryChanges);

        loop.World.Entities[0].SetPosition(new World.Position(260, 260));
        loop.AdvanceOneTick();

        TerritoryMembershipChange entered = Assert.Single(loop.LastTerritoryChanges);
        Assert.Equal(TerritoryMembershipChangeKind.Entered, entered.Kind);
        Assert.Equal("camp", entered.Zone.Id);
        Assert.Equal(1UL, entered.EntityId);
        loop.ClearTerritoryChanges();

        loop.World.Entities[0].SetPosition(new World.Position(100, 100));
        loop.AdvanceOneTick();

        TerritoryMembershipChange left = Assert.Single(loop.LastTerritoryChanges);
        Assert.Equal(TerritoryMembershipChangeKind.Left, left.Kind);
        Assert.Equal("camp", left.Zone.Id);
        Assert.Equal(1UL, left.EntityId);
    }

    [Fact]
    public void Changes_AccumulateUntilDrained()
    {
        SimulationLoop loop = Build(seed: 7, options: OptionsWithTerritories(Zone("camp", 250, 250, 50)), count: 1);
        loop.World.Entities[0].SetPosition(new World.Position(100, 100));
        loop.AdvanceOneTick();

        loop.World.Entities[0].SetPosition(new World.Position(260, 260));
        loop.AdvanceOneTick();
        loop.World.Entities[0].SetPosition(new World.Position(240, 240));
        loop.AdvanceOneTick();

        Assert.Single(loop.LastTerritoryChanges);

        loop.ClearTerritoryChanges();
        Assert.Empty(loop.LastTerritoryChanges);
    }

    [Fact]
    public void Disabled_NoMembershipAndNoChanges()
    {
        SimulationLoop loop = Build(seed: 7, options: ConfigLoader.LoadDefaults(), count: 2);

        loop.Run(5);

        Assert.Empty(loop.LastTerritoryChanges);
        Assert.Empty(loop.World.Territories);
    }

    // ------------------------------------------------------------------
    // Déterminisme (0 PRNG, ordre stable)
    // ------------------------------------------------------------------

    [Fact]
    public void Determinism_IdenticalRuns_IdenticalMembershipTrajectory()
    {
        static string Log(ulong seed)
        {
            SimulationLoop loop = Build(seed: seed, options: OptionsWithTerritories(Zone("camp", 250, 250, 60)), count: 5, inert: false);
            var log = new StringBuilder();
            for (ulong i = 1; i <= 200; i++)
            {
                loop.AdvanceOneTick();
                log.Append(loop.CurrentTick).Append('[').Append(MembershipLog(loop)).Append(']');
                foreach (Simulation.Core.World.TerritoryMembershipChange change in loop.LastTerritoryChanges)
                {
                    log.Append(change.Kind).Append(':').Append(change.Zone.Id).Append(':').Append(change.EntityId).Append(';');
                }
            }

            return Convert.ToHexString(Encoding.UTF8.GetBytes(log.ToString()));
        }

        // Deux graines identiques ⇒ trajectoire bit-à-bit identique ;
        // deux graines différentes ⇒ trajectoires (très probablement) distinctes.
        string first = Log(12_345);
        string second = Log(12_345);
        string other = Log(99_999);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    // ------------------------------------------------------------------
    // Observabilité : snapshot territories[] + événement typé
    // ------------------------------------------------------------------

    [Fact]
    public void Snapshot_IncludesTerritories_WhenEnabled()
    {
        SimulationLoop loop = Build(seed: 7, options: OptionsWithTerritories(Zone("camp", 250, 250, 50)), count: 3);
        loop.World.Entities[0].SetPosition(new World.Position(260, 260));
        loop.World.Entities[1].SetPosition(new World.Position(100, 100));
        loop.World.Entities[2].SetPosition(new World.Position(250, 300));
        loop.Run(2);

        WorldSnapshot snapshot = WorldSnapshot.Capture(loop, seed: 7);

        TerritorySnapshot zone = Assert.Single(snapshot.Territories);
        Assert.Equal("camp", zone.Id);
        Assert.Equal(250, zone.X);
        Assert.Equal(250, zone.Y);
        Assert.Equal(50, zone.Radius);
        Assert.Equal(2, zone.MemberCount);
        Assert.Equal([1UL, 3UL], zone.Members);
    }

    [Fact]
    public void Snapshot_Territories_EmptyWhenDisabled()
    {
        SimulationLoop loop = Build(seed: 7, options: ConfigLoader.LoadDefaults(), count: 2);
        loop.Run(2);

        WorldSnapshot snapshot = WorldSnapshot.Capture(loop, seed: 7);
        Assert.Empty(snapshot.Territories);
    }

    [Fact]
    public void Serializer_EmitsTerritoriesArray()
    {
        SimulationLoop loop = Build(seed: 7, options: OptionsWithTerritories(Zone("camp", 250, 250, 50)), count: 2);
        loop.World.Entities[0].SetPosition(new World.Position(260, 260));
        loop.World.Entities[1].SetPosition(new World.Position(100, 100));
        loop.Run(2);

        JsonObject frame = ObservabilitySerializer.SnapshotMessage(WorldSnapshot.Capture(loop, seed: 7));
        JsonArray territories = (JsonArray)frame["territories"]!;
        JsonObject zone = (JsonObject)territories[0]!;

        Assert.Equal("camp", (string?)zone["id"]);
        Assert.Equal(250, zone["x"]!.GetValue<double>());
        Assert.Equal(250, zone["y"]!.GetValue<double>());
        Assert.Equal(50, zone["radius"]!.GetValue<double>());
        Assert.Equal(1, zone["memberCount"]!.GetValue<int>());
        var members = (JsonArray)zone["members"]!;
        Assert.Equal([1UL], members.Select(member => member!.GetValue<ulong>()));
    }

    [Fact]
    public void Event_TerritoryMembershipChanged_Typed()
    {
        TerritoryMembershipChange entered = new(
            TerritoryMembershipChangeKind.Entered,
            new Territory("camp", new World.Position(250, 250), 50),
            EntityId: 12_345);

        ExternalEvent message = EventSensor.TerritoryMembershipChanged(tick: 88, entered);

        Assert.Equal(ObservabilityContract.TerritoryMembershipChanged, message.Type);
        Assert.Equal(88UL, message.Tick);
        Assert.Equal("12345", message.AgentId);
        Assert.Equal("camp", message.TargetId);
        Assert.Equal("entered", (string?)message.Value!["kind"]);

        TerritoryMembershipChange left = new(
            TerritoryMembershipChangeKind.Left,
            new Territory("camp", new World.Position(250, 250), 50),
            EntityId: 12_345);
        Assert.Equal("left", (string?)EventSensor.TerritoryMembershipChanged(88, left).Value!["kind"]);
    }

    // ------------------------------------------------------------------
    // Validation (§6)
    // ------------------------------------------------------------------

    [Fact]
    public void Validator_AcceptsDefaults()
    {
        Assert.Empty(SimulationOptionsValidator.Validate(ConfigLoader.LoadDefaults()));
    }

    [Fact]
    public void Validator_RejectsZonesWithoutEnabled()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.World.Territories.Zones.Add(new TerritoryZoneDefinition { Id = "z", CenterX = 10, CenterY = 10, Radius = 5 });

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, error => error.Contains("world.territories.zones est fournie alors que world.territories.enabled est false"));
    }

    [Fact]
    public void Validator_RejectsInvalidZoneDefinitions()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.World.Territories.Enabled = true;
        options.World.Territories.Zones.Add(new TerritoryZoneDefinition { Id = "", CenterX = -1, CenterY = 501, Radius = 0 });
        options.World.Territories.Zones.Add(new TerritoryZoneDefinition { Id = "z", CenterX = 10, CenterY = 10, Radius = 5 });
        options.World.Territories.Zones.Add(new TerritoryZoneDefinition { Id = "z", CenterX = 20, CenterY = 20, Radius = 5 });

        var errors = SimulationOptionsValidator.Validate(options);

        Assert.Contains(errors, error => error.Contains("id ne doit pas être vide"));
        Assert.Contains(errors, error => error.Contains("radius doit être &gt; 0"));
        Assert.Contains(errors, error => error.Contains("centerX doit être dans [0, worldWidth]"));
        Assert.Contains(errors, error => error.Contains("centerY doit être dans [0, worldHeight]"));
        Assert.Contains(errors, error => error.Contains("définit deux fois l'identifiant"));
    }

    [Fact]
    public void Validator_AcceptsEnabledZones()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.World.Territories.Enabled = true;
        options.World.Territories.Zones.Add(new TerritoryZoneDefinition { Id = "camp", CenterX = 250, CenterY = 250, Radius = 40 });

        Assert.Empty(SimulationOptionsValidator.Validate(options));
    }
}