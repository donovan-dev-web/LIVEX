using Prism.Core.World;

namespace Prism.Viewer;

public sealed record TerrainRegionMesh(int RegionId, float[] Vertices);

public sealed record TerrainMaterialPalette(
    ushort Grass,
    ushort Sand,
    ushort Stone,
    ushort Soil,
    ushort Bedrock,
    ushort Water,
    bool ZoneOverlay = false,
    ViewerLayer[]? ActiveLayers = null,
    ushort Tree = 0,
    ushort Leaves = 0,
    ushort Ore = 0,
    bool ShowBuried = false)
{
    public bool HasLayer(ViewerLayer layer) =>
        ActiveLayers is not null && Array.IndexOf(ActiveLayers, layer) >= 0;

    public (float R, float G, float B) GetColor(ushort materialId, WorldZone zone)
    {
        if (ZoneOverlay || HasLayer(ViewerLayer.Zones))
        {
            return zone switch
            {
                WorldZone.Forest => (0.25f, 0.62f, 0.38f),
                WorldZone.Rocky => (0.62f, 0.58f, 0.52f),
                WorldZone.Shore => (0.78f, 0.68f, 0.40f),
                _ => (0.40f, 0.58f, 0.78f),
            };
        }

        if (HasLayer(ViewerLayer.Resources))
        {
            // Calque « ressources » : le matériau porte la lecture, la zone est neutralisée
            // pour que la teinte signale la matière et non le biome.
            if (materialId == Water) return (0.14f, 0.34f, 0.60f);
            if (materialId == Sand) return (0.88f, 0.78f, 0.48f);
            if (materialId == Stone) return (0.56f, 0.60f, 0.66f);
            if (materialId == Soil) return (0.46f, 0.30f, 0.18f);
            if (materialId == Bedrock) return (0.22f, 0.22f, 0.26f);
            return (0.52f, 0.54f, 0.36f);
        }

        if (Ore != 0 && materialId == Ore) return (0.74f, 0.50f, 0.24f);
        if (Tree != 0 && materialId == Tree) return (0.40f, 0.27f, 0.16f);
        if (Leaves != 0 && materialId == Leaves) return (0.20f, 0.47f, 0.25f);
        if (materialId == Water) return (0.20f, 0.48f, 0.68f);
        if (materialId == Sand) return (0.70f, 0.64f, 0.46f);
        if (materialId == Stone) return (0.48f, 0.50f, 0.52f);
        if (materialId == Soil) return (0.43f, 0.34f, 0.25f);
        if (materialId == Bedrock) return (0.25f, 0.27f, 0.29f);
        return zone switch
        {
            WorldZone.Forest => (0.31f, 0.46f, 0.34f),
            WorldZone.Rocky => (0.52f, 0.52f, 0.50f),
            WorldZone.Shore => (0.72f, 0.67f, 0.50f),
            _ => (0.54f, 0.65f, 0.43f),
        };
    }
}

public static class TerrainMeshBuilder
{
    /// <summary>Position (3), couleur (3) et coordonnées de texture (2).</summary>
    public const int FloatsPerVertex = 8;

    public static IReadOnlyList<TerrainRegionMesh> BuildRegions(
        WorldGrid world,
        ReadOnlySpan<WorldZone> zones,
        TerrainMaterialPalette palette)
    {
        ValidateInputs(world, zones, palette);
        BuildTopSurfaces(world, palette.Water, out var topHeights, out var topMaterials);
        var regions = new TerrainRegionMesh[world.RegionCount];
        for (var regionId = 0; regionId < world.RegionCount; regionId++)
        {
            regions[regionId] = BuildRegion(world, zones, palette, topHeights, topMaterials, regionId);
        }

        return regions;
    }

