using Simulation.Core.Configuration;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Réserves globales de ressources (SYNE-042, décision n°4) : initialisées depuis
/// la configuration, consommées par les actions terminales Eat/Drink, bornées à zéro.
/// </summary>
public class ResourceStocksTests
{
    [Fact]
    public void InitializedFromSettings()
    {
        var settings = new ResourceSettings
        {
            Food = new ResourceSpec { Initial = 50 },
            Water = new ResourceSpec { Initial = 200 },
            Wood = new ResourceSpec { Initial = 5 },
        };

        var stocks = new ResourceStocks(settings);

        Assert.Equal(50.0, stocks.Stock(ResourceKind.Food));
        Assert.Equal(200.0, stocks.Stock(ResourceKind.Water));
        Assert.Equal(5.0, stocks.Stock(ResourceKind.Wood));
        Assert.False(stocks.IsEmpty(ResourceKind.Food));
    }

    [Fact]
    public void TryConsume_DecormentsAndClampsAtZero()
    {
        var stocks = ResourceStocks.FromState(food: 30.0, water: 1000.0, wood: 50.0);

        Assert.True(stocks.TryConsume(ResourceKind.Food, 12.0));
        Assert.Equal(18.0, stocks.Stock(ResourceKind.Food), 10);

        // La consommation ne descend jamais sous zéro (réserve finie, décision n°4).
        Assert.True(stocks.TryConsume(ResourceKind.Food, 18.0));
        Assert.Equal(0.0, stocks.Stock(ResourceKind.Food), 10);
        Assert.True(stocks.IsEmpty(ResourceKind.Food));

        Assert.False(stocks.TryConsume(ResourceKind.Food, 5.0));
        Assert.Equal(0.0, stocks.Stock(ResourceKind.Food), 10);
    }

    [Fact]
    public void NegativeConsumption_IsRejected()
    {
        var stocks = ResourceStocks.FromState(food: 30.0, water: 1000.0, wood: 50.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => stocks.TryConsume(ResourceKind.Food, -1.0));
    }

    [Fact]
    public void Snapshot_IsACopy()
    {
        var stocks = ResourceStocks.FromState(food: 30.0, water: 1000.0, wood: 50.0);

        IReadOnlyDictionary<ResourceKind, double> snapshot = stocks.Snapshot();
        // La mutation de la copie n'affecte pas l'état interne.
        ((Dictionary<ResourceKind, double>)snapshot)[ResourceKind.Food] = 0.0;

        Assert.Equal(30.0, stocks.Stock(ResourceKind.Food), 10);
    }

    [Fact]
    public void FromState_ExposesCalibrationState()
    {
        var stocks = ResourceStocks.FromState(food: 42.0, water: 0.0, wood: 50.0);

        Assert.Equal(42.0, stocks.Stock(ResourceKind.Food), 10);
        Assert.True(stocks.IsEmpty(ResourceKind.Water));
    }
}