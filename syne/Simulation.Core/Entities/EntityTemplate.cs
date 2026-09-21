namespace Simulation.Core.Entities;

/// <summary>
/// Paramétrage V0.1 (DATA_MODEL.md §3.2 « Entité A »/« Entité B ») : plages de
/// traits d'où les traits individuels sont hérités (tirage uniforme déterministe).
/// Défauts : bande 0.5–1.5 autour du neutre (initialisation Monographie §3.7.4).
/// </summary>
public sealed record EntityTemplate
{
    public EntityTemplate(string species, IReadOnlyDictionary<string, (double Min, double Max)> traitRanges)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(species);
        ArgumentNullException.ThrowIfNull(traitRanges);

        var ranges = new Dictionary<string, (double Min, double Max)>(StringComparer.Ordinal);
        foreach (string name in TraitSet.TraitNames)
        {
            if (!traitRanges.TryGetValue(name, out (double Min, double Max) range))
            {
                throw new ArgumentException($"Plage de trait manquante : \"{name}\".");
            }

            if (range.Max < range.Min)
            {
                throw new ArgumentException($"Plage invalide pour \"{name}\" : max {range.Max} &lt; min {range.Min}.");
            }

            if (range.Min < TraitSet.Min || range.Max > TraitSet.Max)
            {
                throw new ArgumentOutOfRangeException(nameof(traitRanges), $"Plage de \"{name}\" hors de [{TraitSet.Min}, {TraitSet.Max}].");
            }

            ranges[name] = range;
        }

        Species = species;
        TraitRanges = ranges;
    }

    public string Species { get; }

    public IReadOnlyDictionary<string, (double Min, double Max)> TraitRanges { get; }

    /// <summary>Paramétrage par défaut « Entité A » : tous les traits dans [0.5, 1.5].</summary>
    public static EntityTemplate DefaultA => new(
        "Entité A",
        TraitSet.TraitNames.ToDictionary(name => name, _ => (Min: 0.5, Max: 1.5), StringComparer.Ordinal));
}