namespace Simulation.Core.World;

/// <summary>
/// Dimensions du monde (défaut 500×500, Annexe H). Strictement positives.
/// </summary>
public readonly record struct WorldSize
{
    public WorldSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public static WorldSize From(Simulation.Core.Configuration.SimulationOptions options) =>
        new(options.Simulation.WorldWidth, options.Simulation.WorldHeight);
}