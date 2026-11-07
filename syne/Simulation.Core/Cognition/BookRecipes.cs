// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : BookRecipes - les recettes déclarées dans le contenu d'un livre (SYNE-224).

using Simulation.Core.Configuration;

namespace Simulation.Core.Cognition;

/// <summary>
/// Extrait les recettes déclarées dans le <b>contenu</b> d'un livre (SYNE-224,
/// « savoir des recettes : simple puis riche »).
/// </summary>
/// <remarks>
/// Un livre reste du texte libre : seule une ligne qui commence exactement par
/// <c>recipe:</c> déclare une connaissance. Le reste est de la prose ignorée.
/// <code>
/// recipe:&lt;id&gt;|&lt;station ou ->|&lt;entrées&gt;|&lt;sorties&gt;
/// recipe:stone_pickaxe|atelier|stone:3;wood:2|pickaxe_stone:1
/// recipe:sharp_stone|-|stone:1|sharp_stone:1
/// </code>
/// Une ligne mal formée est <b>ignorée</b>, jamais une erreur : une faute dans un
/// livre ne doit pas faire échouer le tick d'une simulation.
/// </remarks>
public static class BookRecipeParser
{
    /// <summary>Préfixe exact d'une ligne de déclaration de recette.</summary>
    public const string LinePrefix = "recipe:";

    /// <summary>Marqueur de recette à mains nues (aucune station).</summary>
    public const string NoStation = "-";

    /// <summary>
    /// Recettes déclarées, dans l'ordre du texte. Une recette répétée garde sa
    /// dernière déclaration (ordre de lecture = ordre du contenu, donc déterministe).
    /// </summary>
    public static IReadOnlyList<RecipeKnowledgeSettings> Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var recipes = new List<RecipeKnowledgeSettings>();
        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim().TrimEnd('\r');
            if (!line.StartsWith(LinePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (TryParseLine(line[LinePrefix.Length..], out RecipeKnowledgeSettings recipe))
            {
                recipes.RemoveAll(existing => string.Equals(existing.Id, recipe.Id, StringComparison.Ordinal));
                recipes.Add(recipe);
            }
        }

        return recipes;
    }

    private static bool TryParseLine(string body, out RecipeKnowledgeSettings recipe)
    {
        recipe = null!;
        string[] fields = body.Split('|');
        if (fields.Length != 4)
        {
            return false;
        }

        string id = fields[0].Trim();
        string station = fields[1].Trim();
        if (id.Length == 0 || station == NoStation)
        {
            station = string.Empty;
        }

        // Les listes sont validées par le même lecteur que les croyances : une liste
        // illisible dans un livre ne doit pas produire une croyance illisible.
        if (!KnowledgeBuilder.TryParseRecipeLine(fields[2].Trim(), out _) ||
            !KnowledgeBuilder.TryParseRecipeLine(fields[3].Trim(), out _))
        {
            return false;
        }

        recipe = new RecipeKnowledgeSettings
        {
            Id = id,
            Station = station.Length == 0 ? null : station,
            Inputs = ToIngredients(fields[2].Trim()),
            Outputs = ToIngredients(fields[3].Trim()),
        };
        return true;
    }

    private static List<RecipeIngredientSettings> ToIngredients(string line)
    {
        KnowledgeBuilder.TryParseRecipeLine(line, out IReadOnlyList<(string Item, int Quantity)> parsed);
        var ingredients = new List<RecipeIngredientSettings>(parsed.Count);
        foreach ((string item, int quantity) in parsed)
        {
            ingredients.Add(new RecipeIngredientSettings { Item = item, Quantity = quantity });
        }

        return ingredients;
    }
}
