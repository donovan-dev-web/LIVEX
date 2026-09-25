namespace Simulation.Core.Configuration;

/// <summary>Profils opérationnels explicites utilisés par les lanceurs de runs.</summary>
public static class SimulationProfiles
{
    /// <summary>
    /// Profil de référence V0.1 : ressources suffisantes et coûts énergétiques
    /// auxiliaires neutres pour éviter une extinction artificielle.
    /// </summary>
    public static SimulationOptions Reference()
    {
        var options = new SimulationOptions();
        options.Resources.Food.Initial = 10_000;
        options.Resources.Food.RegenerationRate = 5;
        options.Resources.Water.Initial = 10_000;
        options.Resources.Water.RegenerationRate = 5;
        options.Communication.RelayEnabled = false;
        options.Communication.SendEnergyCost = 0;
        options.Communication.SendEnergyPayloadFactor = 0;
        options.Communication.ReceiveEnergyCost = 0;
        options.Communication.ReceiveEnergyPayloadFactor = 0;
        options.Communication.MaxSendsPerTick = 1;
        options.Communication.MaxReceivesPerTick = 1;
        options.Agents.Actions.MoveEnergyCost = 0.05;
        options.Agents.Actions.RestEnergyGain = 1.5;
        options.Agents.Actions.RestFatigueRecovery = 2;
        return options;
    }
}
