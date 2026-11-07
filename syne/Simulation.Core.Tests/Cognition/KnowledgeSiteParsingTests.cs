// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Lecture des croyances de chantier (SYNE-222).

using Simulation.Core.Cognition;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

public class KnowledgeSiteParsingTests
{
    [Fact]
    public void TryParseSiteState_ReadsPositionAndStateOrdinal()
    {
        bool parsed = KnowledgeBuilder.TryParseSiteState("120.50,80.25,2", out Position position, out int state);

        Assert.True(parsed);
        Assert.Equal(new Position(120.5, 80.25), position);
        Assert.Equal(2, state);
    }

    [Theory]
    [InlineData("")]
    [InlineData("120.50,80.25")]
    [InlineData("120.50,80.25,brouillon")]
    public void TryParseSiteState_RejectsMalformedValues(string value)
    {
        Assert.False(KnowledgeBuilder.TryParseSiteState(value, out _, out _));
    }

    [Fact]
    public void TryParseSiteProgress_ReadsCompletedAndRequired()
    {
        Assert.True(KnowledgeBuilder.TryParseSiteProgress("3,10", out int completed, out int required));

        Assert.Equal(3, completed);
        Assert.Equal(10, required);
    }

    [Theory]
    [InlineData("")]
    [InlineData("3")]
    [InlineData("trois,10")]
    public void TryParseSiteProgress_RejectsMalformedValues(string value)
    {
        Assert.False(KnowledgeBuilder.TryParseSiteProgress(value, out _, out _));
    }
}
