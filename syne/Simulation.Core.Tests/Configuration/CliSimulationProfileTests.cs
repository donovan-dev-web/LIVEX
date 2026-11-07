// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Scénario batch accepté par --simulation : reference (défaut intégré) — toute autre valeur
//        reste rejetée avec la liste des valeurs acceptées.

using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests.Configuration;

/// <summary>
/// Scénario batch accepté par <c>--simulation</c> : <c>reference</c>
/// (défaut intégré) — toute autre valeur reste rejetée avec la liste des
/// valeurs acceptées.
/// </summary>
public sealed class CliSimulationProfileTests
{
    [Fact]
    public void Parse_AcceptsTheReferenceSimulationId()
    {
        CliOptions cli = CliOptions.Parse(["--simulation", "reference", "--headless"]);

        Assert.Equal("reference", cli.Simulation);
        Assert.True(cli.Headless);
    }

    [Fact]
    public void Parse_RejectsTheRetiredPrismScenario()
    {
        // Le profil `prism` est retiré : PRISM fournit sa géométrie et ses
        // obstacles, un scénario dédié n'a plus de contenu propre.
        var exception = Assert.Throws<ArgumentException>(() =>
            CliOptions.Parse(["--simulation", "prism"]));

        Assert.Contains("prism", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithoutSimulation_DefaultsToNull()
    {
        CliOptions cli = CliOptions.Parse(["--headless"]);

        Assert.Null(cli.Simulation);
        Assert.True(SimulationProfiles.IsKnownSimulationId(cli.Simulation));
    }

    [Fact]
    public void Parse_RejectsUnknownSimulationId_WithTheAcceptedValues()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CliOptions.Parse(["--simulation", "ecosystem_01"]));

        Assert.Contains("reference", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ecosystem_01", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheReferenceScenarioIsKnown()
    {
        Assert.Equal("reference", SimulationProfiles.ReferenceId);
        Assert.True(SimulationProfiles.IsKnownSimulationId("reference"));
        Assert.True(SimulationProfiles.IsKnownSimulationId(null));
        Assert.False(SimulationProfiles.IsKnownSimulationId("prism"));
        Assert.False(SimulationProfiles.IsKnownSimulationId("bogus"));
    }
}
