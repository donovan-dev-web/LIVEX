// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Prism.Contracts;
using Simulation.Console.Integration;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class PrismResourceSitesTests
{
    private const ushort WaterMaterialId = 14;
    private const ushort LogMaterialId = 8;
    private const ushort StoneMaterialId = 3;

    private static PrismWorldGrid Grid(
        int width,
        int height,
        int surfaceZ,
        params (int X, int Y, int Z, ushort Material)[] overrides)
    {
        var columns = new object[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                columns[x + (width * y)] = new { surfaceZ, materialId = 1, state = 0, strataProfileId = 1 };
            }
        }

        JsonElement snapshot = JsonSerializer.SerializeToElement(
            new
            {
                width,
                height,
                verticalSize = 64,
                surfaceColumns = columns,
                overrides = overrides
                    .Select(cell => new
                    {
                        key = new { x = cell.X, y = cell.Y, z = cell.Z },
                        value = new { materialId = cell.Material, state = 0, isAir = false },
                    })
                    .ToArray(),
            },
            ProtocolJson.Options);

        Assert.True(PrismWorldGrid.TryParse(snapshot, out PrismWorldGrid? grid, out string? error), error);
        return grid!;
    }

    private static IReadOnlyDictionary<ushort, ResourceKind> Materials() => new Dictionary<ushort, ResourceKind>
    {
        [WaterMaterialId] = ResourceKind.Water,
        [LogMaterialId] = ResourceKind.Wood,
    };

    [Fact]
    public void Build_PutsTheNodeOnAStandableNeighbour()
    {
        // L'eau est posée en surcharge au-dessus de la surface : l'agent ne peut pas
        // s'y tenir, donc la cible est la case praticable voisine (nord en premier).
        PrismWorldGrid grid = Grid(8, 8, surfaceZ: 10, (3, 3, 11, WaterMaterialId));

        PrismResourceSites sites = PrismResourceSites.Build(grid, Materials());

        ResourceNode node = Assert.Single(sites.Nodes);
        Assert.Equal("resource-water-1", node.Id);
        Assert.Equal(ResourceKind.Water, node.Kind);
        Assert.Equal(new Position(3.5, 2.5), node.Position);
        Assert.Equal(1.0, node.Amount);
        Assert.Equal(1, sites.DrinkableCount);
        Assert.True(sites.TrySource(node.Id, out PrismBlockPosition source));
        Assert.Equal(new PrismBlockPosition(3, 3, 11), source);
    }

    [Fact]
    public void Build_KeepsWoodAsALandmarkWithoutADrinkTarget()
    {
        // Le bois n'est pas buvable : il donne un repère de déplacement, jamais une
        // cible de consommation.
        PrismWorldGrid grid = Grid(8, 8, surfaceZ: 10, (4, 4, 11, LogMaterialId));

        PrismResourceSites sites = PrismResourceSites.Build(grid, Materials());

        ResourceNode node = Assert.Single(sites.Nodes);
        Assert.Equal("resource-wood-1", node.Id);
        Assert.Equal(ResourceKind.Wood, node.Kind);
        Assert.Equal(0, sites.DrinkableCount);
        Assert.False(sites.TrySource(node.Id, out _));
    }

    [Fact]
    public void TryDescribe_RecognisesThePerceivedBlockNotTheApproachCell()
    {
        PrismWorldGrid grid = Grid(8, 8, surfaceZ: 10, (3, 3, 11, WaterMaterialId));
        PrismResourceSites sites = PrismResourceSites.Build(grid, Materials());

        Assert.True(sites.TryDescribe(3, 3, 11, out string label, out Position approach));
        Assert.Equal("resource-water-1", label);
        Assert.Equal(new Position(3.5, 2.5), approach);

        // La case d'approche n'est pas une source : elle ne porte pas d'eau.
        Assert.False(sites.TryDescribe(3, 2, 11, out _, out _));
    }

    [Fact]
    public void Build_SkipsWaterWithoutAnyStandableNeighbour()
    {
        // Colonne isolée : les quatre voisins sont bloqués par de la pierre.
        PrismWorldGrid grid = Grid(
            3,
            3,
            surfaceZ: 10,
            (1, 1, 11, WaterMaterialId),
            (0, 1, 11, StoneMaterialId),
            (2, 1, 11, StoneMaterialId),
            (1, 0, 11, StoneMaterialId),
            (1, 2, 11, StoneMaterialId));

        Assert.Equal(0, PrismResourceSites.Build(grid, Materials()).Count);
    }

    [Fact]
    public void Build_SkipsBuriedWaterWhoseOnlyNeighboursAreHigher()
    {
        // Nappe enterrée (contenu `minimal` : la mer est à 26 sous un plateau à 56) :
        // le bloc d'eau n'a aucun voisin praticable à son niveau, donc `Consommer`
        // serait refusé `not_adjacent`. Un bloc inatteignable n'est pas un gisement.
        PrismWorldGrid grid = Grid(8, 8, surfaceZ: 40, (3, 3, 11, WaterMaterialId));

        Assert.Equal(0, PrismResourceSites.Build(grid, Materials()).Count);
    }

    [Fact]
    public void Build_IgnoresBlocksWhoseMaterialIsNotAResource()
    {
        PrismWorldGrid grid = Grid(8, 8, surfaceZ: 10, (3, 3, 11, StoneMaterialId));

        Assert.Equal(0, PrismResourceSites.Build(grid, Materials()).Count);
    }

    [Fact]
    public void Build_WithoutContentTableExposesNothing()
    {
        PrismWorldGrid grid = Grid(8, 8, surfaceZ: 10, (3, 3, 11, WaterMaterialId));

        Assert.Equal(0, PrismResourceSites.Build(grid, PrismContentMaterials.Empty).Count);
    }

    [Fact]
    public void Build_LabelsAreCanonicalAndOrdered()
    {
        PrismWorldGrid grid = Grid(
            8,
            8,
            surfaceZ: 10,
            (5, 5, 11, WaterMaterialId),
            (2, 2, 11, WaterMaterialId));

        PrismResourceSites sites = PrismResourceSites.Build(grid, Materials());

        Assert.Equal(["resource-water-1", "resource-water-2"], sites.Nodes.Select(node => node.Id).ToArray());
        // Ordre canonique (y puis x) : (2,2) avant (5,5).
        Assert.Equal(new Position(2.5, 1.5), sites.Nodes[0].Position);
    }

    [Fact]
    public void Build_ExposesOneSitePerColumnAndKind()
    {
        // Une colonne de bois (tronc + feuillage) est un seul gisement : une forêt ne
        // doit pas créer un repère par bloc.
        PrismWorldGrid grid = Grid(
            8,
            8,
            surfaceZ: 10,
            (4, 4, 11, LogMaterialId),
            (4, 4, 12, LogMaterialId),
            (4, 4, 13, LogMaterialId),
            (3, 4, 11, LogMaterialId));

        PrismResourceSites sites = PrismResourceSites.Build(grid, Materials());

        Assert.Equal(2, sites.Count);
        Assert.All(sites.Nodes, node => Assert.Equal(ResourceKind.Wood, node.Kind));
    }
}
