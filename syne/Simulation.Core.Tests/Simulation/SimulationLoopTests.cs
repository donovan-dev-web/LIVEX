using Simulation.Core.Prng;
using WorldType = Simulation.Core.World.World;
using Simulation.Core.World;
using Simulation.Core.Loop;

using Xunit;

namespace Simulation.Core.Tests;

public class SimulationLoopTests
{
    private static SimulationLoop NewLoop() =>
        new(new WorldType(new WorldSize(500, 500)), Xoshiro256StarStar.Create(1));

    [Fact]
    public void Run_StopsExactlyAtMaxTicks()
    {
        var loop = NewLoop();
        loop.Run(1000);

        Assert.Equal(1000UL, loop.CurrentTick);
    }

    [Fact]
    public void Run_IsIdempotentWhenAlreadyAtMax()
    {
        var loop = NewLoop();
        loop.Run(10);
        loop.Run(10);

        Assert.Equal(10UL, loop.CurrentTick);
    }

    [Fact]
    public void Run_RejectsNonPositiveMaxTicks()
    {
        var loop = NewLoop();
        Assert.Throws<ArgumentOutOfRangeException>(() => loop.Run(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => loop.Run(-5));
    }

    [Fact]
    public void AdvanceOneTick_IncrementsTickAndAdvancesPrngStream()
    {
        var loop = NewLoop();
        var rngBefore = loop.Rng;

        loop.AdvanceOneTick();

        Assert.Equal(1UL, loop.CurrentTick);
        Assert.NotEqual(rngBefore, loop.Rng);
    }

    [Fact]
    public void SimulationTime_OneTickIsOneSimulatedMinute()
    {
        Assert.Equal(1L, SimulationTime.ToSimulatedMinutes(1));
        Assert.Equal(1440L, SimulationTime.ToSimulatedMinutes(1440));
        Assert.Equal("T+0:00", SimulationTime.FormatClock(0));
        Assert.Equal("T+24:00", SimulationTime.FormatClock(1440));
    }

    [Fact]
    public void DeterministicTwoRuns_ProduceSamePrngTailState()
    {
        var first = NewLoop();
        var second = NewLoop();

        first.Run(500);
        second.Run(500);

        var tailA = first.Rng;
        var tailB = second.Rng;
        Assert.Equal(tailA.NextUInt64(out ulong a), tailB.NextUInt64(out ulong b));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Books_WriteAndRead_AreConfiguredTrackedAndPersisted()
    {
        var options = new Simulation.Core.Configuration.SimulationOptions();
        options.World.Books.Enabled = true;
        options.World.Books.WriteCostEnergy = 7.0;
        options.World.Books.ReadBenefit = 2.5;
        var world = new WorldType(new WorldSize(500, 500));
        world.AddEntity(new Simulation.Core.Entities.Entity(
            new Simulation.Core.Entities.EntityId(1), "Human", null,
            new Simulation.Core.World.Position(10, 10),
            Simulation.Core.Entities.TraitSet.NeutralAll, bornAt: 0));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(9), options);
        loop.Run(1);
        double energyBefore = loop.Cognition.MindOf(1).Needs.Energy;

        var book = new Simulation.Core.World.Book("b1", 1, "Notes", "Knowledge") { };
        loop.WriteBook(book);
        Assert.Equal(1UL, book.WrittenTick);
        Assert.Equal(energyBefore - 7.0, loop.Cognition.MindOf(1).Needs.Energy, 8);
        Assert.Single(loop.LastBookChanges);
        Assert.Equal(Simulation.Core.World.BookChangeKind.Written, loop.LastBookChanges[0].Kind);

        loop.ReadBook(book, 1);
        loop.ReadBook(book, 1);
        Assert.Equal(new ulong[] { 1 }, book.Readers);
        Assert.Equal(3, loop.LastBookChanges.Count);
        Assert.Equal(2.5, loop.LastBookChanges[1].Value);

        Simulation.Core.Persistence.SimulationSnapshot snapshot =
            Simulation.Core.Persistence.SimulationSnapshotCodec.Capture(loop);
        SimulationLoop restored = Simulation.Core.Persistence.SimulationSnapshotRestorer.Restore(snapshot, options);
        Assert.Single(restored.World.Books);
        Assert.Equal(book.Content, restored.World.Books[0].Content);
        Assert.Equal(book.WrittenTick, restored.World.Books[0].WrittenTick);
        Assert.Equal(new ulong[] { 1 }, restored.World.Books[0].Readers);
    }

    [Fact]
    public void Books_AreDisabledByDefaultAndCannotBeWritten()
    {
        var loop = NewLoop();
        var book = new Simulation.Core.World.Book("b1", 1, "Notes", "Knowledge");
        Assert.False(loop.BooksEnabled);
        Assert.Throws<InvalidOperationException>(() => loop.WriteBook(book));
    }

}