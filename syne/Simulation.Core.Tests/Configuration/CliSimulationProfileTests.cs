using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests.Configuration;

/// <summary>
/// Scénarios batch acceptés par <c>--simulation</c> (ADR-017 §4.4) :
/// <c>reference</c> (défaut intégré, ADR-016) et <c>prism</c> (profil PRISM) —
/// toute autre valeur reste rejetée avec la liste des valeurs acceptées.
/// </summary>
public sealed class CliSimulationProfileTests
{
    [Theory]
    [InlineData("reference")]
    [InlineData("prism")]
    public void Parse_AcceptsKnownSimulationIds(string id)
    {
        CliOptions cli = CliOptions.Parse(["--simulation", id, "--headless"]);

        Assert.Equal(id, cli.Simulation);
        Assert.True(cli.Headless);
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
        Assert.Contains("prism", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ecosystem_01", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SimulationIds_AreDistinctConstants()
    {
        Assert.Equal("reference", SimulationProfiles.ReferenceId);
        Assert.Equal("prism", SimulationProfiles.PrismId);
        Assert.False(SimulationProfiles.IsKnownSimulationId("bogus"));
    }
}
