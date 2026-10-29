// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using Setup.Core.Distribution;

namespace Setup.App.Services;

/// <summary>
/// Lit le manifeste de distribution livré à côté de <c>livex-setup</c>.
/// Aucune vérification de signature ici : c'est le rôle du préflight, qui doit
/// refuser une distribution non signée avant toute écriture.
/// </summary>
public static class DistributionLoader
{
    public const string FileName = "distribution.json";

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, FileName);

    public static bool TryLoad(string path, out DistributionManifest? manifest, out string? error)
    {
        manifest = null;
        error = null;

        if (!File.Exists(path))
        {
            error = $"Manifeste absent : {path}";
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            manifest = JsonSerializer.Deserialize<DistributionManifest>(json, options);

            if (manifest is null)
            {
                error = "Manifeste vide.";
                return false;
            }

            if (manifest.Components is null || manifest.Components.Count == 0)
            {
                error = "Le manifeste ne déclare aucun composant.";
                return false;
            }

            if (manifest.Files is null || manifest.Signature is null)
            {
                error = "Le manifeste est incomplet (files ou signature).";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }
}
