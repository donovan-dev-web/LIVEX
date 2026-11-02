using Prism.Core.World;

namespace Prism.Viewer;

public sealed record TerrainRegionMesh(int RegionId, float[] Vertices);

public sealed record TerrainMaterialPalette(
    ushort Grass,
    ushort Sand,
    ushort Stone,
    ushort Soil,
    ushort Bedrock,
    ushort Water)
{
    public (float R, float G, float B) GetColor(ushort materialId, WorldZone zone)
    {
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
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(palette);
        if (zones.Length != checked(world.Width * world.Height))
        {
            throw new ArgumentException("Zone map length must match world dimensions.", nameof(zones));
        }

        var topHeights = new int[zones.Length];
        var topMaterials = new ushort[zones.Length];
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var index = x + world.Width * y;
                var surface = world.GetSurfaceColumn(x, y);
                topHeights[index] = surface.SurfaceZ;
                topMaterials[index] = surface.MaterialId;
            }
        }

        foreach (var entry in world.EnumerateOverrides())
        {
            if (entry.Value.IsAir)
            {
                continue;
            }

            var index = entry.Key.X + world.Width * entry.Key.Y;
            if (entry.Key.Z > topHeights[index])
            {
                topHeights[index] = entry.Key.Z;
                topMaterials[index] = entry.Value.MaterialId;
            }
        }

        var regions = new List<TerrainRegionMesh>(world.RegionCount);
        for (var regionId = 0; regionId < world.RegionCount; regionId++)
        {
            var regionX = regionId % world.RegionCountX;
            var regionY = regionId / world.RegionCountX;
            var minX = regionX * world.RegionSize;
            var minY = regionY * world.RegionSize;
            var maxX = Math.Min(minX + world.RegionSize, world.Width);
            var maxY = Math.Min(minY + world.RegionSize, world.Height);
            var vertices = new List<float>();

            for (var y = minY; y < maxY; y++)
            {
                for (var x = minX; x < maxX; x++)
                {
                    var index = x + world.Width * y;
                    var top = topHeights[index] + 1;
                    var color = palette.GetColor(topMaterials[index], zones[index]);
                    AddQuad(vertices,
                        (x, y, top), (x + 1, y, top), (x + 1, y + 1, top), (x, y + 1, top),
                        color, 1f);

                    AddSideIfExposed(world, topHeights, zones, palette, vertices, x, y, x, y - 1, topMaterials[index]);
                    AddSideIfExposed(world, topHeights, zones, palette, vertices, x, y, x, y + 1, topMaterials[index]);
                    AddSideIfExposed(world, topHeights, zones, palette, vertices, x, y, x - 1, y, topMaterials[index]);
                    AddSideIfExposed(world, topHeights, zones, palette, vertices, x, y, x + 1, y, topMaterials[index]);
                }
            }

            regions.Add(new TerrainRegionMesh(regionId, vertices.ToArray()));
        }

        return regions;
    }

    private static void AddSideIfExposed(
        WorldGrid world,
        int[] heights,
        ReadOnlySpan<WorldZone> zones,
        TerrainMaterialPalette palette,
        List<float> vertices,
        int x,
        int y,
        int neighborX,
        int neighborY,
        ushort materialId)
    {
        var neighborHeight = world.TryGetSurfaceColumn(neighborX, neighborY, out _)
            ? heights[neighborX + world.Width * neighborY]
            : -1;
        var bottom = neighborHeight + 1;
        var top = heights[x + world.Width * y] + 1;
        if (bottom >= top)
        {
            return;
        }

        var zone = zones[x + world.Width * y];
        var color = palette.GetColor(materialId, zone);
        color = (color.R * 0.72f, color.G * 0.72f, color.B * 0.72f);
        if (neighborX < x)
        {
            AddQuad(vertices,
                (x, y + 1, bottom), (x, y, bottom), (x, y, top), (x, y + 1, top), color, 1f);
        }
        else if (neighborX > x)
        {
            AddQuad(vertices,
                (x + 1, y, bottom), (x + 1, y + 1, bottom), (x + 1, y + 1, top), (x + 1, y, top), color, 1f);
        }
        else if (neighborY < y)
        {
            AddQuad(vertices,
                (x, y, bottom), (x + 1, y, bottom), (x + 1, y, top), (x, y, top), color, 1f);
        }
        else
        {
            AddQuad(vertices,
                (x + 1, y + 1, bottom), (x, y + 1, bottom), (x, y + 1, top), (x + 1, y + 1, top), color, 1f);
        }
    }

    private static void AddQuad(
        List<float> vertices,
        (int X, int Y, int Z) a,
        (int X, int Y, int Z) b,
        (int X, int Y, int Z) c,
        (int X, int Y, int Z) d,
        (float R, float G, float B) color,
        float shade)
    {
        AddVertex(vertices, a, color, shade);
        AddVertex(vertices, b, color, shade);
        AddVertex(vertices, c, color, shade);
        AddVertex(vertices, a, color, shade);
        AddVertex(vertices, c, color, shade);
        AddVertex(vertices, d, color, shade);
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
}
