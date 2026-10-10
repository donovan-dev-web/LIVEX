namespace Simulation.Core.Configuration;

/// <summary>Profils opérationnels explicites utilisés par les lanceurs de runs.</summary>
public static class SimulationProfiles
{
    /// <summary>Identifiant de scénario batch du profil de référence (ADR-016).</summary>
    public const string ReferenceId = "reference";

    /// <summary>
    /// Identifiant de scénario batch du profil PRISM (ADR-017, monde 2 240 /
    /// 5 s simulées par tick) — accepté par <c>--simulation</c> et par le
    /// sélecteur de scénario du Launcher.
    /// </summary>
    public const string PrismId = "prism";

    /// <summary>
    /// Vrai si <paramref name="simulationId"/> est un scénario batch connu
    /// (<c>null</c> = défaut historique, équivalent à <see cref="ReferenceId"/> :
    /// le profil de référence EST le défaut intégré, ADR-016).
    /// </summary>
    public static bool IsKnownSimulationId(string? simulationId) =>
        simulationId is null or ReferenceId or PrismId;

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

    /// <summary>
    /// Profil <b>PRISM</b> (ADR-017 §4.4, SCALE_AND_CADENCE_SPEC.md §2) : monde
    /// 2 240 × 2 240 unités (70 × 70 cases de 32 m, case logique = cluster World
    /// Partition 3 200 uu à k = 100 uu/unité), cadence 6 TPS (166,7 ms/tick) et
    /// <b>5 secondes simulées par tick</b> — ratio R = 30 (1 s réelle = 30 s
    /// simulées : faim en 3 min 20, journée en 48 min). Le pas de déplacement
    /// reste 1 unité × trait <c>speed</c> par tick : la vitesse apparente
    /// (600 uu/s à speed 1,0) est celle du joueur UE par défaut.
    ///
    /// <para>
    /// <b>Additif et neutre</b> : seul <c>simulatedSecondsPerTick</c> change
    /// l'écoulement du temps simulé (classe A mise à l'échelle au démarrage) ;
    /// ni TPS de référence, ni trait de vitesse, ni échelle k ne bougent.
    /// rejoue le profil <c>reference</c> calibré (ADR-016) puis applique le bloc
    /// PRISM — un profil doit être <b>complet</b> (le contrôleur applique la
    /// configuration fournie sur les défauts intégrés).
    /// </para>
    /// </summary>
    public static SimulationOptions Prism()
    {
        var options = Reference();
        options.Simulation.WorldWidth = 2240;
        options.Simulation.WorldHeight = 2240;
        options.Simulation.WorldCellSize = 32;
        options.Simulation.TicksPerSecond = 6;
        options.Simulation.SimulatedSecondsPerTick = 5;
        options.World.MetersPerUnit = 1.0;
        return options;
    }

    /// <summary>Profil PRISM sous forme de surcouche JSON (même sémantique que <see cref="ReferenceJson"/>).</summary>
    public static string PrismJson() => ConfigLoader.ToJson(Prism());
}