    internal static TerrainRegionMesh BuildRegion(
        WorldGrid world,
        ReadOnlySpan<WorldZone> zones,
        TerrainMaterialPalette palette,
        int[] topHeights,
        ushort[] topMaterials,
        int regionId)
    {
        if ((uint)regionId >= (uint)world.RegionCount)
        {
            throw new ArgumentOutOfRangeException(nameof(regionId));
        }

        var regionX = regionId % world.RegionCountX;
        var regionY = regionId / world.RegionCountX;
        var minX = regionX * world.RegionSize;
        var minY = regionY * world.RegionSize;
        var regionWidth = Math.Min(world.RegionSize, world.Width - minX);
        var regionHeight = Math.Min(world.RegionSize, world.Height - minY);
        var zoneMap = zones.ToArray();
        var vertices = new List<float>();
        var visited = new bool[regionWidth, regionHeight];
        for (var localY = 0; localY < regionHeight; localY++)
        {
            for (var localX = 0; localX < regionWidth; localX++)
            {
                if (visited[localX, localY])
                {
                    continue;
                }

                var x = minX + localX;
                var y = minY + localY;
                var key = GetSurfaceKey(world, zoneMap, topHeights, topMaterials, x, y);
                var width = 1;
                while (localX + width < regionWidth &&
                       !visited[localX + width, localY] &&
                       GetSurfaceKey(world, zoneMap, topHeights, topMaterials, x + width, y) == key)
                {
                    width++;
                }

                var height = 1;
                while (localY + height < regionHeight &&
                       Enumerable.Range(localX, width).All(offset =>
                           !visited[offset, localY + height] &&
                           GetSurfaceKey(
                               world,
                               zoneMap,
                               topHeights,
                               topMaterials,
                               minX + offset,
                               y + height) == key))
                {
                    height++;
                }

                for (var fillY = localY; fillY < localY + height; fillY++)
                {
                    for (var fillX = localX; fillX < localX + width; fillX++)
                    {
                        visited[fillX, fillY] = true;
                    }
                }

                var color = palette.GetColor(key.MaterialId, key.Zone);
                var tile = ViewerTextureAtlas.GetTileIndex(key.MaterialId, palette);
                AddQuad(
                    vertices,
                    (x, y, key.TopZ),
                    (x + width, y, key.TopZ),
                    (x + width, y + height, key.TopZ),
                    (x, y + height, key.TopZ),
                    color,
                    tile,
                    CornerOcclusion(x, y, key.TopZ),
                    CornerOcclusion(x + width, y, key.TopZ),
                    CornerOcclusion(x + width, y + height, key.TopZ),
                    CornerOcclusion(x, y + height, key.TopZ));
            }
        }

        AddHorizontalSides(true);
        AddHorizontalSides(false);
        AddVerticalSides(true);
        AddVerticalSides(false);
        AddOverrideBoxes();
        return new TerrainRegionMesh(regionId, vertices.ToArray());

        void AddHorizontalSides(bool north)
        {
            for (var localY = 0; localY < regionHeight; localY++)
            {
                var y = minY + localY;
                var localX = 0;
                while (localX < regionWidth)
                {
                    var x = minX + localX;
                    if (!TryGetSideKey(x, y, x, y + (north ? -1 : 1), out var key))
                    {
                        localX++;
                        continue;
                    }

                    var runStart = localX;
                    var runEnd = localX;
                    while (runEnd + 1 < regionWidth &&
                           TryGetSideKey(
                               minX + runEnd + 1,
                               y,
                               minX + runEnd + 1,
                               y + (north ? -1 : 1),
                               out var nextKey) &&
                           nextKey == key)
                    {
                        runEnd++;
                    }

                    var color = ShadeSide(palette.GetColor(key.MaterialId, key.Zone));
                    var tile = ViewerTextureAtlas.GetTileIndex(key.MaterialId, palette);
                    if (north)
                    {
                        AddQuad(vertices,
                            (minX + runStart, y, key.BottomZ),
                            (minX + runEnd + 1, y, key.BottomZ),
                            (minX + runEnd + 1, y, key.TopZ),
                            (minX + runStart, y, key.TopZ),
                            color, tile, 1f);
                    }
                    else
                    {
                        AddQuad(vertices,
                            (minX + runEnd + 1, y + 1, key.BottomZ),
                            (minX + runStart, y + 1, key.BottomZ),
                            (minX + runStart, y + 1, key.TopZ),
                            (minX + runEnd + 1, y + 1, key.TopZ),
                            color, tile, 1f);
                    }

                    localX = runEnd + 1;
                }
            }
        }

        void AddVerticalSides(bool west)
        {
            for (var localX = 0; localX < regionWidth; localX++)
            {
                var x = minX + localX;
                var localY = 0;
                while (localY < regionHeight)
                {
                    var y = minY + localY;
                    if (!TryGetSideKey(x, y, x + (west ? -1 : 1), y, out var key))
                    {
                        localY++;
                        continue;
                    }

                    var runStart = localY;
                    var runEnd = localY;
                    while (runEnd + 1 < regionHeight &&
                           TryGetSideKey(
                               x,
                               minY + runEnd + 1,
                               x + (west ? -1 : 1),
                               minY + runEnd + 1,
                               out var nextKey) &&
                           nextKey == key)
                    {
                        runEnd++;
                    }

                    var color = ShadeSide(palette.GetColor(key.MaterialId, key.Zone));
                    var tile = ViewerTextureAtlas.GetTileIndex(key.MaterialId, palette);
                    if (west)
                    {
                        AddQuad(vertices,
                            (x, minY + runEnd + 1, key.BottomZ),
                            (x, minY + runStart, key.BottomZ),
                            (x, minY + runStart, key.TopZ),
                            (x, minY + runEnd + 1, key.TopZ),
                            color, tile, 1f);
                    }
                    else
                    {
                        AddQuad(vertices,
                            (x + 1, minY + runStart, key.BottomZ),
                            (x + 1, minY + runEnd + 1, key.BottomZ),
                            (x + 1, minY + runEnd + 1, key.TopZ),
                            (x + 1, minY + runStart, key.TopZ),
                            color, tile, 1f);
                    }

                    localY = runEnd + 1;
                }
            }
        }

        // Arbres, minerais et blocs posés : les overrides non aqueux sont rendus en volumes.
        // Un bloc enterré reste dessiné mais caché par la surface, et réapparaît dans la vue en
        // coupe ; un bloc retiré (override air ou entrée disparue du delta) disparaît au rebuild.
        void AddOverrideBoxes()
        {
            foreach (var entry in world.EnumerateRegionOverrides(regionId))
            {
                var block = entry.Value;
                if (block.IsAir || block.MaterialId == 0 || block.MaterialId == palette.Water)
                {
                    continue;
                }

                var coordinate = entry.Key;
                var index = coordinate.X + world.Width * coordinate.Y;
                // Un bloc enterré n'est pas rendu dans la vue normale : la coque du monde
                // n'a pas de volume intérieur, il flotterait devant les parois. La vue en
                // coupe le révèle (ShowBuried), car c'est là qu'il porte une information.
                if (coordinate.Z < topHeights[index] && !palette.ShowBuried)
                {
                    continue;
                }

                // Une face n'est dessinée que si elle donne sur du vide : les blocs d'un
                // arbre ne produisent pas de faces internes, et un bloc au bord du monde
                // ne double pas la paroi extérieure (évite le z-fighting du minerai).
                var west = coordinate.X > 0 &&
                    !IsFilled(coordinate.X - 1, coordinate.Y, coordinate.Z);
                var east = coordinate.X < world.Width - 1 &&
                    !IsFilled(coordinate.X + 1, coordinate.Y, coordinate.Z);
                var north = coordinate.Y > 0 &&
                    !IsFilled(coordinate.X, coordinate.Y - 1, coordinate.Z);
                var south = coordinate.Y < world.Height - 1 &&
                    !IsFilled(coordinate.X, coordinate.Y + 1, coordinate.Z);
                var top = !IsFilled(coordinate.X, coordinate.Y, coordinate.Z + 1);
                var bottom = coordinate.Z > 0 &&
                    !IsFilled(coordinate.X, coordinate.Y, coordinate.Z - 1);
                if (!west && !east && !north && !south && !top && !bottom)
                {
                    continue;
                }

                AddBox(
                    vertices,
                    coordinate.X,
                    coordinate.Y,
                    coordinate.Z,
                    palette.GetColor(block.MaterialId, zoneMap[index]),
                    ViewerTextureAtlas.GetTileIndex(block.MaterialId, palette),
                    west,
                    east,
                    north,
                    south,
                    top,
                    bottom);
            }
        }

        bool IsFilled(int x, int y, int z)
        {
            if ((uint)x >= (uint)world.Width ||
                (uint)y >= (uint)world.Height ||
                (uint)z >= (uint)world.VerticalSize)
            {
                return false;
            }

            return world.TryGetBlockOverride(new BlockCoordinate(x, y, z), out var neighbor) &&
                   !neighbor.IsAir &&
                   neighbor.MaterialId != 0 &&
                   neighbor.MaterialId != palette.Water;
        }

        bool TryGetSideKey(int x, int y, int neighborX, int neighborY, out SideKey key)
        {
            var top = topHeights[x + world.Width * y] + 1;
            var neighborHeight = world.TryGetSurfaceColumn(neighborX, neighborY, out _)
                ? topHeights[neighborX + world.Width * neighborY]
                : -1;
            var bottom = neighborHeight + 1;
            if (bottom >= top)
            {
                key = default;
                return false;
            }

            var index = x + world.Width * y;
            key = new SideKey(bottom, top, topMaterials[index], zoneMap[index]);
            return true;
        }

        float CornerOcclusion(int cornerX, int cornerY, int topZ)
        {
            var coveredNeighbors = 0;
            for (var sampleY = cornerY - 1; sampleY <= cornerY; sampleY++)
            {
                for (var sampleX = cornerX - 1; sampleX <= cornerX; sampleX++)
                {
                    if ((uint)sampleX < (uint)world.Width &&
                        (uint)sampleY < (uint)world.Height &&
                        topHeights[sampleX + world.Width * sampleY] >= topZ)
                    {
                        coveredNeighbors++;
                    }
                }
            }

            return Math.Max(0.68f, 1f - coveredNeighbors * 0.08f);
        }
    }

