using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests.Configuration;

/// <summary>
/// Validation des bornes et des valeurs non finies (jalon review/refactor,
/// engineVersion 0.12.0).
///
/// <para>
/// Les contrôles de plage s'écrivent <c>value is &lt; 0.0 or &gt; 1.0</c>. En IEEE-754,
/// toute comparaison avec NaN est fausse : <c>NaN is &lt; 0.0 or &gt; 1.0</c> vaut
/// <c>false</c>, donc un NaN franchissait la validation et se propageait ensuite
/// dans les distances, l'énergie et la Barclay, corrompant le monde sans erreur.
/// </para>
/// </summary>
public sealed class SimulationOptionsValidatorNonFiniteTests
{
    private static IReadOnlyList<string> Validate(SimulationOptions options) =>
        SimulationOptionsValidator.Validate(options);

    private static void AssertRejected(SimulationOptions options, string expectedFragment)
    {
        IReadOnlyList<string> errors = Validate(options);

        Assert.Contains(errors, error => error.Contains(expectedFragment, StringComparison.Ordinal));
    }

    [Fact]
    public void DefaultProfile_IsValid()
    {
        Assert.Empty(Validate(ConfigLoader.LoadDefaults()));
    }

    [Fact]
    public void ReferenceProfile_IsValid()
    {
        Assert.Empty(Validate(SimulationProfiles.Reference()));
    }

    [Fact]
    public void NaN_ConfidenceFalloff_IsRejected()
    {
        // Le piège précis : `NaN is < 0.0 or > 1.0` == false, la plage l'acceptait.
        SimulationOptions options = SimulationProfiles.Reference();
        options.Agents.Perception.ConfidenceFalloff = double.NaN;

        AssertRejected(options, "confidenceFalloff");
    }

    [Fact]
    public void NaN_InAnyTrait_IsRejected_WithItsKey()
    {
        SimulationOptions options = SimulationProfiles.Reference();
        options.Agents.Traits["bravery"] = double.NaN;

        AssertRejected(options, "traits.bravery");
    }

    [Fact]
    public void Infinity_InResourceRegeneration_IsRejected()
    {
        SimulationOptions options = SimulationProfiles.Reference();
        options.Resources.Food.RegenerationRate = double.PositiveInfinity;

        AssertRejected(options, "regenerationRate");
    }

    [Fact]
    public void NaN_InActionCatalogEntry_IsRejected()
    {
        SimulationOptions options = SimulationProfiles.Reference();
        ActionEntrySettings entry = options.Agents.Actions.Catalog.Entries.Values.First();
        entry.EnergyCost = double.NaN;

        AssertRejected(options, "energyCost");
    }

    [Fact]
    public void NaN_InTerritoryZone_IsRejected()
    {
        SimulationOptions options = SimulationProfiles.Reference();
        options.World.Territories.Enabled = true;
        options.World.Territories.Zones =
        [
            new TerritoryZoneDefinition { Id = "nid", CenterX = 10, CenterY = 10, Radius = double.NaN },
        ];

        AssertRejected(options, "world.territories.zones[0].radius");
    }

    [Fact]
    public void NaN_InObstacleLayout_IsRejected()
    {
        SimulationOptions options = SimulationProfiles.Reference();
        options.World.Obstacles = true;
        options.World.ObstacleLayout =
        [
            new StaticObstacleSettings { Id = "rocher", X = double.NaN, Y = 10, Radius = 5 },
        ];

        AssertRejected(options, "world.obstacleLayout[0].x");
    }

    [Fact]
    public void NaN_InSpatialCellSize_IsRejected()
    {
        SimulationOptions options = SimulationProfiles.Reference();
        options.Agents.Perception.SpatialCellSize = double.NaN;

        AssertRejected(options, "spatialCellSize");
    }

    [Fact]
    public void NaN_InWorldCellSize_IsRejected()
    {
        SimulationOptions options = SimulationProfiles.Reference();
        options.Simulation.WorldCellSize = double.NaN;

        AssertRejected(options, "worldCellSize");
    }

    [Fact]
    public void NegativeOrZero_CellSizes_AreRejected()
    {
        SimulationOptions options = SimulationProfiles.Reference();
        options.Agents.Perception.SpatialCellSize = 0.0;
        options.Simulation.WorldCellSize = -1.0;

        IReadOnlyList<string> errors = Validate(options);

        Assert.Contains(errors, error => error.Contains("spatialCellSize", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("worldCellSize", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidSpatialCellSize_IsAccepted()
    {
        SimulationOptions options = SimulationProfiles.Reference();
        options.Agents.Perception.SpatialCellSize = 12.5;

        Assert.Empty(Validate(options));
    }

    [Fact]
    public void NaN_Validation_IsReported_NotSilentlyAccepted()
    {
        // Le message doit nommer le champ ET la valeur, sinon le diagnostic est
        // inutilisable en exploitation.
        SimulationOptions options = SimulationProfiles.Reference();
        options.Agents.Needs.HungerRate = double.NaN;

        string error = Assert.Single(Validate(options), e => e.Contains("hungerRate", StringComparison.Ordinal));

        Assert.Contains("NaN", error, StringComparison.Ordinal);
        Assert.Contains("agents.needs.hungerRate", error, StringComparison.Ordinal);
    }

    [Fact]
    public void FiniteBounds_AreStillEnforced_AlongsideTheSweep()
    {
        // Le balayage ne remplace pas les contrôles de plage.
        SimulationOptions options = SimulationProfiles.Reference();
        options.Agents.Perception.ConfidenceFalloff = 2.5;

        AssertRejected(options, "confidenceFalloff");
    }
}
