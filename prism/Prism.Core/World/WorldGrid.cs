namespace Prism.Core.World;

public sealed class WorldGrid
{
    private static readonly IComparer<BlockCoordinate> CanonicalCoordinateComparer =
        Comparer<BlockCoordinate>.Create(static (left, right) =>
        {
            var comparison = left.Z.CompareTo(right.Z);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Y.CompareTo(right.Y);
            return comparison != 0 ? comparison : left.X.CompareTo(right.X);
        });

    private readonly WorldRegion[] _regions;
    private readonly int[] _overrideCountsByRegion;
    private readonly SortedDictionary<BlockCoordinate, BlockOverride> _overrides =
        new(CanonicalCoordinateComparer);

    public WorldGrid(int width, int height, int regionSize = 16, int verticalSize = 64)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (regionSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(regionSize));
        }

        if (verticalSize is <= 0 or > byte.MaxValue + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(verticalSize));
        }

        Width = width;
        Height = height;
        RegionSize = regionSize;
        VerticalSize = verticalSize;
        RegionCountX = ((width - 1) / regionSize) + 1;
        RegionCountY = ((height - 1) / regionSize) + 1;
        var regionCount = checked(RegionCountX * RegionCountY);
        _regions = new WorldRegion[regionCount];
        _overrideCountsByRegion = new int[regionCount];
        for (var regionId = 0; regionId < _regions.Length; regionId++)
        {
            _regions[regionId] = new WorldRegion(checked(regionSize * regionSize));
        }
    }

    public int Width { get; }
    public int Height { get; }
    public int RegionSize { get; }
    public int VerticalSize { get; }
    public int RegionCountX { get; }
    public int RegionCountY { get; }
    public int RegionCount => _regions.Length;
    public int OverrideCount => _overrides.Count;
    public long Revision { get; private set; }

    public SurfaceColumn GetSurfaceColumn(int x, int y)
    {
        ValidateHorizontalCoordinate(x, y);
        var (regionId, localIndex) = GetRegionAndLocalIndex(x, y);
        return _regions[regionId].GetSurfaceColumn(localIndex);
    }

    public bool TryGetSurfaceColumn(int x, int y, out SurfaceColumn column)
    {
        if (!IsWithinHorizontalBounds(x, y))
        {
            column = default;
            return false;
        }

        var (regionId, localIndex) = GetRegionAndLocalIndex(x, y);
        column = _regions[regionId].GetSurfaceColumn(localIndex);
        return true;
    }

    public void SetSurfaceColumn(int x, int y, SurfaceColumn column)
    {
        ValidateHorizontalCoordinate(x, y);
        if (column.SurfaceZ >= VerticalSize)
        {
            throw new ArgumentOutOfRangeException(nameof(column), "Surface altitude must be inside the world bounds.");
        }

        var (regionId, localIndex) = GetRegionAndLocalIndex(x, y);
        if (_regions[regionId].SetSurfaceColumn(localIndex, column))
        {
            MarkRegionDirty(x, y);
            Revision = checked(Revision + 1);
        }
    }

    public bool TryGetRegionId(int x, int y, out int regionId)
    {
        if (!IsWithinHorizontalBounds(x, y))
        {
            regionId = -1;
            return false;
        }

        regionId = (x / RegionSize) + RegionCountX * (y / RegionSize);
        return true;
    }

    public bool IsRegionDirty(int regionId) => GetRegion(regionId).IsDirty;

    public void ClearRegionDirty(int regionId) => GetRegion(regionId).IsDirty = false;

    public int GetRegionOverrideCount(int regionId)
    {
        _ = GetRegion(regionId);
        return _overrideCountsByRegion[regionId];
    }

    public bool TryGetBlockOverride(BlockCoordinate coordinate, out BlockOverride block)
    {
        ValidateCoordinate(coordinate);
        return _overrides.TryGetValue(coordinate, out block);
    }

    public void SetBlockOverride(BlockCoordinate coordinate, BlockOverride block)
    {
        ValidateCoordinate(coordinate);
        if (_overrides.TryGetValue(coordinate, out var existing) && existing == block)
        {
            return;
        }

        if (!_overrides.ContainsKey(coordinate))
        {
            var (regionId, _) = GetRegionAndLocalIndex(coordinate.X, coordinate.Y);
            _overrideCountsByRegion[regionId]++;
        }

        _overrides[coordinate] = block;
        MarkRegionDirty(coordinate.X, coordinate.Y);
        Revision = checked(Revision + 1);
    }

    public bool RemoveBlockOverride(BlockCoordinate coordinate)
    {
        ValidateCoordinate(coordinate);
        if (!_overrides.Remove(coordinate))
        {
            return false;
        }

        var (regionId, _) = GetRegionAndLocalIndex(coordinate.X, coordinate.Y);
        _overrideCountsByRegion[regionId]--;
        MarkRegionDirty(coordinate.X, coordinate.Y);
        Revision = checked(Revision + 1);
        return true;
    }

    public IEnumerable<KeyValuePair<BlockCoordinate, BlockOverride>> EnumerateOverrides()
    {
        foreach (var entry in _overrides)
        {
            yield return entry;
        }
    }

    public IEnumerable<KeyValuePair<BlockCoordinate, BlockOverride>> EnumerateRegionOverrides(int regionId)
    {
        _ = GetRegion(regionId);
        var regionX = regionId % RegionCountX;
        var regionY = regionId / RegionCountX;
        var minX = regionX * RegionSize;
        var minY = regionY * RegionSize;
        var maxX = Math.Min(minX + RegionSize, Width);
        var maxY = Math.Min(minY + RegionSize, Height);

        foreach (var entry in _overrides)
        {
            var coordinate = entry.Key;
            if (coordinate.X >= minX && coordinate.X < maxX &&
                coordinate.Y >= minY && coordinate.Y < maxY)
            {
                yield return entry;
            }
        }
    }

    private WorldRegion GetRegion(int regionId)
    {
        if ((uint)regionId >= (uint)_regions.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(regionId));
        }

        return _regions[regionId];
    }

    private (int RegionId, int LocalIndex) GetRegionAndLocalIndex(int x, int y)
    {
        var regionX = x / RegionSize;
        var regionY = y / RegionSize;
        var localX = x % RegionSize;
        var localY = y % RegionSize;
        return (regionX + RegionCountX * regionY, localX + RegionSize * localY);
    }

    private bool IsWithinHorizontalBounds(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height;

    private void ValidateHorizontalCoordinate(int x, int y)
    {
        if (!IsWithinHorizontalBounds(x, y))
        {
            var parameterName = (uint)x >= (uint)Width ? nameof(x) : nameof(y);
            throw new ArgumentOutOfRangeException(
                parameterName, $"Horizontal coordinate ({x}, {y}) is outside the world.");
        }
    }

    private void ValidateCoordinate(BlockCoordinate coordinate)
    {
        ValidateHorizontalCoordinate(coordinate.X, coordinate.Y);
        if ((uint)coordinate.Z >= (uint)VerticalSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coordinate), $"Vertical coordinate {coordinate.Z} is outside the world.");
        }
    }

    private void MarkRegionDirty(int x, int y)
    {
        var (regionId, _) = GetRegionAndLocalIndex(x, y);
        _regions[regionId].IsDirty = true;
        MarkNeighborRegionDirty(x - 1, y, regionId);
        MarkNeighborRegionDirty(x + 1, y, regionId);
        MarkNeighborRegionDirty(x, y - 1, regionId);
        MarkNeighborRegionDirty(x, y + 1, regionId);
    }

    private void MarkNeighborRegionDirty(int x, int y, int currentRegionId)
    {
        if (TryGetRegionId(x, y, out var neighborRegionId) &&
            neighborRegionId != currentRegionId)
        {
            _regions[neighborRegionId].IsDirty = true;
        }
    }

    private sealed class WorldRegion(int cellCount)
    {
        private SurfaceColumn _uniformColumn = default;
        private byte[]? _surfaceZ;
        private ushort[]? _materialIds;
        private byte[]? _states;
        private ushort[]? _strataProfileIds;

        public bool IsDirty { get; set; }

        public SurfaceColumn GetSurfaceColumn(int index) =>
            _surfaceZ is null
                ? _uniformColumn
                : new SurfaceColumn(
                    _surfaceZ[index],
                    _materialIds![index],
                    _states![index],
                    _strataProfileIds![index]);

        public bool SetSurfaceColumn(int index, SurfaceColumn column)
        {
            if (GetSurfaceColumn(index) == column)
            {
                return false;
            }

            if (_surfaceZ is null)
            {
                MaterializeColumns();
            }

            _surfaceZ![index] = column.SurfaceZ;
            _materialIds![index] = column.MaterialId;
            _states![index] = column.State;
            _strataProfileIds![index] = column.StrataProfileId;
            return true;
        }

        private void MaterializeColumns()
        {
            _surfaceZ = new byte[cellCount];
            _materialIds = new ushort[cellCount];
            _states = new byte[cellCount];
            _strataProfileIds = new ushort[cellCount];
            Array.Fill(_surfaceZ, _uniformColumn.SurfaceZ);
            Array.Fill(_materialIds, _uniformColumn.MaterialId);
            Array.Fill(_states, _uniformColumn.State);
            Array.Fill(_strataProfileIds, _uniformColumn.StrataProfileId);
        }
    }
}
