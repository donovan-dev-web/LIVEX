using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Prng;
using Xunit;
using Simulation.Core.World;
using WorldModel = Simulation.Core.World.World;

namespace Simulation.Core.Tests.Configuration;

/// <summary>
/// Découplage de la taille de cellule spatiale du rayon de perception (jalon
/// review/refactor, engineVersion 0.12.0).
///
/// <para>
/// <c>SpatialGrid.QueryCircle</c> renvoie les voisins dans l'ordre de parcours
/// des cellules. L'ordre d'un <c>List&lt;T&gt;</c> n'est pas stable pour un tri
/// ultérieur, donc cet ordre décide de l'ordre de consideration des
/// perceptions — et, de proche en proche, de la trajectoire du monde. Or la
/// cellule était dimensionnée par <c>agents.perception.radius</c> : régler la
/// portée de la perception modifiait donc silencieusement l'évolution du monde,
/// ce qui est un piège de déterminisme. La cellule est devenue un paramètre
/// explicite et figé par graine.
/// </para>
/// </summary>
public sealed class SpatialCellSizeDecouplingTests
{
    private static WorldModel WorldWith(double cellSize, params (ulong Id, double X, double Y)[] agents)
    {
        var world = new WorldModel(new WorldSize(500, 500), cellSize);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(1);
        foreach ((ulong id, double x, double y) in agents)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, id, bornAt: 0);
            entity.SetPosition(new Position(x, y));
            world.AddEntity(entity);
        }

        return world;
    }

    [Fact]
    public void DefaultSpatialCellSize_MatchesTheLegacyPerceptionRadius()
    {
        // Rupture de trajectoire à éviter : la valeur par défaut doit reproduire
        // la cellule historiquement déduite du rayon par défaut (50).
        Assert.Equal(50, new PerceptionSettings().SpatialCellSize);
        Assert.Equal(new PerceptionSettings().Radius, new PerceptionSettings().SpatialCellSize);
    }

    [Fact]
    public void ChangingPerceptionRadius_DoesNotChangeTheGridGeometry()
    {
        var shortRadius = new PerceptionSettings { Radius = 30 };
        var longRadius = new PerceptionSettings { Radius = 70 };

        Assert.Equal(shortRadius.SpatialCellSize, longRadius.SpatialCellSize);
    }

    [Fact]
    public void QueryCircle_ReturnsTheSameNeighbourSet_WhateverTheCellSize()
    {
        (ulong Id, double X, double Y)[] layout =
        [
            (1, 10, 10), (2, 40, 12), (3, 120, 90), (4, 300, 20), (5, 480, 480), (6, 95, 95),
        ];

        WorldModel coarse = WorldWith(200, layout);
        WorldModel fine = WorldWith(5, layout);

        var center = new Position(10, 10);
        List<ulong> fromCoarse = coarse.Grid.QueryCircle(center, 40).Select(e => e.Id.Value).ToList();
        List<ulong> fromFine = fine.Grid.QueryCircle(center, 40).Select(e => e.Id.Value).ToList();

        Assert.Equal(fromCoarse.OrderBy(id => id), fromFine.OrderBy(id => id));
    }

    [Fact]
    public void QueryCircle_Order_DependsOnTheCellSize_WhichIsWhyTheCellSizeMustBeFixed()
    {
        // Ce test ne doit PAS être « corrigé » : il documente la propriété
        // déterministe mais dépendante de la géométrie qui justifie le
        // découplage. Si l'ordre devenait indépendant de la cellule, ce test
        // échouerait et pourrait être remplacé par un test d'indépendance.
        (ulong Id, double X, double Y)[] layout = [(1, 400, 400), (2, 10, 10), (3, 200, 10)];

        WorldModel coarse = WorldWith(500, layout);
        WorldModel fine = WorldWith(5, layout);

        var center = new Position(10, 10);
        List<ulong> fromCoarse = coarse.Grid.QueryCircle(center, 1000).Select(e => e.Id.Value).ToList();
        List<ulong> fromFine = fine.Grid.QueryCircle(center, 1000).Select(e => e.Id.Value).ToList();

        Assert.NotEqual(fromCoarse, fromFine);
    }

    [Fact]
    public void NonFiniteCellSize_IsRejectedByTheValidator()
    {
        SimulationOptions options = SimulationProfiles.Reference();
        options.Agents.Perception.SpatialCellSize = double.NaN;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            error => error.Contains("spatialCellSize", StringComparison.Ordinal));
    }

    [Fact]
    public void ZeroCellSize_IsRejectedByTheValidator()
    {
        SimulationOptions options = SimulationProfiles.Reference();
        options.Agents.Perception.SpatialCellSize = 0.0;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            error => error.Contains("spatialCellSize", StringComparison.Ordinal));
    }
}
