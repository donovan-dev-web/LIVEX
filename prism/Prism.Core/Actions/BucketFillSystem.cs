using Prism.Core.Accounting;
using Prism.Core.World;

namespace Prism.Core.Actions;

/// <summary>
/// Fills an empty wooden bucket from a water source (block or water_access structure) and turns
/// it into a water_bucket. Used by the water chain (PRISM-096): the bucket can then be carried
/// and consumed elsewhere.
/// </summary>
public static class BucketFillSystem
{
    public sealed record FillResult(bool Succeeded, string? ErrorCode);

    public static FillResult FillFromSource(
        WorldGrid world,
        AgentBody agent,
        ushort woodenBucketItemTypeId,
        ushort waterBucketItemTypeId,
        BlockCoordinate target,
        bool targetPerceived,
        Func<ushort, bool> isWaterSource)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(isWaterSource);

        if (!targetPerceived)
        {
            return new FillResult(false, "not_perceived");
        }

        if (!BlockActionSystem.IsAdjacent(agent.Position, target))
        {
            return new FillResult(false, "not_adjacent");
        }

        if (agent.Inventory.Count(woodenBucketItemTypeId) == 0)
        {
            return new FillResult(false, "bucket_missing");
        }

        ushort materialId;
        if (world.TryGetBlockOverride(target, out var block))
        {
            if (block.IsAir)
            {
                return new FillResult(false, "not_water");
            }

            materialId = block.MaterialId;
        }
        else if (world.TryGetSurfaceColumn(target.X, target.Y, out var surface) &&
                 target.Z == surface.SurfaceZ)
        {
            materialId = surface.MaterialId;
        }
        else
        {
            return new FillResult(false, "not_water");
        }

        if (!isWaterSource(materialId))
        {
            return new FillResult(false, "not_water");
        }

        if (!agent.Inventory.TryTake(woodenBucketItemTypeId, 1, out _))
        {
            return new FillResult(false, "bucket_missing");
        }

        if (!agent.Inventory.TryAdd(waterBucketItemTypeId, 1, 1, out _))
        {
            // Restore the empty bucket if the filled one cannot be carried.
            agent.Inventory.TryAdd(woodenBucketItemTypeId, 1, 1, out _);
            return new FillResult(false, "inventory_full");
        }

        return new FillResult(true, null);
    }

    public static FillResult FillFromStructure(
        AgentBody agent,
        ushort woodenBucketItemTypeId,
        ushort waterBucketItemTypeId,
        bool hasWaterAccess)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (!hasWaterAccess)
        {
            return new FillResult(false, "no_water_access");
        }

        if (agent.Inventory.Count(woodenBucketItemTypeId) == 0)
        {
            return new FillResult(false, "bucket_missing");
        }

        if (!agent.Inventory.TryTake(woodenBucketItemTypeId, 1, out _))
        {
            return new FillResult(false, "bucket_missing");
        }

        if (!agent.Inventory.TryAdd(waterBucketItemTypeId, 1, 1, out _))
        {
            agent.Inventory.TryAdd(woodenBucketItemTypeId, 1, 1, out _);
            return new FillResult(false, "inventory_full");
        }

        return new FillResult(true, null);
    }
}