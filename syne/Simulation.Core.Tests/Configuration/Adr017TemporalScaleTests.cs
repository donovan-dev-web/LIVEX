using System.Text.Json;
using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Core.Tests.Configuration;

/// <summary>
/// Tests ADR-017 « échelle temporelle configurable » (spec PRISM « Profil
/// gameplay » §4.5) :
/// <list type="number">
/// <item>neutralité par défaut — à <c>dt = 1</c>, aucune valeur ne bouge ;</item>
/// <item>par quantité de classe A, l'état après 12 ticks à
/// <c>dt = 1/12</c> (5 s simulées/tick, profil <c>prism</c>) est équivalent à
/// 1 tick à <c>dt = 1</c> ;</item>
/// <item>contrats additifs (horloge, snapshot, <c>world_initialized</c>) ;</item>
/// <item>validation de <c>simulatedSecondsPerTick</c> et <c>metersPerUnit</c>.</item>
/// </list>
/// </summary>
public sealed class Adr017TemporalScaleTests
{
    /// <summary>Profil prism : 5 s simulées par tick = 1/12 minute simulée.</summary>
    private const double PrismDt = 5.0 / 60.0;

    // ---------------------------------------------------------------------
    // §4.5-1 — neutralité par défaut (pin contractuel ADR-016)
    // ---------------------------------------------------------------------

    [Fact]
    public void UnitStep_ReturnsTheSameInstances_WithoutAnyArithmetic()
    {
        var needs = new NeedsSettings();
        var memory = new MemorySettings();
        var beliefs = new BeliefSettings();
        var trust = new TrustSettings();
        var actions = new ActionSettings();
        var resources = new ResourceSettings();
        var seasons = new SeasonSettings();

        Assert.Same(needs, TemporalScale.ScaleNeeds(needs, 1.0));
        Assert.Same(memory, TemporalScale.ScaleMemory(memory, 1.0));
        Assert.Same(beliefs, TemporalScale.ScaleBeliefs(beliefs, 1.0));
        Assert.Same(trust, TemporalScale.ScaleTrust(trust, 1.0));
        Assert.Same(actions, TemporalScale.ScaleActions(actions, 1.0));
        Assert.Same(resources, TemporalScale.ScaleResources(resources, 1.0));
        Assert.Same(seasons, TemporalScale.ScaleSeasons(seasons, 1.0));
    }

    [Fact]
    public void DefaultClock_MatchesTheHistoricOneMinuteTick()
    {
        SimulationClock clock = SimulationClock.From(new SimulationSettings());

        Assert.Equal(60, clock.SimulatedSecondsPerTick);
        Assert.Equal(1.0, clock.SimulatedMinutesPerTick);
        Assert.Equal(17_280L, clock.ToSimulatedMinutes(17_280));
        Assert.Equal(1_036_800L, clock.ToSimulatedSeconds(17_280));
    }

    // ---------------------------------------------------------------------
    // §4.5-2 — équivalence « 12 ticks à dt = 1/12 == 1 tick à dt = 1 »
    // ---------------------------------------------------------------------

    [Fact]
    public void NeedsRates_AtPrismScale_AccumulateLikeOneReferenceTick()
    {
        var raw = new NeedsSettings();
        NeedsSettings scaled = TemporalScale.ScaleNeeds(raw, PrismDt);

        var reference = BodyNeeds.FromState(hunger: 10, thirst: 20, fatigue: 30);
        var prism = BodyNeeds.FromState(hunger: 10, thirst: 20, fatigue: 30);

        reference.Advance(raw);
        for (int i = 0; i < 12; i++)
        {
            prism.Advance(scaled);
        }

        Assert.Equal(reference.Hunger, prism.Hunger, 10);
        Assert.Equal(reference.Thirst, prism.Thirst, 10);
        Assert.Equal(reference.Fatigue, prism.Fatigue, 10);
    }

