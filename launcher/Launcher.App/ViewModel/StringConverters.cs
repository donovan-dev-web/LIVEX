using System.Globalization;
using Avalonia.Data.Converters;

namespace Launcher.Presentation.ViewModel;

/// <summary>Convertisseurs d'affichage : visibilité d'une vue selon le mode actif ou le contenu.</summary>
public static class StringConverters
{
    /// <summary>Vrai si la valeur vaut « Contrôle ».</summary>
    public static readonly IValueConverter EqualsContrôle =
        new FuncValueConverter<string?, bool>(value => value == "Contrôle");

    /// <summary>Vrai si la valeur vaut « Analyse ».</summary>
    public static readonly IValueConverter EqualsAnalyse =
        new FuncValueConverter<string?, bool>(value => value == "Analyse");

    /// <summary>Vrai si la chaîne n'est pas vide (rapport présent).</summary>
    public static readonly IValueConverter NotEmpty =
        new FuncValueConverter<string?, bool>(value => !string.IsNullOrEmpty(value));

    /// <summary>Vrai si la chaîne est vide (absence explicite du rapport).</summary>
    public static readonly IValueConverter Empty =
        new FuncValueConverter<string?, bool>(string.IsNullOrEmpty);
}
