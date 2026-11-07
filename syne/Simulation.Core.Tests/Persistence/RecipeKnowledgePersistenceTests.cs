// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Le savoir durable des recettes (SYNE-222) survit à une reprise bit-à-bit.

using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Persistence;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

public class RecipeKnowledgePersistenceTests
{
    private static SimulationOptions Options()
    {
        var options = new SimulationOptions();
        options.Agents.InitialCount = 1;
        options.Agents.Knowledge.Enabled = true;
        options.Agents.Knowledge.Recipes.Add(new RecipeKnowledgeSettings
        {
            Id = "stone_pickaxe",
            Station = "atelier",
            Inputs = [new RecipeIngredientSettings { Item = "stone", Quantity = 3 }],
            Outputs = [new RecipeIngredientSettings { Item = "pickaxe_stone", Quantity = 1 }],
        });
        return options;
    }

    private static (SimulationLoop Loop, Entity Entity) Build(SimulationOptions options)
    {
        var world = new Simulation.Core.World.World(new WorldSize(500, 500), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(4242);
        (Entity entity, Xoshiro256StarStar next) =
            EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, 1, bornAt: 0);
        world.AddEntity(entity);
        return (new SimulationLoop(world, next, options), entity);
    }

    [Fact]
    public void DurableRecipeBeliefSurvivesCaptureAndRestore()
    {
        SimulationOptions options = Options();
        (SimulationLoop loop, Entity entity) = Build(options);
        loop.AdvanceOneTick();

        SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);
        MindSnapshotDto captured = snapshot.Minds.Single(mind => mind.EntityId == entity.Id.Value);
        BeliefSnapshotDto recipe = captured.Beliefs.Single(belief =>
            belief.Subject == "recipe-stone_pickaxe" && belief.Predicate == "outputs");
        Assert.True(recipe.Durable);

        SimulationLoop restored = SimulationSnapshotRestorer.Restore(snapshot, options);
        SimulationSnapshot roundTrip = SimulationSnapshotCodec.Capture(restored);
        MindSnapshotDto restoredMind = roundTrip.Minds.Single(mind => mind.EntityId == entity.Id.Value);

        Assert.Equal(captured.Beliefs, restoredMind.Beliefs);
        Assert.Contains(restoredMind.Beliefs, belief =>
            belief.Subject == "recipe-stone_pickaxe" && belief.Durable);
    }

    [Fact]
    public void WithoutDeclaredKnowledge_NoDurableBeliefIsPersisted()
    {
        SimulationOptions options = new SimulationOptions();
        (SimulationLoop loop, Entity entity) = Build(options);
        loop.AdvanceOneTick();

        SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);
        MindSnapshotDto captured = snapshot.Minds.Single(mind => mind.EntityId == entity.Id.Value);

        Assert.DoesNotContain(captured.Beliefs, belief => belief.Subject.StartsWith("recipe-", StringComparison.Ordinal));
        Assert.All(captured.Beliefs, belief => Assert.False(belief.Durable));
    }
}
