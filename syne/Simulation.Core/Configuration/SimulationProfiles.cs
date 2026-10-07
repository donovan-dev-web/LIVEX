namespace Simulation.Core.Configuration;

/// <summary>Profils opérationnels explicites utilisés par les lanceurs de runs.</summary>
public static class SimulationProfiles
{
    /// <summary>
    /// Profil de référence : scénario de survie calibré.
    /// </summary>
    /// <remarks>
    /// <b>Calibration B1 (ADR-016, horizon 2500 ticks)</b> : les valeurs calibrées
    /// sont désormais portées par les **défauts intégrés** (tout run sans surcouche
    /// — dont le chemin Launcher `--simulation reference` — tourne sur le profil
    /// calibré) et ce profil les rejoue **explicitement**, parce que le chemin HTTP
    /// (`config ?? ReferenceJson()`) ne doit jamais dépendre d'un défaut qu'on
    /// pourrait oublier de recaler. Leviers B1 :
    /// <list type="bullet">
    /// <item>énergie : `moveEnergyCost` 0,03, `restEnergyGain` 1,5, Eat/Drink
    /// `energyRecovery` 2,0 / 1,0 (hérité de D1) ;</item>
    /// <item>besoins sociaux : `socialDriftRate` 0,0002, `curiosityDriftRate`
    /// 0,0005 — un besoin sans mécanisme de satisfaction finissait par saturer
    /// Socialize/Explore (96 % des décisions en fin de run) ;</item>
    /// <item>ressources : nourriture 20 000 + 20/tick nets (dégradation
    /// neutralisée), eau 20 000 + 10/tick ;</item>
    /// <item>communication : coûts énergétiques neutres, relais coupé, 1 envoi /
    /// 1 réception par tick (hérité de D1).</item>
    /// </list>
    /// </remarks>
    public static SimulationOptions Reference()
    {
        var options = new SimulationOptions();
        options.Resources.Food.Initial = 20_000;
        options.Resources.Food.RegenerationRate = 20;
        options.Resources.Food.DegradationTick = null;
        options.Resources.Water.Initial = 20_000;
        options.Resources.Water.RegenerationRate = 10;
        options.Communication.RelayEnabled = false;
        options.Communication.SendEnergyCost = 0;
        options.Communication.SendEnergyPayloadFactor = 0;
        options.Communication.ReceiveEnergyCost = 0;
        options.Communication.ReceiveEnergyPayloadFactor = 0;
        options.Communication.MaxSendsPerTick = 1;
        options.Communication.MaxReceivesPerTick = 1;
        options.Agents.Needs.SocialDriftRate = 0.0002;
        options.Agents.Needs.CuriosityDriftRate = 0.0005;
        options.Agents.Actions.MoveEnergyCost = 0.03;
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
