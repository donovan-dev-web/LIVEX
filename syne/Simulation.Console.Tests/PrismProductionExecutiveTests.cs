// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests de l'exécutif de production (SYNE-223) : plan de recettes → commandes.

using Simulation.Console.Integration;
using Simulation.Core.Cognition;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class PrismProductionExecutiveTests
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

    private static IReadOnlyList<ProductionRecipe> ToolRecipes() =>
    [
        Recipe("make_plank", "plank", 4, null, ("log", 1)),
        Recipe("make_handle", "handle", 1, null, ("plank", 2)),
        Recipe("make_stone_head", "stone_head", 1, null, ("stone", 2)),
        Recipe("craft_workbench", "workbench", 1, null, ("plank", 4)),
        Recipe("craft_stone_pickaxe", "stone_pickaxe", 1, "workbench", ("handle", 1), ("stone_head", 1)),
    ];

    private static Dictionary<string, int> Carried(params (string Item, int Quantity)[] items)
    {
        var carried = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((string item, int quantity) in items)
        {
            carried[item] = quantity;
        }

        return carried;
    }

    [Fact]
    public void TryNext_StartsWithTheFirstDerivedStep_NotAScriptedSequence()
    {
        var executive = new PrismProductionExecutive(ToolRecipes());

        Assert.True(executive.TryNext(1, "stone_pickaxe", Carried(), 0, out ProductionCommand command));
        Assert.Equal(new GatherProductionCommand("log", 1), command);
    }

    [Fact]
    public void TryNext_WhileACommandIsInFlight_EmitsNothing()
    {
        var executive = new PrismProductionExecutive(ToolRecipes());
        executive.TryNext(1, "stone_pickaxe", Carried(), 0, out _);
        executive.MarkIssued(1, "c1");

        Assert.False(executive.TryNext(1, "stone_pickaxe", Carried(), 1, out _));
    }

    [Fact]
    public void TryNext_AdvancesOnlyAfterThePublishedCompletion()
    {
        var executive = new PrismProductionExecutive(ToolRecipes());
        executive.TryNext(1, "stone_pickaxe", Carried(), 0, out ProductionCommand first);
        Assert.Equal(new GatherProductionCommand("log", 1), first);
        executive.MarkIssued(1, "c1");
        executive.MarkCompleted(1, "c1", 2);

        Assert.True(executive.TryNext(1, "stone_pickaxe", Carried(), 3, out ProductionCommand second));
        Assert.Equal(new CraftProductionCommand("make_plank", null), second);
    }

    [Fact]
    public void TryNext_SkipsAGatherAlreadyCoveredByTheObservedInventory()
    {
        var executive = new PrismProductionExecutive(ToolRecipes());

        Assert.True(executive.TryNext(1, "stone_pickaxe", Carried(("log", 1)), 0, out ProductionCommand command));

        Assert.Equal(new CraftProductionCommand("make_plank", null), command);
    }

    [Fact]
    public void TryNext_PlacesTheWorkbenchBeforeTheStationedCraft()
    {
        var executive = new PrismProductionExecutive(ToolRecipes());
        var carried = Carried(("log", 2), ("stone", 2));

        var seen = new List<ProductionCommand>();
        long tick = 0;
        for (int guard = 0; guard < 40; guard++)
        {
            if (!executive.TryNext(1, "stone_pickaxe", carried, tick, out ProductionCommand command))
            {
                tick++;
                continue;
            }

            seen.Add(command);
            string id = $"c{seen.Count}";
            executive.MarkIssued(1, id);
            executive.MarkCompleted(1, id, tick);
            tick++;
        }

        Assert.Contains(seen, step => step is PlaceProductionCommand place && place.StationItem == "workbench");
        int placeIndex = seen.FindIndex(step => step is PlaceProductionCommand);
        int pickaxeIndex = seen.FindIndex(step =>
            step is CraftProductionCommand craft && craft.RecipeId == "craft_stone_pickaxe");
        Assert.True(placeIndex >= 0 && pickaxeIndex > placeIndex,
            $"la pose ({placeIndex}) doit précéder la fabrication ({pickaxeIndex})");
    }

    [Fact]
    public void TryNext_LegacyGoalCarried_CompletesTheRun()
    {
        var executive = new PrismProductionExecutive(ToolRecipes());
        executive.TryNext(1, "stone_pickaxe", Carried(), 0, out _);
        executive.MarkIssued(1, "c1");

        Assert.False(executive.TryNext(1, "stone_pickaxe", Carried(("stone_pickaxe", 1)), 1, out _));
        Assert.Equal(1, executive.CompletedRuns);
        Assert.False(executive.IsRunning(1));
    }

    [Fact]
    public void MarkFailed_SourceNotFound_ExposesTheMissingItemToProspect()
    {
        // « je ne perçois aucune source » n'est pas un refus : réessayer depuis la
        // même case ne peut pas mieux finir, l'agent doit aller voir ailleurs.
        var executive = new PrismProductionExecutive(ToolRecipes(), retryTicks: 10);
        Assert.True(executive.TryNext(1, "stone_pickaxe", Carried(), 0, out _));
        executive.MarkIssued(1, "c0");
        executive.MarkFailed(1, "c0", "source_not_found", 5);

        Assert.True(executive.TrySearch(1, 5, out string item));
        Assert.Equal("log", item);
        // La prospection expire d'elle-même : elle ne remplace pas la production.
        Assert.False(executive.TrySearch(1, 5 + PrismProductionExecutive.SearchTicks, out _));
    }

    [Fact]
    public void MarkFailed_APlainRefusal_DoesNotProspect()
    {
        var executive = new PrismProductionExecutive(ToolRecipes(), retryTicks: 10);
        Assert.True(executive.TryNext(1, "stone_pickaxe", Carried(), 0, out _));
        executive.MarkIssued(1, "c0");
        executive.MarkFailed(1, "c0", "path_not_found", 5);

        Assert.False(executive.TrySearch(1, 5, out _));
    }

    [Fact]
    public void MarkCompleted_EndsTheProspection()
    {
        var executive = new PrismProductionExecutive(ToolRecipes(), retryTicks: 10);
        Assert.True(executive.TryNext(1, "stone_pickaxe", Carried(), 0, out _));
        executive.MarkIssued(1, "c0");
        executive.MarkFailed(1, "c0", "source_not_found", 5);
        Assert.True(executive.TrySearch(1, 5, out _));

        Assert.True(executive.TryNext(1, "stone_pickaxe", Carried(), 15, out _));
        executive.MarkIssued(1, "c1");
        executive.MarkCompleted(1, "c1", 20);

        Assert.False(executive.TrySearch(1, 20, out _));
    }

    [Fact]
    public void ObserveInventory_CountsAFinishedChainWithoutAnotherDecision()
    {
        // L'objet final satisfait le désir : plus aucune décision `Produce` ne
        // suivrait, donc l'accomplissement se lit dans l'inventaire observé.
        var executive = new PrismProductionExecutive(ToolRecipes());
        Assert.True(executive.TryNext(1, "stone_pickaxe", Carried(), 0, out _));
        Assert.True(executive.IsRunning(1));

        executive.ObserveInventory(1, Carried(("stone_pickaxe", 1)));

        Assert.Equal(1, executive.CompletedRuns);
        Assert.Contains("stone_pickaxe", executive.CompletedGoals);
        Assert.False(executive.IsRunning(1));
    }

    [Fact]
    public void ObserveInventory_AnUnrelatedStack_LeavesTheChainRunning()
    {
        var executive = new PrismProductionExecutive(ToolRecipes());
        Assert.True(executive.TryNext(1, "stone_pickaxe", Carried(), 0, out _));

        executive.ObserveInventory(1, Carried(("plank", 4)));

        Assert.Equal(0, executive.CompletedRuns);
        Assert.True(executive.IsRunning(1));
    }

    [Fact]
    public void TryNext_UnsupportedGoal_IsReportedInsteadOfGatheringAWish()
    {
        var executive = new PrismProductionExecutive(ToolRecipes());

        Assert.False(executive.TryNext(1, "iron_pickaxe", Carried(), 0, out _));
        Assert.Contains("iron_pickaxe", executive.UnsupportedGoals);
    }

    [Fact]
    public void MarkFailed_ArmsARetry_ThenAbandonsTheStepAfterTooManyRefusals()
    {
        var executive = new PrismProductionExecutive(ToolRecipes(), retryTicks: 10);
        Assert.True(executive.TryNext(1, "stone_pickaxe", Carried(), 0, out _));

        for (int attempt = 0; attempt < PrismProductionExecutive.MaxStepFailures; attempt++)
        {
            long tick = attempt * 10;
            executive.MarkIssued(1, $"c{attempt}");
            executive.MarkFailed(1, $"c{attempt}", "source_not_found", tick);
            if (attempt == 0)
            {
                // Délai armé : aucune commande avant le tick suivant.
                Assert.False(executive.TryNext(1, "stone_pickaxe", Carried(), tick + 1, out _));
            }

            if (attempt + 1 < PrismProductionExecutive.MaxStepFailures)
            {
                Assert.True(executive.TryNext(1, "stone_pickaxe", Carried(), tick + 10, out _));
            }
        }

        Assert.Equal(1, executive.AbandonedRuns);
        Assert.False(executive.IsRunning(1));
    }
}
