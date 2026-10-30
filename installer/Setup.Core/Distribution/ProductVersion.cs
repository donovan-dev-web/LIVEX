// SPDX-License-Identifier: GPL-3.0-or-later
namespace Setup.Core.Distribution;

/// <summary>
/// Comparaison de versions du produit, limitée à ce dont l'installateur a
/// besoin : refuser un downgrade implicite et distinguer une pré-version de sa
/// version finale. Une version illisible est traitée comme égale, jamais comme
/// une régression (on préfère ne rien bloquer sur une chaîne exotique).
/// </summary>
public static class ProductVersion
{
    public static int Compare(string? left, string? right)
    {
        var a = Parse(left);
        var b = Parse(right);
        if (a is null || b is null) return 0;

        var core = a.Value.Core.CompareTo(b.Value.Core);
        if (core != 0) return core;

        // Une version finale est supérieure à sa pré-version (SemVer §11).
        if (a.Value.PreRelease is null && b.Value.PreRelease is not null) return 1;
        if (a.Value.PreRelease is not null && b.Value.PreRelease is null) return -1;
        return string.CompareOrdinal(a.Value.PreRelease, b.Value.PreRelease);
    }

    /// <summary>Vrai si <paramref name="candidate"/> est strictement antérieure à <paramref name="current"/>.</summary>
    public static bool IsDowngrade(string? current, string? candidate) => Compare(candidate, current) < 0;

    private static (long Core, string? PreRelease)? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var text = value.Trim().TrimStart('v', 'V');
        var plus = text.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0) text = text[..plus];

        string? preRelease = null;
        var dash = text.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            preRelease = text[(dash + 1)..];
            text = text[..dash];
        }

        var parts = text.Split('.');
        long core = 0;
        for (var index = 0; index < 3; index++)
        {
            var part = index < parts.Length ? parts[index] : "0";
            if (!int.TryParse(part, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                return null;
            }

            core = core * 1000 + number;
        }

        return (core, preRelease);
    }
}
