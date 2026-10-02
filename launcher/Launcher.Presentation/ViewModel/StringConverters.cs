using System.Globalization;
using Avalonia.Data.Converters;

namespace Launcher.Presentation.ViewModel;

/// <summary>Convertisseurs d'affichage : visibilité d'une vue selon la navigation ou le contenu.</summary>
public static class StringConverters
{
    /// <summary>Vrai si l'identifiant de navigation vaut la valeur attendue (vue active).</summary>
    public static readonly IValueConverter NavEquals =
        new FuncValueConverter<string?, string?, bool>((value, parameter) =>
            string.Equals(value, parameter, StringComparison.Ordinal));

    /// <summary>Vrai si la chaîne n'est pas vide (rapport présent, tableau alimenté…).</summary>
    public static readonly IValueConverter NotEmpty =
        new FuncValueConverter<string?, bool>(value => !string.IsNullOrEmpty(value));

    /// <summary>Vrai si la chaîne est vide (absence explicite du rapport).</summary>
    public static readonly IValueConverter Empty =
        new FuncValueConverter<string?, bool>(string.IsNullOrEmpty);

    /// <summary>Vrai si la chaîne est nulle (pas de PID affiché).</summary>
    public static readonly IValueConverter Null =
        new FuncValueConverter<int?, bool>(value => value is null);

    /// <summary>Vrai si la chaîne est non nulle (PID affiché).</summary>
    public static readonly IValueConverter NotNull =
        new FuncValueConverter<int?, bool>(value => value is not null);

    /// <summary>Texte d'un run à venir : graine dérivée ou « — ».</summary>
    public static readonly IValueConverter SeedOrDash =
        new FuncValueConverter<long, string>(seed => seed > 0
            ? seed.ToString(CultureInfo.InvariantCulture)
            : "—");

    /// <summary>Initiale d'un nom de composant, pour l'icône circulaire des cartes.</summary>
    public static readonly IValueConverter Initial =
        new FuncValueConverter<string, string>(name =>
            string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant());

    /// <summary>PID affiché ou tiret d'absence.</summary>
    public static readonly IValueConverter PidOrDash =
        new FuncValueConverter<int?, string>(pid => pid?.ToString(CultureInfo.InvariantCulture) ?? "—");

    /// <summary>Vrai si la collection est vide (état vide explicite).</summary>
    public static readonly IValueConverter Zero =
        new FuncValueConverter<int, bool>(count => count == 0);

    /// <summary>Vrai si la collection contient au moins un élément.</summary>
    public static readonly IValueConverter NotZero =
        new FuncValueConverter<int, bool>(count => count != 0);

    /// <summary>Inverse un booléen pour l'affichage exclusif des icônes d'action.</summary>
    public static readonly IValueConverter Not =
        new FuncValueConverter<bool, bool>(value => !value);
}
