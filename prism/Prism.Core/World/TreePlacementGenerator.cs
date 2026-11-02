using Prism.Core.Determinism;

namespace Prism.Core.World;

public sealed record WorldTreeBlock(int X, int Y, int Z, ushort MaterialId);

public sealed record WorldTreeTemplate(string Id, IReadOnlyList<WorldTreeBlock> Blocks);

public sealed record ForestPlacementConfig(
    int CellSize = 5,
    int JitterRadius = 1,
    int ForestDensityPermille = 700,
    int PlainsDensityPermille = 75);

public sealed record PlacedTree(string TemplateId, BlockCoordinate Origin);

public static class TreePlacementGenerator
{
    public static IReadOnlyList<PlacedTree> Place(
        WorldGrid world,
        ReadOnlySpan<WorldZone> zones,
        ulong seed,
        IEnumerable<WorldTreeTemplate> templates,
        ForestPlacementConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(templates);
        if (zones.Length != checked(world.Width * world.Height))
        {
            throw new ArgumentException("Zone map length must match world dimensions.", nameof(zones));
        }

        config ??= new ForestPlacementConfig();
        if (config.CellSize <= 0 ||
            config.JitterRadius < 0 ||
            config.JitterRadius > (config.CellSize - 1) / 2 ||
            config.ForestDensityPermille is < 0 or > 1000 ||
            config.PlainsDensityPermille is < 0 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(config));
        }

        var sortedTemplates = templates.OrderBy(template => template.Id, StringComparer.Ordinal).ToArray();
        if (sortedTemplates.Length == 0 ||
            sortedTemplates.Any(template =>
                string.IsNullOrWhiteSpace(template.Id) ||
                template.Blocks is null ||
                template.Blocks.Count == 0 ||
                template.Blocks.Any(block => block.X < 0 || block.Y < 0 || block.Z < 0)) ||
            sortedTemplates.Select(template => template.Id).Distinct(StringComparer.Ordinal).Count() != sortedTemplates.Length)
        {
            throw new ArgumentException("Tree templates must have unique ids and non-empty non-negative blocks.",
                nameof(templates));
        }

        var placed = new List<PlacedTree>();
        var cellsX = (world.Width - 1) / config.CellSize + 1;
        var cellsY = (world.Height - 1) / config.CellSize + 1;
        for (var cellY = 0; cellY < cellsY; cellY++)
        {
            for (var cellX = 0; cellX < cellsX; cellX++)
            {
                var cellKey = ((ulong)(uint)cellY << 32) | (uint)cellX;
                var jitterX = GetRange(seed, cellKey, 1, config.JitterRadius * 2 + 1) - config.JitterRadius;
                var jitterY = GetRange(seed, cellKey, 2, config.JitterRadius * 2 + 1) - config.JitterRadius;
                var x = cellX * config.CellSize + config.CellSize / 2 + jitterX;
                var y = cellY * config.CellSize + config.CellSize / 2 + jitterY;
                if (!world.TryGetSurfaceColumn(x, y, out var surface))
                {
                    continue;
                }

                var zone = zones[x + world.Width * y];
                var density = zone switch
                {
                    WorldZone.Forest => config.ForestDensityPermille,
                    WorldZone.Plains => config.PlainsDensityPermille,
                    _ => 0,
                };
                var chance = DeterministicRandom.CounterValue(
                    seed, RandomStreamNames.Forest, 0, cellKey ^ 0xA0761D6478BD642FUL) % 1000;
                if (chance >= (uint)density)
                {
                    continue;
                }

                var templateIndex = (int)(DeterministicRandom.CounterValue(
                    seed, RandomStreamNames.Forest, 0, cellKey ^ 0xE7037ED1A0B428DBUL) %
                    (ulong)sortedTemplates.Length);
                var template = sortedTemplates[templateIndex];
                var origin = new BlockCoordinate(x, y, surface.SurfaceZ + 1);
                var blocks = template.Blocks
                    .Select(block => new BlockCoordinate(
                        origin.X + block.X, origin.Y + block.Y, origin.Z + block.Z))
                    .ToArray();
                if (blocks.Any(block =>
                        (uint)block.X >= (uint)world.Width ||
                        (uint)block.Y >= (uint)world.Height ||
                        (uint)block.Z >= (uint)world.VerticalSize ||
                        world.TryGetBlockOverride(block, out _)))
                {
                    continue;
                }

                for (var index = 0; index < template.Blocks.Count; index++)
                {
                    var block = template.Blocks[index];
                    world.SetBlockOverride(
                        blocks[index],
                        new BlockOverride(block.MaterialId, 0, IsAir: false));
                }

                placed.Add(new PlacedTree(template.Id, origin));
            }
        }

        return placed;
    }

    private static int GetRange(ulong seed, ulong cellKey, ulong axis, int exclusiveMax) =>
        (int)(DeterministicRandom.CounterValue(
            seed, RandomStreamNames.Forest, axis, cellKey) % (uint)exclusiveMax);
}
