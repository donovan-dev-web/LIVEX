namespace Simulation.Core.World;

/// <summary>
/// Ligne de vue (SYNE-011, ADR-013) : un segment entre deux positions du monde
/// est occulté si un obstacle l'intersecte. Pure fonction déterministe ;
/// aucune approximation d'échantillonnage (calcul géométrique exact).
/// </summary>
public static class LineOfSight
{
    /// <summary>
    /// Vrai si le segment [<paramref name="from"/>, <paramref name="to"/>] ne
    /// traverse aucun des <paramref name="obstacles"/> (ligne de vue dégagée).
    /// </summary>
    public static bool IsClear(Position from, Position to, IReadOnlyList<Obstacle> obstacles)
    {
        ArgumentNullException.ThrowIfNull(obstacles);

        foreach (Obstacle obstacle in obstacles)
        {
            if (SegmentIntersectsDisk(from, to, obstacle.Position, obstacle.Radius))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SegmentIntersectsDisk(Position start, Position end, Position center, double radius)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double lengthSquared = (dx * dx) + (dy * dy);

        if (lengthSquared == 0.0)
        {
            return start.DistanceTo(center) <= radius;
        }

        double t = (((center.X - start.X) * dx) + ((center.Y - start.Y) * dy)) / lengthSquared;
        t = Math.Clamp(t, 0.0, 1.0);

        double closestX = start.X + (t * dx);
        double closestY = start.Y + (t * dy);
        double distanceSquared = ((center.X - closestX) * (center.X - closestX)) + ((center.Y - closestY) * (center.Y - closestY));
        return distanceSquared <= radius * radius;
    }
}