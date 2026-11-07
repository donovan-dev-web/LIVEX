// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests « échelle temporelle configurable » : neutralité par défaut — à dt = 1, aucune valeur ne bouge ; par
//        quantité de classe A, l'état après 12 ticks à dt = 1/12 (5 s simulées/tick) est équivalent à
//        1 tick à dt = 1 ; contrats additifs (horloge, snapshot, world_initialized) ; validation de
//        simulatedSecondsPerTick et metersPerUnit.

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
/// Tests « échelle temporelle configurable » (spec PRISM « Profil
/// gameplay ») :
/// <list type="number">
/// <item>neutralité par défaut — à <c>dt = 1</c>, aucune valeur ne bouge ;</item>
/// <item>par quantité de classe A, l'état après 12 ticks à
/// <c>dt = 1/12</c> (5 s simulées/tick) est équivalent à
/// 1 tick à <c>dt = 1</c> ;</item>
/// <item>contrats additifs (horloge, snapshot, <c>world_initialized</c>) ;</item>
/// <item>validation de <c>simulatedSecondsPerTick</c> et <c>metersPerUnit</c>.</item>
/// </list>
/// </summary>
public sealed class Adr017TemporalScaleTests
{
    /// <summary>Échelle non standard : 5 s simulées par tick = 1/12 minute simulée.</summary>
    private const double FiveSecondsDt = 5.0 / 60.0;

    /// <summary>
    /// Échelle fine (5 s simulées par tick, 6 TPS) déclarée par surcouche sur le
    /// profil de référence : l'échelle temporelle est un levier de configuration
    /// ordinaire (ADR-017), pas un scénario dédié.
    /// </summary>
    private static SimulationOptions FineScaleOptions()
    {
        SimulationOptions options = SimulationProfiles.Reference();
        options.Simulation.TicksPerSecond = 6;
        options.Simulation.SimulatedSecondsPerTick = 5;
        return options;
    }

    // ---------------------------------------------------------------------
    //  — neutralité par défaut (pin contractuel)
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
    //  — équivalence « 12 ticks à dt = 1/12 == 1 tick à dt = 1 »
    // ---------------------------------------------------------------------

    [Fact]
    public void NeedsRates_AtAFinerScale_AccumulateLikeOneReferenceTick()
    {
        var raw = new NeedsSettings();
        NeedsSettings scaled = TemporalScale.ScaleNeeds(raw, FiveSecondsDt);

        var reference = BodyNeeds.FromState(hunger: 10, thirst: 20, fatigue: 30);
        var fine = BodyNeeds.FromState(hunger: 10, thirst: 20, fatigue: 30);

        reference.Advance(raw);
        for (int i = 0; i < 12; i++)
        {
            fine.Advance(scaled);
        }

        Assert.Equal(reference.Hunger, fine.Hunger, 10);
        Assert.Equal(reference.Thirst, fine.Thirst, 10);
        Assert.Equal(reference.Fatigue, fine.Fatigue, 10);
    }

