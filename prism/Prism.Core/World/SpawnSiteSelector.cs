using Prism.Core.Determinism;

namespace Prism.Core.World;

[Flags]
public enum SpawnResourceMask : byte
{
    None = 0,
    Water = 1,
    Wood = 2,
    Stone = 4,
    Coal = 8,
    ExposedIronOre = 16,
    Farmland = 32,
    Required = Water | Wood | Stone | Coal | ExposedIronOre | Farmland,
}

public sealed record WorldResourceMaterials(
    ushort Water,
    IReadOnlySet<ushort> Wood,
    ushort Stone,
    ushort Coal,
    ushort IronOre,
    ushort Farmland,
    IReadOnlySet<ushort> SolidMaterials);

public sealed record SpawnSiteConfig(
    int Count = 1,
    int MinimumSpacing = 24,
    int MaximumResourceDistance = 120,
    int MaximumCandidates = 128);

public sealed record SpawnSiteSelectionResult(
    IReadOnlyList<BlockCoordinate> Sites,
    string? Error)
{
    public bool Succeeded => Error is null;
}

public static class SpawnSiteSelector
{
    private static readonly (int X, int Y)[] Neighbors =
    [
        (0, -1),
        (-1, 0),
        (1, 0),
        (0, 1),
    ];

    public static SpawnSiteSelectionResult Select(
        WorldGrid world,
        ReadOnlySpan<WorldZone> zones,
        WorldResourceMaterials materials,
        ulong seed,
        SpawnSiteConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(materials);
        config ??= new SpawnSiteConfig();
        if (zones.Length != checked(world.Width * world.Height))
        {
            throw new ArgumentException("Zone map length must match world dimensions.", nameof(zones));
        }

        if (materials.Wood is null || materials.SolidMaterials is null)
        {
            throw new ArgumentException("Wood and solid material sets are required.", nameof(materials));
        }

        if (config.Count <= 0 ||
            config.MinimumSpacing < 0 ||
            config.MaximumResourceDistance < 0 ||
            config.MaximumCandidates <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(config));
        }

        var resources = BuildResourceMap(world, materials);
        ExpandResourcesToReachableNeighbors(resources, world.Width, world.Height);
        var candidates = new List<(ulong Priority, int X, int Y)>();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (zones[x + world.Width * y] != WorldZone.Plains || !IsWalkable(world, x, y, materials))
                {
                    continue;
                }

