namespace Prism.Core.World;

public sealed record WorldSurfaceMaterialIds(
    ushort Grass,
    ushort Sand,
    ushort Stone,
    ushort Soil,
    ushort Bedrock);

public sealed record GeneratedSurface(WorldGrid Grid, WorldZone[] Zones, IReadOnlyList<StrataProfile> Profiles);

public static class WorldSurfaceGenerator
{
    public static GeneratedSurface Generate(
        int width,
        int height,
        int regionSize,
        int verticalSize,
        ulong seed,
        SurfaceHeightConfig? heightConfig = null,
        WorldZoneConfig? zoneConfig = null,
        WorldSurfaceMaterialIds? materialIds = null)
    {
        materialIds ??= new WorldSurfaceMaterialIds(1, 2, 3, 4, 5);
        var heights = SurfaceHeightGenerator.Generate(width, height, verticalSize, seed, heightConfig);
        var zones = WorldZoneGenerator.Generate(width, height, heights, verticalSize, seed, zoneConfig);
        var profiles = CreateProfiles(materialIds);
        var profilesByZone = new ushort[]
        {
            profiles[0].Id,
            profiles[1].Id,
            profiles[2].Id,
            profiles[3].Id,
        };
        var world = new WorldGrid(width, height, regionSize, verticalSize);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = x + width * y;
                var zone = zones[index];
                var surfaceMaterial = zone switch
                {
                    WorldZone.Plains or WorldZone.Forest => materialIds.Grass,
                    WorldZone.Rocky => materialIds.Stone,
                    WorldZone.Shore => materialIds.Sand,
                    _ => throw new ArgumentOutOfRangeException(nameof(zones)),
                };
                world.SetSurfaceColumn(
                    x,
                    y,
                    new SurfaceColumn(heights[index], surfaceMaterial, 0, profilesByZone[(int)zone]));
            }
        }

        return new GeneratedSurface(world, zones, profiles);
    }

    public static IReadOnlyList<StrataProfile> CreateProfiles(WorldSurfaceMaterialIds materials) =>
    [
        new StrataProfile((ushort)WorldZone.Plains, materials.Soil, materials.Stone, materials.Bedrock, 4),
        new StrataProfile((ushort)WorldZone.Forest, materials.Soil, materials.Stone, materials.Bedrock, 4),
        new StrataProfile((ushort)WorldZone.Rocky, materials.Stone, materials.Stone, materials.Bedrock, 1),
        new StrataProfile((ushort)WorldZone.Shore, materials.Soil, materials.Stone, materials.Bedrock, 2),
    ];
}
