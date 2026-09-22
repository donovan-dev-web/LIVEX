using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Xunit;

namespace Simulation.Core.Tests;

public class InheritanceTests
{
    private static MemorySettings MemorySettings() => new();

    private static BeliefSettings BeliefSettings() => new();

    private static TraitSet Traits(bool brave, bool clever) =>
        new(TraitSet.TraitNames.ToDictionary(
            name => name,
            name => name switch
            {
                "bravery" => brave ? 1.6 : 0.4,
                "curiosity" => clever ? 1.6 : 0.4,
                _ => 1.0,
            },
            StringComparer.Ordinal));

    [Fact]
    public void FuseTraits_AveragesPerTrait_AndClamps()
    {
        TraitSet parentA = Traits(brave: true, clever: true);
        TraitSet parentB = Traits(brave: false, clever: false);

        TraitSet fused = Inheritance.FuseTraits(parentA, parentB);

        Assert.Equal(1.0, fused["bravery"], 12);
        Assert.Equal(1.0, fused["curiosity"], 12);
        Assert.Equal(TraitSet.TraitNames.Count, fused.Values.Count());
    }

    [Fact]
    public void FuseTraits_ClampsToBounds()
    {
        var max = new TraitSet(TraitSet.TraitNames.ToDictionary(
            name => name,
            _ => 2.0,
            StringComparer.Ordinal));
        var min = new TraitSet(TraitSet.TraitNames.ToDictionary(
            name => name,
            _ => 0.0,
            StringComparer.Ordinal));

        TraitSet fused = Inheritance.FuseTraits(max, min);

        Assert.Equal(1.0, fused["speed"], 12);
    }

    [Fact]
    public void FuseTraits_DefaultSettings_MatchesAverage_BackwardCompatible()
    {
        // SYNE-063 : dominance 0 + mutation 0 ⇒ fusion égalitaire (V0.1 préservée).
        TraitSet parentA = Traits(brave: true, clever: false);
        TraitSet parentB = Traits(brave: false, clever: false);

        TraitSet fused = Inheritance.FuseTraits(parentA, parentB, new InheritanceSettings(), seed: 7);

        Assert.Equal(Inheritance.FuseTraits(parentA, parentB).Values, fused.Values);
        Assert.Equal(1.0, fused["bravery"], 12);
    }

