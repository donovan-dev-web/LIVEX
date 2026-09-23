using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests.Configuration;

/// <summary>
/// Couverture des branches de validation restantes (SYNE-100, jalon ph10) :
/// chaque garde-fou de CONFIGURATION.md §6 qui n'était pas encore exercé par
/// les tests ph1/ph5/ph6 (rangées de ``SimulationOptionsValidator`` laissées
/// non exécutées dans la couverture) est déclenchée ici, un test par garde.
/// </summary>
public class Ph10ValidationCoverageTests
{
    [Fact]
    public void InvalidWorldHeight_IsRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Simulation.WorldHeight = 0;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("worldHeight"));
    }

    [Fact]
    public void InvalidAutoSaveInterval_IsRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Simulation.AutoSaveEveryNTicks = 0;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("autoSaveEveryNTicks"));
    }

    [Fact]
    public void NegativeMaxBackups_IsRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Simulation.MaxBackups = -1;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("maxBackups"));
    }

    [Fact]
    public void InvalidTicksPerSecond_IsRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Simulation.TicksPerSecond = 0;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("ticksPerSecond"));
    }

    [Fact]
    public void NegativeInitialCount_IsRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.InitialCount = -1;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("initialCount"));
    }

    [Fact]
    public void NonPositivePerceptionRadius_IsRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Perception.Radius = 0;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("perception.radius"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void ConfidenceFalloffOutOfRange_IsRejected(double value)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Perception.ConfidenceFalloff = value;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("confidenceFalloff"));
    }

    [Fact]
    public void NegativeCommunicationCaps_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Communication.MaxSendsPerTick = -1;
        options.Communication.MaxReceivesPerTick = -1;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("maxSendsPerTick"));
        Assert.Contains(errors, e => e.Contains("maxReceivesPerTick"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(71)]
    public void TransmissionRangeOutOfRange_IsRejected(int range)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Communication.TransmissionRange = range;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("transmissionRange"));
    }

    [Fact]
    public void NegativeMaxHops_IsRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Communication.MaxHops = -1;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("maxHops"));
    }

    [Fact]
    public void NegativeSendEnergyCosts_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Communication.SendEnergyCost = -0.1;
        options.Communication.SendEnergyPayloadFactor = -1.0;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("sendEnergyCost"));
    }

    [Fact]
    public void NegativeReceiveEnergyCosts_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Communication.ReceiveEnergyCost = -0.1;
        options.Communication.ReceiveEnergyPayloadFactor = -1.0;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("receiveEnergyCost"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void IncomprehensionRateOutOfRange_IsRejected(double value)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Communication.IncomprehensionRate = value;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("incomprehensionRate"));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.1)]
    public void HopConfidenceDecayOutOfRange_IsRejected(double value)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Communication.HopConfidenceDecay = value;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("hopConfidenceDecay"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void RecallThresholdOutOfRange_IsRejected(double value)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Memory.RecallThreshold = value;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("recallThreshold"));
    }

    [Fact]
    public void NegativeNeedsRates_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Needs.HungerRate = -1.0;
        options.Agents.Needs.ThirstRate = -0.5;
        options.Agents.Needs.FatigueRate = -0.2;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("needs.*Rate"));
    }

    [Fact]
    public void NeedsTriggerThresholdsOutOfRange_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Needs.HungerTriggerThreshold = -1.0;
        options.Agents.Needs.ThirstTriggerThreshold = 101.0;
        options.Agents.Needs.FatigueTriggerThreshold = 200.0;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("hungerTriggerThreshold"));
        Assert.Contains(errors, e => e.Contains("thirstTriggerThreshold"));
        Assert.Contains(errors, e => e.Contains("fatigueTriggerThreshold"));
    }

    [Fact]
    public void NegativeNeedsDriftRates_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Needs.SafetyDriftRate = -1.0;
        options.Agents.Needs.SocialDriftRate = -1.0;
        options.Agents.Needs.CuriosityDriftRate = -1.0;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("needs.*DriftRate"));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.1)]
    public void MaxChangePerSnapOutOfRange_IsRejected(double value)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Beliefs.MaxChangePerSnap = value;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("maxChangePerSnap"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void ExpiredCapOutOfRange_IsRejected(double value)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Beliefs.ExpiredCap = value;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("expiredCap"));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.1)]
    public void TimeDecayPerTickOutOfRange_IsRejected(double value)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Beliefs.TimeDecayPerTick = value;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("timeDecayPerTick"));
    }

    [Fact]
    public void NonPositiveCollectiveAlignBonus_IsRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Deliberation.CollectiveAlignBonus = 0.0;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            e => e.Contains("collectiveAlignBonus"));
    }

    [Fact]
    public void InvalidCatalogEntries_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Catalog.Entries["eat"].EnergyCost = -1.0;
        options.Agents.Actions.Catalog.Entries["eat"].HungerRecovery = -1.0;
        options.Agents.Actions.Catalog.Entries["eat"].ReserveConsumption = 0.0;
        options.Agents.Actions.Catalog.Entries["drink"].ThirstRecovery = -1.0;
        options.Agents.Actions.Catalog.Entries["rest"].EnergyRecovery = -1.0;
        options.Agents.Actions.Catalog.Entries["rest"].FatigueRecovery = -1.0;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("catalog.eat.energyCost"));
        Assert.Contains(errors, e => e.Contains("catalog.eat.hungerRecovery"));
        Assert.Contains(errors, e => e.Contains("catalog.eat.reserveConsumption"));
        Assert.Contains(errors, e => e.Contains("catalog.drink.thirstRecovery"));
        Assert.Contains(errors, e => e.Contains("catalog.rest.energyRecovery"));
        Assert.Contains(errors, e => e.Contains("catalog.rest.fatigueRecovery"));
    }

    [Fact]
    public void InvalidMergeSettings_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Reproduction.MergeRange = 0.0;
        options.Reproduction.MergeMinimumEnergy = 101.0;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("mergeRange"));
        Assert.Contains(errors, e => e.Contains("mergeMinimumEnergy"));
    }

    [Fact]
    public void InvalidLifeSettings_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Life.DeathEnergyThreshold = 100.0;
        options.Agents.Life.EnergyExhaustionCause = "   ";

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("deathEnergyThreshold"));
        Assert.Contains(errors, e => e.Contains("energyExhaustionCause"));
    }

    [Fact]
    public void InvalidPathfindingSettings_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Pathfinding.CellSize = 0.0;
        options.Agents.Pathfinding.MaxExpansionCells = 0;
        options.Agents.Pathfinding.CacheCapacity = -1;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("cellSize"));
        Assert.Contains(errors, e => e.Contains("maxExpansionCells"));
        Assert.Contains(errors, e => e.Contains("cacheCapacity"));
    }

    [Fact]
    public void NullOptions_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => SimulationOptionsValidator.Validate(null!));
    }
}
