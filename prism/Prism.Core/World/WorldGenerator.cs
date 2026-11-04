using Prism.Core.Determinism;

namespace Prism.Core.World;

public sealed record WorldGenerationConfig(
    int Width,
    int Height,
    int VerticalSize,
    int RegionSize,
    int WorldGenVersion,
    SurfaceHeightConfig HeightConfig,
    WorldZoneConfig Zones,
    WorldSurfaceMaterialIds Materials,
    IReadOnlyList<OreVeinDefinition> OreVeins,
    ushort WaterMaterialId,
    IReadOnlyList<WorldTreeTemplate> TreeTemplates,
    ForestPlacementConfig Forest,
    WorldResourceMaterials? SpawnResources = null,
    SpawnSiteConfig? SpawnConfig = null,
    string ContentHash = "");

public sealed record WorldGenerationResult(
    WorldGrid Grid,
    WorldZone[] Zones,
    IReadOnlyList<StrataProfile> StrataProfiles,
    IReadOnlyList<PlacedTree> Trees,
    int OreBlocksPlaced,
    int WaterBlocksPlaced,
    int WorldGenVersion,
    ulong Seed,
    int Attempt,
    IReadOnlyList<BlockCoordinate> SpawnSites,
    string ContentHash,
    ulong WorldHash);

public sealed class WorldGenerationException(
    string code,
    int attempts,
    string message) : Exception(message)
{
    public string Code { get; } = code;
    public int Attempts { get; } = attempts;
}

public static class WorldGenerator
{
    private const int MaximumAttempts = 8;

    public static WorldGenerationResult Generate(ulong seed, WorldGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(config.WorldGenVersion);
        ArgumentNullException.ThrowIfNull(config.OreVeins);
        ArgumentNullException.ThrowIfNull(config.TreeTemplates);
        if ((config.SpawnResources is null) != (config.SpawnConfig is null))
        {
            throw new ArgumentException("Spawn resources and spawn selection configuration must be provided together.",
                nameof(config));
        }

        var attempts = config.SpawnConfig is null ? 1 : MaximumAttempts;
        string? lastSpawnError = null;
        var lastErrorCode = "world.spawn.unsatisfied";
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var attemptSeed = attempt == 0
                ? seed
                : DeterministicRandom.Mix64(seed ^ DeterministicRandom.Mix64((ulong)attempt));
            var surface = WorldSurfaceGenerator.Generate(
                config.Width,
                config.Height,
                config.RegionSize,
                config.VerticalSize,
                attemptSeed,
                config.HeightConfig,
                config.Zones,
                config.Materials);
            var oreBlocksPlaced = OreDepositGenerator.Generate(surface.Grid, attemptSeed, config.OreVeins);
            var waterBlocksPlaced = WaterBodyGenerator.FillBelowSeaLevel(
                surface.Grid, config.WaterMaterialId, config.Zones.SeaLevelZ);
            IReadOnlyList<PlacedTree> trees = config.TreeTemplates.Count == 0
                ? Array.Empty<PlacedTree>()
                : TreePlacementGenerator.Place(
                    surface.Grid, surface.Zones, attemptSeed, config.TreeTemplates, config.Forest);
            IReadOnlyList<BlockCoordinate> spawnSites = [];
            if (config.SpawnResources is not null && config.SpawnConfig is not null)
            {
                var selection = SpawnSiteSelector.Select(
                    surface.Grid, surface.Zones, config.SpawnResources, attemptSeed, config.SpawnConfig);
                if (!selection.Succeeded)
                {
                    lastSpawnError = selection.Error;
                    lastErrorCode = selection.ErrorCode ?? lastErrorCode;
                    continue;
                }

                spawnSites = selection.Sites;
            }

            var metadataHash = WorldGridHasher.ComputeWorldHash(
                surface.Grid,
                config.WorldGenVersion,
                seed,
                attempt,
                surface.Zones,
                spawnSites,
                config.ContentHash);
            return new WorldGenerationResult(
                surface.Grid,
                surface.Zones,
                surface.Profiles,
                trees,
                oreBlocksPlaced,
                waterBlocksPlaced,
                config.WorldGenVersion,
                seed,
                attempt,
                spawnSites,
                config.ContentHash,
                metadataHash);
        }

        throw new WorldGenerationException(
            lastErrorCode,
            attempts,
            $"No generated world met spawn-site requirements after {attempts} attempts. {lastSpawnError}");
    }
}
