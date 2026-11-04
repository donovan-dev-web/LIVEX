using System.Collections.Frozen;

namespace Prism.Core.World;

public sealed class MovementRules
{
    public MovementRules(
        IEnumerable<ushort> solidMaterials,
        IEnumerable<ushort> liquidMaterials,
        int maximumAscent = 1,
        int maximumDescent = 3,
        IEnumerable<ushort>? pathMaterials = null,
        int pathSpeedupPermille = 2000)
    {
        ArgumentNullException.ThrowIfNull(solidMaterials);
        ArgumentNullException.ThrowIfNull(liquidMaterials);
        if (maximumAscent < 0 || maximumDescent < 0 ||
            pathSpeedupPermille is < 1000 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(MovementRules));
        }

        SolidMaterials = solidMaterials.ToFrozenSet();
        LiquidMaterials = liquidMaterials.ToFrozenSet();
        PathMaterials = (pathMaterials ?? []).ToFrozenSet();
        if (SolidMaterials.Overlaps(LiquidMaterials))
        {
            throw new ArgumentException("A material cannot be both solid and liquid.");
        }

        if (!PathMaterials.IsSubsetOf(SolidMaterials))
        {
            throw new ArgumentException("Walkable path materials must be solid.");
        }

        PathSpeedupPermille = pathSpeedupPermille;
        MaximumAscent = maximumAscent;
        MaximumDescent = maximumDescent;
    }

    public FrozenSet<ushort> SolidMaterials { get; }
    public FrozenSet<ushort> LiquidMaterials { get; }
    public FrozenSet<ushort> PathMaterials { get; }
    public int PathSpeedupPermille { get; }
    public int MaximumAscent { get; }
    public int MaximumDescent { get; }
}

public readonly record struct MovementNode(int X, int Y, int FeetZ);

public readonly record struct MovementEdge(MovementNode Destination, int Cost);

public sealed class MovementGraph
{
    private static readonly (int X, int Y, int Cost)[] NeighborOffsets =
    [
        (0, -1, 1000),
        (-1, 0, 1000),
        (1, 0, 1000),
        (0, 1, 1000),
        (-1, -1, 1414),
        (1, -1, 1414),
        (-1, 1, 1414),
        (1, 1, 1414),
    ];

    public MovementGraph(WorldGrid world, MovementRules rules)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rules);
        World = world;
        Rules = rules;
    }

    public WorldGrid World { get; }
    public MovementRules Rules { get; }

    public bool CanStandAt(int x, int y, out int feetZ)
    {
        feetZ = 0;
        if (!World.TryGetSurfaceColumn(x, y, out var surface))
        {
            return false;
        }

        var floorCoordinate = new BlockCoordinate(x, y, surface.SurfaceZ);
        if (World.TryGetBlockOverride(floorCoordinate, out var floorOverride))
        {
            if (floorOverride.IsAir || !Rules.SolidMaterials.Contains(floorOverride.MaterialId))
            {
                return false;
            }
        }
        else if (!Rules.SolidMaterials.Contains(surface.MaterialId))
        {
            return false;
        }

        feetZ = surface.SurfaceZ + 1;
        for (var z = feetZ; z < feetZ + 2; z++)
        {
            if ((uint)z >= (uint)World.VerticalSize)
            {
                return false;
            }

            if (World.TryGetBlockOverride(new BlockCoordinate(x, y, z), out var block))
            {
                if (block.IsAir)
                {
                    continue;
                }

                if (Rules.LiquidMaterials.Contains(block.MaterialId) ||
                    Rules.SolidMaterials.Contains(block.MaterialId))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public MovementNode? GetNode(int x, int y)
    {
        return CanStandAt(x, y, out var feetZ)
            ? new MovementNode(x, y, feetZ)
            : null;
    }

    public IEnumerable<MovementEdge> GetNeighbors(MovementNode from)
    {
        foreach (var (offsetX, offsetY, cost) in NeighborOffsets)
        {
            var x = from.X + offsetX;
            var y = from.Y + offsetY;
            if (!CanStandAt(x, y, out var feetZ))
            {
                continue;
            }

            var deltaZ = feetZ - from.FeetZ;
            if (deltaZ > Rules.MaximumAscent || deltaZ < -Rules.MaximumDescent)
            {
                continue;
            }

            if (offsetX != 0 && offsetY != 0 &&
                (!CanTraverseCardinal(from, from.X + offsetX, from.Y) ||
                 !CanTraverseCardinal(from, from.X, from.Y + offsetY)))
            {
                continue;
            }

            var destination = new MovementNode(x, y, feetZ);
            var edgeCost = cost;
            if (IsPath(from) && IsPath(destination))
            {
                edgeCost = Math.Max(1, (cost * 1000) / Rules.PathSpeedupPermille);
            }

            yield return new MovementEdge(destination, edgeCost);
        }
    }

    private bool IsPath(MovementNode node)
    {
        var surface = World.GetSurfaceColumn(node.X, node.Y);
        var coordinate = new BlockCoordinate(node.X, node.Y, surface.SurfaceZ);
        var materialId = World.TryGetBlockOverride(coordinate, out var block) && !block.IsAir
            ? block.MaterialId
            : surface.MaterialId;
        return Rules.PathMaterials.Contains(materialId);
    }

    private bool CanTraverseCardinal(MovementNode from, int x, int y)
    {
        return CanStandAt(x, y, out var feetZ) &&
               feetZ - from.FeetZ <= Rules.MaximumAscent &&
               feetZ - from.FeetZ >= -Rules.MaximumDescent;
    }
}
