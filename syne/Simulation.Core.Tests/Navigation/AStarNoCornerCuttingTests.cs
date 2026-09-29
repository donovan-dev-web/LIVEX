using Simulation.Core.Configuration;
using Simulation.Core.Navigation;
using Simulation.Core.Performance;
using Simulation.Core.World;
using Xunit;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Core.Tests.Navigation;

/// <summary>
/// Règle de non-coupe de coin et cohérence du collecteur de budgets (jalon
/// review/refactor, engineVersion 0.12.0).
///
/// <para>
/// A* autorisait une diagonale même lorsque les deux cellules orthogonales
/// intermédiaires étaient bloquées : le pas diagonal rase alors le point
/// d'appui entre deux cellules solides et l'entité traverse un angle d'obstacle
/// qu'elle ne peut pas contourner. Par ailleurs
/// <c>TickBudgetCollector.PhaseCount</c> valait 8 pour une énumération de
/// 7 phases, un tableau surnuméraire invisible.
/// </para>
/// </summary>
public sealed class AStarNoCornerCuttingTests
{
    private static PathfindingSettings Settings() => ConfigLoader.LoadDefaults().Agents.Pathfinding;

    /// <summary>
    /// Une cellule est bloquée si un disque obstacle est à moins de
    /// <c>radius + CellSize/2</c> de son centre — la règle de rasterisation de
    /// <see cref="AStarPathfinder"/>, reproduite pour vérifier le chemin sans
    /// dépendre de l'implémentation interne.
    /// </summary>
    private static bool IsBlocked(WorldType world, PathfindingSettings settings, int gx, int gy)
    {
        double cellSize = settings.CellSize;
        Position center = new((gx + 0.5) * cellSize, (gy + 0.5) * cellSize);
        double margin = cellSize * 0.5;
        return world.Obstacles.Any(o => o.Position.DistanceTo(center) <= o.Radius + margin);
    }

    private static (int X, int Y) CellOf(Position point, double cellSize) =>
        ((int)Math.Floor(point.X / cellSize), (int)Math.Floor(point.Y / cellSize));

    /// <summary>
    /// Vérifie l'invariant sur un chemin réel : aucun pas ne doit être une
    /// diagonale dont les deux cellules orthogonales intermédiaires sont
    /// bloquées.
    /// </summary>
    private static void AssertNoCornerCutting(
        WorldType world,
        PathfindingSettings settings,
        Position from,
        IReadOnlyList<Position> path)
    {
        double cellSize = settings.CellSize;
        var cells = new List<(int X, int Y)> { CellOf(from, cellSize) };
        cells.AddRange(path.Select(p => CellOf(p, cellSize)));

        for (int i = 1; i < cells.Count; i++)
        {
            (int px, int py) = cells[i - 1];
            (int cx, int cy) = cells[i];
            int dx = Math.Abs(cx - px);
            int dy = Math.Abs(cy - py);
            Assert.True(dx <= 1 && dy <= 1, $"pas non-adjacent ({dx}, {dy}) à l'index {i}");

            if (dx == 1 && dy == 1)
            {
                bool horizontalFree = !IsBlocked(world, settings, cx, py);
                bool verticalFree = !IsBlocked(world, settings, px, cy);
                Assert.True(horizontalFree && verticalFree,
                    $"diagonale ({px},{py})→({cx},{cy}) coupe un coin : "
                    + $"orthogonales ({cx},{py}) free={horizontalFree}, ({px},{cy}) free={verticalFree}");
            }
        }
    }

    /// <summary>
    /// Mur vertical d'une cellule d'épaisseur (rayon 0,4 cellule : la marge
    /// demi-cellule ne bloque que la cellule du centre).
    /// </summary>
    private static WorldType WallWithGap(double cellSize, int gapRow, params (int Gx, int Gy)[] extra)
    {
        var world = new WorldType(new WorldSize(500, 500));
        for (int gy = 0; gy < 50; gy++)
        {
            if (gy == gapRow)
            {
                continue;
            }

            world.AddObstacle(new Obstacle($"mur-{gy}", new Position(20.5 * cellSize, (gy + 0.5) * cellSize), radius: cellSize * 0.4));
        }

        foreach ((int gx, int gy) in extra)
        {
            world.AddObstacle(new Obstacle($"bouchon-{gx}-{gy}", new Position((gx + 0.5) * cellSize, (gy + 0.5) * cellSize), radius: cellSize * 0.4));
        }

        return world;
    }

