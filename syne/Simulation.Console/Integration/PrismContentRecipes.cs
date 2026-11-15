// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Lecture du graphe de recettes du pack PRISM (SYNE-223) pour le planificateur de production.

using System.Text.Json;
using Simulation.Core.Cognition;

namespace Simulation.Console.Integration;

/// <summary>
/// Recettes du pack de contenu (<c>recipes/*.json</c>) compilées pour
/// <see cref="ProductionPlanner"/>.
/// </summary>
/// <remarks>
/// <para>
/// C'est la <b>matière</b> du plan : quelles entrées, quelles sorties, quelle
/// station. Le plan, lui, est dérivé par la cognition à partir de ces faits —
/// le pont ne contient aucune chaîne d'étapes écrite.
/// </para>
/// <para>
/// Une entrée déclarée par <c>tag</c> (le pack minimal en utilise pour
/// <c>handle</c>, <c>stone_head</c> et <c>fuel</c>) est lue comme son identifiant
/// de tag : dans le pack minimal ces tags portent le même nom que l'objet, ce qui
/// suffit à la chaîne d'outils. Un pack où un tag couvre plusieurs objets devra
/// exposer la résolution tag → objet ; le planificateur ne l'invente pas.
/// </para>
/// <para>
/// Un répertoire absent ou illisible renvoie une liste vide plutôt que de lever :
/// le couplage reste possible sans contenu, la production est simplement
/// indisponible.
/// </para>
/// </remarks>
public static class PrismContentRecipes
{
    /// <summary>Charge les recettes d'un pack (ordre déterministe : fichiers puis entrées).</summary>
    public static IReadOnlyList<ProductionRecipe> Load(string? contentDirectory)
    {
        if (string.IsNullOrWhiteSpace(contentDirectory))
        {
            return [];
        }

        string recipesDirectory = Path.Combine(contentDirectory, "recipes");
        if (!Directory.Exists(recipesDirectory))
        {
            return [];
        }

        var recipes = new List<ProductionRecipe>();
        foreach (string file in Directory
                     .EnumerateFiles(recipesDirectory, "*.json", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(File.ReadAllText(file));
            }
            catch (Exception exception) when (exception is JsonException or IOException)
            {
                return [];
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement recipe in document.RootElement.EnumerateArray())
                {
                    if (ReadRecipe(recipe) is { } parsed)
                    {
                        recipes.Add(parsed);
                    }
                }
            }
        }

        return recipes;
    }

    private static ProductionRecipe? ReadRecipe(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("id", out JsonElement idElement) ||
            idElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string id = idElement.GetString()!;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        IReadOnlyList<ProductionIngredient> inputs = ReadIngredients(element, "inputs");
        IReadOnlyList<ProductionIngredient> outputs = ReadIngredients(element, "outputs");
        if (inputs.Count == 0 || outputs.Count == 0)
        {
            return null;
        }

        string? station = element.TryGetProperty("station", out JsonElement stationElement) &&
                          stationElement.ValueKind == JsonValueKind.String
            ? stationElement.GetString()
            : null;

        return new ProductionRecipe(id, inputs, outputs, station);
    }

    private static IReadOnlyList<ProductionIngredient> ReadIngredients(JsonElement recipe, string property)
    {
        if (!recipe.TryGetProperty(property, out JsonElement array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var ingredients = new List<ProductionIngredient>(array.GetArrayLength());
        foreach (JsonElement entry in array.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("qty", out JsonElement quantityElement) ||
                !quantityElement.TryGetInt32(out int quantity) ||
                quantity <= 0)
            {
                continue;
            }

            string? key = entry.TryGetProperty("item", out JsonElement item) &&
                          item.ValueKind == JsonValueKind.String
                ? item.GetString()
                : entry.TryGetProperty("tag", out JsonElement tag) &&
                  tag.ValueKind == JsonValueKind.String
                    ? tag.GetString()
                    : null;
            if (!string.IsNullOrWhiteSpace(key))
            {
                ingredients.Add(new ProductionIngredient(key!, quantity));
            }
        }

        return ingredients;
    }
}
