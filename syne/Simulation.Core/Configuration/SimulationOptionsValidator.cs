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

        return errors;
    }
}