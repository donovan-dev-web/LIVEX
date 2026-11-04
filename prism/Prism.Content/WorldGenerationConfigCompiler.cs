using System.Text.Json;
using Prism.Core.World;

namespace Prism.Content;

public static class WorldGenerationConfigCompiler
{
    private const int MinimumHorizontalSize = 16;
    private const int MaximumHorizontalSize = 512;
    private const int MinimumVerticalSize = 48;
    private const int MaximumVerticalSize = 256;

    public static WorldGenerationConfig Compile(CompiledContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var (width, height, verticalSize, regionSize) = ReadWorldSize(pack.Parameters);
        var materials = new WorldSurfaceMaterialIds(
            GetMaterialId(pack, "grass"),
            GetMaterialId(pack, "sand"),
            GetMaterialId(pack, "stone"),
            GetMaterialId(pack, "dirt"),
            GetMaterialId(pack, "bedrock"));
        var waterMaterialId = GetMaterialId(pack, "water");
        var treeTemplates = pack.Templates
            .Where(static template => template.IsTree)
            .Select(template => new WorldTreeTemplate(
                template.Id,
                template.Blocks.Select(block => new WorldTreeBlock(
                    block.At[0],
                    block.At[1],
                    block.At[2],
                    GetMaterialId(pack, block.Block))).ToArray()))
            .ToArray();
        var oreVeins = pack.Materials
            .Where(static material => material.Tags.Contains("ore", StringComparer.Ordinal))
            .OrderBy(static material => material.Id, StringComparer.Ordinal)
            .Select(material => new OreVeinDefinition(
                material.Id,
                GetMaterialId(pack, material.Id),
                MinimumDepthBelowSurface: 2,
                MaximumDepthBelowSurface: Math.Max(2, Math.Min(verticalSize / 2, 24)),
                LatticeScale: 18,
                Threshold: 10_000))
            .ToArray();

        var seaLevel = verticalSize * 26 / 64;
        var spawnResources = TryCreateSpawnResources(pack, waterMaterialId);
        var spawnConfig = spawnResources is null ? null : new SpawnSiteConfig();
        return new WorldGenerationConfig(
            width,
            height,
            verticalSize,
            regionSize,
            WorldGenVersion: 1,
            HeightConfig: new SurfaceHeightConfig(
                BaseHeight: verticalSize * 30 / 64,
                Amplitude: verticalSize * 12 / 64,
                BorderWidth: Math.Min(6, Math.Min(width, height) / 4)),
            Zones: new WorldZoneConfig(
                SeaLevelZ: seaLevel,
                RockyMinHeight: verticalSize * 42 / 64),
            Materials: materials,
            OreVeins: oreVeins,
            WaterMaterialId: waterMaterialId,
            TreeTemplates: treeTemplates,
            Forest: new ForestPlacementConfig(),
            SpawnResources: spawnResources,
            SpawnConfig: spawnConfig,
            ContentHash: pack.ContentHash);
    }

    private static (int Width, int Height, int VerticalSize, int RegionSize) ReadWorldSize(
        JsonElement? parameters)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty("world", out var world) ||
            world.ValueKind != JsonValueKind.Object)
        {
            return (96, 96, 64, 16);
        }

        var size = ReadIntegerArray(world, "size", [96, 96, 64], 3);
        var regionSizes = ReadIntegerArray(world, "regionSize", [16, 16], 2);
        if (size[0] is < MinimumHorizontalSize or > MaximumHorizontalSize ||
            size[1] is < MinimumHorizontalSize or > MaximumHorizontalSize ||
            size[2] is < MinimumVerticalSize or > MaximumVerticalSize ||
            regionSizes[0] <= 0 ||
            regionSizes[0] > MaximumHorizontalSize ||
            regionSizes[1] != regionSizes[0])
        {
            throw new InvalidDataException("World dimensions or square regionSize are outside supported bounds.");
        }

        return (size[0], size[1], size[2], regionSizes[0]);
    }

    private static int[] ReadIntegerArray(JsonElement parent, string propertyName, int[] fallback, int length)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return fallback;
        }

        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != length)
        {
            throw new InvalidDataException($"World parameter '{propertyName}' must contain {length} integers.");
        }

        var result = new int[length];
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (!item.TryGetInt32(out result[index++]))
            {
                throw new InvalidDataException($"World parameter '{propertyName}' must contain only integers.");
            }
        }

        return result;
    }

    private static WorldResourceMaterials? TryCreateSpawnResources(
        CompiledContentPack pack,
        ushort waterMaterialId)
    {
        if (!pack.Registries.Materials.TryGetId("stone", out var stone) ||
            !pack.Registries.Materials.TryGetId("coal_ore", out var coal) ||
            !pack.Registries.Materials.TryGetId("iron_ore", out var iron) ||
            !pack.Registries.Materials.TryGetId("grass", out _))
        {
            return null;
        }

        var wood = pack.Materials
            .Where(static material => material.Tags.Contains("wood", StringComparer.Ordinal))
            .Select(material => GetMaterialId(pack, material.Id))
            .ToHashSet();
        var solidMaterials = pack.Materials
            .Where(static material => material.Solid)
            .Select(material => GetMaterialId(pack, material.Id))
            .ToHashSet();
        if (wood.Count == 0)
        {
            return null;
        }

        return new WorldResourceMaterials(
            waterMaterialId,
            wood,
            stone,
            coal,
            iron,
            GetMaterialId(pack, "grass"),
            solidMaterials);
    }

    private static ushort GetMaterialId(CompiledContentPack pack, string key) =>
        pack.Registries.Materials.TryGetId(key, out var materialId)
            ? materialId
            : throw new InvalidDataException($"World generation requires material '{key}'.");
}
