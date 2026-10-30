// SPDX-License-Identifier: GPL-3.0-or-later
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace Setup.App.Localization;

/// <summary>
/// Liaison XAML vers une clé de ressource : <c>Text="{l:Tr welcomeTitle}"</c>.
/// Le texte suit la langue courante sans recharger la vue.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]")
        {
            Source = Strings.Instance,
            Mode = BindingMode.OneWay,
            FallbackValue = $"[{Key}]"
        };
}
