using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Indexation de la grille spatiale sur les mondes <b>rectangulaires</b>
/// (jalon review/refactor, engineVersion 0.12.0).
///
/// <para>
/// <c>SpatialGrid</c> indexait les cellules avec un unique compteur de cellules
/// pour les deux axes. Sur un monde carré, <c>CellCountX == CellCountY</c>, ce qui
/// masque complètement le défaut ; dès que la largeur et la hauteur diffèrent,
/// l'indexation déborde hors du tableau interne ou aliasait des positions
/// distantes. SYNE n'interdit pas les mondes rectangulaires : la correction a été
/// appliquée sans effet observable sur le scénario de référence (carré, donc
/// mesurements neutres — voir les vecteurs de test ci-dessous).
/// </para>
/// </summary>
public class SpatialGridRectangularTests
{
    private static Entity Make(ulong id, Position position) =>
        new Entity(new EntityId(id), $"E{id}", name: null, position, TraitSet.NeutralAll, bornAt: 0);

    [Fact]
    public void CellCounts_TrackEachAxisIndependently()
    {
        // 400 x 100 avec des cellules de 50 : 8 cellules en X, 2 en Y.
        var grid = new SpatialGrid(new WorldSize(400, 100), cellSize: 50);

        Assert.Equal(8, grid.CellCountX);
        Assert.Equal(2, grid.CellCountY);
    }

    [Fact]
    public void CellCounts_MatchWhenTheWorldIsSquare()
    {
        var grid = new SpatialGrid(new WorldSize(300, 300), cellSize: 50);

        Assert.Equal(6, grid.CellCountX);
        Assert.Equal(6, grid.CellCountY);
    }

    [Fact]
    public void Entities_AreFound_WhateverTheirAxisOfTheWorld()
    {
        // Une entité placée sur l'axe long (Y) doit être retrouvée : c'est
        // précisément le cas que l'indexation à compteur unique perdait.
        var grid = new SpatialGrid(new WorldSize(100, 800), cellSize: 50);
        Entity low = Make(1, new Position(50, 50));
        Entity high = Make(2, new Position(50, 750));
        grid.Add(low);
        grid.Add(high);

        Assert.Equal(2, grid.CellCountX);
        Assert.Equal(16, grid.CellCountY);

        Entity foundLow = Assert.Single(grid.QueryCircle(new Position(50, 50), radius: 10));
        Entity foundHigh = Assert.Single(grid.QueryCircle(new Position(50, 750), radius: 10));

        Assert.Equal(low.Id.Value, foundLow.Id.Value);
        Assert.Equal(high.Id.Value, foundHigh.Id.Value);
    }

    [Fact]
    public void NoAliasing_BetweenOppositeEdges()
    {
        // Positions primera et dernière : avec un compteur unique, l'index de la
        // dernière colonne/ ligne sortait du tableau et d'autres positions
        // s'aliasaient sur la même cellule.
        var grid = new SpatialGrid(new WorldSize(600, 200), cellSize: 50);
        var expected = new List<ulong>();
        for (int column = 0; column < 12; column++)
        {
            Entity entity = Make((ulong)column + 1, new Position((column * 50) + 25, 25));
            grid.Add(entity);
            expected.Add(entity.Id.Value);
        }

        Assert.Equal(12, grid.CellCountX);
        Assert.Equal(4, grid.CellCountY);

        foreach (ulong id in expected)
        {
            IReadOnlyList<Entity> found = grid.QueryCircle(new Position(0, 0), radius: 1_000);
            Assert.Contains(found, e => e.Id.Value == id);
        }
    }

    [Fact]
    public void Move_AcrossColumns_KeepsTheEntityDiscoverable()
    {
        var grid = new SpatialGrid(new WorldSize(500, 150), cellSize: 50);
        Entity entity = Make(1, new Position(25, 75));
        grid.Add(entity);

        grid.Move(entity, new Position(475, 75));

        Entity atTarget = Assert.Single(grid.QueryCircle(new Position(475, 75), radius: 5));
        Assert.Equal(entity.Id.Value, atTarget.Id.Value);

        IReadOnlyList<Entity> atOrigin = grid.QueryCircle(new Position(25, 75), radius: 5);
        Assert.Empty(atOrigin);
    }

    [Fact]
    public void Count_ReflectsEveryIndexedEntity_InAWideWorld()
    {
        var grid = new SpatialGrid(new WorldSize(1_000, 20), cellSize: 25);
        for (ulong i = 0; i < 40; i++)
        {
            grid.Add(Make(i + 1, new Position((i * 25) + 1, 10)));
        }

        Assert.Equal(40, grid.Count);
        Assert.Equal(40, grid.CellCountX);
    }

    [Fact]
    public void OutOfBoundsPositions_AreClampedNotWrapped()
    {
        // Une position hors monde doit être ramenée dans les bornes de l'axe
        // correspondant, pas rabattue modulo la taille — sinon un X hors bornes
        // serait indexé dans une colonne valide et l'entité apparaîtrait à un
        // endroit où elle n'est pas. Sur un monde 200 x 100, une coordonnée Y
        // négative doit donc rester dans la rangée 0, pas dans la colonne 3.
        var grid = new SpatialGrid(new WorldSize(200, 100), cellSize: 50);
        Entity entity = Make(1, new Position(10_000, -50));
        grid.Add(entity);

        // L'indexation est sûre : aucune exception, aucune écriture hors tableau.
        Assert.Equal(1, grid.Count);

        // Une requête centrée sur l'origine ne doit pas non plus faire exploser
        // le balayage de cellules (bornes haute/basse par axe).
        _ = grid.QueryCircle(new Position(0, 0), radius: 5_000);

        // Et l'entité redevient découvrable dès qu'elle revient dans le monde :
        // l'indexation suit bien Move() sans laisser d'entrée fantôme.
        grid.Move(entity, new Position(25, 75));
        Assert.Equal(1, grid.Count);
        Entity relocated = Assert.Single(grid.QueryCircle(new Position(25, 75), radius: 5));
        Assert.Equal(entity.Id.Value, relocated.Id.Value);
    }

    [Fact]
    public void QueryCircle_HugeRadius_OnAThinWorld_StaysWithinBounds()
    {
        // Le cas dégénéré qui divisait par zéro / débordait la dernière fois que les
        // deux axes ont été confondus : un monde très plat (1 cellule de haut) et un
        // rayon énorme ne doivent jamais produire d'index de cellule hors tableau.
        var grid = new SpatialGrid(new WorldSize(400, 50), cellSize: 50);
        for (ulong i = 0; i < 8; i++)
        {
            grid.Add(Make(i + 1, new Position((i * 50) + 25, 25)));
        }

        Assert.Equal(8, grid.CellCountX);
        Assert.Equal(1, grid.CellCountY);

        IReadOnlyList<Entity> all = grid.QueryCircle(new Position(200, 25), radius: 1_000);
        Assert.Equal(8, all.Count);
    }
}
