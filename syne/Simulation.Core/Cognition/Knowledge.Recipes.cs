// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Knowledge - partie Recettes (SYNE-222) : savoir durable et format canonique.

using System.Globalization;
using System.Text;
using Simulation.Core.Configuration;

namespace Simulation.Core.Cognition;

public static partial class KnowledgeBuilder
{
    /// <summary>
    /// Prédicat des entrées d'une recette connue, sujet <c>recipe-&lt;id&gt;</c> :
    /// valeur « item:quantité;item:quantité », triée par identifiant d'objet.
    /// </summary>
    public const string RecipeInputsPredicate = "inputs";

    /// <summary>Prédicat des sorties d'une recette connue (même format que les entrées).</summary>
    public const string RecipeOutputsPredicate = "outputs";

    /// <summary>Prédicat de la station requise, sujet <c>recipe-&lt;id&gt;</c> (absent si mains nues).</summary>
    public const string RecipeStationPredicate = "station";

    /// <summary>Sujet canonique d'une recette connue.</summary>
    public static string RecipeSubject(string recipeId) =>
        string.Concat("recipe-", recipeId);

    /// <summary>
    /// Injecte les recettes déclarées dans les croyances d'une entité, comme
    /// <b>savoir durable</b> : confiance 1, source <c>config</c>, jamais décru ni
    /// expiré (une connaissance ne s'oublie pas comme un fait observé).
    /// </summary>
    /// <param name="beliefs">Croyances de l'entité.</param>
    /// <param name="settings">Section <c>agents.knowledge</c>.</param>
    /// <returns>Nombre de recettes injectées (0 quand la section est désactivée).</returns>
    public static int SeedRecipeKnowledge(BeliefSet beliefs, KnowledgeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(beliefs);
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.Enabled)
        {
            return 0;
        }

        return AddRecipeKnowledge(beliefs, settings.Recipes, source: "config", tick: 0);
    }

    /// <summary>
    /// Apprend les recettes déclarées dans un livre (SYNE-224) : même forme
    /// canonique que la connaissance initiale, mais source <c>book:&lt;id&gt;</c> et
    /// horodatée au tick de lecture — on peut dire <b>quand</b> l'agent l'a appris.
    /// </summary>
    /// <returns>Nombre de recettes apprises.</returns>
    public static int LearnRecipeKnowledge(BeliefSet beliefs, string source, string content, ulong tick)
    {
        ArgumentNullException.ThrowIfNull(beliefs);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        IReadOnlyList<RecipeKnowledgeSettings> recipes = BookRecipeParser.Parse(content);
        return recipes.Count == 0
            ? 0
            : AddRecipeKnowledge(beliefs, recipes, source, tick);
    }

    private static int AddRecipeKnowledge(
        BeliefSet beliefs,
        IEnumerable<RecipeKnowledgeSettings> recipes,
        string source,
        ulong tick)
    {
        int added = 0;
        foreach (RecipeKnowledgeSettings recipe in recipes)
        {
            if (string.IsNullOrWhiteSpace(recipe.Id))
            {
                continue;
            }

            string subject = RecipeSubject(recipe.Id);
            beliefs.Upsert(new Belief(
                new Fact(subject, RecipeInputsPredicate, FormatRecipeLine(recipe.Inputs)),
                1.0,
                source,
                bornTick: tick,
                expiryTick: ulong.MaxValue,
                durable: true));
            beliefs.Upsert(new Belief(
                new Fact(subject, RecipeOutputsPredicate, FormatRecipeLine(recipe.Outputs)),
                1.0,
                source,
                bornTick: tick,
                expiryTick: ulong.MaxValue,
                durable: true));

            if (!string.IsNullOrWhiteSpace(recipe.Station))
            {
                beliefs.Upsert(new Belief(
                    new Fact(subject, RecipeStationPredicate, recipe.Station),
                    1.0,
                    source,
                    bornTick: tick,
                    expiryTick: ulong.MaxValue,
                    durable: true));
            }

            added++;
        }

        return added;
    }

    /// <summary>
    /// Forme canonique d'une liste d'ingrédients : « item:quantité;… » triée par
    /// identifiant (ordinal) — deux déclarations équivalentes produisent la même
    /// croyance, donc la même empreinte.
    /// </summary>
    public static string FormatRecipeLine(IEnumerable<RecipeIngredientSettings> ingredients)
    {
        ArgumentNullException.ThrowIfNull(ingredients);

        var ordered = ingredients
            .OrderBy(ingredient => ingredient.Item, StringComparer.Ordinal)
            .ToList();
        var builder = new StringBuilder();
        foreach (RecipeIngredientSettings ingredient in ordered)
        {
            if (builder.Length > 0)
            {
                builder.Append(';');
            }

            builder.Append(ingredient.Item);
            builder.Append(':');
            builder.Append(ingredient.Quantity.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>« item:quantité;… » → liste d'ingrédients ; refuse le format libre.</summary>
    public static bool TryParseRecipeLine(string? value, out IReadOnlyList<(string Item, int Quantity)> ingredients)
    {
        ingredients = [];
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var parsed = new List<(string Item, int Quantity)>();
        foreach (string part in value.Split(';'))
        {
            int separator = part.LastIndexOf(':');
            if (separator <= 0 || separator == part.Length - 1)
            {
                return false;
            }

            string item = part[..separator];
            if (!int.TryParse(part[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantity) ||
                quantity <= 0)
            {
                return false;
            }

            parsed.Add((item, quantity));
        }

        ingredients = parsed;
        return parsed.Count > 0;
    }
}
