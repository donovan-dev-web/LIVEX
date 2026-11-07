// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Profils opérationnels explicites utilisés par les lanceurs de runs.

namespace Simulation.Core.Configuration;

/// <summary>Profils opérationnels explicites utilisés par les lanceurs de runs.</summary>
public static class SimulationProfiles
{
    /// <summary>Identifiant de scénario batch du profil de référence.</summary>
    public const string ReferenceId = "reference";

    /// <summary>
    /// Vrai si <paramref name="simulationId"/> est un scénario batch connu
    /// (<c>null</c> = défaut historique, équivalent à <see cref="ReferenceId"/> :
    /// le profil de référence EST le défaut intégré).
    /// </summary>
    /// <remarks>
    /// Un seul scénario calibré subsiste. Le profil <c>prism</c> qui existait à
    /// côté est retiré : il ne portait plus rien que PRISM exige. Sa géométrie
    /// (256 × 256, cellule 16) est désormais <b>lue</b> dans l'instantané de monde
    /// de PRISM (ADR-013, PRISM autoritaire sur la géométrie), son décor en
    /// disques était une approximation de la scène Unreal (jamais injectée dans
    /// l'adaptateur couplé), et sa cadence (6 TPS, 5 s simulées/tick) était une
    /// calibration SYNE qui ralentissait les désirs sans contrepartie côté PRISM.
    /// </remarks>
    public static bool IsKnownSimulationId(string? simulationId) =>
        simulationId is null or ReferenceId;

    /// <summary>
    /// Profil de référence : scénario de survie calibré.
    /// </summary>
    /// <remarks>
    /// <b>Calibration B1 (horizon 2500 ticks)</b> : les valeurs calibrées
    /// sont désormais portées par les **défauts intégrés** (tout run sans surcouche
    /// — dont le chemin Launcher `--simulation reference` — tourne sur le profil
    /// calibré) et ce profil les rejoue **explicitement**, parce que le chemin HTTP
    /// (`config ?? ReferenceJson()`) ne doit jamais dépendre d'un défaut qu'on
    /// pourrait oublier de recaler. Leviers B1 :
    /// <list type="bullet">
    /// <item>énergie : `moveEnergyCost` 0,03, `restEnergyGain` 1,5, Eat/Drink
    /// `energyRecovery` 2,0 / 1,0 (hérité de D1) ;</item>
    /// <item>besoins sociaux : `socialDriftRate` 0,003, `curiosityDriftRate`
    /// 0,002 — <b>réalignement</b> : la calibration B1 avait ralenti ces dérives
    /// (0,0002 / 0,0005) parce que Socialize/Explore ne réduisaient aucun besoin, si
    /// bien que la dérive ne pouvait que saturer un besoin sans exutoire. Les deux
    /// actions <b>satisfont</b> désormais leur besoin à l'arrivée
    /// (<c>socialRecovery</c> / <c>curiosityRecovery</c>), donc la dérive retrouve une
    /// valeur de cycle : seuil franchi (~230 / ~150 ticks), poursuite ciblée, arrivée,
    /// redescente sous le seuil — l'effet de la dérive est <b>mesuré</b> sur le run de
    /// référence, pas supposé ;</item>
    /// <item>ressources : nourriture 20 000 + 20/tick nets (dégradation
    /// neutralisée), eau 20 000 + 10/tick ;</item>
    /// <item><b>modèle de ressources localisé</b> (A0-2 / BL-35) : `resources.localized
    /// = true` est déclaré ici **explicitement** et dans
    /// `configs/simulation/reference.json` — le profil de référence tourne sur les
    /// gisements localisés `ResourceNode`, condition posée par A0-2 et remplie par le
    /// **gate de viabilité du profil localisé** publié et franchi le 17/10
    /// (`docs/realignment-evidence/BL35-GATE-VIABILITE-LOCALISE.md` : V1 viabilité
    /// 3/3 graines sans mort, V2 sensibilité à la graine, V3 quête adossée à un
    /// dépôt connu, V4 compétition sur gisement). Les réserves globales restent le
    /// **modèle baseline historique** — déclaré et testé séparément — pour la
    /// continuité des baselines ;</item>
    /// <item>communication : coûts énergétiques neutres, relais coupé, 1 envoi /
    /// 1 réception par tick (hérité de D1).</item>
    /// </list>
    /// </remarks>
    public static SimulationOptions Reference()
    {
        var options = new SimulationOptions();
        // A0-2 / BL-35 : déclaration explicite du modèle de ressources du profil
        // de référence. Ce n'est pas un héritage muet du défaut : A0-2 pose les
        // réserves globales comme baseline historique et les gisements localisés
        // comme modèle cible, sous gate de viabilité publié et franchi le 17/10
        // (docs/realignment-evidence/BL35-GATE-VIABILITE-LOCALISE.md).
        options.Resources.Localized = true;
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
        options.Agents.Needs.SocialDriftRate = 0.003;
        options.Agents.Needs.CuriosityDriftRate = 0.002;
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
