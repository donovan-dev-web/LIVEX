namespace Simulation.Core.Configuration;

/// <summary>
/// Validation des plages à l'import (CONFIGURATION.md §6).
/// Une configuration invalide stoppe avec un message d'erreur explicite.
/// </summary>
public static class SimulationOptionsValidator
{
    public static IReadOnlyList<string> Validate(SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();

        if (options.Simulation.WorldWidth <= 0)
        {
            errors.Add($"simulation.worldWidth doit être &gt; 0 (reçu : {options.Simulation.WorldWidth}).");
        }

        if (options.Simulation.WorldHeight <= 0)
        {
            errors.Add($"simulation.worldHeight doit être &gt; 0 (reçu : {options.Simulation.WorldHeight}).");
        }

        if (options.Simulation.MaxTicks <= 0)
        {
            errors.Add($"simulation.maxTicks doit être &gt; 0 (reçu : {options.Simulation.MaxTicks}).");
        }

        if (options.Simulation.AutoSaveEveryNTicks <= 0)
        {
            errors.Add($"simulation.autoSaveEveryNTicks doit être &gt; 0 (reçu : {options.Simulation.AutoSaveEveryNTicks}).");
        }

        if (options.Simulation.MaxBackups < 0)
        {
            errors.Add($"simulation.maxBackups doit être &gt;= 0 (reçu : {options.Simulation.MaxBackups}).");
        }

        if (options.Simulation.TicksPerSecond <= 0)
        {
            errors.Add($"simulation.ticksPerSecond doit être &gt; 0 (reçu : {options.Simulation.TicksPerSecond}).");
        }

        if (options.Agents.InitialCount < 0)
        {
            errors.Add($"agents.initialCount doit être &gt;= 0 (reçu : {options.Agents.InitialCount}).");
        }

        foreach (var (key, value) in options.Agents.Traits)
        {
            if (value < 0 || value > 2)
            {
                errors.Add($"agents.traits.{key} doit être dans [0, 2] (reçu : {value}).");
            }
        }

        if (options.Agents.Perception.Radius <= 0)
        {
            errors.Add($"agents.perception.radius doit être &gt; 0 (reçu : {options.Agents.Perception.Radius}).");
        }
        else if (options.Agents.Perception.Radius < 20 || options.Agents.Perception.Radius > 70)
        {
            errors.Add($"agents.perception.radius doit être dans [20, 70] (décision n°6 — reçu : {options.Agents.Perception.Radius}).");
        }

        if (options.Agents.Perception.ConfidenceFalloff is < 0.0 or > 1.0)
        {
            errors.Add($"agents.perception.confidenceFalloff doit être dans [0, 1] (reçu : {options.Agents.Perception.ConfidenceFalloff}).");
        }

        if (options.Agents.Perception.RotationInterval is < 1 or > 64)
        {
            errors.Add($"agents.perception.rotationInterval doit être dans [1, 64] (reçu : {options.Agents.Perception.RotationInterval}).");
        }

        if (options.Random.Engine != "xoshiro256**")
        {
            errors.Add($"random.engine doit être \"xoshiro256**\" (reçu : \"{options.Random.Engine}\").");
        }

        if (options.Communication.MaxSendsPerTick < 0)
        {
            errors.Add($"communication.maxSendsPerTick doit être &gt;= 0 (reçu : {options.Communication.MaxSendsPerTick}).");
        }

        if (options.Communication.MaxReceivesPerTick < 0)
        {
            errors.Add($"communication.maxReceivesPerTick doit être &gt;= 0 (reçu : {options.Communication.MaxReceivesPerTick}).");
        }

        if (options.Communication.TransmissionRange is <= 0 or > 70)
        {
            errors.Add($"communication.transmissionRange doit être dans [1, 70] (reçu : {options.Communication.TransmissionRange}).");
        }

        if (options.Communication.MaxHops < 0)
        {
            errors.Add($"communication.maxHops doit être &gt;= 0 (reçu : {options.Communication.MaxHops}).");
        }

        if (options.Communication.SendEnergyCost < 0.0 || options.Communication.SendEnergyPayloadFactor < 0.0)
        {
            errors.Add("communication.sendEnergyCost et sendEnergyPayloadFactor doivent être &gt;= 0.");
        }

        if (options.Communication.ReceiveEnergyCost < 0.0 || options.Communication.ReceiveEnergyPayloadFactor < 0.0)
        {
            errors.Add("communication.receiveEnergyCost et receiveEnergyPayloadFactor doivent être &gt;= 0.");
        }

        if (options.Communication.IncomprehensionRate is < 0.0 or > 1.0)
        {
            errors.Add($"communication.incomprehensionRate doit être dans [0, 1] (reçu : {options.Communication.IncomprehensionRate}).");
        }

        if (options.Communication.HopConfidenceDecay is <= 0.0 or > 1.0)
        {
            errors.Add($"communication.hopConfidenceDecay doit être dans (0, 1] (reçu : {options.Communication.HopConfidenceDecay}).");
        }

        if (options.Agents.Memory.MaxCapacity <= 0)
        {
            errors.Add($"agents.memory.maxCapacity doit être &gt; 0 (reçu : {options.Agents.Memory.MaxCapacity}).");
        }

        if (options.Agents.Memory.RecallThreshold is < 0.0 or > 1.0)
        {
            errors.Add($"agents.memory.recallThreshold doit être dans [0, 1] (reçu : {options.Agents.Memory.RecallThreshold}).");
        }

        if (options.Agents.Memory.ObservationDecayRate < 0.0 ||
            options.Agents.Memory.EventDecayRate < 0.0 ||
            options.Agents.Memory.InteractionDecayRate < 0.0)
        {
            errors.Add("agents.memory.*DecayRate doivent être &gt;= 0.");
        }

        if (options.Agents.Needs.HungerRate < 0.0 || options.Agents.Needs.ThirstRate < 0.0 || options.Agents.Needs.FatigueRate < 0.0)
        {
            errors.Add("agents.needs.*Rate doivent être &gt;= 0.");
        }

        if (options.Agents.Needs.HungerTriggerThreshold is < 0.0 or > 100.0)
        {
            errors.Add($"agents.needs.hungerTriggerThreshold doit être dans [0, 100] (reçu : {options.Agents.Needs.HungerTriggerThreshold}).");
        }

        if (options.Agents.Needs.ThirstTriggerThreshold is < 0.0 or > 100.0)
        {
            errors.Add($"agents.needs.thirstTriggerThreshold doit être dans [0, 100] (reçu : {options.Agents.Needs.ThirstTriggerThreshold}).");
        }

        if (options.Agents.Needs.FatigueTriggerThreshold is < 0.0 or > 100.0)
        {
            errors.Add($"agents.needs.fatigueTriggerThreshold doit être dans [0, 100] (reçu : {options.Agents.Needs.FatigueTriggerThreshold}).");
        }

        if (options.Agents.Needs.SafetyDriftRate < 0.0 || options.Agents.Needs.SocialDriftRate < 0.0 || options.Agents.Needs.CuriosityDriftRate < 0.0)
        {
            errors.Add("agents.needs.*DriftRate doivent être &gt;= 0.");
        }

        if (options.Agents.Beliefs.UpdateStrength is <= 0.0 or > 1.0)
        {
            errors.Add($"agents.beliefs.updateStrength doit être dans (0, 1] (reçu : {options.Agents.Beliefs.UpdateStrength}).");
        }

        if (options.Agents.Beliefs.MaxChangePerSnap is <= 0.0 or > 1.0)
        {
            errors.Add($"agents.beliefs.maxChangePerSnap doit être dans (0, 1] (reçu : {options.Agents.Beliefs.MaxChangePerSnap}).");
        }

        if (options.Agents.Beliefs.ExpiredCap is < 0.0 or > 1.0)
        {
            errors.Add($"agents.beliefs.expiredCap doit être dans [0, 1] (reçu : {options.Agents.Beliefs.ExpiredCap}).");
        }

        if (options.Agents.Beliefs.TimeDecayPerTick is <= 0.0 or > 1.0)
        {
            errors.Add($"agents.beliefs.timeDecayPerTick doit être dans (0, 1] (reçu : {options.Agents.Beliefs.TimeDecayPerTick}).");
        }

        if (options.Agents.Actions.Deliberation.IntervalTicks < 1)
        {
            errors.Add($"agents.actions.deliberation.intervalTicks doit être &gt;= 1 (reçu : {options.Agents.Actions.Deliberation.IntervalTicks}).");
        }

        if (options.Agents.Actions.Deliberation.AlignBonus <= 0.0)
        {
            errors.Add($"agents.actions.deliberation.alignBonus doit être &gt; 0 (reçu : {options.Agents.Actions.Deliberation.AlignBonus}).");
        }

        if (options.Agents.Actions.Deliberation.ActionSwitchMargin < 0.0)
        {
            errors.Add($"agents.actions.deliberation.actionSwitchMargin doit être &gt;= 0 (reçu : {options.Agents.Actions.Deliberation.ActionSwitchMargin}).");
        }

        if (options.Agents.Actions.Deliberation.ConflictTieMargin < 0.0)
        {
            errors.Add($"agents.actions.deliberation.conflictTieMargin doit être &gt;= 0 (reçu : {options.Agents.Actions.Deliberation.ConflictTieMargin}).");
        }

        if (options.Agents.Actions.Interruption.UtilityExcessMargin < 0.0)
        {
            errors.Add($"agents.actions.interruption.utilityExcessMargin doit être &gt;= 0 (reçu : {options.Agents.Actions.Interruption.UtilityExcessMargin}).");
        }

        if (options.Agents.Actions.Interruption.CriticalHunger is <= 0.0 or > 100.0)
        {
            errors.Add($"agents.actions.interruption.criticalHunger doit être dans (0, 100] (reçu : {options.Agents.Actions.Interruption.CriticalHunger}).");
        }

        if (options.Agents.Actions.Interruption.CriticalEnergy is < 0.0 or >= 100.0)
        {
            errors.Add($"agents.actions.interruption.criticalEnergy doit être dans [0, 100) (reçu : {options.Agents.Actions.Interruption.CriticalEnergy}).");
        }

        foreach (var (name, entry) in options.Agents.Actions.Catalog.Entries)
        {
            if (entry.EnergyCost is < 0.0)
            {
                errors.Add($"agents.actions.catalog.{name}.energyCost doit être &gt;= 0 (reçu : {entry.EnergyCost}).");
            }

            if (entry.EnergyRecovery is < 0.0)
            {
                errors.Add($"agents.actions.catalog.{name}.energyRecovery doit être &gt;= 0 (reçu : {entry.EnergyRecovery}).");
            }

            if (entry.FatigueRecovery is < 0.0)
            {
                errors.Add($"agents.actions.catalog.{name}.fatigueRecovery doit être &gt;= 0 (reçu : {entry.FatigueRecovery}).");
            }

            if (entry.HungerRecovery is < 0.0)
            {
                errors.Add($"agents.actions.catalog.{name}.hungerRecovery doit être &gt;= 0 (reçu : {entry.HungerRecovery}).");
            }

            if (entry.ThirstRecovery is < 0.0)
            {
                errors.Add($"agents.actions.catalog.{name}.thirstRecovery doit être &gt;= 0 (reçu : {entry.ThirstRecovery}).");
            }

            if (entry.ReserveConsumption is <= 0.0)
            {
                errors.Add($"agents.actions.catalog.{name}.reserveConsumption doit être &gt; 0 (reçu : {entry.ReserveConsumption}).");
            }
        }

        if (options.Groups.ReviewIntervalTicks < 1)
        {
            errors.Add($"groups.reviewIntervalTicks doit être &gt;= 1 (reçu : {options.Groups.ReviewIntervalTicks}).");
        }

        if (options.Groups.TrustThreshold is < 0.0 or > 1.0)
        {
            errors.Add($"groups.trustThreshold doit être dans [0, 1] (reçu : {options.Groups.TrustThreshold}).");
        }

        if (options.Groups.MinGroupSize < 2)
        {
            errors.Add($"groups.minGroupSize doit être &gt;= 2 (reçu : {options.Groups.MinGroupSize}).");
        }

        if (options.Groups.SharedBeliefBonus < 0.0 || options.Groups.GoalAlignmentBonus < 0.0)
        {
            errors.Add("groups.sharedBeliefBonus et groups.goalAlignmentBonus doivent être &gt;= 0.");
        }

        if (options.Groups.ConsensusThreshold is < 0.0 or > 1.0)
        {
            errors.Add($"groups.consensusThreshold doit être dans [0, 1] (reçu : {options.Groups.ConsensusThreshold}).");
        }

        if (options.Reproduction.IntervalTicks < 1)
        {
            errors.Add($"reproduction.intervalTicks doit être &gt;= 1 (reçu : {options.Reproduction.IntervalTicks}).");
        }

        if (options.Reproduction.ConsentTrustThreshold is < 0.0 or > 1.0)
        {
            errors.Add($"reproduction.consentTrustThreshold doit être dans [0, 1] (reçu : {options.Reproduction.ConsentTrustThreshold}).");
        }

        if (options.Reproduction.MaxBirthsPerTick < 0)
        {
            errors.Add($"reproduction.maxBirthsPerTick doit être &gt;= 0 (reçu : {options.Reproduction.MaxBirthsPerTick}).");
        }

        if (options.Agents.Inheritance.Dominance is < 0.0 or > 1.0)
        {
            errors.Add($"agents.inheritance.dominance doit être dans [0, 1] (reçu : {options.Agents.Inheritance.Dominance}).");
        }

        if (options.Agents.Inheritance.MutationRate is < 0.0 or > 1.0)
        {
            errors.Add($"agents.inheritance.mutationRate doit être dans [0, 1] (reçu : {options.Agents.Inheritance.MutationRate}).");
        }

        if (options.Agents.Inheritance.MutationMagnitude < 0.0)
        {
            errors.Add($"agents.inheritance.mutationMagnitude doit être &gt;= 0 (reçu : {options.Agents.Inheritance.MutationMagnitude}).");
        }

        if (options.Agents.Inheritance.SalienceThreshold is <= 0.0 or > 1.0)
        {
            errors.Add($"agents.inheritance.salienceThreshold doit être dans (0, 1] (reçu : {options.Agents.Inheritance.SalienceThreshold}).");
        }

        return errors;
    }
}