    private static void ValidateInputs(
        WorldGrid world,
        ReadOnlySpan<WorldZone> zones,
        TerrainMaterialPalette palette)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(palette);
        if (zones.Length != checked(world.Width * world.Height))
        {
            throw new ArgumentException("Zone map length must match world dimensions.", nameof(zones));
        }
    }

    /// <summary>Hauteurs du sol seul : aucun override, pas même l'eau, ne relève la colonne.</summary>
    internal static void BuildTopSurfaces(
        WorldGrid world,
        out int[] heights,
        out ushort[] materials) =>
        BuildTopSurfaces(world, waterMaterialId: 0, out heights, out materials);

    internal static void BuildTopSurfaces(
        WorldGrid world,
        ushort waterMaterialId,
        out int[] heights,
        out ushort[] materials)
    {
        var cellCount = checked(world.Width * world.Height);
        heights = new int[cellCount];
        materials = new ushort[cellCount];
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var index = x + world.Width * y;
                var surface = world.GetSurfaceColumn(x, y);
                heights[index] = surface.SurfaceZ;
                materials[index] = surface.MaterialId;
            }
        }

        // Seule l'eau relève la surface visible : les arbres, minerais et blocs posés sont des
        // volumes (AddOverrideBoxes) et ne doivent pas aplatir la colonne qu'ils surplombent.
        foreach (var entry in world.EnumerateOverrides())
        {
            if (!entry.Value.IsAir &&
                entry.Value.MaterialId == waterMaterialId &&
                entry.Key.Z > heights[entry.Key.X + world.Width * entry.Key.Y])
            {
                var index = entry.Key.X + world.Width * entry.Key.Y;
                heights[index] = entry.Key.Z;
                materials[index] = entry.Value.MaterialId;
            }
        }
    }

    private static SurfaceKey GetSurfaceKey(
        WorldGrid world,
        WorldZone[] zones,
        int[] topHeights,
        ushort[] topMaterials,
        int x,
        int y)
    {
        var index = x + world.Width * y;
        return new SurfaceKey(topHeights[index] + 1, topMaterials[index], zones[index]);
    }

    private static (float R, float G, float B) ShadeSide((float R, float G, float B) color) =>
        (color.R * 0.72f, color.G * 0.72f, color.B * 0.72f);

    private static void AddQuad(
        List<float> vertices,
        (int X, int Y, int Z) a,
        (int X, int Y, int Z) b,
        (int X, int Y, int Z) c,
        (int X, int Y, int Z) d,
        (float R, float G, float B) color,
        int tile,
        float shade)
    {
        AddQuad(vertices, a, b, c, d, color, tile, shade, shade, shade, shade);
    }

    private static void AddQuad(
        List<float> vertices,
        (int X, int Y, int Z) a,
        (int X, int Y, int Z) b,
        (int X, int Y, int Z) c,
        (int X, int Y, int Z) d,
        (float R, float G, float B) color,
        int tile,
        float shadeA,
        float shadeB,
        float shadeC,
        float shadeD)
    {
        var (minU, minV, maxU, maxV) = GetTileBounds(tile);
        AddVertex(vertices, a, color, shadeA, minU, minV);
        AddVertex(vertices, b, color, shadeB, maxU, minV);
        AddVertex(vertices, c, color, shadeC, maxU, maxV);
        AddVertex(vertices, a, color, shadeA, minU, minV);
        AddVertex(vertices, c, color, shadeC, maxU, maxV);
        AddVertex(vertices, d, color, shadeD, minU, maxV);
    }

    private static void AddBox(
        List<float> vertices,
        int x,
        int y,
        int z,
        (float R, float G, float B) color,
        int tile,
        bool west,
        bool east,
        bool north,
        bool south,
        bool top,
        bool bottom)
    {
        var (minU, minV, maxU, maxV) = GetTileBounds(tile);
        if (top)
        {
            AddFace(vertices, (x, y, z + 1), (x + 1, y, z + 1), (x + 1, y + 1, z + 1), (x, y + 1, z + 1),
                color, 1.00f, minU, minV, maxU, maxV);
        }

        if (bottom)
        {
            AddFace(vertices, (x, y, z), (x, y + 1, z), (x + 1, y + 1, z), (x + 1, y, z),
                color, 0.55f, minU, minV, maxU, maxV);
        }

        if (north)
        {
            AddFace(vertices, (x, y, z), (x + 1, y, z), (x + 1, y, z + 1), (x, y, z + 1),
                color, 0.86f, minU, minV, maxU, maxV);
        }

        if (south)
        {
            AddFace(vertices, (x + 1, y + 1, z), (x, y + 1, z), (x, y + 1, z + 1), (x + 1, y + 1, z + 1),
                color, 0.86f, minU, minV, maxU, maxV);
        }

        if (west)
        {
            AddFace(vertices, (x, y + 1, z), (x, y, z), (x, y, z + 1), (x, y + 1, z + 1),
                color, 0.72f, minU, minV, maxU, maxV);
        }

        if (east)
        {
            AddFace(vertices, (x + 1, y, z), (x + 1, y + 1, z), (x + 1, y + 1, z + 1), (x + 1, y, z + 1),
                color, 0.72f, minU, minV, maxU, maxV);
        }
    }

    private static void AddFace(
        List<float> vertices,
        (int X, int Y, int Z) a,
        (int X, int Y, int Z) b,
        (int X, int Y, int Z) c,
        (int X, int Y, int Z) d,
        (float R, float G, float B) color,
        float shade,
        float minU,
        float minV,
        float maxU,
        float maxV)
    {
        AddVertex(vertices, a, color, shade, minU, minV);
        AddVertex(vertices, b, color, shade, maxU, minV);
        AddVertex(vertices, c, color, shade, maxU, maxV);
        AddVertex(vertices, a, color, shade, minU, minV);
        AddVertex(vertices, c, color, shade, maxU, maxV);
        AddVertex(vertices, d, color, shade, minU, maxV);
    }

    private static (float MinU, float MinV, float MaxU, float MaxV) GetTileBounds(int tile)
    {
        var column = tile % ViewerTextureAtlas.Columns;
        var row = tile / ViewerTextureAtlas.Columns;
        var step = 1f / ViewerTextureAtlas.Columns;
        var minU = column * step;
        var minV = row * step;
        return (minU, minV, minU + step, minV + step);
    }

    private static void AddVertex(
        List<float> vertices,
        (int X, int Y, int Z) point,
        (float R, float G, float B) color,
        float shade,
        float u,
        float v)
    {
        vertices.Add(point.X);
        vertices.Add(point.Y);
        vertices.Add(point.Z);
        vertices.Add(color.R * shade);
        vertices.Add(color.G * shade);
        vertices.Add(color.B * shade);
        vertices.Add(u);
        vertices.Add(v);
    }

    private readonly record struct SurfaceKey(int TopZ, ushort MaterialId, WorldZone Zone);
    private readonly record struct SideKey(int BottomZ, int TopZ, ushort MaterialId, WorldZone Zone);
}
