using Simulation.Core.Performance;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Pool d'objets réutilisables (SYNE-092, PERFORMANCE.md §5) : vérifie la
/// réutilisation (mêmes instances), le vidage au retour, et l'équilibre des
/// compteurs Rent/Return (observabilité).
/// </summary>
public class ObjectPoolTests
{
    [Fact]
    public void Rent_AfterReturn_ReusesSameInstance()
    {
        var pool = new ObjectPool<int>();
        List<int> first = pool.Rent();
        pool.Return(first);

        List<int> second = pool.Rent();
        Assert.Same(first, second);
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void Return_ClearsBeforeReuse()
    {
        var pool = new ObjectPool<int>();
        List<int> rented = pool.Rent();
        rented.AddRange([1, 2, 3]);
        pool.Return(rented);

        List<int> reused = pool.Rent();
        Assert.Empty(reused);
    }

    [Fact]
    public void Rent_FromEmptyPool_AllocatesFresh()
    {
        var pool = new ObjectPool<int>();
        List<int> first = pool.Rent();
        pool.Return(first);

        List<int> second = pool.Rent();
        List<int> third = pool.Rent();
        Assert.Same(first, second);
        Assert.NotSame(second, third);
    }

    [Fact]
    public void RentReturn_BalanceIsKept()
    {
        var pool = new ObjectPool<int>();
        List<int> a = pool.Rent();
        List<int> b = pool.Rent();
        List<int> c = pool.Rent();
        Assert.Equal(0, pool.Count);

        pool.Return(a);
        pool.Return(b);
        Assert.Equal(2, pool.Count);
        Assert.Equal(3, pool.RentCount);
        Assert.Equal(2, pool.ReturnCount);
    }

    [Fact]
    public void Perception_SortBuffersAreBalancedPerPerceive()
    {
        var world = new Simulation.Core.World.World(new Simulation.Core.World.WorldSize(500, 500), spatialCellSize: 50);
        var system = new Simulation.Core.Perception.PerceptionSystem(world, new Simulation.Core.Configuration.PerceptionSettings { LineOfSight = false });

        long rentsBefore = system.SortBuffers.RentCount;
        long returnsBefore = system.SortBuffers.ReturnCount;

        _ = system.Perceive(new Simulation.Core.Entities.Entity(
            new Simulation.Core.Entities.EntityId(1),
            "croix",
            name: null,
            new Simulation.Core.World.Position(250, 250),
            Simulation.Core.Entities.TraitSet.NeutralAll,
            bornAt: 0), tick: 5);

        Assert.Equal(rentsBefore + 1, system.SortBuffers.RentCount);
        Assert.Equal(returnsBefore + 1, system.SortBuffers.ReturnCount);
        Assert.Equal(1, system.SortBuffers.Count);
    }
}