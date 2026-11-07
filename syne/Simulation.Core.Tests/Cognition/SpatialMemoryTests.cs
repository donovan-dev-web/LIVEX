// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests de la mémoire spatiale par chunk (SYNE-221).

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests;

public class SpatialMemoryTests
{
    private static BeliefSettings Settings(
        int maxChunks = 512,
        double decay = 0.999,
        double forget = 0.05) =>
        new()
        {
            UpdateStrength = 0.5,
            MaxChangePerSnap = 0.5,
            SpatialMemory = new SpatialMemorySettings
            {
                MaxChunksPerAgent = maxChunks,
                DecayPerTick = decay,
                ForgetThreshold = forget,
            },
        };

    [Fact]
    public void Observe_CreatesAChunkWithTheConfiguredFirstConfidence()
    {
        var memory = new SpatialMemory();
        BeliefSettings settings = Settings();

        memory.Observe(2, 3, 0b0101u, settings, tick: 10);

        Assert.Equal(1, memory.Count);
        Assert.True(memory.TryGet(2, 3, out SpatialChunkMemory chunk));
        Assert.Equal(0b0101u, chunk.TagMask);
        Assert.Equal(0.5, chunk.Confidence, precision: 6);
        Assert.Equal(10UL, chunk.LastSeenTick);
    }

    [Fact]
    public void Observe_MergesTagMasksWithoutLosingEarlierTags()
    {
        var memory = new SpatialMemory();
        BeliefSettings settings = Settings();

        memory.Observe(0, 0, 0b0001u, settings, tick: 1);
        memory.Observe(0, 0, 0b0100u, settings, tick: 2);

        Assert.Equal(0b0101u, memory.TagMaskAt(0, 0));
        Assert.Equal(1, memory.Count);
    }

    [Fact]
    public void Observe_RaisesConfidenceOnRepeatAndSaturatesAtOne()
    {
        var memory = new SpatialMemory();
        BeliefSettings settings = Settings();

        memory.Observe(1, 1, 1u, settings, tick: 1);
        memory.Observe(1, 1, 1u, settings, tick: 2);
        memory.TryGet(1, 1, out SpatialChunkMemory chunk);
        Assert.True(chunk.Confidence > 0.5);

        for (int i = 0; i < 50; i++)
        {
            memory.Observe(1, 1, 1u, settings, tick: (ulong)(3 + i));
        }

        memory.TryGet(1, 1, out chunk);
        Assert.Equal(1.0, chunk.Confidence, precision: 6);
        Assert.Equal(1u, chunk.TagMask);
    }

    [Fact]
    public void Tick_DecaysUnseenChunksAndKeepsTheObservedOne()
    {
        var memory = new SpatialMemory();
        BeliefSettings settings = Settings(decay: 0.5, forget: 0.0);

        memory.Observe(0, 0, 1u, settings, tick: 1);
        memory.Observe(5, 5, 1u, settings, tick: 2);

        memory.Tick(2, settings);

        memory.TryGet(0, 0, out SpatialChunkMemory decayed);
        memory.TryGet(5, 5, out SpatialChunkMemory confirmed);
        Assert.Equal(0.25, decayed.Confidence, precision: 6);
        Assert.Equal(0.5, confirmed.Confidence, precision: 6);
    }

    [Fact]
    public void Tick_ForgetsChunksUnderTheThreshold()
    {
        var memory = new SpatialMemory();
        BeliefSettings settings = Settings(decay: 0.5, forget: 0.3);

        memory.Observe(0, 0, 1u, settings, tick: 1);
        memory.Tick(2, settings);

        Assert.Equal(0, memory.Count);
        Assert.False(memory.TryGet(0, 0, out _));
        Assert.Equal(0u, memory.TagMaskAt(0, 0));
    }

    [Fact]
    public void Observe_EvictsTheLeastConfidentChunkBeyondCapacity()
    {
        var memory = new SpatialMemory();
        BeliefSettings settings = Settings(maxChunks: 2);

        memory.Observe(0, 0, 1u, settings, tick: 1);
        memory.Observe(1, 0, 1u, settings, tick: 2);
        // Le chunk (1,0) est revu : il devient plus sûr que (0,0).
        memory.Observe(1, 0, 1u, settings, tick: 3);
        memory.Observe(2, 0, 1u, settings, tick: 4);

        Assert.Equal(2, memory.Count);
        Assert.False(memory.TryGet(0, 0, out _));
        Assert.True(memory.TryGet(1, 0, out _));
        Assert.True(memory.TryGet(2, 0, out _));
    }

    [Fact]
    public void Chunks_AreOrderedByCoordinates()
    {
        var memory = new SpatialMemory();
        BeliefSettings settings = Settings();

        memory.Observe(3, 1, 1u, settings, tick: 1);
        memory.Observe(1, 2, 1u, settings, tick: 1);
        memory.Observe(1, 1, 1u, settings, tick: 1);

        Assert.Equal(
            [(1, 1), (1, 2), (3, 1)],
            memory.Chunks.Select(chunk => (chunk.ChunkX, chunk.ChunkY)).ToArray());
    }

    [Fact]
    public void RestoreState_ReplacesTheWholeMemory()
    {
        var memory = new SpatialMemory();
        BeliefSettings settings = Settings();
        memory.Observe(9, 9, 1u, settings, tick: 1);

        memory.RestoreState([new SpatialChunkMemory(4, 2, 0b11u, 0.75, 12)]);

        Assert.Equal(1, memory.Count);
        Assert.False(memory.TryGet(9, 9, out _));
        Assert.True(memory.TryGet(4, 2, out SpatialChunkMemory restored));
        Assert.Equal(0b11u, restored.TagMask);
        Assert.Equal(0.75, restored.Confidence, precision: 6);
        Assert.Equal(12UL, restored.LastSeenTick);
    }

    [Fact]
    public void Settings_DefaultBoundsAreTheDocumentedOnes()
    {
        var settings = new SpatialMemorySettings();

        Assert.Equal(512, settings.MaxChunksPerAgent);
        Assert.Equal(0.999, settings.DecayPerTick);
        Assert.Equal(0.05, settings.ForgetThreshold);
        Assert.NotNull(new SimulationOptions().Agents.Beliefs.SpatialMemory);
    }
}
