// SPDX-License-Identifier: GPL-3.0-or-later
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace Setup.App.Localization;

/// <summary>
/// Point d'accès unique aux textes de l'assistant.
/// Les vues se lient à l'indexeur (clé de ressource) ; un changement de langue
/// notifie <c>Item[]</c> pour rafraîchir toutes les liaisons sans recréer les vues.
/// </summary>
public sealed class Strings : INotifyPropertyChanged
{
    private static readonly ResourceManager Resources =
        new("Setup.App.Resources.Strings", typeof(Strings).Assembly);

    public static Strings Instance { get; } = new();

    private Strings() { }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo Culture { get; private set; } = new("fr");

    /// <summary>Code de langue à deux lettres : <c>fr</c> ou <c>en</c>.</summary>
    public string Language => Culture.TwoLetterISOLanguageName;

    public string this[string key] => Resources.GetString(key, Culture)
        ?? Resources.GetString(key, CultureInfo.GetCultureInfo("en"))
        ?? $"[{key}]";

    public string Get(string key) => this[key];

    public string Format(string key, params object?[] args)
    {
        var format = this[key];
        try
        {
            return string.Format(Culture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    public void SetLanguage(string? language)
    {
        var code = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fr";
        var culture = CultureInfo.GetCultureInfo(code);
        if (Culture.Name == culture.Name)
        {
            return;
        }

        Culture = culture;
        CultureInfo.CurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }
}
