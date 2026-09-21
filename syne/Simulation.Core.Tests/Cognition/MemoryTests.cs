using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests;

public class MemoryTests
{
    private static Memory NewMemory(int capacity = 1000) =>
        new(new MemorySettings { MaxCapacity = capacity });

    [Fact]
    public void StoreAndRecall_PreservesSourceCategoryContentConfidenceAndDate()
    {
        var memory = NewMemory();
        memory.Store(MemoryCategory.Observation, "entity-1", "entity-3@12.00,14.00", 0.85, storedAt: 10);

        IReadOnlyList<MemoryRecall> recalled = memory.Recall(10);

        MemoryRecall recall = Assert.Single(recalled);
        Assert.Equal(MemoryCategory.Observation, recall.Entry.Category);
        Assert.Equal("entity-1", recall.Entry.Source);
        Assert.Equal("entity-3@12.00,14.00", recall.Entry.Content);
        Assert.Equal(0.85, recall.Entry.Confidence);
        Assert.Equal(10UL, recall.Entry.StoredAt);
        Assert.Equal(1.0, recall.Salience, 10);
    }

    [Fact]
    public void Recall_OrdersDeterministicallyByStoredAtThenSequence()
    {
        var memory = NewMemory();
        memory.Store(MemoryCategory.Observation, "s", "later", 0.9, storedAt: 20);
        memory.Store(MemoryCategory.Observation, "s", "earlier", 0.9, storedAt: 5);
        memory.Store(MemoryCategory.Observation, "s", "same-tick-second", 0.9, storedAt: 5);

        IReadOnlyList<MemoryRecall> recalled = memory.Recall(20);

        Assert.Equal(["earlier", "same-tick-second", "later"], recalled.Select(r => r.Entry.Content));
    }

    [Theory]
    [InlineData(MemoryCategory.Observation, 500, 0.01, false)]
    [InlineData(MemoryCategory.Event, 500, 0.005, true)]
    [InlineData(MemoryCategory.Interaction, 500, 0.002, true)]
    [InlineData(MemoryCategory.Observation, 100, 0.01, true)]
    public void Recall_DecayByCategory_Decision11(
        MemoryCategory category,
        ulong age,
        double expectedDecay,
        bool shouldBeRecalled)
    {
        var memory = NewMemory();
        memory.Store(category, "s", "c", 0.9, storedAt: 0);

        IReadOnlyList<MemoryRecall> recalled = memory.Recall(age);

        Assert.Equal(shouldBeRecalled, recalled.Count == 1);
        if (recalled.Count == 1)
        {
            double salience = Math.Exp(-expectedDecay * age);
            Assert.Equal(salience, recalled[0].Salience, 10);
        }
    }

    [Fact]
    public void Recall_AppliesForgettingThreshold()
    {
        var memory = NewMemory();
        memory.Store(MemoryCategory.Event, "s", "old", 0.9, storedAt: 0);
        memory.Store(MemoryCategory.Interaction, "s", "fresh", 0.9, storedAt: 950);

        IReadOnlyList<MemoryRecall> recalled = memory.Recall(1000);

        MemoryRecall recall = Assert.Single(recalled);
        Assert.Equal("fresh", recall.Entry.Content);
    }

    [Fact]
    public void Store_EvictsLeastSalientWhenCapacityReached()
    {
        var memory = NewMemory(capacity: 3);
        memory.Store(MemoryCategory.Event, "s", "old-event", 0.9, storedAt: 0);
        memory.Store(MemoryCategory.Observation, "s", "obs-a", 0.9, storedAt: 100);
        memory.Store(MemoryCategory.Observation, "s", "obs-b", 0.9, storedAt: 200);
        memory.Store(MemoryCategory.Observation, "s", "obs-c", 0.9, storedAt: 300);

        Assert.Equal(3, memory.Count);
        IReadOnlyList<MemoryRecall> recalled = memory.Recall(300);
        Assert.Contains(recalled, r => r.Entry.Content == "old-event");
        Assert.Contains(recalled, r => r.Entry.Content == "obs-b");
        Assert.Contains(recalled, r => r.Entry.Content == "obs-c");
        Assert.DoesNotContain(recalled, r => r.Entry.Content == "obs-a");
    }

    [Fact]
    public void Store_AtTieEvictsOldestByDateThenSequence()
    {
        var memory = NewMemory(capacity: 2);
        memory.Store(MemoryCategory.Observation, "s", "first", 0.9, storedAt: 10);
        memory.Store(MemoryCategory.Observation, "s", "second", 0.9, storedAt: 10);
        memory.Store(MemoryCategory.Observation, "s", "third", 0.9, storedAt: 10);

        Assert.Equal(2, memory.Count);
        IReadOnlyList<MemoryRecall> recalled = memory.Recall(10);
        Assert.Equal(["second", "third"], recalled.Select(r => r.Entry.Content));
    }

    [Fact]
    public void Store_NeverExceedsDefaultCapacityOfOneThousand()
    {
        var memory = NewMemory(); // capacité par défaut : 1000 (SYNE-022)

        for (int tick = 1; tick <= 1500; tick++)
        {
            memory.Store(MemoryCategory.Observation, "s", $"souvenir-{tick}", 0.9, storedAt: (ulong)tick);
        }

        Assert.Equal(1000, memory.Count);
        // Les plus récents sont conservés (les anciens sont les moins saillants).
        MemoryRecall newest = memory.Recall(1500).Last();
        Assert.Equal("souvenir-1500", newest.Entry.Content);
    }

    [Fact]
    public void Store_EvictsAcrossMixedCategories_LeastSalientFirst()
    {
        var memory = NewMemory(capacity: 4);
        // Catégorie à decay faible (Interaction 0.002) : très saillant longtemps.
        memory.Store(MemoryCategory.Interaction, "s", "rituel", 0.9, storedAt: 0);
        // Observation (0.01) : décroît le plus vite.
        memory.Store(MemoryCategory.Observation, "s", "obs-py1", 0.9, storedAt: 0);
        memory.Store(MemoryCategory.Event, "s", "event-py", 0.9, storedAt: 100);
        memory.Store(MemoryCategory.Observation, "s", "obs-rec", 0.9, storedAt: 200);

        // Le moins saillant à t=900 est « obs-py1 » (exp(-0.01×900) ≈ 0.00012).
        memory.Store(MemoryCategory.Observation, "s", "obs-push", 0.9, storedAt: 900);

        Assert.Equal(4, memory.Count);
        IReadOnlyList<MemoryRecall> recalled = memory.Recall(900);
        Assert.DoesNotContain(recalled, r => r.Entry.Content == "obs-py1");
        Assert.Contains(recalled, r => r.Entry.Content == "rituel");
    }
}