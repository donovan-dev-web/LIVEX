namespace Prism.Core.World;

public sealed record StructureBlockRule(int X, int Y, int Z, ushort MaterialId);

public sealed record StructureMaterialRule(ushort ItemTypeId, int Quantity);

public sealed record StructureEffectRule(
    string Type,
    BlockCoordinate MinimumOffset,
    BlockCoordinate MaximumOffset,
    int FatigueRecoveryBonusPermille);

public sealed record StructureTemplateRule(
    string Id,
    int SizeX,
    int SizeY,
    int SizeZ,
    IReadOnlyList<StructureBlockRule> Blocks,
    bool RequiresFlatGround,
    bool RequiresClearVolume,
    int WorkTicks,
    int IntegrityThresholdPermille,
    StructureEffectRule? Effect = null,
    IReadOnlyList<StructureMaterialRule>? Materials = null);

public sealed record StructurePlacementResult(bool Succeeded, string? ErrorCode)
{
    public static StructurePlacementResult Success() => new(true, null);
    public static StructurePlacementResult Failure(string errorCode) => new(false, errorCode);
}

public static class StructurePlacementValidator
{
    public static StructurePlacementResult Validate(
        WorldGrid world,
        StructureTemplateRule template,
        BlockCoordinate origin)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(template);
        ValidateTemplate(template);

        if (!IsOriginWithinBounds(world, template, origin))
        {
            return StructurePlacementResult.Failure("target_out_of_world");
        }

        if (template.RequiresFlatGround)
        {
            for (var y = origin.Y; y < origin.Y + template.SizeY; y++)
            {
                for (var x = origin.X; x < origin.X + template.SizeX; x++)
                {
                    if (world.GetSurfaceColumn(x, y).SurfaceZ != origin.Z - 1)
                    {
                        return StructurePlacementResult.Failure("terrain_unsuitable");
                    }
                }
            }
        }

        if (template.RequiresClearVolume)
        {
            for (var z = origin.Z; z < origin.Z + template.SizeZ; z++)
            {
                for (var y = origin.Y; y < origin.Y + template.SizeY; y++)
                {
                    for (var x = origin.X; x < origin.X + template.SizeX; x++)
                    {
                        if (world.GetSurfaceColumn(x, y).SurfaceZ >= z ||
                            HasSolidOverride(world, new BlockCoordinate(x, y, z)))
                        {
                            return StructurePlacementResult.Failure("volume_not_clear");
                        }
                    }
                }
            }
        }
        else
        {
            foreach (var block in template.Blocks)
            {
                var coordinate = new BlockCoordinate(
                    origin.X + block.X,
                    origin.Y + block.Y,
                    origin.Z + block.Z);
                if (world.GetSurfaceColumn(coordinate.X, coordinate.Y).SurfaceZ >= coordinate.Z ||
                    HasSolidOverride(world, coordinate))
                {
                    return StructurePlacementResult.Failure("volume_not_clear");
                }
            }
        }

        return StructurePlacementResult.Success();
    }

    internal static void ValidateTemplate(StructureTemplateRule template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var blockCoordinates = new HashSet<BlockCoordinate>();
        if (template.SizeX <= 0 ||
            template.SizeY <= 0 ||
            template.SizeZ <= 0 ||
            string.IsNullOrWhiteSpace(template.Id) ||
            template.WorkTicks < 0 ||
            template.IntegrityThresholdPermille is < 0 or > 1000 ||
            (template.Effect is { } effect &&
             (string.IsNullOrWhiteSpace(effect.Type) ||
              effect.FatigueRecoveryBonusPermille < 0 ||
              effect.MinimumOffset.X < 0 ||
              effect.MinimumOffset.Y < 0 ||
              effect.MinimumOffset.Z < 0 ||
              effect.MaximumOffset.X < effect.MinimumOffset.X ||
              effect.MaximumOffset.Y < effect.MinimumOffset.Y ||
              effect.MaximumOffset.Z < effect.MinimumOffset.Z ||
              effect.MaximumOffset.X >= template.SizeX ||
              effect.MaximumOffset.Y >= template.SizeY ||
              effect.MaximumOffset.Z >= template.SizeZ)) ||
            template.Blocks is null ||
            (template.Materials ?? []).Any(material => material is null || material.Quantity <= 0) ||
            (template.Materials ?? []).Select(material => material.ItemTypeId).Distinct().Count() !=
            (template.Materials ?? []).Count ||
            ((template.Materials?.Count ?? 0) > 0 && template.WorkTicks == 0) ||
            template.Blocks.Any(block =>
                block is null ||
                (uint)block.X >= (uint)template.SizeX ||
                (uint)block.Y >= (uint)template.SizeY ||
                (uint)block.Z >= (uint)template.SizeZ ||
                !blockCoordinates.Add(new BlockCoordinate(block.X, block.Y, block.Z))))
        {
            throw new ArgumentException("The compiled structure template is invalid.", nameof(template));
        }
    }

    internal static bool IsOriginWithinBounds(
        WorldGrid world,
        StructureTemplateRule template,
        BlockCoordinate origin) =>
        origin.Z > 0 &&
        origin.X >= 0 &&
        origin.Y >= 0 &&
        (long)origin.X + template.SizeX <= world.Width &&
        (long)origin.Y + template.SizeY <= world.Height &&
        (long)origin.Z + template.SizeZ <= world.VerticalSize;

    private static bool HasSolidOverride(WorldGrid world, BlockCoordinate coordinate) =>
        world.TryGetBlockOverride(coordinate, out var block) && !block.IsAir;
}
