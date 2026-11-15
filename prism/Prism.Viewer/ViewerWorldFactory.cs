using Prism.Core.World;
using Prism.Host.Hosting;

namespace Prism.Viewer;

/// <summary>
/// Construction de la grille du viewer depuis une source autoritaire (snapshot du host) ou
/// depuis le générateur de démonstration. Le viewer ne génère jamais un monde qu'il prétend
/// live : en mode client, la grille vient du snapshot publié par l'hôte.
/// </summary>
public static class ViewerWorldFactory
{
    /// <summary>Matière de minerai du monde d'aperçu (identifiant local, hors pack).</summary>
    public const ushort OreMaterial = 7;
    /// <summary>Matière de tronc du monde d'aperçu.</summary>
    public const ushort TreeMaterial = 8;
    /// <summary>Matière de feuillage du monde d'aperçu.</summary>
    public const ushort LeavesMaterial = 9;

    public static (WorldGrid Grid, WorldZone[] Zones) CreateGenerated(int size, ulong seed)
    {
        if (size is < 16 or > 512)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        var materials = new WorldSurfaceMaterialIds(1, 2, 3, 4, 5);
        var generated = WorldSurfaceGenerator.Generate(
            size,
            size,
            regionSize: 16,
            verticalSize: 64,
            seed,
            new SurfaceHeightConfig(),
            new WorldZoneConfig(),
            materials);
        WaterBodyGenerator.FillBelowSeaLevel(generated.Grid, waterMaterialId: 6, seaLevelZ: 26);
        OreDepositGenerator.Generate(
            generated.Grid,
            seed,
            [new OreVeinDefinition("preview_copper", OreMaterial, 2, 16, 18, 12_000)]);
        PlantPreviewTrees(generated.Grid, seed);
        ClearDirty(generated.Grid);
        return (generated.Grid, generated.Zones);
    }

    public static (WorldGrid Grid, WorldZone[] Zones) CreateFromSnapshot(GeneratedWorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SurfaceColumns.Length != checked(snapshot.Width * snapshot.Height))
        {
            throw new ArgumentException(
                "The world snapshot surface columns do not match its dimensions.",
                nameof(snapshot));
        }

        var grid = new WorldGrid(
            snapshot.Width,
            snapshot.Height,
            snapshot.RegionSize,
            snapshot.VerticalSize);
        for (var y = 0; y < snapshot.Height; y++)
        {
            for (var x = 0; x < snapshot.Width; x++)
            {
                grid.SetSurfaceColumn(x, y, snapshot.SurfaceColumns[x + snapshot.Width * y]);
            }
        }

        foreach (var entry in snapshot.Overrides)
        {
            grid.SetBlockOverride(entry.Key, entry.Value);
        }

        ClearDirty(grid);
        return (grid, snapshot.Zones);
    }

    public static void ApplyDelta(WorldGrid grid, WorldDeltaSnapshot delta)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(delta);
        foreach (var region in delta.Regions)
        {
            if (region.SurfaceColumns.Length != checked(region.Width * region.Height))
            {
                throw new ArgumentException(
                    "The region delta surface columns do not match its dimensions.",
                    nameof(delta));
            }

            for (var localY = 0; localY < region.Height; localY++)
            {
                for (var localX = 0; localX < region.Width; localX++)
                {
                    var x = region.MinX + localX;
                    var y = region.MinY + localY;
                    if ((uint)x >= (uint)grid.Width || (uint)y >= (uint)grid.Height)
                    {
                        continue;
                    }

                    grid.SetSurfaceColumn(x, y, region.SurfaceColumns[localX + region.Width * localY]);
                }
            }

            // Le delta porte l'ensemble des overrides de la région, pas seulement les ajouts :
            // un bloc miné disparaît donc explicitement de la vue (sinon il resterait affiché
            // jusqu'au prochain snapshot complet).
            var incoming = new HashSet<BlockCoordinate>();
            foreach (var entry in region.Overrides)
            {
                incoming.Add(entry.Key);
            }

            var stale = grid.EnumerateRegionOverrides(region.RegionId)
                .Select(entry => entry.Key)
                .Where(coordinate => !incoming.Contains(coordinate))
                .ToArray();
            foreach (var coordinate in stale)
            {
                grid.RemoveBlockOverride(coordinate);
            }

            foreach (var entry in region.Overrides)
            {
                grid.SetBlockOverride(entry.Key, entry.Value);
            }
        }
    }

    /// <summary>
    /// Arbres du monde d'aperçu : plantation déterministe sur une trame espacée, jamais sur une
    /// colonne en eau. Le host, lui, plante depuis ses modèles de contenu ; ces blocs ne servent
    /// qu'à vérifier le rendu des volumes (tronc, feuillage) hors flux live.
    /// </summary>
    private static void PlantPreviewTrees(WorldGrid grid, ulong seed)
    {
        const int spacing = 9;
        for (var y = 5; y < grid.Height - 5; y += spacing)
        {
            for (var x = 5; x < grid.Width - 5; x += spacing)
            {
                var originX = x + (int)((seed + (ulong)(x * 31 + y * 17)) % 5);
                var originY = y + (int)((seed + (ulong)(x * 13 + y * 29)) % 5);
                if ((uint)originX >= (uint)grid.Width || (uint)originY >= (uint)grid.Height)
                {
                    continue;
                }

                var surfaceZ = grid.GetSurfaceColumn(originX, originY).SurfaceZ;
                if (surfaceZ + 5 >= grid.VerticalSize)
                {
                    continue;
                }

                if (grid.TryGetBlockOverride(
                        new BlockCoordinate(originX, originY, surfaceZ + 1),
                        out _))
                {
                    continue;
                }

                SetPreviewBlock(grid, originX, originY, surfaceZ + 1, TreeMaterial);
                SetPreviewBlock(grid, originX, originY, surfaceZ + 2, TreeMaterial);
                SetPreviewBlock(grid, originX, originY, surfaceZ + 3, LeavesMaterial);
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0)
                        {
                            continue;
                        }

                        SetPreviewBlock(
                            grid,
                            originX + offsetX,
                            originY + offsetY,
                            surfaceZ + 3,
                            LeavesMaterial);
                    }
                }

                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (Math.Abs(offsetX) == 1 && Math.Abs(offsetY) == 1)
                        {
                            continue;
                        }

                        SetPreviewBlock(
                            grid,
                            originX + offsetX,
                            originY + offsetY,
                            surfaceZ + 4,
                            LeavesMaterial);
                    }
                }

                SetPreviewBlock(grid, originX, originY, surfaceZ + 5, LeavesMaterial);
            }
        }
    }

    private static void SetPreviewBlock(WorldGrid grid, int x, int y, int z, ushort materialId)
    {
        if ((uint)x >= (uint)grid.Width ||
            (uint)y >= (uint)grid.Height ||
            (uint)z >= (uint)grid.VerticalSize)
        {
            return;
        }

        var coordinate = new BlockCoordinate(x, y, z);
        if (grid.TryGetBlockOverride(coordinate, out _))
        {
            return;
        }

        grid.SetBlockOverride(coordinate, new BlockOverride(materialId, 0, IsAir: false));
    }

    private static void ClearDirty(WorldGrid grid)
    {
        for (var regionId = 0; regionId < grid.RegionCount; regionId++)
        {
            grid.ClearRegionDirty(regionId);
        }
    }
}
