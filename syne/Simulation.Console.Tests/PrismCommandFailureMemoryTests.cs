// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using Simulation.Console.Integration;
using Xunit;

namespace Simulation.Console.Tests;

/// <summary>
/// Mémoire des refus durables (SYNE-220, étape 3b) : une consigne que PRISM a
/// refusée parce qu'elle est inapplicable ne doit pas être réémise en boucle,
/// alors qu'un refus transitoire ou un défaut de payload doit rester visible.
/// </summary>
public sealed class PrismCommandFailureMemoryTests
{
    private static readonly string Payload = """{"target":{"x":12,"y":30,"z":57}}""";

    [Fact]
    public void APathNotFoundStopsTheSameCommandForTheRetryWindow()
    {
        var memory = new PrismCommandFailureMemory(retryAfterTicks: 100);

        Assert.True(memory.Record(agentId: 7, kind: "Aller", Payload, "path_not_found", tick: 10));

        Assert.True(memory.IsSuppressed(7, "Aller", Payload, tick: 40));
        Assert.False(memory.IsSuppressed(7, "Aller", Payload, tick: 110));
        Assert.Equal(1, memory.FailureCount);
    }

    [Fact]
    public void ATransientOrInvalidFailureStaysRetried()
    {
        var memory = new PrismCommandFailureMemory();

        // `not_adjacent` se corrige en bougeant, `invalid_payload` est un défaut de
        // notre write-back : les mémoriser masquerait le problème au lieu de le voir.
        Assert.False(memory.Record(1, "Consommer", Payload, "not_adjacent", tick: 5));
        Assert.False(memory.Record(1, "Aller", Payload, "invalid_payload", tick: 5));
        Assert.False(memory.IsSuppressed(1, "Aller", Payload, tick: 6));
        Assert.Equal(0, memory.FailureCount);
    }

    [Fact]
    public void TheMemoryIsPerAgentAndPerTarget()
    {
        var memory = new PrismCommandFailureMemory();
        const string Other = """{"target":{"x":13,"y":30,"z":57}}""";

        memory.Record(3, "Aller", Payload, "path_not_found", tick: 10);

        Assert.False(memory.IsSuppressed(4, "Aller", Payload, tick: 11));
        Assert.False(memory.IsSuppressed(3, "Aller", Other, tick: 11));
        Assert.True(memory.IsSuppressed(3, "Aller", Payload, tick: 11));
    }

    [Fact]
    public void ADuplicatedFailureIsCountedAndDescribed()
    {
        var memory = new PrismCommandFailureMemory();

        memory.Record(9, "ObtenirObjet", Payload, "source_not_found", tick: 20);
        memory.Record(9, "ObtenirObjet", Payload, "source_not_found", tick: 25);

        Assert.Equal(2, memory.FailureCount);
        Assert.Equal(1, memory.SuppressedCount);
        string description = memory.Describe();
        Assert.Contains("source_not_found=2", description, StringComparison.Ordinal);
        Assert.Contains("cibles écartées 1", description, StringComparison.Ordinal);
    }
}
