// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests de la lecture du graphe de recettes du pack PRISM (SYNE-223).

using Simulation.Console.Integration;
using Simulation.Core.Cognition;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class PrismContentRecipesTests
{
    private static string WritePack(string recipesJson)
    {
        string root = Path.Combine(Path.GetTempPath(), "syne-prism-recipes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "recipes"));
        File.WriteAllText(Path.Combine(root, "recipes", "base.json"), recipesJson);
        return root;
    }

    [Fact]
    public void Load_ReadsInputsOutputsAndStation()
    {
        string root = WritePack(
            """
            [
              {
                "id": "make_plank",
                "inputs": [{ "item": "log", "qty": 1 }],
                "outputs": [{ "item": "plank", "qty": 4 }]
              },
              {
                "id": "craft_stone_pickaxe",
                "inputs": [{ "tag": "handle", "qty": 1 }, { "tag": "stone_head", "qty": 1 }],
                "outputs": [{ "item": "stone_pickaxe", "qty": 1 }],
                "station": "workbench"
              }
            ]
            """);
        try
        {
            IReadOnlyList<ProductionRecipe> recipes = PrismContentRecipes.Load(root);

            Assert.Equal(2, recipes.Count);
            ProductionRecipe pickaxe = Assert.Single(recipes, recipe => recipe.Id == "craft_stone_pickaxe");
            Assert.Equal("workbench", pickaxe.Station);
            Assert.Equal(["handle", "stone_head"], pickaxe.Inputs.Select(input => input.Item));
            Assert.Equal(1, pickaxe.OutputQuantity("stone_pickaxe"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_AndPlanner_DeriveTheMinimalPackToolChain()
    {
        string root = WritePack(
            """
            [
              { "id": "make_plank", "inputs": [{ "item": "log", "qty": 1 }], "outputs": [{ "item": "plank", "qty": 4 }] },
              { "id": "make_handle", "inputs": [{ "item": "plank", "qty": 2 }], "outputs": [{ "item": "handle", "qty": 1 }] },
              { "id": "make_stone_head", "inputs": [{ "item": "stone", "qty": 2 }], "outputs": [{ "item": "stone_head", "qty": 1 }] },
              { "id": "craft_workbench", "inputs": [{ "item": "plank", "qty": 4 }], "outputs": [{ "item": "workbench", "qty": 1 }] },
              {
                "id": "craft_stone_pickaxe",
                "inputs": [{ "tag": "handle", "qty": 1 }, { "tag": "stone_head", "qty": 1 }],
                "outputs": [{ "item": "stone_pickaxe", "qty": 1 }],
                "station": "workbench"
              }
            ]
            """);
        try
        {
            ProductionPlan plan = ProductionPlanner.Build(
                "stone_pickaxe", 1, PrismContentRecipes.Load(root));

            Assert.False(plan.Truncated);
            Assert.Contains(plan.Steps, step => step is PlaceStep place && place.StationItem == "workbench");
            Assert.Contains(plan.Steps, step => step is CraftStep craft && craft.RecipeId == "craft_stone_pickaxe");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingDirectory_ReturnsEmptyInsteadOfThrowing()
    {
        Assert.Empty(PrismContentRecipes.Load(Path.Combine(Path.GetTempPath(), "absent-" + Guid.NewGuid().ToString("N"))));
        Assert.Empty(PrismContentRecipes.Load(null));
    }
}
