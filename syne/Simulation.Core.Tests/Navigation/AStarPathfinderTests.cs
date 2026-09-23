using Simulation.Core.Configuration;
using Simulation.Core.Navigation;
using Simulation.Core.World;
using Xunit;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Core.Tests.Navigation;

/// <summary>
/// Tests du cheminement A* déterministe (SYNE-077) : contournement d'obstacles,
/// repli « sur place » (destination bloquée ou expansion plafonnée), déterminisme
/// (même grille → même chemin) et cache LRU borné.
/// </summary>
public class AStarPathfinderTests
{
    private static SimulationOptions Options() => ConfigLoader.LoadDefaults();

    private static WorldType EmptyWorld(int width = 500, int height = 500)
        => new(new WorldSize(width, height));

    [Fact]
    public void FindPath_AroundObstacle_ReturnsRouteAvoidingDisc()
    {
        // Obstacle plein sur l'axe source→but : le chemin doit le contourner
        // sans jamais placer un centre dans le disque (+ marge demi-cellule).
        var world = EmptyWorld();
        var obstacle = new Obstacle("rocher", new Position(250, 250), radius: 30);
        world.AddObstacle(obstacle);

        var pathfinder = new AStarPathfinder(world, Options().Agents.Pathfinding);
        IReadOnlyList<Position> path = pathfinder.FindPath(new Position(100, 250), new Position(400, 250));

        Assert.NotEmpty(path);
        foreach (Position waypoint in path)
        {
            // Marge : centre de cellule jamais dans disque + demi-cellule.
            Assert.True(obstacle.Position.DistanceTo(waypoint) > obstacle.Radius + 1e-9);
        }

        Position last = path[^1];
        Assert.True(last.DistanceTo(new Position(400, 250)) < 20.0);
    }

    [Fact]
    public void FindPath_NoObstacle_StraightRouteToTargetCell()
    {
        var world = EmptyWorld();
        var pathfinder = new AStarPathfinder(world, Options().Agents.Pathfinding);
        IReadOnlyList<Position> path = pathfinder.FindPath(new Position(100, 100), new Position(101, 101));

        // Même cellule (taille 10) → chemin vide (aucun déplacement nécessaire).
        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_WithoutCheminementEnabled_FallsBackInPlace()
    {
        WorldType world = EmptyWorld();
        world.AddObstacle(new Obstacle("rocher", new Position(250, 250), radius: 30));
        SimulationOptions options = Options();
        options.Agents.Pathfinding.Enabled = false;

        var pathfinder = new AStarPathfinder(world, options.Agents.Pathfinding);
        Assert.Empty(pathfinder.FindPath(new Position(100, 250), new Position(400, 250)));
    }

    [Fact]
    public void FindPath_AgainstWall_FallsBackInPlace()
    {
        // Mur vertical continu (disques qui se couvrent du bord haut au bord bas) :
        // aucun chemin ne relie les deux côtés → repli « sur place » (chemin vide).
        var world = EmptyWorld();
        for (int y = 0; y <= 500; y += 40)
        {
            world.AddObstacle(new Obstacle($"mur-{y}", new Position(250, y), radius: 30));
        }

        var pathfinder = new AStarPathfinder(world, Options().Agents.Pathfinding);
        IReadOnlyList<Position> path = pathfinder.FindPath(new Position(100, 250), new Position(400, 250));

        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_IsDeterministic_AcrossCallsAndInstances()
    {
        var world = EmptyWorld();
        world.AddObstacle(new Obstacle("rocher", new Position(250, 250), radius: 30));

        var first = new AStarPathfinder(world, Options().Agents.Pathfinding);
        var second = new AStarPathfinder(world, Options().Agents.Pathfinding);

        IReadOnlyList<Position> a = first.FindPath(new Position(100, 250), new Position(400, 250));
        IReadOnlyList<Position> b = first.FindPath(new Position(100, 250), new Position(400, 250));
        IReadOnlyList<Position> c = second.FindPath(new Position(100, 250), new Position(400, 250));

        Assert.Equal(a, b);
        Assert.Equal(b, c);
    }

    [Fact]
    public void FindPath_ExpansionCap_HonorsMaxExpansionCells()
    {
        // Monde 500×500, cellule 10 → 50×50 = 2500 cellules balayables ; un cap
        // minuscule force le repli « sur place » sur un long détour.
        var world = EmptyWorld();
        world.AddObstacle(new Obstacle("rocher", new Position(250, 250), radius: 30));
        SimulationOptions options = Options();
        options.Agents.Pathfinding.MaxExpansionCells = 1;

        var pathfinder = new AStarPathfinder(world, options.Agents.Pathfinding);
        IReadOnlyList<Position> path = pathfinder.FindPath(new Position(100, 250), new Position(400, 250));

        Assert.Empty(path);
    }

    [Fact]
    public void PathCache_StoresAndEvictsLeastRecentlyUsed()
    {
        var cache = new PathCache(capacity: 2);
        Position p1 = new(5, 5);
        Position p2 = new(15, 5);
        Position p3 = new(15, 15);

        cache.Add(0, 0, 1, 0, [p1]);
        cache.Add(0, 0, 1, 1, [p2]);
        Assert.Equal(2, cache.Count);

        // Accès à (0,0)→(1,0) : re-marque récemment utilisée.
        IReadOnlyList<Position>? hit = cache.TryGet(0, 0, 1, 0);
        Assert.NotNull(hit);
        Assert.Equal(p1, Assert.Single(hit));

        // Insertion d'une troisième clé : éviction de la plus ancienne (0,0)→(1,1).
        cache.Add(2, 2, 3, 3, [p3]);
        Assert.Equal(2, cache.Count);
        Assert.NotNull(cache.TryGet(0, 0, 1, 0));
        Assert.Null(cache.TryGet(0, 0, 1, 1));
        Assert.NotNull(cache.TryGet(2, 2, 3, 3));
    }

    [Fact]
    public void PathCache_ZeroCapacity_StoresNothingAndReturnsNull()
    {
        var cache = new PathCache(capacity: 0);
        cache.Add(0, 0, 1, 0, [new Position(5, 5)]);

        Assert.Equal(0, cache.Count);
        Assert.Null(cache.TryGet(0, 0, 1, 0));
    }
}