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
    private readonly long[] _regionRevisions;
    private readonly int[] _overrideCountsByRegion;
    private readonly SortedSet<BlockCoordinate>[] _overridesByRegion;

    /// <summary>
    /// Write counter of every column, bumped for the written column and its four cardinal
    /// neighbours. A block's visibility is a function of its own column and of those neighbours
    /// (the exposure test asks them whether they are open), so this is the exact dependency of a
    /// perception window, one step finer than a region (PRISM-119).
    /// <para>
    /// Never serialised and never read by the simulation: it is a cache key. Wraparound at
    /// <see cref="int.MaxValue"/> writes to the same column is accepted — a wrong answer would
    /// need two writes to one cell to land exactly 2^31 apart in the same comparison window.
    /// </para>
    /// </summary>
    private int[]? _columnRevisions;

    /// <summary>
    /// Overrides of one column, in Z order. Indexed by column so a perception window can be slid
    /// one column at a time (PRISM-105): the leading band of a sliding window must read a single
    /// column's overrides, and walking a whole region's set for one column would cost more than
    /// the full rescan it replaces. Entries are kept in the canonical Z/Y/X order the rest of the
    /// grid uses, restricted to a column where Y and X are fixed.
    /// </summary>
    private readonly Dictionary<long, List<BlockCoordinate>> _overridesByColumn = [];
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
        _regionRevisions = new long[regionCount];
        _overrideCountsByRegion = new int[regionCount];
        _overridesByRegion = new SortedSet<BlockCoordinate>[regionCount];
        for (var regionId = 0; regionId < _regions.Length; regionId++)
        {
            _regions[regionId] = new WorldRegion(checked(regionSize * regionSize));
            // Same comparer as the global index, so a region enumeration stays in canonical Z/Y/X
            // order and every hash computed from it is unchanged.
            _overridesByRegion[regionId] = new SortedSet<BlockCoordinate>(CanonicalCoordinateComparer);
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

    /// <summary>
    /// Global revision of the grid: one bump per write, wherever it lands.
    /// <para>
    /// Kept because consumers that mirror the whole world need exactly this — the viewer mesher
    /// (<c>IncrementalTerrainMesher</c>), the host's "did the world move during this tick" check,
    /// and the path caches (<c>PathfindingCache</c>, <c>AgentLifecycleSystem._paths</c>), whose
    /// result depends on every cell A* explored and therefore cannot be narrowed to a region.
    /// A cache that depends on a known set of cells must use <see cref="GetRegionRevision"/>
    /// instead: the global counter invalidates every agent's window on every write, which is the
    /// defect PRISM-109 fixes.
    /// </para>
    /// </summary>
    public long Revision { get; private set; }

    /// <summary>
    /// Monotonic revision of one region, bumped only by writes whose cell belongs to it.
    /// <para>
    /// A revision is a write counter, not a content hash: two writes can cancel out and the
    /// revision still moves. Consumers that read across a region border (the exposure test reads
    /// the four cardinal neighbours, for instance) must include the neighbouring region in their
    /// key — the grid deliberately does not propagate a bump, because a consumer's dependency set
    /// is its own to declare and a propagated bump would invalidate far more than it must.
    /// </para>
    /// </summary>
    public long GetRegionRevision(int regionId)
    {
        _ = GetRegion(regionId);
        return _regionRevisions[regionId];
    }

    /// <summary>Revision of the region containing a cell; the cell must be inside the world.</summary>
    public long GetRegionRevisionAt(int x, int y)
    {
        ValidateHorizontalCoordinate(x, y);
        return _regionRevisions[(x / RegionSize) + RegionCountX * (y / RegionSize)];
    }

    /// <summary>
    /// Write counter of one column, or <c>0</c> outside the world. The four cardinal neighbours
    /// are bumped too, so a consumer that compares a set of columns sees every write that can
    /// change what it reads from them (PRISM-119).
    /// </summary>
    public int GetColumnRevision(int x, int y) =>
        _columnRevisions is not null && IsWithinHorizontalBounds(x, y)
            ? _columnRevisions[y * Width + x]
            : 0;

    /// <summary>
    /// Counters of a whole row, for a bulk comparison: a consumer that reuses a window compares
    /// row segments instead of calling <see cref="GetColumnRevision"/> once per cell, and the
    /// BCL's <c>SequenceEqual</c> does that compare with vector instructions. Empty when the grid
    /// has never been written or the row is outside the world.
    /// </summary>
    public ReadOnlySpan<int> GetColumnRevisionRow(int y)
    {
        if (_columnRevisions is null || (uint)y >= (uint)Height)
        {
            return ReadOnlySpan<int>.Empty;
        }

        return _columnRevisions.AsSpan(y * Width, Width);
    }

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
            BumpRevision(regionId);
            BumpColumnRevisions(x, y);
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

        var (regionId, _) = GetRegionAndLocalIndex(coordinate.X, coordinate.Y);
        if (!_overrides.ContainsKey(coordinate))
        {
            _overrideCountsByRegion[regionId]++;
            _overridesByRegion[regionId].Add(coordinate);
            AddToColumnIndex(coordinate);
        }

        _overrides[coordinate] = block;
        MarkRegionDirty(coordinate.X, coordinate.Y);
        BumpRevision(regionId);
        BumpColumnRevisions(coordinate.X, coordinate.Y);
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
        _overridesByRegion[regionId].Remove(coordinate);
        RemoveFromColumnIndex(coordinate);
        MarkRegionDirty(coordinate.X, coordinate.Y);
        BumpRevision(regionId);
        BumpColumnRevisions(coordinate.X, coordinate.Y);
        return true;
    }

    public IEnumerable<KeyValuePair<BlockCoordinate, BlockOverride>> EnumerateOverrides()
    {
        foreach (var entry in _overrides)
        {
            yield return entry;
        }
    }

    /// <summary>
    /// Number of overrides in one column. Combined with <see cref="GetColumnOverrideAt"/>, this
    /// lets a consumer read a column without allocating an enumerator or a list (PRISM-105).
    /// </summary>
    public int GetColumnOverrideCount(int x, int y)
    {
        ValidateHorizontalCoordinate(x, y);
        return _overridesByColumn.TryGetValue(ColumnKey(x, y), out var coordinates)
            ? coordinates.Count
            : 0;
    }

    /// <summary>Coordinate of the n-th override of a column, in ascending Z order.</summary>
    public BlockCoordinate GetColumnOverrideAt(int x, int y, int index)
    {
        ValidateHorizontalCoordinate(x, y);
        if (!_overridesByColumn.TryGetValue(ColumnKey(x, y), out var coordinates) ||
            (uint)index >= (uint)coordinates.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return coordinates[index];
    }

    private static long ColumnKey(int x, int y) => ((long)x << 32) | (uint)y;

    private void AddToColumnIndex(BlockCoordinate coordinate)
    {
        var key = ColumnKey(coordinate.X, coordinate.Y);
        if (!_overridesByColumn.TryGetValue(key, out var coordinates))
        {
            coordinates = [];
            _overridesByColumn.Add(key, coordinates);
        }

        // Ascending Z keeps the column in canonical order without sorting on every read.
        var low = 0;
        var high = coordinates.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (coordinates[middle].Z < coordinate.Z)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        coordinates.Insert(low, coordinate);
    }

    private void RemoveFromColumnIndex(BlockCoordinate coordinate)
    {
        var key = ColumnKey(coordinate.X, coordinate.Y);
        if (!_overridesByColumn.TryGetValue(key, out var coordinates))
        {
            return;
        }

        coordinates.Remove(coordinate);
        if (coordinates.Count == 0)
        {
            _overridesByColumn.Remove(key);
        }
    }

    public IEnumerable<KeyValuePair<BlockCoordinate, BlockOverride>> EnumerateRegionOverrides(int regionId)
    {
        _ = GetRegion(regionId);
        // Index-backed: walks only this region's overrides, in the same canonical order as the
        // global index, so hashes stay byte-for-byte identical while the cost drops from
        // O(all overrides in the world) to O(overrides in the region).
        foreach (var coordinate in _overridesByRegion[regionId])
        {
            yield return new KeyValuePair<BlockCoordinate, BlockOverride>(coordinate, _overrides[coordinate]);
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

    /// <summary>
    /// One write: the global revision moves, and only the written region's revision moves. The
    /// neighbour dirty flags are for the mesher and the delta stream, which need "what could look
    /// different"; revisions are the exact write key caches compare against.
    /// </summary>
    private void BumpRevision(int regionId)
    {
        Revision = checked(Revision + 1);
        _regionRevisions[regionId] = checked(_regionRevisions[regionId] + 1);
    }

    /// <summary>
    /// One write marks five columns: the written one and its four cardinal neighbours, the exact
    /// set of columns whose perception can change. Deliberately not propagated further: the four
    /// diagonals never decide a block's visibility, so bumping them would only invalidate work.
    /// </summary>
    private void BumpColumnRevisions(int x, int y)
    {
        _columnRevisions ??= new int[checked(Width * Height)];
        BumpColumnRevision(x, y);
        BumpColumnRevision(x - 1, y);
        BumpColumnRevision(x + 1, y);
        BumpColumnRevision(x, y - 1);
        BumpColumnRevision(x, y + 1);
    }

    private void BumpColumnRevision(int x, int y)
    {
        if (IsWithinHorizontalBounds(x, y))
        {
            _columnRevisions![y * Width + x] = unchecked(_columnRevisions[y * Width + x] + 1);
        }
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
