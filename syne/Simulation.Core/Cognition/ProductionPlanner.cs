// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Planificateur de production (SYNE-223) : graphe de recettes → suite d'étapes ordonnée.

namespace Simulation.Core.Cognition;

/// <summary>Une entrée ou une sortie de recette : identifiant de contenu et quantité.</summary>
public readonly record struct ProductionIngredient(string Item, int Quantity);

/// <summary>
/// Recette connue du planificateur de production : ce que coûte l'objet, ce
/// qu'il rend et la station requise (nulle pour les recettes à mains nues).
/// </summary>
/// <remarks>
/// Les identifiants sont ceux du <b>pack de contenu</b> (« log », « plank »,
/// « stone_pickaxe », « workbench »). Le planificateur ne lit aucun fichier : il
/// reçoit ce graphe déjà compilé, ce qui le rend testable sans monde.
/// </remarks>
public sealed record ProductionRecipe(
    string Id,
    IReadOnlyList<ProductionIngredient> Inputs,
    IReadOnlyList<ProductionIngredient> Outputs,
    string? Station)
{
    /// <summary>Quantité produite de <paramref name="item"/> par une exécution (0 si absente).</summary>
    public int OutputQuantity(string item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(item);
        foreach (ProductionIngredient output in Outputs)
        {
            if (string.Equals(output.Item, item, StringComparison.Ordinal))
            {
                return output.Quantity;
            }
        }

        return 0;
    }
}

/// <summary>Étape ordonnée du plan de production.</summary>
public abstract record ProductionStep(string Label);

/// <summary>Prélever une matière première (macro-action d'acquisition de l'hôte).</summary>
public sealed record GatherStep(string Item, int Quantity)
    : ProductionStep(string.Concat("gather:", Item, ":", Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)));

/// <summary>Fabriquer une recette (la station est placée avant, si nécessaire).</summary>
public sealed record CraftStep(string RecipeId, string OutputItem, int OutputQuantity, string? Station)
    : ProductionStep(string.Concat("craft:", RecipeId));

/// <summary>Poser la station fabriquée pour que la recette suivante puisse l'utiliser.</summary>
public sealed record PlaceStep(string StationItem)
    : ProductionStep(string.Concat("place:", StationItem));

/// <summary>Plan de production : objectif, étapes ordonnées, et drapeau de troncature.</summary>
public sealed class ProductionPlan
{
    public ProductionPlan(string goal, IReadOnlyList<ProductionStep> steps, bool truncated = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        ArgumentNullException.ThrowIfNull(steps);
        Goal = goal;
        Steps = steps;
        Truncated = truncated;
    }

    public string Goal { get; }

    public IReadOnlyList<ProductionStep> Steps { get; }

    /// <summary>Vrai si la borne de nœuds a coupé le dépliage (plan incomplet).</summary>
    public bool Truncated { get; }

    public bool IsEmpty => Steps.Count == 0;
}

/// <summary>
/// Déplie un objectif en plan de production à partir du <b>graphe de recettes</b>
/// et de l'inventaire observé (SYNE-223, étape 4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Chaînage arrière borné</b> : pour produire un objet, on cherche une recette
/// qui le produit, on satisfait d'abord ses entrées (récursivement), puis on
/// fabrique. Une entrée qu'aucune recette ne produit devient un prélèvement. La
/// station d'une recette est d'abord fabriquée puis <b>posée</b> : la séquence
/// « bûches → planches → manche + tête → établi posé → pioche » est dérivée, pas
/// écrite.
/// </para>
/// <para>
/// <b>Surplus réutilisé</b> : un budget par objet suit ce que les étapes déjà
/// planifiées rendront disponible. Fabriquer une recette qui rend 4 planches pour
/// un besoin de 2 laisse 2 planches utilisables par la suite — sans quoi le plan
/// demanderait deux fois trop de bûches.
/// </para>
/// <para>
/// <b>Déterminisme et totalité</b> : recettes triées par identifiant, entrées
/// triées par identifiant, aucun tirage. Un cycle ou un pack trop profond est
/// arrêté par la borne de nœuds (<see cref="ProductionPlan.Truncated"/>) plutôt
/// que par une récursion infinie.
/// </para>
/// </remarks>
public static class ProductionPlanner
{
    /// <summary>Profondeur maximale de dépliage (une chaîne de production réaliste tient en moins).</summary>
    public const int MaxDepth = 16;

