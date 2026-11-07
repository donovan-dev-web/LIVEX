// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests du savoir des recettes déclaré (SYNE-222) et des croyances durables.

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests;

public class RecipeKnowledgeTests
{
    private static SimulationOptions OptionsWithRecipe(bool enabled = true)
    {
        var options = new SimulationOptions();
        options.Agents.Knowledge.Enabled = enabled;
        options.Agents.Knowledge.Recipes.Add(new RecipeKnowledgeSettings
        {
            Id = "stone_pickaxe",
            Station = "atelier",
            Inputs =
            [
                new RecipeIngredientSettings { Item = "wood", Quantity = 2 },
                new RecipeIngredientSettings { Item = "stone", Quantity = 3 },
            ],
            Outputs = [new RecipeIngredientSettings { Item = "pickaxe_stone", Quantity = 1 }],
        });
        return options;
    }

    [Fact]
    public void DisabledKnowledgeInjectsNoBelief()
    {
        var mind = new MindState(OptionsWithRecipe(enabled: false));

        Assert.Empty(mind.Beliefs.All);
    }

    [Fact]
    public void EnabledKnowledgeInjectsDurableRecipeBeliefs()
    {
        var mind = new MindState(OptionsWithRecipe());

        Assert.True(mind.Beliefs.TryGetCurrent("recipe-stone_pickaxe", KnowledgeBuilder.RecipeInputsPredicate, out Belief inputs));
        Assert.Equal("stone:3;wood:2", inputs.Fact.Value);
        Assert.True(inputs.IsDurable);
        Assert.Equal("config", inputs.Source);
        Assert.Equal(1.0, inputs.Confidence, precision: 6);

        Assert.True(mind.Beliefs.TryGetCurrent("recipe-stone_pickaxe", KnowledgeBuilder.RecipeOutputsPredicate, out Belief outputs));
        Assert.Equal("pickaxe_stone:1", outputs.Fact.Value);

        Assert.True(mind.Beliefs.TryGetCurrent("recipe-stone_pickaxe", KnowledgeBuilder.RecipeStationPredicate, out Belief station));
        Assert.Equal("atelier", station.Fact.Value);
    }

    [Fact]
    public void AHandMadeRecipeCarriesNoStationBelief()
    {
        var options = new SimulationOptions();
        options.Agents.Knowledge.Enabled = true;
        options.Agents.Knowledge.Recipes.Add(new RecipeKnowledgeSettings
        {
            Id = "sharp_stone",
            Inputs = [new RecipeIngredientSettings { Item = "stone", Quantity = 1 }],
            Outputs = [new RecipeIngredientSettings { Item = "sharp_stone", Quantity = 1 }],
        });

        var mind = new MindState(options);

        Assert.False(mind.Beliefs.TryGetCurrent("recipe-sharp_stone", KnowledgeBuilder.RecipeStationPredicate, out _));
    }

    [Fact]
    public void DurableBeliefsNeitherDecayNorExpire()
    {
        var beliefs = new BeliefSet();
        beliefs.Upsert(new Belief(
            new Fact("recipe-x", KnowledgeBuilder.RecipeOutputsPredicate, "x:1"),
            1.0,
            "config",
            bornTick: 0,
            expiryTick: ulong.MaxValue,
            durable: true));
        BeliefSettings settings = new BeliefSettings().Also(value => value.TimeDecayPerTick = 0.5);

        for (ulong tick = 1; tick <= 50; tick++)
        {
            beliefs.Tick(tick, settings);
        }

        Assert.True(beliefs.TryGetCurrent("recipe-x", KnowledgeBuilder.RecipeOutputsPredicate, out Belief belief));
        Assert.Equal(1.0, belief.Confidence, precision: 6);
        Assert.False(belief.IsExpired(currentTick: 10_000));
    }

    [Fact]
    public void ObservedBeliefsStillDecayNextToDurableOnes()
    {
        var beliefs = new BeliefSet();
        BeliefSettings settings = new BeliefSettings().Also(value => value.TimeDecayPerTick = 0.5);
        beliefs.Upsert(new Belief(new Fact("recipe-x", "outputs", "x:1"), 1.0, "config", 0, ulong.MaxValue, durable: true));
        beliefs.Upsert(new Belief(new Fact("entity-9", "position", "1.00,2.00"), 1.0, "perception", 0, ulong.MaxValue));

        beliefs.Tick(currentTick: 1, settings);

        beliefs.TryGetCurrent("recipe-x", "outputs", out Belief durable);
        beliefs.TryGetCurrent("entity-9", "position", out Belief observed);
        Assert.Equal(1.0, durable.Confidence, precision: 6);
        Assert.Equal(0.5, observed.Confidence, precision: 6);
    }

