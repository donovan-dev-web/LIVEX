namespace Simulation.Core.World;

/// <summary>
/// Grille spatiale uniforme (ADR — perceptualité O(quasi-linéaire), Monographie §3.5.4).
/// Cellules de taille configurable ; requêtes par rayon (centre ± r) en ordre
/// déterministe : cellules en parcours ligne-par-ligne, entités dans l'ordre d'insertion.
/// </summary>
public sealed class SpatialGrid
{
    private readonly double _cellSize;
    private readonly int _cellCountX;
    private readonly int _cellCountY;
    private readonly List<Simulation.Core.Entities.Entity>[] _cells;
    private readonly Dictionary<ulong, (int X, int Y)> _cellOfEntity = new();

    public SpatialGrid(WorldSize size, double cellSize)
    {
        if (cellSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), "La taille de cellule doit être strictement positive.");
        }

        Size = size;
        _cellSize = cellSize;
        _cellCountX = (int)Math.Ceiling(size.Width / cellSize);
        _cellCountY = (int)Math.Ceiling(size.Height / cellSize);
        _cells = new List<Simulation.Core.Entities.Entity>[_cellCountX * _cellCountY];
        for (int i = 0; i < _cells.Length; i++)
        {
            _cells[i] = [];
        }
    }

    public WorldSize Size { get; }

    public double CellSize => _cellSize;

    public int CellCountX => _cellCountX;

    public int CellCountY => _cellCountY;

    public void Add(Simulation.Core.Entities.Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (_cellOfEntity.ContainsKey(entity.Id.Value))
        {
            throw new InvalidOperationException($"L'entité {entity.Id.Value} est déjà indexée dans la grille.");
        }

        (int cx, int cy) = CellOf(entity.Position);
        _cells[(cy * _cellCountX) + cx].Add(entity);
        _cellOfEntity[entity.Id.Value] = (cx, cy);
    }

    public void Move(Simulation.Core.Entities.Entity entity, Position newPosition)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!_cellOfEntity.TryGetValue(entity.Id.Value, out (int X, int Y) oldCell))
        {
            throw new InvalidOperationException($"L'entité {entity.Id.Value} n'est pas indexée dans la grille.");
        }

        (int newX, int newY) = CellOf(newPosition);
        if (oldCell == (newX, newY))
        {
            entity.SetPosition(newPosition);
            return;
        }

        bool removed = _cells[(oldCell.Y * _cellCountX) + oldCell.X].Remove(entity);
        if (!removed)
        {
            throw new InvalidOperationException($"Incohérence de grille pour l'entité {entity.Id.Value}.");
        }

        _cells[(newY * _cellCountX) + newX].Add(entity);
        _cellOfEntity[entity.Id.Value] = (newX, newY);
        entity.SetPosition(newPosition);
    }

    /// <summary>
    /// Entités dans le rayon <paramref name="radius"/> autour de <paramref name="center"/>,
    /// ordre déterministe. Ne renvoie pas l'entité <paramref name="center"/> de référence
    /// (<paramref name="excludeId"/>, nullable).
    /// </summary>
    public IReadOnlyList<Simulation.Core.Entities.Entity> QueryCircle(Position center, double radius, ulong? excludeId = null)
    {
        if (radius <= 0)
        {
            return [];
        }

        var result = new List<Simulation.Core.Entities.Entity>();
        int startX = Math.Max(0, CellIndexLow(center.X - radius));
        int endX = Math.Min(_cellCountX - 1, CellIndexHigh(center.X + radius));
        int startY = Math.Max(0, CellIndexLow(center.Y - radius));
        int endY = Math.Min(_cellCountY - 1, CellIndexHigh(center.Y + radius));

        for (int cy = startY; cy <= endY; cy++)
        {
            for (int cx = startX; cx <= endX; cx++)
            {
                foreach (Simulation.Core.Entities.Entity entity in _cells[(cy * _cellCountX) + cx])
                {
                    if (excludeId.HasValue && entity.Id.Value == excludeId.Value)
                    {
                        continue;
                    }

                    if (entity.Position.DistanceTo(center) <= radius)
                    {
                        result.Add(entity);
                    }
                }
            }
        }

        return result;
    }

    public int Count => _cellOfEntity.Count;

    private (int X, int Y) CellOf(Position position)
    {
        Position clamped = Position.Clamp(position, Size);
        int x = CellIndex(clamped.X);
        int y = CellIndex(clamped.Y);
        return (x, y);
    }

    private int CellIndex(double coordinate)
    {
        int index = (int)Math.Floor(coordinate / _cellSize);
        return Math.Clamp(index, 0, Math.Max(0, _cellCountX - 1));
    }

    private int CellIndexLow(double coordinate)
    {
        int index = (int)Math.Floor(coordinate / _cellSize);
        return Math.Max(0, index);
    }

    private int CellIndexHigh(double coordinate)
    {
        int index = (int)Math.Floor(coordinate / _cellSize);
        return Math.Min(_cellCountX - 1, index);
    }
}