    [Fact]
    public void CornerSqueeze_IsRefused_EvenThoughTheTargetCellIsFree()
    {
        PathfindingSettings settings = Settings();
        double cellSize = settings.CellSize;

        // La cellule (20, 25) est le seul passage du mur, et ses deux voisins
        // orthogonaux (19, 25) et (20, 24) sont bouchés : la seule façon de
        // l'atteindre depuis (19, 24) est une diagonale qui rase l'angle solide.
        WorldType world = WallWithGap(cellSize, gapRow: 25, extra: [(19, 25)]);

        Assert.False(IsBlocked(world, settings, 20, 25), "la cellule cible doit être libre");
        Assert.True(IsBlocked(world, settings, 19, 25), "l'orthogonale (19, 25) doit être bloquée");
        Assert.True(IsBlocked(world, settings, 20, 24), "l'orthogonale (20, 24) doit être bloquée");

        var pathfinder = new AStarPathfinder(world, settings);
        IReadOnlyList<Position> path = pathfinder.FindPath(
            new Position(19.5 * cellSize, 24.5 * cellSize),
            new Position(21.5 * cellSize, 26.5 * cellSize));

        // Repli « sur place » : aucun passage légal, donc aucun chemin.
        Assert.Empty(path);
    }

    [Fact]
    public void OrthogonalGap_StillYieldsAPath_AndRespectsTheRule()
    {
        PathfindingSettings settings = Settings();
        double cellSize = settings.CellSize;

        // Même mur, mais l'approche de la brèche se fait par un couloir orthogonal :
        // un chemin doit exister, et il ne doit rien couper.
        WorldType world = WallWithGap(cellSize, gapRow: 25, extra: [(19, 24), (21, 24)]);
        var pathfinder = new AStarPathfinder(world, settings);

        IReadOnlyList<Position> path = pathfinder.FindPath(
            new Position(19.5 * cellSize, 25.5 * cellSize),
            new Position(21.5 * cellSize, 25.5 * cellSize));

        Assert.NotEmpty(path);
        AssertNoCornerCutting(world, settings, new Position(19.5 * cellSize, 25.5 * cellSize), path);
    }

    [Fact]
    public void OpenField_ProducesADirectPath_WithoutCornerCutting()
    {
        PathfindingSettings settings = Settings();
        double cellSize = settings.CellSize;
        var world = new WorldType(new WorldSize(500, 500));
        var pathfinder = new AStarPathfinder(world, settings);

        var from = new Position(5.5 * cellSize, 5.5 * cellSize);
        IReadOnlyList<Position> path = pathfinder.FindPath(from, new Position(40.5 * cellSize, 40.5 * cellSize));

        Assert.NotEmpty(path);
        AssertNoCornerCutting(world, settings, from, path);
    }

    [Fact]
    public void EveryObstacleField_RespectsTheRule()
    {
        PathfindingSettings settings = Settings();
        double cellSize = settings.CellSize;
        var world = new WorldType(new WorldSize(500, 500));

        // Champ dense : beaucoup d'angles « presque » franchissables.
        for (int gy = 0; gy < 50; gy++)
        {
            for (int gx = 0; gx < 50; gx++)
            {
                if ((gx + 2 * gy) % 4 != 0)
                {
                    continue;
                }

                world.AddObstacle(new Obstacle($"damier-{gx}-{gy}",
                    new Position((gx + 0.5) * cellSize, (gy + 0.5) * cellSize),
                    radius: cellSize * 0.45));
            }
        }

        var pathfinder = new AStarPathfinder(world, settings);
        var from = new Position(1.5 * cellSize, 1.5 * cellSize);
        IReadOnlyList<Position> path = pathfinder.FindPath(from, new Position(20.5 * cellSize, 20.5 * cellSize));

        // Repli « sur place » si aucun chemin légal n'existe — conforme.
        if (path.Count > 0)
        {
            AssertNoCornerCutting(world, settings, from, path);
        }
    }

    [Fact]
    public void PhaseCount_MatchesThePhaseEnumeration()
    {
        // La constante valait 8 pour 7 phases : le tableau surnuméraire ne se
        // voyait que dans les mesures.
        Assert.Equal(Enum.GetValues<TickPhase>().Length, TickBudgetCollector.PhaseCount);
    }

    [Fact]
    public void EveryPhase_IsAddressable()
    {
        TickBudgetCollector collector = TickBudgetCollector.CreateEnabled();

        foreach (TickPhase phase in Enum.GetValues<TickPhase>())
        {
            using (collector.Begin(phase))
            {
            }
        }

        TickBudgetSnapshot snapshot = collector.Snapshot();

        foreach (TickPhase phase in Enum.GetValues<TickPhase>())
        {
            Assert.Equal(1, snapshot.Samples(phase));
        }
    }
}