    [Fact]
    public void FuseTraits_FullDominance_TakesExpressingParent()
    {
        // SYNE-063 : le parent « exprimant » (écart au neutre maximal) l'emporte à
        // dominance pleine. Bravery : parentA 1.8 (écart 0.8) vs parentB 1.2 (0.2).
        var parentA = new TraitSet(TraitSet.TraitNames.ToDictionary(
            name => name,
            name => name == "bravery" ? 1.8 : 1.0,
            StringComparer.Ordinal));
        var parentB = new TraitSet(TraitSet.TraitNames.ToDictionary(
            name => name,
            name => name == "bravery" ? 1.2 : 1.0,
            StringComparer.Ordinal));

        var settings = new InheritanceSettings { Dominance = 1.0 };
        TraitSet fused = Inheritance.FuseTraits(parentA, parentB, settings, seed: 7);

        Assert.Equal(1.8, fused["bravery"], 12);
        Assert.Equal(1.0, fused["curiosity"], 12);
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.75)]
    public void FuseTraits_Dominance_InterpolatesBetweenAverageAndExpress(double dominance)
    {
        var parentA = new TraitSet(TraitSet.TraitNames.ToDictionary(
            name => name,
            name => name == "bravery" ? 1.8 : 1.0,
            StringComparer.Ordinal));
        var parentB = new TraitSet(TraitSet.TraitNames.ToDictionary(
            name => name,
            name => name == "bravery" ? 1.2 : 1.0,
            StringComparer.Ordinal));

        var settings = new InheritanceSettings { Dominance = dominance };
        TraitSet fused = Inheritance.FuseTraits(parentA, parentB, settings, seed: 7);

        double expected = (1.5 * (1.0 - dominance)) + (1.8 * dominance);
        Assert.Equal(expected, fused["bravery"], 12);
    }

    [Fact]
    public void FuseTraits_Mutation_IsDeterministicForSameSeed()
    {
        var settings = new InheritanceSettings { MutationRate = 1.0, MutationMagnitude = 0.5 };
        TraitSet parentA = Traits(brave: true, clever: true);
        TraitSet parentB = Traits(brave: false, clever: false);

        TraitSet first = Inheritance.FuseTraits(parentA, parentB, settings, seed: 123);
        TraitSet second = Inheritance.FuseTraits(parentA, parentB, settings, seed: 123);

        Assert.Equal(first.Values.OrderBy(pair => pair.Key), second.Values.OrderBy(pair => pair.Key));
    }

    [Fact]
    public void FuseTraits_Mutation_RespectsBounds()
    {
        var settings = new InheritanceSettings { MutationRate = 1.0, MutationMagnitude = 10.0 };
        TraitSet fused = Inheritance.FuseTraits(
            Traits(brave: true, clever: true),
            Traits(brave: false, clever: false),
            settings,
            seed: 5);

        foreach (double value in fused.Values.Select(pair => pair.Value))
        {
            Assert.InRange(value, TraitSet.Min, TraitSet.Max);
        }
    }

    [Fact]
    public void InheritMemory_ReunitesParentMemories_AtBirthTick()
    {
        var memoryA = new Memory(MemorySettings());
        memoryA.Store(MemoryCategory.Observation, "a", "cartes-a", 0.9, storedAt: 10);
        var memoryB = new Memory(MemorySettings());
        memoryB.Store(MemoryCategory.Event, "b", "carteb", 0.7, storedAt: 20);

        Memory inherited = Inheritance.InheritMemory(
            memoryA.AllEntries.Concat(memoryB.AllEntries),
            MemorySettings(),
            birthTick: 50);

        Assert.Equal(2, inherited.Count);
        IReadOnlyList<MemoryRecall> recalled = inherited.Recall(50);
        Assert.Equal(2, recalled.Count);

        // Ré-horodatés à la naissance : salience 1.0 au tick de naissance.
        Assert.All(recalled, recall => Assert.Equal(1.0, recall.Salience, 10));
        Assert.Equal(50UL, recalled[0].Entry.StoredAt);
    }

    [Fact]
    public void InheritMemory_FiltersBelowSalienceThreshold()
    {
        var memory = new Memory(MemorySettings());
        memory.Store(MemoryCategory.Observation, "a", "vieux", 0.9, storedAt: 0);
        memory.Store(MemoryCategory.Observation, "a", "recent", 0.9, storedAt: 40);

        Memory inherited = Inheritance.InheritMemory(
            memory.AllEntries,
            MemorySettings(),
            birthTick: 50,
            salienceThreshold: 0.7);

        // « vieux » : exp(-0.01×50) = 0.607 < 0.7 → exclu.
        // « recent » : exp(-0.01×10) = 0.905 ≥ 0.7 → conservé.
        IReadOnlyList<MemoryRecall> recalled = inherited.Recall(50);
        MemoryRecall recall = Assert.Single(recalled);
        Assert.Equal("recent", recall.Entry.Content);
    }

    [Theory]
    [InlineData(0.005)]
    [InlineData(0.9)]
    public void InheritMemory_SalienceThreshold_FiltersDeterministically(double threshold)
    {
        var memory = new Memory(MemorySettings());
        memory.Store(MemoryCategory.Observation, "a", "old", 0.9, storedAt: 0);

        Memory inherited = Inheritance.InheritMemory(
            memory.AllEntries,
            MemorySettings(),
            birthTick: 300,
            salienceThreshold: threshold);

        double salience = Math.Exp(-0.01 * 300); // exp(-3) ≈ 0.0498
        Assert.Equal(salience >= threshold, inherited.Count == 1);
    }

    [Fact]
    public void InheritBeliefs_KeepsStrongestOnSameFact_AndRestampsExpiry()
    {
        Fact shared = new("entity-3", "position", "12.00,14.00");
        var mother = new BeliefSet();
        mother.ApplyEvidence(shared, 0.8, "perception-1", BeliefSettings(), tick: 10);
        var father = new BeliefSet();
        father.ApplyEvidence(shared, 0.5, "perception-2", BeliefSettings(), tick: 12);
        father.ApplyEvidence(new Fact("entity-9", "position", "1.00,2.00"), 0.6, "perception-2", BeliefSettings(), tick: 12);

        BeliefSet inherited = Inheritance.InheritBeliefs(
            mother.All.Concat(father.All),
            BeliefSettings(),
            birthTick: 50);

        Assert.Equal(2, inherited.Count);

        Assert.True(inherited.TryGet(shared, out Belief? strongest));
        Assert.Equal(0.8, strongest.Confidence, 12);
        Assert.Equal("héritage", strongest.Source);
        Assert.Equal(50UL, strongest.BornTick);
        Assert.Equal(50UL + BeliefSettings().ExpiryTicks, strongest.ExpiryTick);
    }

    [Fact]
    public void MindState_Born_InheritsMemoryAndBeliefs_WithFreshNeeds()
    {
        var options = ConfigLoader.LoadDefaults();
        var mother = new MindState(options);
        var father = new MindState(options);
        mother.Memory.Store(MemoryCategory.Interaction, "a", "rituel", 0.8, storedAt: 5);
        mother.Beliefs.ApplyEvidence(
            new Fact("entity-2", "position", "3.00,4.00"),
            0.75,
            "perception-1",
            options.Agents.Beliefs,
            tick: 6);

        MindState born = MindState.Born(options, mother, father, birthTick: 30);

        Assert.Equal(1, born.Memory.Count);
        IReadOnlyList<MemoryRecall> recalled = born.Memory.Recall(30);
        Assert.Equal("rituel", Assert.Single(recalled).Entry.Content);
        Assert.True(born.Beliefs.TryGet(
            new Fact("entity-2", "position", "3.00,4.00"),
            out Belief? belief));
        Assert.Equal(0.75, belief.Confidence, 12);
        Assert.Equal(0.0, born.Needs.Hunger);
        Assert.Equal(0, born.Trust.Count);
    }

    [Fact]
    public void MindState_Born_UsesConfiguredSalienceThreshold()
    {
        // SYNE-063 : le seuil de salience câblé via agents.inheritance.salienceThreshold.
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Inheritance.SalienceThreshold = 0.7;

        var mother = new MindState(options);
        for (int i = 0; i < 3; i++)
        {
            mother.Memory.Store(MemoryCategory.Observation, "a", $"souvenir-{i}", 0.9, storedAt: (ulong)i);
        }

        MindState born = MindState.Born(options, mother, new MindState(options), birthTick: 50);

        // exp(-0.01×50) = 0.607 < 0.7 → aucun souvenir transmis.
        Assert.Equal(0, born.Memory.Count);
    }
}