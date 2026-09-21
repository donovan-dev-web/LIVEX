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
}