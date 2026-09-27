using Xunit;
using Simulation.Core.Entities;
using Simulation.Core.World;

namespace Simulation.Core.Tests.WorldDescription;

public sealed class WorldDescriptionTests
{
    [Fact]
    public void SameSeedProducesSameVersionedLayout()
    {
        var first = new Simulation.Core.World.World(new WorldSize(25, 15));
        var second = new Simulation.Core.World.World(new WorldSize(25, 15));
        first.AddEntity(new Entity(new EntityId(1), "Human", null, new Position(3.25, 7.5), TraitSet.NeutralAll, 0));
        second.AddEntity(new Entity(new EntityId(1), "Human", null, new Position(3.25, 7.5), TraitSet.NeutralAll, 0));
        first.AddObstacle(new Obstacle("rock-1", new Position(8.5, 4.25), 2.0));
        second.AddObstacle(new Obstacle("rock-1", new Position(8.5, 4.25), 2.0));
        var a = WorldDescriptionBuilder.Build(first, 42, 10, ticksPerSecond: 24);
        var b = WorldDescriptionBuilder.Build(second, 42, 10, ticksPerSecond: 24);
        Assert.Equal(a with { Agents = Array.Empty<WorldAgent>(), Cells = Array.Empty<WorldCell>(), Obstacles = Array.Empty<WorldObstacle>(), Resources = Array.Empty<WorldResource>(), Regions = Array.Empty<WorldRegion>() },
            b with { Agents = Array.Empty<WorldAgent>(), Cells = Array.Empty<WorldCell>(), Obstacles = Array.Empty<WorldObstacle>(), Resources = Array.Empty<WorldResource>(), Regions = Array.Empty<WorldRegion>() });
        Assert.Equal(
            a.Cells.Select(cell => (cell.X, cell.Y, cell.TerrainType, cell.Walkable, cell.Height,
                cell.MovementCost, ObstacleIds: string.Join(",", cell.Obstacles))),
            b.Cells.Select(cell => (cell.X, cell.Y, cell.TerrainType, cell.Walkable, cell.Height,
                cell.MovementCost, ObstacleIds: string.Join(",", cell.Obstacles))));
        Assert.Equal(a.Obstacles, b.Obstacles);
        Assert.Equal(a.Resources, b.Resources);
        Assert.Equal(a.Regions, b.Regions);
        Assert.Equal(new WorldAgent(1, "Human", new WorldAgentPosition(3.25, 7.5)), Assert.Single(a.Agents));
        Assert.Equal((3, 2), (a.CellCountX, a.CellCountY));
        Assert.Equal(24, a.TicksPerSecond);
        Assert.Equal(new WorldObstacle("rock-1", 8.5, 4.25, 2.0), Assert.Single(a.Obstacles));
        Assert.Equal(12, a.Resources.Count);
        Assert.All(a.Resources.GroupBy(resource => resource.Kind), resources =>
        {
            Assert.Equal(3, resources.Count());
            Assert.Equal(3, resources.Select(resource => (resource.X, resource.Y)).Distinct().Count());
        });
        Assert.NotEmpty(a.Regions);
    }
}
