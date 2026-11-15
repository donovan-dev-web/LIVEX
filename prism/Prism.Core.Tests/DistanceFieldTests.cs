using Prism.Core.Navigation;

namespace Prism.Core.Tests;

public sealed class DistanceFieldTests
{
    [Fact]
    public void EmptyField_HasNoReachableCells()
    {
        var field = DistanceField.Compute(4, 4, (_, _) => false, (_, _) => true);
        Assert.Equal(0, field.ReachableCount);
        for (var y = 0; y < 4; y++)
        for (var x = 0; x < 4; x++)
        {
            Assert.Equal(DistanceField.Unreachable, field.Get(x, y));
        }
    }

    [Fact]
    public void SingleSource_PropagatesManhattanDistances()
    {
        var field = DistanceField.Compute(5, 5, (x, y) => x == 2 && y == 2, (_, _) => true);
        Assert.Equal(0, field.Get(2, 2));
        Assert.Equal(1, field.Get(1, 2));
        Assert.Equal(1, field.Get(3, 2));
        Assert.Equal(2, field.Get(0, 2));
        Assert.Equal(2, field.Get(4, 2));
        Assert.Equal(4, field.Get(0, 0));
        Assert.Equal(25, field.ReachableCount);
    }

    [Fact]
    public void Wall_BlocksPropagation()
    {
        var field = DistanceField.Compute(5, 5,
            isSource: (x, y) => x == 4 && y == 2,
            isWalkable: (x, y) => !(x == 2 && y >= 1 && y <= 3));
        Assert.Equal(DistanceField.Unreachable, field.Get(2, 2));
        Assert.True(field.Get(1, 2) > 0);
        Assert.Equal(0, field.Get(4, 2));
    }

    [Fact]
    public void MultiSource_TakesMinimum()
    {
        var field = DistanceField.Compute(6, 1,
            isSource: (x, _) => x == 0 || x == 5,
            isWalkable: (_, _) => true);
        Assert.Equal(0, field.Get(0, 0));
        Assert.Equal(0, field.Get(5, 0));
        Assert.Equal(1, field.Get(1, 0));
        Assert.Equal(1, field.Get(4, 0));
        Assert.Equal(2, field.Get(2, 0));
        Assert.Equal(2, field.Get(3, 0));
    }

    [Fact]
    public void MaxDistance_BoundsPropagation()
    {
        var field = DistanceField.Compute(7, 1,
            isSource: (x, _) => x == 3,
            isWalkable: (_, _) => true,
            maxDistance: 2);
        Assert.Equal(0, field.Get(3, 0));
        Assert.Equal(1, field.Get(2, 0));
        Assert.Equal(2, field.Get(1, 0));
        Assert.Equal(DistanceField.Unreachable, field.Get(0, 0));
        Assert.Equal(2, field.Get(5, 0));
        Assert.Equal(DistanceField.Unreachable, field.Get(6, 0));
    }

    [Fact]
    public void Deterministic_SameInputsSameOutput()
    {
        Func<int, int, bool> src = (x, y) => (x + y) % 7 == 0;
        Func<int, int, bool> walk = (x, y) => (x * 3 + y) % 5 != 0;
        var a = DistanceField.Compute(13, 13, src, walk);
        var b = DistanceField.Compute(13, 13, src, walk);
        Assert.Equal(a.Distances, b.Distances);
    }

    [Fact]
    public void NextStep_MovesDownhill()
    {
        var field = DistanceField.Compute(5, 5, (x, y) => x == 4 && y == 4, (_, _) => true);
        var step = field.NextStep(0, 0);
        Assert.NotNull(step);
        Assert.True(field.Get(step!.Value.x, step.Value.y) < field.Get(0, 0));
    }

    [Fact]
    public void NextStep_OnSource_ReturnsNull()
    {
        var field = DistanceField.Compute(5, 5, (x, y) => x == 2 && y == 2, (_, _) => true);
        Assert.Null(field.NextStep(2, 2));
    }

    [Fact]
    public void NextStep_OnUnreachable_ReturnsNull()
    {
        var field = DistanceField.Compute(5, 5, (x, y) => x == 4 && y == 4, (x, y) => !(x == 1 && y == 1));
        Assert.Equal(DistanceField.Unreachable, field.Get(1, 1));
        Assert.Null(field.NextStep(1, 1));
    }

    [Fact]
    public void OutOfBounds_GetReturnsUnreachable()
    {
        var field = DistanceField.Compute(3, 3, (x, y) => x == 1 && y == 1, (_, _) => true);
        Assert.Equal(DistanceField.Unreachable, field.Get(-1, 0));
        Assert.Equal(DistanceField.Unreachable, field.Get(3, 0));
        Assert.Equal(DistanceField.Unreachable, field.Get(0, 3));
    }
}
