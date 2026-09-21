namespace Simulation.Core.Entities;

/// <summary>
/// L'ensemble des 8 traits (DATA_MODEL.md §3.2 — plage [0, 2], neutre 1.0).
/// Type immuable : tous les invariants sont validés à la construction.
/// </summary>
public sealed class TraitSet
{
    public const double Min = 0.0;
    public const double Max = 2.0;
    public const double Neutral = 1.0;

    public static readonly IReadOnlyList<string> TraitNames =
        ["bravery", "curiosity", "sociability", "greed", "pessimism", "aggression", "strength", "speed"];

    private readonly Dictionary<string, double> _values;

    public TraitSet(IReadOnlyDictionary<string, double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (string name in TraitNames)
        {
            if (!values.TryGetValue(name, out double value))
            {
                throw new ArgumentException($"Trait manquant : \"{name}\".");
            }

            _values[name] = Validate(name, value);
        }
    }

    public static TraitSet NeutralAll =>
        new(TraitNames.ToDictionary(name => name, _ => Neutral, StringComparer.Ordinal));

    public double this[string name]
    {
        get
        {
            if (!_values.TryGetValue(name, out double value))
            {
                throw new KeyNotFoundException($"Trait inconnu : \"{name}\".");
            }

            return value;
        }
    }

    public IEnumerable<KeyValuePair<string, double>> Values => _values;

    public double Mean => _values.Values.Sum() / _values.Count;

    private static double Validate(string name, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < Min || value > Max)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Le trait \"{name}\" doit être dans [{Min}, {Max}] (reçu : {value}).");
        }

        return value;
    }
}