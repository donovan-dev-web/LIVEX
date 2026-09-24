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

    [Fact]
    public void Mineral_IsPartOfTheFourReservedStocks()
    {
        // SYNE-070 : les minéraux intègrent le cycle des ressources (décision n°2.4 —
        // types nourriture, eau, bois, minéraux). Défaut : 0.
        var stocks = ResourceStocks.FromState(food: 30.0, water: 1000.0, wood: 50.0, mineral: 20.0);

        Assert.Equal(20.0, stocks.Stock(ResourceKind.Mineral), 10);
        Assert.Single(stocks.Snapshot(), kv => kv.Key == ResourceKind.Mineral);
    }

    [Fact]
    public void ApplyLifecycle_RegeneratesPerTickWithinBounds()
    {
        // SYNE-070 : la régénération ajoute regenerationRate par tick (Water +5, Wood +0.1),
        // les stocks restent bornés à zéro. 0 tirage PRNG (DETERMINISM.md §3).
        var settings = new ResourceSettings();
        var stocks = new ResourceStocks(settings);

        for (ulong tick = 1; tick <= 10; tick++)
        {
            stocks.ApplyLifecycle(tick, settings);
        }

        Assert.Equal(1000.0 + 10 * 5.0, stocks.Stock(ResourceKind.Water), 10);
        Assert.Equal(50.0 + 10 * 0.1, stocks.Stock(ResourceKind.Wood), 8);
        Assert.Equal(100.0, stocks.Stock(ResourceKind.Food), 10);
        Assert.Equal(0.0, stocks.Stock(ResourceKind.Mineral), 10);
    }

    [Fact]
    public void ApplyLifecycle_DegradesAtDegradationTickPeriod()
    {
        // SYNE-070 : à chaque période degradationTick, la réserve perd la régénération
        // cumulée de la période (regenerationRate × degradationTick) — mécanisme activable,
        // inerte sans taux de régénération. Ici Water : degradationTick 10 → perte de
        // 5 × 10 = 50 tous les 10 ticks — la réserve repasse à son niveau de période.
        var settings = new ResourceSettings
        {
            Water = new ResourceSpec { Initial = 1000, RegenerationRate = 5, DegradationTick = 10 },
        };
        var stocks = new ResourceStocks(settings);

        for (ulong tick = 1; tick <= 10; tick++)
        {
            stocks.ApplyLifecycle(tick, settings);
        }

        // Fin de période n°1 (ticks 1-10) : régénération +50 puis dégradation −50 → net 0.
        Assert.Equal(1000.0, stocks.Stock(ResourceKind.Water), 10);

        for (ulong tick = 11; tick <= 20; tick++)
        {
            stocks.ApplyLifecycle(tick, settings);
        }

        // Fin de période n°2 (ticks 11-20) : même annulation → toujours au niveau initial.
        Assert.Equal(1000.0, stocks.Stock(ResourceKind.Water), 10);

        // Sans taux de régénération, la dégradation est nulle (Food) — mécanisme inerte.
        for (ulong tick = 21; tick <= 100; tick++)
        {
            stocks.ApplyLifecycle(tick, settings);
        }

        Assert.Equal(100.0, stocks.Stock(ResourceKind.Food), 10);
    }

    [Fact]
    public void ApplyLifecycle_NeverGoesNegative()
    {
        var settings = new ResourceSettings
        {
            Food = new ResourceSpec { Initial = 0, RegenerationRate = 0, DegradationTick = 1 },
        };
        var stocks = new ResourceStocks(settings);

        stocks.ApplyLifecycle(1, settings);
        Assert.Equal(0.0, stocks.Stock(ResourceKind.Food), 10);
    }

    [Fact]
    public void ApplyLifecycle_IsDeterministicAcrossRuns()
    {
        // Déterminisme : le cycle est purement additif/subtractif, 0 tirage PRNG —
        // deux répliques (même config, même séquence de ticks) aboutissent aux mêmes niveaux.
        var settings = new ResourceSettings
        {
            Water = new ResourceSpec { Initial = 1000, RegenerationRate = 5, DegradationTick = 25 },
            Wood = new ResourceSpec { Initial = 50, RegenerationRate = 0.1 },
        };
        var first = new ResourceStocks(settings);
        var second = new ResourceStocks(settings);

        for (ulong tick = 1; tick <= 250; tick++)
        {
            first.ApplyLifecycle(tick, settings);
            second.ApplyLifecycle(tick, settings);
        }

        Assert.Equal(first.Snapshot(), second.Snapshot());
    }
}