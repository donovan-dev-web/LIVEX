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

    /// <summary>
    /// Profil de référence sous forme de <b>surcouche JSON</b>, à passer tel quel à
    /// <see cref="ConfigLoader.MergeJson(SimulationOptions, string)"/>. Utilisé par le lanceur HTTP : il
    /// transporte le profil comme le ferait un fichier <c>--config</c>, donc avec
    /// la même sémantique de fusion — et non comme un objet <c>SimulationOptions</c>,
    /// dont la désérialisation rendrait chaque clé absente destructive.
    /// </summary>
    public static string ReferenceJson() => ConfigLoader.ToJson(Reference());
}