                var key = ((ulong)(uint)y << 32) | (uint)x;
                candidates.Add((DeterministicRandom.CounterValue(
                    seed, RandomStreamNames.Spawn, 0, key), x, y));
            }
        }

        candidates.Sort(static (left, right) =>
        {
            var comparison = left.Priority.CompareTo(right.Priority);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Y.CompareTo(right.Y);
            return comparison != 0 ? comparison : left.X.CompareTo(right.X);
        });

        var selected = new List<BlockCoordinate>(config.Count);
        var considered = 0;
        foreach (var candidate in candidates)
        {
            if (considered++ >= config.MaximumCandidates)
            {
                break;
            }

            var reachable = FindReachableResources(world, resources, materials, candidate.X, candidate.Y, config);
            if ((reachable & SpawnResourceMask.Required) != SpawnResourceMask.Required)
            {
                continue;
            }

            if (selected.Any(site =>
                    Math.Abs(site.X - candidate.X) + Math.Abs(site.Y - candidate.Y) < config.MinimumSpacing))
            {
                continue;
            }

            var surface = world.GetSurfaceColumn(candidate.X, candidate.Y);
            selected.Add(new BlockCoordinate(candidate.X, candidate.Y, surface.SurfaceZ + 1));
            if (selected.Count == config.Count)
            {
                return new SpawnSiteSelectionResult(selected, null);
            }
        }

        return new SpawnSiteSelectionResult(
            selected,
            $"Only {selected.Count} of {config.Count} spawn sites met terrain, spacing, and resource-distance requirements.");
    }

    private static SpawnResourceMask[] BuildResourceMap(WorldGrid world, WorldResourceMaterials materials)
    {
        var resourceMap = new SpawnResourceMask[checked(world.Width * world.Height)];
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var index = x + world.Width * y;
                var column = world.GetSurfaceColumn(x, y);
                var mask = SpawnResourceMask.None;
                if (column.MaterialId == materials.Stone)
                {
                    mask |= SpawnResourceMask.Stone;
                }

                if (column.MaterialId == materials.Farmland)
                {
                    mask |= SpawnResourceMask.Farmland;
                }

                resourceMap[index] = mask;
            }
        }

        foreach (var entry in world.EnumerateOverrides())
        {
            var index = entry.Key.X + world.Width * entry.Key.Y;
            var materialId = entry.Value.MaterialId;
            if (materialId == materials.Water)
            {
                resourceMap[index] |= SpawnResourceMask.Water;
            }

            if (materials.Wood.Contains(materialId))
            {
                resourceMap[index] |= SpawnResourceMask.Wood;
            }

            if (materialId == materials.Stone)
            {
                resourceMap[index] |= SpawnResourceMask.Stone;
            }

            if (materialId == materials.Coal)
            {
                resourceMap[index] |= SpawnResourceMask.Coal;
            }

            if (materialId == materials.IronOre && IsExposedIron(world, entry.Key))
            {
                resourceMap[index] |= SpawnResourceMask.ExposedIronOre;
            }
        }

        return resourceMap;
    }

    private static bool IsExposedIron(WorldGrid world, BlockCoordinate iron)
    {
        var surface = world.GetSurfaceColumn(iron.X, iron.Y);
        if (iron.Z >= surface.SurfaceZ)
        {
            return true;
        }

        foreach (var (offsetX, offsetY) in Neighbors)
        {
            if (world.TryGetSurfaceColumn(iron.X + offsetX, iron.Y + offsetY, out var neighbor) &&
                neighbor.SurfaceZ < iron.Z)
            {
                return true;
            }
        }

        return false;
    }

    private static void ExpandResourcesToReachableNeighbors(
        SpawnResourceMask[] resources,
        int width,
        int height)
    {
        var expanded = (SpawnResourceMask[])resources.Clone();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var mask = resources[x + width * y];
                if (mask == SpawnResourceMask.None)
                {
                    continue;
                }

                foreach (var (offsetX, offsetY) in Neighbors)
                {
                    var neighborX = x + offsetX;
                    var neighborY = y + offsetY;
                    if ((uint)neighborX < (uint)width && (uint)neighborY < (uint)height)
                    {
                        expanded[neighborX + width * neighborY] |= mask;
                    }
                }
            }
        }

        Array.Copy(expanded, resources, resources.Length);
    }

    private static SpawnResourceMask FindReachableResources(
        WorldGrid world,
        SpawnResourceMask[] resources,
        WorldResourceMaterials materials,
        int startX,
        int startY,
        SpawnSiteConfig config)
    {
        var distances = new int[resources.Length];
        Array.Fill(distances, -1);
        var queue = new Queue<int>();
        var startIndex = startX + world.Width * startY;
        distances[startIndex] = 0;
        queue.Enqueue(startIndex);
        var found = SpawnResourceMask.None;

        while (queue.TryDequeue(out var index))
        {
            var x = index % world.Width;
            var y = index / world.Width;
            var distance = distances[index];
            found |= resources[index];
            if ((found & SpawnResourceMask.Required) == SpawnResourceMask.Required)
            {
                return found;
            }

            if (distance >= config.MaximumResourceDistance)
            {
                continue;
            }

            foreach (var (offsetX, offsetY) in Neighbors)
            {
                var nextX = x + offsetX;
                var nextY = y + offsetY;
                if (!world.TryGetSurfaceColumn(nextX, nextY, out var nextSurface))
                {
                    continue;
                }

                var nextIndex = nextX + world.Width * nextY;
                if (distances[nextIndex] >= 0 ||
                    !IsWalkable(world, nextX, nextY, materials))
                {
                    continue;
                }

                var currentZ = world.GetSurfaceColumn(x, y).SurfaceZ;
                var rise = nextSurface.SurfaceZ - currentZ;
                if (rise > 1 || rise < -3)
                {
                    continue;
                }

                distances[nextIndex] = distance + 1;
                queue.Enqueue(nextIndex);
            }
        }

        return found;
    }

    private static bool IsWalkable(
        WorldGrid world,
        int x,
        int y,
        WorldResourceMaterials materials)
    {
        if (!world.TryGetSurfaceColumn(x, y, out var surface) ||
            surface.SurfaceZ + 2 >= world.VerticalSize)
        {
            return false;
        }

        for (var z = surface.SurfaceZ + 1; z <= surface.SurfaceZ + 2; z++)
        {
            if (world.TryGetBlockOverride(new BlockCoordinate(x, y, z), out var block) &&
                !block.IsAir &&
                (block.MaterialId == materials.Water || materials.SolidMaterials.Contains(block.MaterialId)))
            {
                return false;
            }
        }

        return true;
    }

}
