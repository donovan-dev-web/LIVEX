namespace Simulation.Core.World;

/// <summary>
/// Saisons du cycle environnemental (SYNE-072, jalon U8) : quatre saisons
/// en boucle déterministe. Le cycle est purement une fonction du tick
/// (aucun PRNG, DETERMINISM.md §3) — la saison courante se déduit de
/// <c>(indexInitial + tick / seasonLengthTicks) % 4</c>.
/// </summary>
public enum Season
{
    Spring = 0,
    Summer = 1,
    Autumn = 2,
    Winter = 3,
}

/// <summary>
/// Points d'accroche du type <see cref="Season"/> : noms stables (clés JSON
/// camelCase) et conversion java — culture invariante, déterminisme d'émission.
/// </summary>
public static class Seasons
{
    private static readonly IReadOnlyDictionary<string, Season> ByName =
        new Dictionary<string, Season>(StringComparer.Ordinal)
        {
            ["spring"] = Season.Spring,
            ["summer"] = Season.Summer,
            ["autumn"] = Season.Autumn,
            ["winter"] = Season.Winter,
        };

    public const int Count = 4;

    /// <summary>Nom stable de la saison (JSON/API, camelCase).</summary>
    public static string Name(Season season) => season switch
    {
        Season.Spring => "spring",
        Season.Summer => "summer",
        Season.Autumn => "autumn",
        Season.Winter => "winter",
        _ => throw new ArgumentOutOfRangeException(nameof(season), season, "Saison inconnue."),
    };

    /// <summary>Résout un nom (config/validation) ; <c>null</c> si inconnu.</summary>
    public static Season? TryParse(string? name) =>
        name is null ? null : ByName.TryGetValue(name, out Season season) ? season : null;
}