using Prism.Core.World;

namespace Prism.Viewer;

public sealed class IncrementalTerrainMesher
{
    private readonly Dictionary<int, TerrainRegionMesh> _meshes = [];
    private int[] _topHeights = [];
    private ushort[] _topMaterials = [];
    private long _worldRevision = -1;

    public IReadOnlyDictionary<int, TerrainRegionMesh> Meshes =>
        new Dictionary<int, TerrainRegionMesh>(_meshes);

    public void InvalidateAll()
    {
        _meshes.Clear();
        _worldRevision = -1;
    }

    public IReadOnlyList<TerrainRegionMesh> Update(
        WorldGrid world,
        ReadOnlySpan<WorldZone> zones,
        TerrainMaterialPalette palette,
        int maximumRegions)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRegions);
        if (zones.Length != checked(world.Width * world.Height))
        {
            throw new ArgumentException("Zone map length must match world dimensions.", nameof(zones));
        }

        var pendingRegions = Enumerable.Range(0, world.RegionCount)
            .Where(regionId => !_meshes.ContainsKey(regionId) || world.IsRegionDirty(regionId))
            .Take(maximumRegions)
            .ToArray();
        if (pendingRegions.Length == 0)
        {
            return [];
        }

        if (_worldRevision != world.Revision)
        {
            TerrainMeshBuilder.BuildTopSurfaces(world, out _topHeights, out _topMaterials);
            _worldRevision = world.Revision;
        }

        var updated = new List<TerrainRegionMesh>(pendingRegions.Length);
        foreach (var regionId in pendingRegions)
        {
            var mesh = TerrainMeshBuilder.BuildRegion(
                world,
                zones,
                palette,
                _topHeights,
                _topMaterials,
                regionId);
            _meshes[regionId] = mesh;
            world.ClearRegionDirty(regionId);
            updated.Add(mesh);
        }

        return updated;
    }
}
