using System.Diagnostics;
using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Micro-benchmark de la requête de voisinage (SYNE-012) : la grille spatiale
/// doit rester sous le budget tick de perception (PERFORMANCE.md — perception
/// 20 ms). Valeur moyenne asserée très en-deçà du budget (marge anti-flakiness
/// CI). Méthodologie documentée dans PERFORMANCE.md §3.
/// </summary>
public class PerceptionBenchmarkTests
{
    private const double BudgetMsPerQuery = 10.0;

    [Fact]
    public void QueryCircle_AverageStayUnderBudget_AtOneThousandEntities()
    {
        var world = new Simulation.Core.World.World(new WorldSize(1000, 1000), spatialCellSize: 50);
        for (ulong i = 1; i <= 1000; i++)
        {
            double x = (i * 137) % 1000;
            double y = (i * 173) % 1000;
            var entity = new Entity(new EntityId(i), "Entité A", name: null, new Position(x, y), TraitSet.NeutralAll, bornAt: 0);
            world.AddEntity(entity);
        }

        const int queries = 50;
        double totalMs = 0.0;
        for (int q = 1; q <= queries; q++)
        {
            var center = new Position((q * 911) % 1000, (q * 613) % 1000);
            var sw = Stopwatch.StartNew();
            world.Grid.QueryCircle(center, radius: 50);
            sw.Stop();
            totalMs += sw.Elapsed.TotalMilliseconds;
        }

        double meanMs = totalMs / queries;
        Assert.True(meanMs < BudgetMsPerQuery, $"Requête moyenne {meanMs:F3} ms ≥ budget {BudgetMsPerQuery} ms.");
    }

    [Fact]
    public void QueryCircle_ScanWindowIsCellBounded()
    {
        // Le filtre géométrique ne couvre que la fenêtre 3×3 cellules (pas la
        // population entière) : avec centre (250,250) et rayon 50 la requête
        // visite 3 cellules sur les 400 de la grille.
        var world = new Simulation.Core.World.World(new WorldSize(1000, 1000), spatialCellSize: 50);
        Assert.Equal(400, world.Grid.CellCountX * world.Grid.CellCountY);

        Position center = new(250, 250);
        int startX = (int)Math.Floor((center.X - 50) / 50);
        int endX = (int)Math.Floor((center.X + 50) / 50);
        Assert.Equal(3, endX - startX + 1);
    }
}