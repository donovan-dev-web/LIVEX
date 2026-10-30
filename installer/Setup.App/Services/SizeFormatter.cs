// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using Setup.App.Localization;

namespace Setup.App.Services;

/// <summary>Formatage lisible des tailles, dans la langue de l'assistant.</summary>
public static class SizeFormatter
{
    private static readonly string[] UnitsFr = ["o", "Ko", "Mo", "Go", "To"];
    private static readonly string[] UnitsEn = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(long bytes, string? language = null)
    {
        var units = string.Equals(language ?? Strings.Instance.Language, "en", StringComparison.OrdinalIgnoreCase)
            ? UnitsEn
            : UnitsFr;

        if (bytes < 0)
        {
            bytes = 0;
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = unit == 0 ? "0" : value < 10 ? "0.0" : "0";
        return string.Format(Strings.Instance.Culture, "{0} {1}", value.ToString(format, Strings.Instance.Culture), units[unit]);
    }

    public static string FormatCulture(long bytes, CultureInfo culture) =>
        string.Format(culture, "{0:0.0} Go", bytes / 1024d / 1024d / 1024d);
}