    [Fact]
    public void HungerTrigger_IsCrossedAroundTwelveHundredFineScaleTicks()
    {
        // Critère d'acceptation n°4 : seuil 50 franchi à ≈ 1 200 ticks
        // (0,5/min ÷ 12 = 0,0417/tick → 50 au tick 1 200).
        NeedsSettings scaled = TemporalScale.ScaleNeeds(new NeedsSettings(), FiveSecondsDt);
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
    public void MemoryDecay_AtAFinerScale_KeepsTheSameSalienceAfterOneSimulatedMinute()
    {
        MemorySettings scaled = TemporalScale.ScaleMemory(new MemorySettings(), FiveSecondsDt);
        var referenceMemory = new Memory(new MemorySettings());
        var fineMemory = new Memory(scaled);
        referenceMemory.Store(MemoryCategory.Observation, "entity-1", "c", 0.9, storedAt: 0);
        fineMemory.Store(MemoryCategory.Observation, "entity-1", "c", 0.9, storedAt: 0);

        double reference = referenceMemory.SalienceOf(referenceMemory.AllEntries[0], currentTick: 1);
        double fine = fineMemory.SalienceOf(fineMemory.AllEntries[0], currentTick: 12);

        Assert.Equal(reference, fine, 12);
    }

    [Fact]
    public void BeliefDecay_AtAFinerScale_MultipliesLikeOneReferenceTick()
    {
        var raw = new BeliefSettings { TimeDecayPerTick = 0.9 };
        BeliefSettings scaled = TemporalScale.ScaleBeliefs(raw, FiveSecondsDt);
        var fact = new Fact("entity-2", "alive", "true");

        var referenceSet = new BeliefSet();
        referenceSet.ApplyEvidence(fact, signal: 0.8, source: "p1", raw, tick: 0);
        referenceSet.Tick(currentTick: 1, raw);

        var fineSet = new BeliefSet();
        fineSet.ApplyEvidence(fact, signal: 0.8, source: "p1", scaled, tick: 0);
        for (ulong tick = 1; tick <= 12; tick++)
        {
            fineSet.Tick(currentTick: tick, scaled);
        }

        Assert.True(referenceSet.TryGet(fact, out Belief? reference));
        Assert.True(fineSet.TryGet(fact, out Belief? fine));
        Assert.Equal(reference!.Confidence, fine!.Confidence, 12);
    }

    [Fact]
    public void BeliefExpiry_AtAFinerScale_IsExpressedInSimulatedMinutes()
    {
        // expiryTicks 100 (minutes simulées) : 100 ticks à dt = 1,
        // 1 200 ticks à dt = 1/12 — même durée de validité simulée.
        BeliefSettings scaled = TemporalScale.ScaleBeliefs(new BeliefSettings(), FiveSecondsDt);

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
    public void TrustDecay_AtAFinerScale_MultipliesLikeOneReferenceTick()
    {
        var raw = new TrustSettings { DecayFactorPerTick = 0.9 };
        TrustSettings scaled = TemporalScale.ScaleTrust(raw, FiveSecondsDt);

        var reference = new Relationships(raw);
        var fine = new Relationships(scaled);
        reference.Reward(7UL, 0.5);
        fine.Reward(7UL, 0.5);
        reference.Tick(); // purge le marqueur « interagi » du cycle d'amorçage
        fine.Tick();

        reference.Tick(); // 1 minute simulée à dt = 1
        for (int i = 0; i < 12; i++)
        {
            fine.Tick(); // 12 ticks × 5 s = 1 minute simulée à dt = 1/12
        }

        Assert.Equal(reference.TrustWith(7UL), fine.TrustWith(7UL), 12);
    }

    [Fact]
    public void ResourceLifecycle_AtAFinerScale_RegeneratesTheSameAmountPerSimulatedMinute()
    {
        var raw = new ResourceSettings();
        ResourceSettings scaled = TemporalScale.ScaleResources(raw, FiveSecondsDt);

        Assert.Equal(raw.Food.RegenerationRate / 12.0, scaled.Food.RegenerationRate, 12);
        Assert.Null(scaled.Food.DegradationTick);

        var reference = new ResourceStocks(raw);
        var fine = new ResourceStocks(scaled);
        reference.ApplyLifecycle(1, raw); // 1 tick = 1 minute simulée à dt = 1
        for (ulong tick = 1; tick <= 12; tick++)
        {
            fine.ApplyLifecycle(tick, scaled); // 12 ticks × 5 s = 1 minute simulée
        }

        Assert.Equal(reference.Stock(ResourceKind.Food), fine.Stock(ResourceKind.Food), 9);
        Assert.Equal(reference.Stock(ResourceKind.Water), fine.Stock(ResourceKind.Water), 9);
    }

    [Fact]
    public void DegradationPeriod_AtAFinerScale_IsExpressedInSimulatedMinutes()
    {
        var raw = new ResourceSettings();
        raw.Wood.DegradationTick = 12;
        ResourceSettings scaled = TemporalScale.ScaleResources(raw, FiveSecondsDt);

        // Période de 12 minutes simulées : 12 ticks à dt = 1, 144 à dt = 1/12.
        Assert.Equal(144, scaled.Wood.DegradationTick);
    }

    [Fact]
    public void SeasonLength_AtAFinerScale_ChangesEveryFourThousandThreeHundredTwentyTicks()
    {
        // Critère d'acceptation n°6 : saison de 360 minutes simulées
        // → 4 320 ticks à 5 s/tick (360 / (1/12)).
        var raw = new SeasonSettings { Enabled = true };
        SeasonSettings scaled = TemporalScale.ScaleSeasons(raw, FiveSecondsDt);

        Assert.Equal(4_320, scaled.SeasonLengthTicks);
        Assert.Equal(raw.At(359), scaled.At(4_319));
        Assert.Equal(World.Season.Summer, scaled.At(4_320));
        Assert.Equal(World.Season.Winter, scaled.At(3 * 4_320));
    }

    [Fact]
    public void ActionEnergyTemporalValues_AtAFinerScale_PayPerEffectiveTick()
    {
        var raw = new ActionSettings();
        ActionSettings scaled = TemporalScale.ScaleActions(raw, FiveSecondsDt);

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
    //  — validation
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
        // exigence « non entier → rejet ».
        Assert.ThrowsAny<JsonException>(() => ConfigLoader.MergeJson(
            ConfigLoader.LoadDefaults(),
            """{ "simulation": { "simulatedSecondsPerTick": 5.5 } }"""));
    }

    // ---------------------------------------------------------------------
    //  — horloge, profil fine et contrats additifs
    // ---------------------------------------------------------------------

    [Fact]
    public void FineClock_ReachesEightySixThousandFourHundredSecondsAtTick17280()
    {
        // Critère d'acceptation n°5 : simulatedTimeSeconds == 86 400 au
        // tick 17 280 (journée complète à R = 30).
        SimulationClock clock = SimulationClock.From(FineScaleOptions().Simulation);

        Assert.Equal(5, clock.SimulatedSecondsPerTick);
        Assert.Equal(30.0, clock.RealTimeRatio(6));
        Assert.Equal(86_400L, clock.ToSimulatedSeconds(17_280));
        Assert.Equal(1_440L, clock.ToSimulatedMinutes(17_280));
    }

    [Fact]
    public void FineScaleOverlay_ReplaysExactlyTheScaledOptions()
    {
        // L'échelle temporelle n'a plus de profil dédié : elle se déclare par
        // surcouche, avec la sémantique de fusion usuelle, et reste valide.
        const string overlay = """{ "simulation": { "ticksPerSecond": 6, "simulatedSecondsPerTick": 5 } }""";
        SimulationOptions replayed = ConfigLoader.MergeJson(ConfigLoader.LoadDefaults(), overlay);

        Assert.Equal(5, replayed.Simulation.SimulatedSecondsPerTick);
        Assert.Equal(6, replayed.Simulation.TicksPerSecond);
        Assert.Equal(500, replayed.Simulation.WorldWidth);
        Assert.Equal(1.0, replayed.World.MetersPerUnit);
        Assert.Empty(SimulationOptionsValidator.Validate(replayed));
    }

    [Fact]
    public void FineScaleRun_ExposesCoherentSimulatedTimeOnTheSnapshot()
    {
        // Critères d'acceptation n°5 et n°6 sur le run réel : secondes au
        // tick 17 280, minutes en plancher entier (contrat ECHOS) et saisons
        // tous les 4 320 ticks.
        SimulationOptions options = FineScaleOptions();
        options.World.Seasons.Enabled = true;
        options.Agents.InitialCount = 0;
        var world = new WorldType(
            new WorldSize(options.Simulation.WorldWidth, options.Simulation.WorldHeight),
            options.Agents.Perception.SpatialCellSize);
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
        // Contrat 0.4.0 : la description du monde porte
        // simulatedSecondsPerTick et metersPerUnit (version 1.0 → 1.1), et le
        // découpage en cases suit la taille de case configurée.
        SimulationOptions options = FineScaleOptions();
        options.Agents.InitialCount = 0;
        var world = new WorldType(
            new WorldSize(options.Simulation.WorldWidth, options.Simulation.WorldHeight),
            options.Agents.Perception.SpatialCellSize);

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
        Assert.Equal(50, description.CellCountX);
        Assert.Equal(50, description.CellCountY);
        Assert.Equal(2_500, description.Cells.Count);
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
