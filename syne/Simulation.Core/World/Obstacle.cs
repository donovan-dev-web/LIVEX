namespace Simulation.Core.World;

/// <summary>
/// Obstacle statique (DATA_MODEL.md §2) : disque {Position, Radius}.
/// Bloque le mouvement (collision simple) et, depuis U1, la ligne de vue
/// (SYNE-011 « un obstacle masque la ligne de vue » ; ADR-013).
/// </summary>
public sealed record Obstacle
{
    public Obstacle(string id, Position position, double radius)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Le rayon d'un obstacle doit être strictement positif.");
        }

        Id = id;
        Position = position;
        Radius = radius;
    }

    public string Id { get; }

    public Position Position { get; }

    public double Radius { get; }
}