    [Fact]
    public void FormatRecipeLine_IsCanonicalWhateverTheDeclarationOrder()
    {
        string first = KnowledgeBuilder.FormatRecipeLine(
        [
            new RecipeIngredientSettings { Item = "wood", Quantity = 2 },
            new RecipeIngredientSettings { Item = "stone", Quantity = 3 },
        ]);
        string second = KnowledgeBuilder.FormatRecipeLine(
        [
            new RecipeIngredientSettings { Item = "stone", Quantity = 3 },
            new RecipeIngredientSettings { Item = "wood", Quantity = 2 },
        ]);

        Assert.Equal("stone:3;wood:2", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void TryParseRecipeLine_RoundTripsTheCanonicalForm()
    {
        string line = KnowledgeBuilder.FormatRecipeLine(
        [
            new RecipeIngredientSettings { Item = "stone", Quantity = 3 },
            new RecipeIngredientSettings { Item = "wood", Quantity = 2 },
        ]);

        Assert.True(KnowledgeBuilder.TryParseRecipeLine(line, out IReadOnlyList<(string Item, int Quantity)> parsed));
        Assert.Equal([("stone", 3), ("wood", 2)], parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("stone")]
    [InlineData("stone:0")]
    [InlineData("stone:")]
    [InlineData(":2")]
    public void TryParseRecipeLine_RejectsMalformedValues(string? value)
    {
        Assert.False(KnowledgeBuilder.TryParseRecipeLine(value, out _));
    }

    [Fact]
    public void RecipesWithoutEnabledKnowledge_AreRejectedByTheValidator()
    {
        SimulationOptions options = OptionsWithRecipe(enabled: false);

        IReadOnlyList<string> errors = SimulationOptionsValidator.Validate(options);

        Assert.Contains(errors, error => error.Contains("agents.knowledge.recipes est fourni alors que agents.knowledge.enabled est false", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateRecipeIdentifiers_AreRejected()
    {
        SimulationOptions options = OptionsWithRecipe();
        options.Agents.Knowledge.Recipes.Add(new RecipeKnowledgeSettings
        {
            Id = "stone_pickaxe",
            Inputs = [new RecipeIngredientSettings { Item = "stone", Quantity = 1 }],
            Outputs = [new RecipeIngredientSettings { Item = "pickaxe_stone", Quantity = 1 }],
        });

        IReadOnlyList<string> errors = SimulationOptionsValidator.Validate(options);

        Assert.Contains(errors, error => error.Contains("deux fois la recette", StringComparison.Ordinal));
    }

    [Fact]
    public void EmptyIdentifierAndNonPositiveQuantity_AreRejected()
    {
        SimulationOptions options = new SimulationOptions();
        options.Agents.Knowledge.Enabled = true;
        options.Agents.Knowledge.Recipes.Add(new RecipeKnowledgeSettings
        {
            Id = " ",
            Inputs = [new RecipeIngredientSettings { Item = "stone", Quantity = 0 }],
            Outputs = [],
        });

        IReadOnlyList<string> errors = SimulationOptionsValidator.Validate(options);

        Assert.Contains(errors, error => error.Contains(".id ne doit pas être vide", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("au moins une sortie", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("quantity doit être >= 1", StringComparison.Ordinal));
    }

    [Fact]
    public void AValidDeclaredRecipePassesValidation()
    {
        IReadOnlyList<string> errors = SimulationOptionsValidator.Validate(OptionsWithRecipe());

        Assert.DoesNotContain(errors, error => error.StartsWith("agents.knowledge", StringComparison.Ordinal));
    }
}

/// <summary>Petit utilitaire de test : configurer une instance en une expression.</summary>
internal static class TestSettingsExtensions
{
    public static T Also<T>(this T value, Action<T> configure)
    {
        configure(value);
        return value;
    }
}
