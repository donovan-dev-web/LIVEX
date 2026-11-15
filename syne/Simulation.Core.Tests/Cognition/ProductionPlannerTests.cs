// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests du planificateur de production (SYNE-223) : graphe de recettes → étapes ordonnées.

using Simulation.Core.Cognition;
using Xunit;

namespace Simulation.Core.Tests;

public class ProductionPlannerTests
{
    private static ProductionRecipe Recipe(
        string id,
        string output,
        int outputQuantity,
        string? station = null,
        params (string Item, int Quantity)[] inputs) =>
        new(
            id,
            inputs.Select(input => new ProductionIngredient(input.Item, input.Quantity)).ToList(),
            [new ProductionIngredient(output, outputQuantity)],
            station);

    /// <summary>Le sous-ensemble de recettes du pack minimal utile à la chaîne d'outils.</summary>
    private static IReadOnlyList<ProductionRecipe> ToolRecipes() =>
    [
        Recipe("make_plank", "plank", 4, null, ("log", 1)),
        Recipe("make_handle", "handle", 1, null, ("plank", 2)),
        Recipe("make_stone_head", "stone_head", 1, null, ("stone", 2)),
        Recipe("craft_workbench", "workbench", 1, null, ("plank", 4)),
        Recipe("craft_stone_pickaxe", "stone_pickaxe", 1, "workbench", ("handle", 1), ("stone_head", 1)),
    ];

    [Fact]
    public void Build_StonePickaxe_DerivesCompleteChainWithoutScript()
    {
        ProductionPlan plan = ProductionPlanner.Build("stone_pickaxe", 1, ToolRecipes());

        Assert.False(plan.Truncated);
        Assert.Equal("stone_pickaxe", plan.Goal);

        Assert.Collection(
            plan.Steps,
            step => Assert.Equal(new GatherStep("log", 1), step),
            step => Assert.Equal(new CraftStep("make_plank", "plank", 4, null), step),
            step => Assert.Equal(new CraftStep("make_handle", "handle", 1, null), step),
            step => Assert.Equal(new GatherStep("stone", 2), step),
            step => Assert.Equal(new CraftStep("make_stone_head", "stone_head", 1, null), step),
            step => Assert.Equal(new GatherStep("log", 1), step),
            step => Assert.Equal(new CraftStep("make_plank", "plank", 4, null), step),
            step => Assert.Equal(new CraftStep("craft_workbench", "workbench", 1, null), step),
            step => Assert.Equal(new PlaceStep("workbench"), step),
            step => Assert.Equal(new CraftStep("craft_stone_pickaxe", "stone_pickaxe", 1, "workbench"), step));
    }

    [Fact]
    public void Build_ReusesAlreadyCarriedItems_AndDoesNotGatherThemAgain()
    {
        var carried = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["log"] = 2,
            ["stone"] = 2,
        };

        ProductionPlan plan = ProductionPlanner.Build("stone_pickaxe", 1, ToolRecipes(), carried);

        Assert.DoesNotContain(plan.Steps, step => step is GatherStep);
        Assert.Equal(6, plan.Steps.OfType<CraftStep>().Count());
        Assert.Single(plan.Steps.OfType<PlaceStep>());
    }

    [Fact]
    public void Build_ReusesSurplusOfACraft_InsteadOfPlanningItTwice()
    {
        // Fabriquer les planches rend 4 ; le manche en consomme 2 et l'établi 4.
        // Le budget doit réutiliser les 2 planches restantes et ne commander
        // qu'une seule seconde fabrication de planches (soit 2 bûches au total).
        ProductionPlan plan = ProductionPlanner.Build("stone_pickaxe", 1, ToolRecipes());

        Assert.Equal(2, plan.Steps.OfType<GatherStep>().Count(step => step.Item == "log"));
        Assert.Equal(2, plan.Steps.OfType<CraftStep>().Count(step => step.RecipeId == "make_plank"));
        Assert.Equal(2, plan.Steps.OfType<GatherStep>().Where(step => step.Item == "log").Sum(step => step.Quantity));
    }

    [Fact]
    public void Build_SimpleChain_IsGatherThenCraft()
    {
        IReadOnlyList<ProductionRecipe> recipes =
        [
            Recipe("craft_bucket", "wooden_bucket", 1, null, ("plank", 3)),
            Recipe("make_plank", "plank", 4, null, ("log", 1)),
        ];

        ProductionPlan plan = ProductionPlanner.Build("wooden_bucket", 1, recipes);

        Assert.Collection(
            plan.Steps,
            step => Assert.Equal(new GatherStep("log", 1), step),
            step => Assert.Equal(new CraftStep("make_plank", "plank", 4, null), step),
            step => Assert.Equal(new CraftStep("craft_bucket", "wooden_bucket", 1, null), step));
    }

    [Fact]
    public void Build_MergesAdjacentGathersOfTheSameItem()
    {
        IReadOnlyList<ProductionRecipe> recipes =
        [
            Recipe("craft_bench", "bench", 1, null, ("log", 1), ("stone", 2), ("log", 2)),
        ];

        ProductionPlan plan = ProductionPlanner.Build("bench", 1, recipes);

        Assert.Collection(
            plan.Steps,
            step => Assert.Equal(new GatherStep("log", 3), step),
            step => Assert.Equal(new GatherStep("stone", 2), step),
            step => Assert.Equal(new CraftStep("craft_bench", "bench", 1, null), step));
    }

    [Fact]
    public void Build_Cycle_StopsAtTheBoundInsteadOfRecursingForever()
    {
        IReadOnlyList<ProductionRecipe> recipes =
        [
            Recipe("craft_a", "a", 1, null, ("b", 1)),
            Recipe("craft_b", "b", 1, null, ("a", 1)),
        ];

        ProductionPlan plan = ProductionPlanner.Build("a", 1, recipes, maxNodes: 16);

        Assert.Contains(plan.Steps, step => step is GatherStep gather && gather.Item == "a");
    }

    [Fact]
    public void Build_MissingGoalWithoutRecipe_BecomesAGather()
    {
        ProductionPlan plan = ProductionPlanner.Build("iron_ore_chunk", 2, ToolRecipes());

        Assert.Collection(plan.Steps, step => Assert.Equal(new GatherStep("iron_ore_chunk", 2), step));
    }

    [Fact]
    public void Build_BoundedPlan_IsTruncatedNotInfinite()
    {
        IReadOnlyList<ProductionRecipe> recipes = ToolRecipes();

        ProductionPlan plan = ProductionPlanner.Build("stone_pickaxe", 1, recipes, maxNodes: 3);

        Assert.True(plan.Truncated);
        Assert.True(plan.Steps.Count <= 3);
    }
}
