namespace Prism.Viewer;

/// <summary>
/// Carte de chaleur des chemins fréquentés (RENDERING_SPEC.md §5) : le viewer compte les
/// cellules occupées par les agents au fil des ticks ; rien n'est simulé ni renvoyé au host.
/// </summary>
public sealed class ViewerMovementHeatmap
{
    private readonly Dictionary<int, ViewerHeatCell> _cells = [];
    private readonly int _width;
    private readonly int _height;

    public ViewerMovementHeatmap(int worldWidth, int worldHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(worldWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(worldHeight);
        _width = worldWidth;
        _height = worldHeight;
    }

    public int Peak { get; private set; }

    public int CellCount => _cells.Count;

    public IReadOnlyList<ViewerHeatCell> Cells => _cells.Values.ToArray();

    public void Observe(IEnumerable<ViewerAgentSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        foreach (var sample in samples)
        {
            var x = (int)MathF.Floor(sample.X);
            var y = (int)MathF.Floor(sample.Y);
            var z = (int)MathF.Floor(sample.Z);
            Visit(x, y, z);
        }
    }

    public void Visit(int x, int y, int z)
    {
        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height)
        {
            return;
        }

        var key = x + _width * y;
        var count = _cells.TryGetValue(key, out var cell) ? cell.Count + 1 : 1;
        _cells[key] = new ViewerHeatCell(x, y, z, count);
        Peak = Math.Max(Peak, count);
    }

    public void Clear()
    {
        _cells.Clear();
        Peak = 0;
    }
}

public readonly record struct ViewerHeatCell(int X, int Y, int Z, int Count);
