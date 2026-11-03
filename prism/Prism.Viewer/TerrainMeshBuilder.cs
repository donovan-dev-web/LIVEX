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
    bool ZoneOverlay = false)
{
    public (float R, float G, float B) GetColor(ushort materialId, WorldZone zone)
    {
        if (ZoneOverlay)
        {
            return zone switch
            {
                WorldZone.Forest => (0.25f, 0.62f, 0.38f),
                WorldZone.Rocky => (0.62f, 0.58f, 0.52f),
                WorldZone.Shore => (0.78f, 0.68f, 0.40f),
                _ => (0.40f, 0.58f, 0.78f),
            };
        }

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
    public static IReadOnlyList<TerrainRegionMesh> BuildRegions(
        WorldGrid world,
        ReadOnlySpan<WorldZone> zones,
        TerrainMaterialPalette palette)
    {
        ValidateInputs(world, zones, palette);
        BuildTopSurfaces(world, out var topHeights, out var topMaterials);
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
                AddQuad(
                    vertices,
                    (x, y, key.TopZ),
                    (x + width, y, key.TopZ),
                    (x + width, y + height, key.TopZ),
                    (x, y + height, key.TopZ),
                    color,
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
                    if (north)
                    {
                        AddQuad(vertices,
                            (minX + runStart, y, key.BottomZ),
                            (minX + runEnd + 1, y, key.BottomZ),
                            (minX + runEnd + 1, y, key.TopZ),
                            (minX + runStart, y, key.TopZ),
                            color, 1f);
                    }
                    else
                    {
                        AddQuad(vertices,
                            (minX + runEnd + 1, y + 1, key.BottomZ),
                            (minX + runStart, y + 1, key.BottomZ),
                            (minX + runStart, y + 1, key.TopZ),
                            (minX + runEnd + 1, y + 1, key.TopZ),
                            color, 1f);
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
                    if (west)
                    {
                        AddQuad(vertices,
                            (x, minY + runEnd + 1, key.BottomZ),
                            (x, minY + runStart, key.BottomZ),
                            (x, minY + runStart, key.TopZ),
                            (x, minY + runEnd + 1, key.TopZ),
                            color, 1f);
                    }
                    else
                    {
                        AddQuad(vertices,
                            (x + 1, minY + runStart, key.BottomZ),
                            (x + 1, minY + runEnd + 1, key.BottomZ),
                            (x + 1, minY + runEnd + 1, key.TopZ),
                            (x + 1, minY + runStart, key.TopZ),
                            color, 1f);
                    }

                    localY = runEnd + 1;
                }
            }
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

    internal static void BuildTopSurfaces(WorldGrid world, out int[] heights, out ushort[] materials)
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

        foreach (var entry in world.EnumerateOverrides())
        {
            if (!entry.Value.IsAir && entry.Key.Z > heights[entry.Key.X + world.Width * entry.Key.Y])
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
        float shade)
    {
        AddQuad(vertices, a, b, c, d, color, shade, shade, shade, shade);
    }

    private static void AddQuad(
        List<float> vertices,
        (int X, int Y, int Z) a,
        (int X, int Y, int Z) b,
        (int X, int Y, int Z) c,
        (int X, int Y, int Z) d,
        (float R, float G, float B) color,
        float shadeA,
        float shadeB,
        float shadeC,
        float shadeD)
    {
        AddVertex(vertices, a, color, shadeA);
        AddVertex(vertices, b, color, shadeB);
        AddVertex(vertices, c, color, shadeC);
        AddVertex(vertices, a, color, shadeA);
        AddVertex(vertices, c, color, shadeC);
        AddVertex(vertices, d, color, shadeD);
    }

    private static void AddVertex(
        List<float> vertices,
        (int X, int Y, int Z) point,
        (float R, float G, float B) color,
        float shade)
    {
        vertices.Add(point.X);
        vertices.Add(point.Y);
        vertices.Add(point.Z);
        vertices.Add(color.R * shade);
        vertices.Add(color.G * shade);
        vertices.Add(color.B * shade);
    }

    private readonly record struct SurfaceKey(int TopZ, ushort MaterialId, WorldZone Zone);
    private readonly record struct SideKey(int BottomZ, int TopZ, ushort MaterialId, WorldZone Zone);
}
