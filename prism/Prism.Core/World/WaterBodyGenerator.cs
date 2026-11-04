namespace Prism.Core.World;

public static class WaterBodyGenerator
{
    public static int FillBelowSeaLevel(WorldGrid world, ushort waterMaterialId, int seaLevelZ)
    {
        ArgumentNullException.ThrowIfNull(world);
        if ((uint)seaLevelZ >= (uint)world.VerticalSize)
        {
            throw new ArgumentOutOfRangeException(nameof(seaLevelZ));
        }

        var filled = 0;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var surfaceZ = world.GetSurfaceColumn(x, y).SurfaceZ;
                for (var z = surfaceZ + 1; z <= seaLevelZ; z++)
                {
                    if (world.TryGetBlockOverride(new BlockCoordinate(x, y, z), out _))
                    {
                        continue;
                    }

                    world.SetBlockOverride(
                        new BlockCoordinate(x, y, z),
                        new BlockOverride(waterMaterialId, 0, IsAir: false));
                    filled++;
                }
            }
        }

        return filled;
    }
}
