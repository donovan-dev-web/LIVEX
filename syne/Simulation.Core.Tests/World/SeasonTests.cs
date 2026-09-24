using System.Globalization;
using System.Text;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Prng;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Cycle de saisons (SYNE-072, jalon U8) : cycle déterministe de 4 saisons —
/// saison(tick) = (indexInitial + tick / seasonLengthTicks) mod 4, modificateurs
/// de régénération par ressource appliqués en fin de tick (SYNE-070), événement
/// <c>world.season_changed</c>, champs <c>season</c>/<c>seasonIndex</c> du snapshot.
/// Déterminisme total : 0 tirage PRNG (DETERMINISM.md §3). Cycle désactivé par
/// défaut ⇒ trajectoire du scénario de référence inchangée.
/// </summary>
public class SeasonTests
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static SimulationOptions OptionsWithSeasons(int seasonLengthTicks, string initialSeason = "spring",
        double winterFood = 0.8)
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.World.Seasons.Enabled = true;
        options.World.Seasons.SeasonLengthTicks = seasonLengthTicks;
        options.World.Seasons.InitialSeason = initialSeason;
        foreach (SeasonDefinition definition in options.World.Seasons.Cycle)
        {
            if (definition.Name == "winter" && winterFood != 0.8)
            {
                definition.FoodFactor = winterFood;
            }
        }

        return options;
    }

    private static SimulationLoop Build(ulong seed, SimulationOptions options, int count = 2)
    {
        var world = new WorldType(new WorldSize(500, 500), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= (ulong)count; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, options);
    }

    // ------------------------------------------------------------------
    // Cycle déterministe (0 PRNG)
    // ------------------------------------------------------------------

    [Fact]
    public void SeasonAt_WalksTheFourSeasonsInOrder()
    {
        SimulationOptions options = OptionsWithSeasons(seasonLengthTicks: 360);

        Assert.Equal(Season.Spring, options.World.Seasons.At(0));
        Assert.Equal(Season.Spring, options.World.Seasons.At(359));
        Assert.Equal(Season.Summer, options.World.Seasons.At(360));
        Assert.Equal(Season.Summer, options.World.Seasons.At(719));
        Assert.Equal(Season.Autumn, options.World.Seasons.At(720));
        Assert.Equal(Season.Winter, options.World.Seasons.At(1080));
        Assert.Equal(Season.Spring, options.World.Seasons.At(1440));
        Assert.Equal(Season.Spring, options.World.Seasons.At(1479));
    }

    [Fact]
    public void SeasonAt_HonorsInitialSeason()
    {
        SimulationOptions options = OptionsWithSeasons(seasonLengthTicks: 60, initialSeason: "winter");

        Assert.Equal(Season.Winter, options.World.Seasons.At(0));
        Assert.Equal(Season.Winter, options.World.Seasons.At(59));
        Assert.Equal(Season.Spring, options.World.Seasons.At(60));
        Assert.Equal(Season.Summer, options.World.Seasons.At(120));
    }

    [Fact]
    public void SeasonAt_SingleTickLength_ChangesEveryTick()
    {
        SimulationOptions options = OptionsWithSeasons(seasonLengthTicks: 1);

        Assert.Equal(Season.Spring, options.World.Seasons.At(0));
        Assert.Equal(Season.Summer, options.World.Seasons.At(1));
        Assert.Equal(Season.Autumn, options.World.Seasons.At(2));
        Assert.Equal(Season.Winter, options.World.Seasons.At(3));
        Assert.Equal(Season.Spring, options.World.Seasons.At(4));
    }

    // ------------------------------------------------------------------
    // Facteurs de régénération par ressource
    // ------------------------------------------------------------------

    [Fact]
    public void Factors_ResolvePerSeasonAndResource()
    {
        SimulationOptions options = OptionsWithSeasons(seasonLengthTicks: 360);

        SeasonFactors spring = options.World.Seasons.Factors(0);
        Assert.Equal(1.0, spring.For(ResourceKind.Food));
        Assert.Equal(1.0, spring.For(ResourceKind.Water));

        SeasonFactors summer = options.World.Seasons.Factors(360);
        Assert.Equal(1.2, summer.For(ResourceKind.Water));
        Assert.Equal(1.0, summer.For(ResourceKind.Wood));

        SeasonFactors autumn = options.World.Seasons.Factors(720);
        Assert.Equal(1.1, autumn.For(ResourceKind.Food));
        Assert.Equal(1.2, autumn.For(ResourceKind.Wood));
        Assert.Equal(1.0, autumn.For(ResourceKind.Mineral));

        SeasonFactors winter = options.World.Seasons.Factors(1080);
        Assert.Equal(0.8, winter.For(ResourceKind.Food));
        Assert.Equal(0.9, winter.For(ResourceKind.Water));
    }

    [Fact]
    public void Factors_ArePureFunctions_Reproducible()
    {
        SimulationOptions options = OptionsWithSeasons(seasonLengthTicks: 360);
        for (ulong tick = 0; tick <= 1500; tick += 37)
        {
            SeasonFactors first = options.World.Seasons.Factors(tick);
            SeasonFactors second = options.World.Seasons.Factors(tick);
            Assert.Equal(first, second);
        }
    }

    [Fact]
    public void ResourceLifecycle_SeasonFactors_ScaleRegenerationDeterministically()
    {
        var settings = new ResourceSettings
        {
            Food = new ResourceSpec { Initial = 100, RegenerationRate = 10 },
            Water = new ResourceSpec { Initial = 100, RegenerationRate = 10 },
            Wood = new ResourceSpec { Initial = 100, RegenerationRate = 10 },
            Mineral = new ResourceSpec { Initial = 100, RegenerationRate = 10 },
        };
        var stocks = new ResourceStocks(settings);
        var factors = new SeasonFactors(Food: 2.0, Water: 0.5, Wood: 1.0, Mineral: 0.0);

        stocks.ApplyLifecycle(currentTick: 1, settings, factors);

        Assert.Equal(120.0, stocks.Stock(ResourceKind.Food));
        Assert.Equal(105.0, stocks.Stock(ResourceKind.Water));
        Assert.Equal(110.0, stocks.Stock(ResourceKind.Wood));
        Assert.Equal(100.0, stocks.Stock(ResourceKind.Mineral));
    }

    [Fact]
    public void ResourceLifecycle_SeasonFactors_ScaleDegradationAndStayNonNegative()
    {
        var settings = new ResourceSettings
        {
            Food = new ResourceSpec { Initial = 100, RegenerationRate = 10, DegradationTick = 5 },
            Water = new ResourceSpec { Initial = 100, RegenerationRate = 50, DegradationTick = 5 },
            Wood = new ResourceSpec { Initial = 0, RegenerationRate = 0, DegradationTick = 5 },
            Mineral = new ResourceSpec { Initial = 0, RegenerationRate = 0 },
        };
        var stocks = new ResourceStocks(settings);
        var factors = new SeasonFactors(Food: 2.0, Water: 0.5, Wood: 1.0, Mineral: 1.0);

        for (ulong tick = 1; tick <= 5; tick++)
        {
            stocks.ApplyLifecycle(tick, settings, factors);
        }

        Assert.True(stocks.Stock(ResourceKind.Food) >= 0.0);
        Assert.True(stocks.Stock(ResourceKind.Water) >= 0.0);
        Assert.Equal(0.0, stocks.Stock(ResourceKind.Wood));
        Assert.Equal(0.0, stocks.Stock(ResourceKind.Mineral));
    }

    // ------------------------------------------------------------------
    // Boucle : détection et trace du changement de saison
    // ------------------------------------------------------------------

    [Fact]
    public void Loop_SeasonChanges_AreTracedExactlyAtBoundaries()
    {
        SimulationLoop loop = Build(seed: 7, options: OptionsWithSeasons(seasonLengthTicks: 4));
        loop.Run(10);

        Assert.Equal(Season.Autumn, loop.CurrentSeason);
        Assert.Equal(2, loop.LastSeasonChanges.Count);
        Assert.Equal(new SeasonChange(Season.Spring, Season.Summer), loop.LastSeasonChanges[0]);
        Assert.Equal(new SeasonChange(Season.Summer, Season.Autumn), loop.LastSeasonChanges[1]);
    }

    [Fact]
    public void Loop_SeasonChanges_AreDrainedAndNotRepeated()
    {
        SimulationLoop loop = Build(seed: 7, options: OptionsWithSeasons(seasonLengthTicks: 3));
        loop.Run(3);

        Assert.Single(loop.LastSeasonChanges);
        loop.ClearSeasonChanges();
        Assert.Empty(loop.LastSeasonChanges);
    }

    [Fact]
    public void Loop_SeasonsDisabled_ProducesNoSeasonChange()
    {
        SimulationLoop loop = Build(seed: 7, options: ConfigLoader.LoadDefaults());
        loop.Run(200);

        Assert.False(loop.SeasonsEnabled);
        Assert.Empty(loop.LastSeasonChanges);
    }

    [Fact]
    public void Loop_SeasonsEnabled_IsBitForBitDeterministic()
    {
        ulong first = Fnv1a(ResourceLog(Build(seed: 99, options: OptionsWithSeasons(seasonLengthTicks: 37)), ticks: 200));
        ulong second = Fnv1a(ResourceLog(Build(seed: 99, options: OptionsWithSeasons(seasonLengthTicks: 37)), ticks: 200));
        Assert.Equal(first, second);
        Assert.NotEqual(0UL, first);
    }

    private static string ResourceLog(SimulationLoop loop, int ticks)
    {
        var builder = new StringBuilder();
        for (int tick = 1; tick <= ticks; tick++)
        {
            loop.AdvanceOneTick();
            builder.Append(loop.CurrentTick.ToString(CultureInfo.InvariantCulture));
            builder.Append(';');
            builder.Append(Seasons.Name(loop.CurrentSeason));
            foreach (ResourceKind kind in Enum.GetValues<ResourceKind>())
            {
                builder.Append(';');
                builder.Append(loop.Resources.Stock(kind).ToString("R", CultureInfo.InvariantCulture));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static ulong Fnv1a(string text)
    {
        ulong hash = FnvOffsetBasis;
        foreach (byte value in Encoding.UTF8.GetBytes(text))
        {
            hash ^= value;
            hash *= FnvPrime;
        }

        return hash;
    }

    // ------------------------------------------------------------------
    // Observabilité : snapshot + événement
    // ------------------------------------------------------------------

    [Fact]
    public void Snapshot_CarriesSeasonAndSeasonIndex()
    {
        SimulationLoop loop = Build(seed: 3, options: OptionsWithSeasons(seasonLengthTicks: 4));
        loop.Run(5);

        WorldSnapshot snapshot = WorldSnapshot.Capture(loop, seed: 3);

        Assert.Equal("summer", snapshot.Season);
        Assert.Equal(1, snapshot.SeasonIndex);
    }

    [Fact]
    public void Serializer_EmitsSeasonFields()
    {
        SimulationLoop loop = Build(seed: 3, options: OptionsWithSeasons(seasonLengthTicks: 4));
        loop.Run(10);

        var message = ObservabilitySerializer.SnapshotMessage(WorldSnapshot.Capture(loop, seed: 3));
        string season = message["season"]!.GetValue<string>();

        Assert.Equal("autumn", season);
        Assert.Equal(2, message["seasonIndex"]!.GetValue<int>());
    }

    [Fact]
    public void SeasonChangedEvent_IsTypedWithPreviousAndCurrent()
    {
        SimulationLoop loop = Build(seed: 3, options: OptionsWithSeasons(seasonLengthTicks: 4));
        loop.Run(5);

        var change = Assert.Single(loop.LastSeasonChanges);
        ExternalEvent external = EventSensor.SeasonChanged(loop.CurrentTick, change);

        Assert.Equal(ObservabilityContract.SeasonChanged, external.Type);
        Assert.Equal(5UL, external.Tick);
        Assert.Equal("summer", (string?)external.TargetId);
        Assert.Equal("spring", (string?)external.Value!["previous"]);
        Assert.Equal("summer", (string?)external.Value!["current"]);
    }

    // ------------------------------------------------------------------
    // Validation (branches du cycle)
    // ------------------------------------------------------------------

    [Fact]
    public void Validator_AcceptsDefaultSeasonBlock()
    {
        IReadOnlyList<string> errors = SimulationOptionsValidator.Validate(ConfigLoader.LoadDefaults());
        Assert.DoesNotContain(errors, error => error.StartsWith("world.seasons", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_RejectsInvalidSeasonSettings()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();

        options.World.Seasons.SeasonLengthTicks = 0;
        Assert.Contains(SimulationOptionsValidator.Validate(options),
            error => error.Contains("seasonLengthTicks", StringComparison.Ordinal));

        options = ConfigLoader.LoadDefaults();
        options.World.Seasons.InitialSeason = "monsoon";
        Assert.Contains(SimulationOptionsValidator.Validate(options),
            error => error.Contains("initialSeason", StringComparison.Ordinal));

        options = ConfigLoader.LoadDefaults();
        options.World.Seasons.Cycle.Add(new SeasonDefinition { Name = "summer" });
        Assert.Contains(SimulationOptionsValidator.Validate(options),
            error => error.Contains("définit deux fois", StringComparison.Ordinal));

        options = ConfigLoader.LoadDefaults();
        options.World.Seasons.Cycle.RemoveAt(0);
        Assert.Contains(SimulationOptionsValidator.Validate(options),
            error => error.Contains("manquante", StringComparison.Ordinal));

        options = ConfigLoader.LoadDefaults();
        options.World.Seasons.Cycle[0].FoodFactor = -0.5;
        Assert.Contains(SimulationOptionsValidator.Validate(options),
            error => error.Contains("facteurs", StringComparison.Ordinal));
    }
}