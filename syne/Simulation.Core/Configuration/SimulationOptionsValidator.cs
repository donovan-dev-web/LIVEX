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

        if (options.Agents.Memory.MaxCapacity < 0)
        {
            errors.Add($"agents.memory.maxCapacity doit être &gt;= 0 (reçu : {options.Agents.Memory.MaxCapacity}).");
        }

        return errors;
    }
}