    [Fact]
    public void HungerTrigger_IsCrossedAroundTwelveHundredPrismTicks()
    {
        // Critère d'acceptation n°4 : seuil 50 franchi à ≈ 1 200 ticks
        // (0,5/min ÷ 12 = 0,0417/tick → 50 au tick 1 200).
        NeedsSettings scaled = TemporalScale.ScaleNeeds(new NeedsSettings(), PrismDt);
        var needs = BodyNeeds.FromState();

        int crossedAt = 0;
        double hungerAtCrossing = 0.0;
        for (int tick = 1; tick <= 1_300 && crossedAt == 0; tick++)
        {
            needs.Advance(scaled);
            if (needs.IsTriggered(DesireKind.Eat, new NeedsSettings()))
            {
                crossedAt = tick;
                hungerAtCrossing = needs.Hunger;
            }
        }

        // 0,5/min ÷ 12 ≈ 0,04167/tick : le seuil 50 est franchi à ≈ 1 200 ticks
        // (ε d'accumulation flottante admis autour de la valeur nominale).
        Assert.InRange(crossedAt, 1_195, 1_205);
        Assert.InRange(hungerAtCrossing, 49.95, 50.05);
    }

    [Fact]
    public void MemoryDecay_AtPrismScale_KeepsTheSameSalienceAfterOneSimulatedMinute()
    {
        MemorySettings scaled = TemporalScale.ScaleMemory(new MemorySettings(), PrismDt);
        var referenceMemory = new Memory(new MemorySettings());
        var prismMemory = new Memory(scaled);
        referenceMemory.Store(MemoryCategory.Observation, "entity-1", "c", 0.9, storedAt: 0);
        prismMemory.Store(MemoryCategory.Observation, "entity-1", "c", 0.9, storedAt: 0);

        double reference = referenceMemory.SalienceOf(referenceMemory.AllEntries[0], currentTick: 1);
        double prism = prismMemory.SalienceOf(prismMemory.AllEntries[0], currentTick: 12);

        Assert.Equal(reference, prism, 12);
    }

    [Fact]
    public void BeliefDecay_AtPrismScale_MultipliesLikeOneReferenceTick()
    {
        var raw = new BeliefSettings { TimeDecayPerTick = 0.9 };
        BeliefSettings scaled = TemporalScale.ScaleBeliefs(raw, PrismDt);
        var fact = new Fact("entity-2", "alive", "true");

        var referenceSet = new BeliefSet();
        referenceSet.ApplyEvidence(fact, signal: 0.8, source: "p1", raw, tick: 0);
        referenceSet.Tick(currentTick: 1, raw);

        var prismSet = new BeliefSet();
        prismSet.ApplyEvidence(fact, signal: 0.8, source: "p1", scaled, tick: 0);
        for (ulong tick = 1; tick <= 12; tick++)
        {
            prismSet.Tick(currentTick: tick, scaled);
        }

        Assert.True(referenceSet.TryGet(fact, out Belief? reference));
        Assert.True(prismSet.TryGet(fact, out Belief? prism));
        Assert.Equal(reference!.Confidence, prism!.Confidence, 12);
    }

    [Fact]
    public void BeliefExpiry_AtPrismScale_IsExpressedInSimulatedMinutes()
    {
        // expiryTicks 100 (minutes simulées) : 100 ticks à dt = 1,
        // 1 200 ticks à dt = 1/12 — même durée de validité simulée.
        BeliefSettings scaled = TemporalScale.ScaleBeliefs(new BeliefSettings(), PrismDt);

        Assert.Equal(1_200UL, scaled.ExpiryTicks);

        var fact = new Fact("entity-2", "alive", "true");
        var set = new BeliefSet();
        set.ApplyEvidence(fact, signal: 0.9, source: "p1", scaled, tick: 0);

        Assert.True(set.TryGet(fact, out Belief? belief));
        Assert.Equal(1_200UL, belief!.ExpiryTick);
        Assert.False(belief.IsExpired(1_199));
        Assert.True(belief.IsExpired(1_200));
    }

