// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests du savoir des recettes appris par les livres (SYNE-224).

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

public class BookRecipeTests
{
    private const string PickaxeBook =
        "Traité du tailleur de pierre.\n" +
        "recipe:stone_pickaxe|atelier|stone:3;wood:2|pickaxe_stone:1\n" +
        "Le reste est de la prose, sans déclaration.\n";

    private static SimulationOptions Options(bool learnFromBooks = true)
    {
        var options = new SimulationOptions();
        options.World.Books.Enabled = true;
        options.Agents.Knowledge.LearnFromBooks = learnFromBooks;
        return options;
    }

    private static (SimulationLoop Loop, Entity Entity) Build(SimulationOptions options)
    {
        var world = new Simulation.Core.World.World(new WorldSize(500, 500), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(99);
        (Entity entity, Xoshiro256StarStar next) =
            EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, 1, bornAt: 0);
        world.AddEntity(entity);
        var loop = new SimulationLoop(world, next, options);
        loop.AdvanceOneTick();
        return (loop, entity);
    }

    [Fact]
    public void Parse_ReadsDeclaredRecipesAndIgnoresProse()
    {
        IReadOnlyList<RecipeKnowledgeSettings> recipes = BookRecipeParser.Parse(PickaxeBook);

        RecipeKnowledgeSettings recipe = Assert.Single(recipes);
        Assert.Equal("stone_pickaxe", recipe.Id);
        Assert.Equal("atelier", recipe.Station);
        Assert.Equal("stone:3;wood:2", KnowledgeBuilder.FormatRecipeLine(recipe.Inputs));
        Assert.Equal("pickaxe_stone:1", KnowledgeBuilder.FormatRecipeLine(recipe.Outputs));
    }

    [Fact]
    public void Parse_TreatsADashAsNoStation()
    {
        RecipeKnowledgeSettings recipe = Assert.Single(
            BookRecipeParser.Parse("recipe:sharp_stone|-|stone:1|sharp_stone:1"));

        Assert.Null(recipe.Station);
    }

    [Fact]
    public void Parse_IgnoresMalformedLinesWithoutThrowing()
    {
        const string content =
            "recipe:\n" +
            "recipe:no_pipes\n" +
            "recipe:x|station|stone|out:1\n" +
            "recipe:y|station|stone:0|out:1\n" +
            "recipe:z|station|stone:1|out:1\n";

        IReadOnlyList<RecipeKnowledgeSettings> recipes = BookRecipeParser.Parse(content);

        Assert.Equal("z", Assert.Single(recipes).Id);
    }

    [Fact]
    public void Parse_KeepsTheLastDeclarationOfARepeatedRecipe()
    {
        const string content =
            "recipe:p|-|stone:1|out:1\n" +
            "recipe:p|-|stone:5|out:2\n";

        RecipeKnowledgeSettings recipe = Assert.Single(BookRecipeParser.Parse(content));

        Assert.Equal("stone:5", KnowledgeBuilder.FormatRecipeLine(recipe.Inputs));
        Assert.Equal("out:2", KnowledgeBuilder.FormatRecipeLine(recipe.Outputs));
    }

    [Fact]
    public void ReadingABookTeachesItsRecipesDurably()
    {
        (SimulationLoop loop, Entity entity) = Build(Options());
        var book = new Book("codex", authorId: entity.Id.Value, "Codex", PickaxeBook);
        loop.World.AddBook(book);

        loop.ReadBook(book, entity.Id.Value);

        BeliefSet beliefs = loop.Cognition.MindOf(entity.Id.Value).Beliefs;
        Assert.True(beliefs.TryGetCurrent("recipe-stone_pickaxe", KnowledgeBuilder.RecipeInputsPredicate, out Belief inputs));
        Assert.Equal("stone:3;wood:2", inputs.Fact.Value);
        Assert.True(inputs.IsDurable);
        Assert.Equal("book:codex", inputs.Source);
        Assert.Equal(loop.CurrentTick, inputs.BornTick);
        Assert.False(inputs.IsExpired(currentTick: inputs.BornTick + 100_000));
    }

    [Fact]
    public void ReadingABookWithoutDeclarationsTeachesNothing()
    {
        (SimulationLoop loop, Entity entity) = Build(Options());
        var book = new Book("roman", entity.Id.Value, "Roman", "Un récit sans recette.");
        loop.World.AddBook(book);

        loop.ReadBook(book, entity.Id.Value);

        BeliefSet beliefs = loop.Cognition.MindOf(entity.Id.Value).Beliefs;
        Assert.DoesNotContain(beliefs.All, belief => belief.Fact.Subject.StartsWith("recipe-", StringComparison.Ordinal));
    }

    [Fact]
    public void LearnFromBooksCanBeDisabled()
    {
        (SimulationLoop loop, Entity entity) = Build(Options(learnFromBooks: false));
        var book = new Book("codex", entity.Id.Value, "Codex", PickaxeBook);
        loop.World.AddBook(book);

        loop.ReadBook(book, entity.Id.Value);

        BeliefSet beliefs = loop.Cognition.MindOf(entity.Id.Value).Beliefs;
        Assert.DoesNotContain(beliefs.All, belief => belief.Fact.Subject.StartsWith("recipe-", StringComparison.Ordinal));
    }

    [Fact]
    public void ABookOverridesAConfiguredRecipeWithItsOwnDeclaration()
    {
        SimulationOptions options = Options();
        options.Agents.Knowledge.Enabled = true;
        options.Agents.Knowledge.Recipes.Add(new RecipeKnowledgeSettings
        {
            Id = "stone_pickaxe",
            Inputs = [new RecipeIngredientSettings { Item = "stone", Quantity = 1 }],
            Outputs = [new RecipeIngredientSettings { Item = "pickaxe_stone", Quantity = 1 }],
        });
        (SimulationLoop loop, Entity entity) = Build(options);
        var book = new Book("codex", entity.Id.Value, "Codex", PickaxeBook);
        loop.World.AddBook(book);

        loop.ReadBook(book, entity.Id.Value);

        BeliefSet beliefs = loop.Cognition.MindOf(entity.Id.Value).Beliefs;
        Assert.True(beliefs.TryGetCurrent("recipe-stone_pickaxe", KnowledgeBuilder.RecipeInputsPredicate, out Belief inputs));
        Assert.Equal("stone:3;wood:2", inputs.Fact.Value);
        Assert.Equal("book:codex", inputs.Source);
    }
}
