namespace Simulation.Core.Configuration;

/// <summary>Profils opérationnels explicites utilisés par les lanceurs de runs.</summary>
public static class SimulationProfiles
{
    /// <summary>
    /// Profil de référence V0.1 : ressources suffisantes et coûts énergétiques
    /// auxiliaires neutres pour éviter une extinction artificielle.
    /// </summary>
    /// <remarks>
    /// Calibration de survie (décision D1 du 29/09/2026, ADR de calibration —
    /// campagne 3 × 1200 ticks) : manger/boire compense désormais le coût
    /// métabolique du déplacement vers la ressource (EnergyRecovery 2.0 / 1.0).
    /// Sans ce gain, l'énergie moyenne décroissait continûment (mort lente :
    /// 69 → 49 entre t800 et t1200) car seul Rest rapportait de l'énergie.
    /// Le reste du profil est inchangé (isolation des causes).
    /// </remarks>
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
        options.Agents.Actions.Catalog.Entries["eat"].EnergyRecovery = 2.0;
        options.Agents.Actions.Catalog.Entries["drink"].EnergyRecovery = 1.0;
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
