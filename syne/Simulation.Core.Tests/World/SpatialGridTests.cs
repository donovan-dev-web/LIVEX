using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

public class SpatialGridTests
{
    private static Entity MakeEntity(ulong id, Position position) =>
        new(new EntityId(id), "Entité A", name: null, position, TraitSet.NeutralAll, bornAt: 0);

    [Fact]
    public void Add_IndexesEntity_AndCounts()
    {
        var grid = new SpatialGrid(new WorldSize(500, 500), cellSize: 50);
        grid.Add(MakeEntity(1, new Position(10, 10)));
        grid.Add(MakeEntity(2, new Position(400, 400)));

        Assert.Equal(2, grid.Count);
    }

    [Fact]
    public void Add_SameEntityTwice_Throws()
    {
        var grid = new SpatialGrid(new WorldSize(500, 500), cellSize: 50);
        var entity = MakeEntity(1, new Position(10, 10));
        grid.Add(entity);

        Assert.Throws<InvalidOperationException>(() => grid.Add(entity));
    }

    [Fact]
    public void QueryCircle_ReturnsOnlyNearbyEntities_InDeterministicOrder()
    {
        var grid = new SpatialGrid(new WorldSize(500, 500), cellSize: 50);
        var near1 = MakeEntity(1, new Position(210, 210));
        var near2 = MakeEntity(2, new Position(220, 205));
        var far = MakeEntity(3, new Position(400, 400));
        grid.Add(near1);
        grid.Add(near2);
        grid.Add(far);

        var found = grid.QueryCircle(new Position(200, 200), radius: 30);

        Assert.Equal([near1, near2], found);
        Assert.DoesNotContain(far, found);
    }

    [Fact]
    public void QueryCircle_ExcludesReferenceEntity()
    {
        var grid = new SpatialGrid(new WorldSize(500, 500), cellSize: 50);
        var self = MakeEntity(1, new Position(10, 10));
        var other = MakeEntity(2, new Position(12, 12));
        grid.Add(self);
        grid.Add(other);

        var found = grid.QueryCircle(new Position(10, 10), radius: 30, excludeId: self.Id.Value);

        Assert.Equal([other], found);
    }

    [Fact]
    public void Move_RelocatesEntityBetweenCells()
    {
        var grid = new SpatialGrid(new WorldSize(500, 500), cellSize: 50);
        var entity = MakeEntity(1, new Position(10, 10));
        grid.Add(entity);

        grid.Move(entity, new Position(450, 450));

        Assert.Equal(new Position(450, 450), entity.Position);
        var found = grid.QueryCircle(new Position(450, 450), radius: 30);
        Assert.Equal([entity], found);
        Assert.Empty(grid.QueryCircle(new Position(10, 10), radius: 30));
    }

    [Fact]
    public void QueryCircle_ZeroRadius_ReturnsEmpty()
    {
        var grid = new SpatialGrid(new WorldSize(500, 500), cellSize: 50);
        grid.Add(MakeEntity(1, new Position(10, 10)));

        Assert.Empty(grid.QueryCircle(new Position(10, 10), radius: 0));
    }
}