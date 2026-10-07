using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Protocol;

namespace Launcher.Application;

/// <summary>
/// Profil moteur effectif d'un run : la surcouche de configuration réellement remise
/// au moteur, et le document <c>config.resolved.json</c> qui en atteste (EXPERIMENTS.md §12).
///
/// <para>
/// Les deux sont produits ici, à partir du seul <see cref="RunSpec"/>, parce qu'ils
/// décrivent la même chose : si la surcouche écrite dans <c>launcher-config.json</c>
/// et le document archivé dans le paquet pouvaient diverger, le paquet cesserait
/// d'attester la configuration réellement appliquée — exactement ce que §12 exige.
/// </para>
///
/// <para>
/// Ce type n'a pas qualité pour décrire les réglages internes du moteur : ceux-ci
/// appartiennent au composant (DATA_FLOW.md §6.3, « le Launcher ne dégrade pas un
/// composant en écrivant à sa place »). Il n'enregistre que ce que le Launcher a
/// décidé et transmis, chacun explicitement, sans valeur implicite.
/// </para>
/// </summary>
public static class RunEngineProfile
{
    /// <summary>
    /// Fréquence de tick par défaut des runs batch (EXPERIMENTS.md §6) — plancher
    /// appliqué quand la définition ne demande aucune cadence explicite.
    ///
    /// <para>
    /// Un run n'a pas d'observateur : rien ne justifie de le rejouer en temps réel.
    /// Or le moteur cadence ses ticks par <c>Task.Delay(1s / TicksPerSecond)</c>,
    /// d'où un plancher de <c>ticks / 10</c> secondes — 100 s pour le profil de
    /// référence à 1000 ticks.
    /// </para>
    ///
    /// <para>
    /// Mesuré sur ce moteur (200 ticks, graine 42) : 20,6 s à 10 ticks/s, 0,90 s à
    /// 1000, 0,64 s à 100 000. Au-delà de 1000 le gain est marginal — la résolution
    /// du timer plafonne le délai aux alentours de 1 ms — d'où ce plancher
    /// volontairement conservateur plutôt qu'un délai nul.
    /// </para>
    ///
    /// <para>
    /// La cadence ne change que le rythme : <c>stateChecksum</c> et le contenu
    /// scientifique de <c>stream.jsonl</c> sont identiques à ceux du temps réel, la
    /// seule ligne divergente étant la valeur recopiée dans l'enregistrement
    /// <c>world_initialized</c> (provenance, pas résultat).
    /// </para>
    /// </summary>
    public const int BatchTicksPerSecond = 1_000;

    /// <summary>
    /// Cadence effectivement transmise : celle demandée par la définition
    /// (<c>RunSpec.TicksPerSecond</c>), faute de quoi le plancher batch
    /// <see cref="BatchTicksPerSecond"/> — comportement historique des paquets
    /// créés avant l'introduction du réglage.
    /// </summary>
    public static int TicksPerSecondFor(RunSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.TicksPerSecond > 0 ? spec.TicksPerSecond : BatchTicksPerSecond;
    }

    /// <summary>
    /// La surcouche <c>launcher-config.json</c> remise au moteur. Partielle par
    /// construction : le moteur la fusionne sur ses défauts intégrés, seul
    /// <c>ticksPerSecond</c> est remplacé, le reste du profil étant conservé.
    /// </summary>
    public static object ConfigOverlay(RunSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return new
        {
            agents = new { initialCount = spec.AgentCount },
            simulation = new { ticksPerSecond = TicksPerSecondFor(spec) },
        };
    }

    /// <summary>
    /// Le document <c>config.resolved.json</c> : ce que la campagne a demandé
    /// (<paramref name="definition"/>) et ce que le Launcher a effectivement transmis
    /// au moteur pour ce run.
    ///
    /// <para>
    /// Les valeurs de transport en sont volontairement absentes — port de contrôle,
    /// jeton de session, identifiant de corrélation, horodatages, chemins. Elles sont
    /// propres à une exécution et non à la configuration : les inscrire rendrait le
    /// document différent à chaque run, alors qu'un paquet scellé se compare octet pour
    /// octet (PACKAGE_FORMAT.md §6).
    /// </para>
    /// </summary>
    public static string SerializeResolved(RunSpec spec, ExperimentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(definition);
        return ContractJson.Serialize(new
        {
            schema = 1,
            campaign = definition,
            run = new
            {
                spec.ExperimentId,
                spec.RunId,
                spec.Attempt,
                spec.Simulation,
                spec.Seed,
                spec.Ticks,
                spec.AgentCount,
            },
            engine = new
            {
                component = "syne",
                profile = spec.Simulation,
                headless = true,
                autoStart = true,
                exportStream = true,
                // Identité analytique : SYNE l'écrit dans le flux exporté, ECHOS
                // l'enregistre sous cette clé. Elle est dérivée de l'identifiant de
                // campagne et du run, donc reproductible — contrairement au jeton.
                analyticsRunId = RunIdentity.For(spec.ExperimentId, spec.RunId),
                configOverlay = ConfigOverlay(spec),
            },
        });
    }
}