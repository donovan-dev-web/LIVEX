namespace Simulation.Core.World;

/// <summary>
/// Position continue 2D dans le monde. Monde non-toroidal : la position est
/// clampée dans [[0, Width], [0, Height]] (DATA_MODEL.md §2).
/// </summary>
public readonly record struct Position(double X, double Y)
{
    public static Position Clamp(Position position, WorldSize size)
    {
        double x = Math.Clamp(position.X, 0.0, size.Width);
        double y = Math.Clamp(position.Y, 0.0, size.Height);
        return new Position(x, y);
    }

    public double DistanceTo(Position other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}