    [Fact]
    public void TrustDecay_AtPrismScale_MultipliesLikeOneReferenceTick()
    {
        var raw = new TrustSettings { DecayFactorPerTick = 0.9 };
        TrustSettings scaled = TemporalScale.ScaleTrust(raw, PrismDt);

        var reference = new Relationships(raw);
        var prism = new Relationships(scaled);
        reference.Reward(7UL, 0.5);
        prism.Reward(7UL, 0.5);
        reference.Tick(); // purge le marqueur « interagi » du cycle d'amorçage
        prism.Tick();

        reference.Tick(); // 1 minute simulée à dt = 1
        for (int i = 0; i < 12; i++)
        {
            prism.Tick(); // 12 ticks × 5 s = 1 minute simulée à dt = 1/12
        }

        Assert.Equal(reference.TrustWith(7UL), prism.TrustWith(7UL), 12);
    }

    [Fact]
    public void ResourceLifecycle_AtPrismScale_RegeneratesTheSameAmountPerSimulatedMinute()
    {
        var raw = new ResourceSettings();
        ResourceSettings scaled = TemporalScale.ScaleResources(raw, PrismDt);

        Assert.Equal(raw.Food.RegenerationRate / 12.0, scaled.Food.RegenerationRate, 12);
        Assert.Null(scaled.Food.DegradationTick);

        var reference = new ResourceStocks(raw);
        var prism = new ResourceStocks(scaled);
        reference.ApplyLifecycle(1, raw); // 1 tick = 1 minute simulée à dt = 1
        for (ulong tick = 1; tick <= 12; tick++)
        {
            prism.ApplyLifecycle(tick, scaled); // 12 ticks × 5 s = 1 minute simulée
        }

        Assert.Equal(reference.Stock(ResourceKind.Food), prism.Stock(ResourceKind.Food), 9);
        Assert.Equal(reference.Stock(ResourceKind.Water), prism.Stock(ResourceKind.Water), 9);
    }

    [Fact]
    public void DegradationPeriod_AtPrismScale_IsExpressedInSimulatedMinutes()
    {
        var raw = new ResourceSettings();
        raw.Wood.DegradationTick = 12;
        ResourceSettings scaled = TemporalScale.ScaleResources(raw, PrismDt);

        // Période de 12 minutes simulées : 12 ticks à dt = 1, 144 à dt = 1/12.
        Assert.Equal(144, scaled.Wood.DegradationTick);
    }

    [Fact]
    public void SeasonLength_AtPrismScale_ChangesEveryFourThousandThreeHundredTwentyTicks()
    {
        // Critère d'acceptation n°6 : saison de 360 minutes simulées
        // → 4 320 ticks à 5 s/tick (360 / (1/12)).
        var raw = new SeasonSettings { Enabled = true };
        SeasonSettings scaled = TemporalScale.ScaleSeasons(raw, PrismDt);

        Assert.Equal(4_320, scaled.SeasonLengthTicks);
        Assert.Equal(raw.At(359), scaled.At(4_319));
        Assert.Equal(World.Season.Summer, scaled.At(4_320));
        Assert.Equal(World.Season.Winter, scaled.At(3 * 4_320));
    }

