using System.Text.Json;
using Prism.Contracts;
using Simulation.Console.Integration;
using Simulation.Core.Entities;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class PrismWorldSynchronizerTests
{
    private static (PrismWorldAdapter World, Entity Entity) Build()
    {
        var world = new PrismWorldAdapter(new WorldSize(256, 256), spatialCellSize: 25);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(1);
        (Entity entity, _) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, 7, bornAt: 0);
        world.AddEntity(entity);
        return (world, entity);
    }

    private static ObservationEnvelope Envelope(long tick, ulong agentId, object? self)
    {
        JsonElement changes = JsonSerializer.SerializeToElement(
            new { changes = Array.Empty<object>() },
            ProtocolJson.Options);
        JsonElement selfElement = self is null
            ? JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options)
            : JsonSerializer.SerializeToElement(self, ProtocolJson.Options);
        return new ObservationEnvelope(ProtocolVersion.Current.ToString(), tick, agentId, changes, selfElement);
    }

    [Fact]
    public void Apply_MovesTheAgentToThePositionOfItsSelfObservation()
    {
        (PrismWorldAdapter world, Entity entity) = Build();
        var synchronizer = new PrismWorldSynchronizer(world);

        // ADR-013 : PRISM publie des milli-blocs (1 000 par bloc) ; 128 500 est
        // le centre de la case 128, soit 128,5 blocs SYNE — sans arrondi.
        bool applied = synchronizer.Apply(Envelope(3, 7, new { position = new { x = 128_500, y = 64_000, z = 1_000 } }));

        Assert.True(applied);
        Assert.Equal(new Position(128.5, 64.0), entity.Position);
        Assert.Equal(1, synchronizer.AppliedTotal);
        Assert.Equal(1, synchronizer.MovedTotal);
    }

    [Fact]
    public void Apply_IgnoresAnEnvelopeWithoutSelfPosition()
    {
        (PrismWorldAdapter world, Entity entity) = Build();
        var synchronizer = new PrismWorldSynchronizer(world);
        Position initial = entity.Position;

        Assert.False(synchronizer.Apply(Envelope(3, 7, self: null)));
        Assert.False(synchronizer.Apply(Envelope(3, 7, new { position = new { y = 10 } })));
        Assert.False(synchronizer.Apply(Envelope(3, 7, new { position = new { x = "12", y = 10 } })));

        Assert.Equal(initial, entity.Position);
        Assert.Equal(0, synchronizer.AppliedTotal);
    }

    [Fact]
    public void Apply_IgnoresAnAgentUnknownToSyne()
    {
        (PrismWorldAdapter world, Entity entity) = Build();
        var synchronizer = new PrismWorldSynchronizer(world);
        Position initial = entity.Position;

        bool applied = synchronizer.Apply(Envelope(3, 42, new { position = new { x = 10_000, y = 10_000 } }));

        Assert.True(applied);
        Assert.Equal(1, synchronizer.AppliedTotal);
        Assert.Equal(0, synchronizer.MovedTotal);
        Assert.Equal(initial, entity.Position);
    }

    [Fact]
    public void Apply_BatchAppliesEveryReadableEnvelope()
    {
        (PrismWorldAdapter world, Entity entity) = Build();
        var synchronizer = new PrismWorldSynchronizer(world);

        int applied = synchronizer.Apply(
        [
            Envelope(4, 7, new { position = new { x = 30_000, y = 40_000 } }),
            Envelope(4, 99, new { position = new { x = 1_000, y = 1_000 } }),
            Envelope(4, 8, self: null),
        ]);

        Assert.Equal(2, applied);
        Assert.Equal(2, synchronizer.AppliedTotal);
        Assert.Equal(new Position(30.0, 40.0), entity.Position);
    }

    [Fact]
    public void Apply_ClampsPositionsToTheSyneWorld()
    {
        (PrismWorldAdapter world, Entity entity) = Build();
        var synchronizer = new PrismWorldSynchronizer(world);

        // 9 000 blocs dans un monde de 256 : hors bornes, donc rogné — et une
        // position négative aussi (monde non-toroidal).
        synchronizer.Apply(Envelope(5, 7, new { position = new { x = 9_000_000, y = -40_000, z = 0 } }));

        Assert.Equal(new Position(256, 0), entity.Position);
    }

    [Fact]
    public void TryReadSelfPosition_ReadsMilliBlocksWithoutRounding()
    {
        using var document = JsonDocument.Parse("{\"position\": {\"x\": 2500, \"y\": 3750}}");

        Assert.True(PrismWorldSynchronizer.TryReadSelfPosition(document.RootElement, out Position position));
        Assert.Equal(new Position(2.5, 3.75), position);
        Assert.False(PrismWorldSynchronizer.TryReadSelfPosition(default, out _));
    }

    [Fact]
    public void TryReadSelfPosition_AcceptsTheCellCenterEncoding()
    {
        // Le centre de la case (x, y) vaut x * 1 000 + 500 côté PRISM.
        using var document = JsonDocument.Parse("{\"position\": {\"x\": 128500, \"y\": 64500}}");

        Assert.True(PrismWorldSynchronizer.TryReadSelfPosition(document.RootElement, out Position position));
        Assert.Equal(WorldSpace.CellCenter(128, 64), position);
    }

    [Fact]
    public async Task CoupledRunner_NotifiesTheEnvelopeHookBeforeMapping()
    {
        var observedEnvelopes = new List<(ulong AgentId, long Tick)>();
        var observedMappings = new List<ulong>();
        Func<TickRequest, CancellationToken, Task<TickResult>> step = (request, _) =>
        {
            var result = new TickResult(
                ProtocolVersion.Current.ToString(),
                request.Tick,
                [Envelope(request.Tick, 7, new { position = new { x = 10_000, y = 20_000, z = 0 } })],
                []);
            return Task.FromResult(result);
        };

        var runner = new PrismCoupledRunner(
            step,
            onObservations: (agentId, _, _) => observedMappings.Add(agentId),
            onEnvelope: (envelope, tick) => observedEnvelopes.Add((envelope.AgentId, tick)));
        var run = await runner.RunAsync(3);

        Assert.True(run.Succeeded);
        Assert.Equal([(7UL, 0L), (7UL, 1L), (7UL, 2L)], observedEnvelopes);
        Assert.Equal([7UL, 7UL, 7UL], observedMappings);
    }
}
