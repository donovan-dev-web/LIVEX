// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests de la description versionnée du monde envoyée à Unreal.

using Xunit;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;

namespace Simulation.Core.Tests.WorldDescription;

public sealed class WorldDescriptionTests
{
    [Fact]
    public void WorldDescription_SameSeed_ProducesSameVersionedLayout()
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

    [Fact]
    public void ResourceLocations_NeverLandOnABlockedCell_OnAnySeed()
    {
        // Défaut mesuré : le placement des marqueurs ne regardait pas la
        // marchabilité, alors que `cells[]` publiait déjà la case comme bloquée —
        // PRISM instanciait donc une ressource dans un rocher (profil `prism`,
        // seed 1 : `resource-food-2` sur la case (8, 68), dans la côte sud).
        for (ulong seed = 1; seed <= 32; seed++)
        {
            Simulation.Core.World.WorldDescription description = BuildDescription(seed, Obstacle("cote-sud", 400, 2180, 150));
            var walkable = description.Cells
                .Where(cell => cell.Walkable)
                .Select(cell => (cell.X, cell.Y))
                .ToHashSet();

            Assert.Equal(12, description.Resources.Count);
            Assert.All(description.Resources, resource =>
                Assert.Contains((resource.X, resource.Y), walkable));
        }
    }

    [Fact]
    public void ResourceLocations_ShrinkToTheWalkableCells_InsteadOfLooping()
    {
        // Monde à trois cases dont une seule bloquée : le placement doit rendre
        // deux emplacements par type (le minorant suit les cases marchables),
        // chacun sur une case libre — sans boucler sur la case bloquée.
        Simulation.Core.World.WorldDescription description = BuildDescription(
            seed: 5,
            Obstacle("roc-milieu", 15, 5, 1),
            width: 30, height: 10, cellSize: 10);

        var walkable = description.Cells
            .Where(cell => cell.Walkable)
            .Select(cell => (cell.X, cell.Y))
            .ToHashSet();

        Assert.Equal(2, walkable.Count);
        Assert.Equal(8, description.Resources.Count);
        Assert.All(description.Resources, resource =>
            Assert.Contains((resource.X, resource.Y), walkable));
    }

    [Fact]
    public void ResourceLocations_AreEmpty_WhenNoCellIsWalkable()
    {
        // Un disque qui couvre tout le monde : aucun emplacement plutôt qu'une
        // boucle infinie ou un marqueur dans l'obstacle.
        Simulation.Core.World.WorldDescription description = BuildDescription(
            seed: 7,
            Obstacle("tout", 15, 5, 6),
            width: 30, height: 10, cellSize: 10);

        Assert.DoesNotContain(description.Cells, cell => cell.Walkable);
        Assert.Empty(description.Resources);
    }

    [Fact]
    public void ResourceLocations_OnThePrismProfile_StayOutOfTheSouthCoast()
    {
        // Rejeu exact du cas mesuré : profil `prism` tel qu'il est livré, seed 1.
        // La case (8, 68) est bloquée par `cote-sud-1` et ne doit porter aucun
        // marqueur — c'est cette case que l'ancienne règle piochait.
        SimulationOptions options = SimulationProfiles.Prism();
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var world = new Simulation.Core.World.World(
                WorldSize.From(options), options.Agents.Perception.SpatialCellSize);
            world.ApplyConfiguredLayout(options.World);
            Simulation.Core.World.WorldDescription description = WorldDescriptionBuilder.Build(
                world,
                seed,
                options.Simulation.WorldCellSize,
                options.Simulation.TicksPerSecond,
                options.Simulation.SimulatedSecondsPerTick,
                options.World.MetersPerUnit);

            WorldCell coastCell = description.Cells.Single(cell => cell.X == 8 && cell.Y == 68);
            Assert.False(coastCell.Walkable);
            Assert.Contains("cote-sud-1", coastCell.Obstacles);
            Assert.Equal(12, description.Resources.Count);
            Assert.DoesNotContain(description.Resources, resource => resource.X == 8 && resource.Y == 68);
            Assert.All(description.Resources, resource =>
                Assert.True(description.Cells.Single(cell => cell.X == resource.X && cell.Y == resource.Y).Walkable));
        }
    }

    private static StaticObstacleSettings Obstacle(string id, double x, double y, double radius) =>
        new() { Id = id, X = x, Y = y, Radius = radius };

    private static Simulation.Core.World.WorldDescription BuildDescription(
        ulong seed,
        StaticObstacleSettings obstacle,
        int width = 2240,
        int height = 2240,
        double cellSize = 32)
    {
        var world = new Simulation.Core.World.World(new WorldSize(width, height));
        world.AddObstacle(new Obstacle(obstacle.Id, new Position(obstacle.X, obstacle.Y), obstacle.Radius));
        return WorldDescriptionBuilder.Build(world, seed, cellSize);
    }
}