    [Fact]
    public void ActionEnergyTemporalValues_AtPrismScale_PayPerEffectiveTick()
    {
        var raw = new ActionSettings();
        ActionSettings scaled = TemporalScale.ScaleActions(raw, PrismDt);

        Assert.Equal(raw.MoveEnergyCost / 12.0, scaled.MoveEnergyCost, 12);
        Assert.Equal(raw.RestEnergyGain / 12.0, scaled.RestEnergyGain, 12);
        Assert.Equal(raw.RestFatigueRecovery / 12.0, scaled.RestFatigueRecovery, 12);

        var catalog = new ActionCatalog(scaled);
        Assert.Equal(raw.MoveEnergyCost / 12.0, catalog[DesireKind.SeekFood].EnergyCost, 12);

        // Classe C inchangée : les effets explicites du catalogue (Eat/Drink)
        // ne sont pas multipliés par dt.
        Assert.Equal(raw.Catalog.Entries["eat"].EnergyCost, catalog[DesireKind.Eat].EnergyCost);
        Assert.Equal(raw.Catalog.Entries["eat"].HungerRecovery, catalog[DesireKind.Eat].HungerRecovery);
    }

    // ---------------------------------------------------------------------
    // §4.5-4 — validation
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3601)]
    public void SimulatedSecondsPerTick_OutOfRange_IsRejected(int value)
    {
        var options = new SimulationOptions();
        options.Simulation.SimulatedSecondsPerTick = value;

        IReadOnlyList<string> errors = SimulationOptionsValidator.Validate(options);

        Assert.Contains(errors, error => error.Contains("simulatedSecondsPerTick", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(60)]
    [InlineData(3600)]
    public void SimulatedSecondsPerTick_InRange_IsAccepted(int value)
    {
        var options = new SimulationOptions();
        options.Simulation.SimulatedSecondsPerTick = value;

        Assert.Empty(SimulationOptionsValidator.Validate(options));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void MetersPerUnit_NonPositiveOrNonFinite_IsRejected(double value)
    {
        var options = new SimulationOptions();
        options.World.MetersPerUnit = value;

        IReadOnlyList<string> errors = SimulationOptionsValidator.Validate(options);

        Assert.Contains(errors, error => error.Contains("metersPerUnit", StringComparison.Ordinal));
    }

    [Fact]
    public void NonIntegerSimulatedSecondsPerTick_IsRejectedByTheJsonLoader()
    {
        // Le type entier du contrat rejette « 5.5 » dès la désérialisation —
        // exigence §4.5-4 « non entier → rejet ».
        Assert.ThrowsAny<JsonException>(() => ConfigLoader.MergeJson(
            ConfigLoader.LoadDefaults(),
            """{ "simulation": { "simulatedSecondsPerTick": 5.5 } }"""));
    }

    // ---------------------------------------------------------------------
    // §4.5-3 — horloge, profil prism et contrats additifs
    // ---------------------------------------------------------------------

    [Fact]
    public void PrismClock_ReachesEightySixThousandFourHundredSecondsAtTick17280()
    {
        // Critère d'acceptation n°5 : simulatedTimeSeconds == 86 400 au
        // tick 17 280 (journée complète à R = 30).
        SimulationClock clock = SimulationClock.From(SimulationProfiles.Prism().Simulation);

        Assert.Equal(5, clock.SimulatedSecondsPerTick);
        Assert.Equal(30.0, clock.RealTimeRatio(6));
        Assert.Equal(86_400L, clock.ToSimulatedSeconds(17_280));
        Assert.Equal(1_440L, clock.ToSimulatedMinutes(17_280));
    }

    [Fact]
    public void PrismProfile_IsCompleteValidAndNeutralForTheEngine()
    {
        SimulationOptions prism = SimulationProfiles.Prism();

        Assert.Equal(2240, prism.Simulation.WorldWidth);
        Assert.Equal(2240, prism.Simulation.WorldHeight);
        Assert.Equal(32, prism.Simulation.WorldCellSize);
        Assert.Equal(6, prism.Simulation.TicksPerSecond);
        Assert.Equal(5, prism.Simulation.SimulatedSecondsPerTick);
        Assert.Equal(1.0, prism.World.MetersPerUnit);
        Assert.Empty(SimulationOptionsValidator.Validate(prism));

        // Le profil est complet : rejoué comme surcouche sur les défauts, il
        // redonne exactement le même objet (§4.4).
        SimulationOptions replayed = ConfigLoader.MergeJson(ConfigLoader.LoadDefaults(), SimulationProfiles.PrismJson());
        Assert.Equal(5, replayed.Simulation.SimulatedSecondsPerTick);
        Assert.Equal(2240, replayed.Simulation.WorldWidth);
        Assert.Equal(6, replayed.Simulation.TicksPerSecond);
    }

    [Fact]
    public void PrismRun_ExposesCoherentSimulatedTimeOnTheSnapshot()
    {
        // Critères d'acceptation n°5 et n°6 sur le run réel : secondes au
        // tick 17 280, minutes en plancher entier (contrat ECHOS) et saisons
        // tous les 4 320 ticks.
        SimulationOptions options = SimulationProfiles.Prism();
        options.World.Seasons.Enabled = true;
        options.Agents.InitialCount = 0;
        var world = new WorldType(new WorldSize(2240, 2240), options.Agents.Perception.SpatialCellSize);
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(12_345), options);

        loop.Run(17_280);

        WorldSnapshot snapshot = WorldSnapshot.Capture(loop, seed: 12_345);
        Assert.Equal(86_400L, snapshot.SimulatedTimeSeconds);
        Assert.Equal(1_440L, snapshot.SimulatedTimeMinutes);
        Assert.Equal(4, loop.LastSeasonChanges.Count);

        var message = ObservabilitySerializer.SnapshotMessage(snapshot).ToJsonString();
        Assert.Contains("\"simulatedTimeSeconds\":86400", message, StringComparison.Ordinal);
        Assert.Contains("\"simulatedTimeMinutes\":1440", message, StringComparison.Ordinal);
    }

    [Fact]
    public void WorldInitialized_CarriesTheTemporalAndSpatialScale()
    {
        // Contrat 0.4.0 (ADR-017 §4.3) : la description du monde porte
        // simulatedSecondsPerTick et metersPerUnit (version 1.0 → 1.1), et le
        // monde prism est bien découpé en 70 × 70 cases de 32 (4 900 cases).
        SimulationOptions options = SimulationProfiles.Prism();
        options.Agents.InitialCount = 0;
        var world = new WorldType(new WorldSize(2240, 2240), options.Agents.Perception.SpatialCellSize);

        Simulation.Core.World.WorldDescription description = WorldDescriptionBuilder.Build(
            world,
            seed: 7,
            cellSize: options.Simulation.WorldCellSize,
            ticksPerSecond: options.Simulation.TicksPerSecond,
            simulatedSecondsPerTick: options.Simulation.SimulatedSecondsPerTick,
            metersPerUnit: options.World.MetersPerUnit);

        Assert.Equal("1.1", description.Version);
        Assert.Equal(5, description.SimulatedSecondsPerTick);
        Assert.Equal(1.0, description.MetersPerUnit);
        Assert.Equal(70, description.CellCountX);
        Assert.Equal(70, description.CellCountY);
        Assert.Equal(4_900, description.Cells.Count);
    }

    [Fact]
    public void ReferenceRun_KeepsTheHistoricOneMinuteContract()
    {
        // Non-régression du profil reference : 1 tick = 1 minute simulée,
        // contrat snapshot inchangé (minutes == tick).
        SimulationOptions options = SimulationProfiles.Reference();
        options.Agents.InitialCount = 0;
        var world = new WorldType(new WorldSize(500, 500), options.Agents.Perception.SpatialCellSize);
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(42_4242UL), options);

        loop.Run(120);

        WorldSnapshot snapshot = WorldSnapshot.Capture(loop, seed: 42_4242UL);
        Assert.Equal(120L, snapshot.SimulatedTimeMinutes);
        Assert.Equal(7_200L, snapshot.SimulatedTimeSeconds);
        Assert.Equal(60, loop.Clock.SimulatedSecondsPerTick);
    }
}
