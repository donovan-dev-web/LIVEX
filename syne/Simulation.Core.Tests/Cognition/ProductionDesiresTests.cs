// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests du désir de production (SYNE-223) : un objectif, pas un plan.

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

public class ProductionDesiresTests
{
    private static ProductionSettings Settings(params string[] goals) => new()
    {
        Enabled = true,
        Goals = [.. goals],
        Benefit = 45.0,
    };

    private static MindState Mind(SimulationOptions? options = null)
    {
        options ??= ConfigLoader.LoadDefaults();
        return new MindState(options);
    }

    [Fact]
    public void Evaluate_DisabledSection_ReturnsNoDesire()
    {
        var settings = new ProductionSettings { Enabled = false, Goals = ["stone_pickaxe"] };

        Assert.Null(ProductionDesires.Evaluate(Mind(), 1, settings));
    }

    [Fact]
    public void Evaluate_EnabledWithGoal_ReturnsThatGoal()
    {
        ProductionDesire? desire = ProductionDesires.Evaluate(Mind(), 1, Settings("stone_pickaxe"));

        Assert.NotNull(desire);
        Assert.Equal("stone_pickaxe", desire.Value.Goal);
        Assert.Equal(45.0, desire.Value.Benefit);
    }

    [Fact]
    public void Evaluate_AlreadyCarriedGoal_ExtinguishesTheDesire()
    {
        MindState mind = Mind();
        mind.ObserveCarried(new Dictionary<string, int>(StringComparer.Ordinal) { ["stone_pickaxe"] = 1 });

        Assert.Null(ProductionDesires.Evaluate(mind, 1, Settings("stone_pickaxe")));
    }

    [Fact]
    public void Evaluate_AssignsGoalsDeterministicallyByEntityId()
    {
        ProductionSettings settings = Settings("stone_pickaxe", "chest", "wooden_bucket");
        MindState mind = Mind();

        string[] goals = [.. Enumerable.Range(1, 6)
            .Select(id => ProductionDesires.Evaluate(mind, (ulong)id, settings)?.Goal ?? string.Empty)];

        // Répartition round-robin stable : aucune dépendance au hasard ni à
        // l'ordre d'itération d'une collection.
        Assert.Equal(
            ["chest", "wooden_bucket", "stone_pickaxe", "chest", "wooden_bucket", "stone_pickaxe"],
            goals);
    }

    [Fact]
    public void Target_IsAnItem_NotAPlace()
    {
        GoalTarget target = ProductionDesires.Target("stone_pickaxe", new Position(12.5, 8.25));

        Assert.Equal("item", target.TargetType);
        Assert.Equal("stone_pickaxe", target.Label);
        Assert.Equal(0ul, target.TargetId);
        Assert.Equal("item:stone_pickaxe", target.Canonical);
    }
}