    /// <summary>
    /// Construit le plan de <paramref name="quantity"/> exemplaires de
    /// <paramref name="goal"/>. <paramref name="carried"/> est l'inventaire
    /// <b>observé</b> (identifiant → quantité) : ce qui est déjà porté n'est ni
    /// prélevé ni fabriqué à nouveau.
    /// </summary>
    public static ProductionPlan Build(
        string goal,
        int quantity,
        IReadOnlyList<ProductionRecipe> recipes,
        IReadOnlyDictionary<string, int>? carried = null,
        int maxNodes = 128)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxNodes, 1);

        var ordered = recipes
            .Where(static recipe => recipe is not null)
            .OrderBy(static recipe => recipe.Id, StringComparer.Ordinal)
            .ToList();
        var steps = new List<ProductionStep>();
        var budget = new Dictionary<string, int>(StringComparer.Ordinal);
        if (carried is not null)
        {
            foreach ((string item, int count) in carried)
            {
                if (count > 0 && !string.IsNullOrWhiteSpace(item))
                {
                    budget[item] = budget.GetValueOrDefault(item) + count;
                }
            }
        }

        bool truncated = false;

        void Need(string item, int needed, HashSet<string> chain, int depth)
        {
            if (needed <= 0)
            {
                return;
            }

            if (steps.Count >= maxNodes)
            {
                truncated = true;
                return;
            }

            int available = budget.GetValueOrDefault(item);
            if (available >= needed)
            {
                budget[item] = available - needed;
                return;
            }

            int missing = needed - available;
            int before = available;
            budget[item] = 0;
            if (depth >= MaxDepth || chain.Contains(item))
            {
                // Cycle : on ne peut pas produire l'objet à partir de lui-même,
                // le prélèvement reste la seule issue honnête.
                truncated |= depth >= MaxDepth;
                AddGather(item, missing, steps);
                budget[item] = before + missing - needed;
                return;
            }

            ProductionRecipe? recipe = ordered.FirstOrDefault(candidate => candidate.OutputQuantity(item) > 0);
            if (recipe is null)
            {
                AddGather(item, missing, steps);
                budget[item] = before + missing - needed;
                return;
            }

            int perCraft = recipe.OutputQuantity(item);
            int crafts = (missing + perCraft - 1) / perCraft;
            chain.Add(item);
            foreach (ProductionIngredient input in recipe.Inputs
                         .OrderBy(static input => input.Item, StringComparer.Ordinal))
            {
                Need(input.Item, input.Quantity * crafts, chain, depth + 1);
            }

            if (!string.IsNullOrWhiteSpace(recipe.Station))
            {
                Need(recipe.Station!, 1, chain, depth + 1);
                if (steps.Count >= maxNodes)
                {
                    truncated = true;
                }
                else
                {
                    steps.Add(new PlaceStep(recipe.Station!));
                }
            }

            for (int index = 0; index < crafts && steps.Count < maxNodes; index++)
            {
                steps.Add(new CraftStep(recipe.Id, item, perCraft, recipe.Station));
            }

            if (steps.Count >= maxNodes)
            {
                truncated = true;
            }

            chain.Remove(item);
            budget[item] = before + (crafts * perCraft) - needed;
        }

        Need(goal, quantity, new HashSet<string>(StringComparer.Ordinal), 0);
        return new ProductionPlan(goal, steps, truncated);
    }

    private static void AddGather(string item, int quantity, List<ProductionStep> steps)
    {
        if (quantity <= 0)
        {
            return;
        }

        if (steps.Count > 0 && steps[^1] is GatherStep previous &&
            string.Equals(previous.Item, item, StringComparison.Ordinal))
        {
            steps[^1] = new GatherStep(item, previous.Quantity + quantity);
            return;
        }

        steps.Add(new GatherStep(item, quantity));
    }
}
