using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests.Actions;

/// <summary>
/// Catalogue déclaratif des actions (SYNE-040) : chaque action est déclarée en
/// configuration (<c>agents.actions.catalog</c>) et résolue en définition.
/// </summary>
public class ActionCatalogTests
{
    private static ActionCatalog NewCatalog(ActionCatalogSettings? catalog = null)
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        if (catalog is not null)
        {
            options.Agents.Actions.Catalog = catalog;
        }

        return new ActionCatalog(options.Agents.Actions);
    }

    [Fact]
    public void Defaults_DeclareEveryDesireKind()
    {
        ActionCatalog catalog = NewCatalog();

        foreach (DesireKind kind in Enum.GetValues<DesireKind>())
        {
            Assert.NotNull(catalog[kind]);
        }

        // Ordre du catalogue = ordre de l'enum (départage stable, décision n°22).
        Assert.Equal(Enum.GetValues<DesireKind>().Length, catalog.Definitions.Count);
    }

    [Fact]
    public void EatAndDrink_DeclareReserveDependency()
    {
        ActionCatalog catalog = NewCatalog();

        Assert.Equal(ResourceKind.Food, catalog[DesireKind.Eat].RequiresReserve);
        Assert.Equal(30.0, catalog[DesireKind.Eat].HungerRecovery);
        Assert.Equal(0.2, catalog[DesireKind.Eat].EnergyCost);

        Assert.Equal(ResourceKind.Water, catalog[DesireKind.Drink].RequiresReserve);
        Assert.Equal(30.0, catalog[DesireKind.Drink].ThirstRecovery);
    }

    [Fact]
    public void MovementKinds_AreDeclaredWithMoveEnergyCost()
    {
        ActionCatalog catalog = NewCatalog();

        Assert.True(catalog[DesireKind.SeekFood].Movement);
        Assert.True(catalog[DesireKind.Explore].Movement);
        Assert.Equal(0.5, catalog[DesireKind.SeekFood].EnergyCost);
        Assert.False(catalog[DesireKind.Rest].Movement);
    }

    [Fact]
    public void IsViable_RequiresNonEmptyReserve()
    {
        ActionCatalog catalog = NewCatalog();

        Assert.True(catalog.IsViable(DesireKind.Eat, ResourceStocks.FromState(10.0, 1000.0, 50.0)));
        Assert.False(catalog.IsViable(DesireKind.Eat, ResourceStocks.FromState(0.0, 1000.0, 50.0)));
        Assert.True(catalog.IsViable(DesireKind.SeekFood, ResourceStocks.FromState(0.0, 1000.0, 50.0)));
    }

    [Fact]
    public void MissingEntry_ThrowsDeclarativeError()
    {
        var catalog = new ActionCatalogSettings();
        catalog.Entries.Clear();

        Assert.Throws<InvalidOperationException>(() => NewCatalog(catalog));
    }

    [Fact]
    public void EntryOverrides_AreHonored()
    {
        var catalogSettings = new ActionCatalogSettings();
        catalogSettings.Entries["eat"].HungerRecovery = 40.0;

        ActionCatalog catalog = NewCatalog(catalogSettings);
        Assert.Equal(40.0, catalog[DesireKind.Eat].HungerRecovery);
    }
}