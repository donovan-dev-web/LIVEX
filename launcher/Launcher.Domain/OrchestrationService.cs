using Launcher.Domain.Model;
using Launcher.Protocol.Model;

namespace Launcher.Domain;

/// <summary>
/// Règles d'orchestration du domaine, sans aucun accès processus, réseau ou disque
/// (ARCHITECTURE.md : le domaine ne référence ni Avalonia, ni System.Diagnostics, ni System.Net).
/// </summary>
public sealed class OrchestrationService
{
    private readonly ServiceRegistry _registry;
    private readonly IClock _clock;
    private readonly ISessionJournal _journal;
    private readonly IEndpointResolver _resolver;

    /// <summary>Initialise le service d'orchestration.</summary>
    public OrchestrationService(ServiceRegistry registry, IEndpointResolver resolver, IClock clock, ISessionJournal journal)
    {
        _registry = registry;
        _resolver = resolver;
        _clock = clock;
        _journal = journal;
    }

    /// <summary>Registre des installations et instances.</summary>
    public ServiceRegistry Registry => _registry;

    /// <summary>Installe les installations détectées dans le registre.</summary>
    public void Adopt(IEnumerable<ComponentInstallation> installations)
    {
        // Une seule énumération : « installations » peut être paresseuse (détection disque),
        // la compter après coup relancerait la détection et pourrait compter autre chose.
        var detected = installations as IReadOnlyList<ComponentInstallation> ?? installations.ToList();
        foreach (var installation in detected)
        {
            _registry.RegisterInstallation(installation);
        }

        _journal.Info("Detection", $"{detected.Count} installation(s) enregistrée(s) dans le registre");
    }

    /// <summary>
    /// Résout un profil : complétion des dépendances, validation des capacités,
    /// ordonnancement (SYNE en premier, arrêt en ordre inverse), verrou Immersion évalué.
    /// </summary>
    public ProfileResolution ResolveProfile(
        string profileId,
        SessionKind? sessionKind = null,
        IReadOnlyCollection<string>? extraComponents = null,
        bool allowStubs = false)
    {
        var resolution = new ProfileResolution();
        var requested = new List<string>(extraComponents ?? Array.Empty<string>());

        switch (profileId)
        {
            case WellKnownProfiles.SimulationSeule:
                requested.Add("syne");
                break;
            case WellKnownProfiles.Analyse or WellKnownProfiles.AnalyseAvecTelemetrie or WellKnownProfiles.Experience:
                requested.AddRange(SyndicateComponents);
                break;
            case WellKnownProfiles.Immersion:
                requested.AddRange(ImmersionComponents);
                break;
            case WellKnownProfiles.Developpement:
                requested.AddRange(DevelopmentComponents);
                break;
            case WellKnownProfiles.Personnalise:
                break;
            default:
                resolution.Problems.Add($"profil inconnu : {profileId}");
                return resolution;
        }

        if (requested.Contains("syne", StringComparer.Ordinal)
            && requested.Contains("syne-mock", StringComparer.Ordinal))
        {
            resolution.Problems.Add("SYNE réel et SYNE émulé sont exclusifs : un seul moteur peut être sélectionné");
            return resolution;
        }

        var usesMockEngine = requested.Contains("syne-mock", StringComparer.Ordinal);

        // Complétion : ajoute les dépendances obligatoires non sélectionnées.
        foreach (var componentId in requested.ToList())
        {
            foreach (var required in ComponentDependencies.RequiredBy(componentId))
            {
                if (required == "syne" && usesMockEngine)
                {
                    continue;
                }

                if (!requested.Contains(required))
                {
                    requested.Insert(0, required);
                }
            }
        }

        var distinct = requested.Distinct().ToList();

        // Le moteur peut être remplacé par un stub en type Développement (COMPONENTS.md §8.3),
        // soit parce que l'appelant autorise les stubs sur une session Développement, soit parce
        // que l'opérateur a explicitement choisi l'émulation. Les deux conditions sont distinctes :
        // sans parenthèses, « && » l'emporterait sur « || » par simple priorité d'opérateur.
        if ((allowStubs && sessionKind == SessionKind.Developpement) || usesMockEngine)
        {
            resolution.StubsAllowed = true;
        }

        if (sessionKind == SessionKind.Experience && distinct.Contains("prism"))
        {
            resolution.Problems.Add("le mode Console ne démarre pas PRISM");
        }

        // Verrou du mode Immersion : condition évaluée au manifeste, jamais codée en dur
        // (ADR-006, INTEGRATION_CONTRACT.md §11.1). Tant qu'une exigence échoue, le verrou
        // tient avec la cause exacte et le jalon attendu.
        if (distinct.Contains("prism"))
        {
            var lockCause = EvaluateImmersionLock(_registry.GetActiveInstallation("prism"));
            if (lockCause is not null)
            {
                resolution.ImmersionLocked = true;
                resolution.Problems.Add($"mode Immersion verrouillé : {lockCause}");
            }
        }

        foreach (var componentId in distinct)
        {
            var installation = _registry.GetActiveInstallation(componentId);
            if (installation is null)
            {
                resolution.Problems.Add($"composant « {componentId} » non détecté : profil non satisfiable");
                continue;
            }

            if (!installation.ManifestValid)
            {
                resolution.Problems.Add($"composant « {componentId} » : {installation.DetectionCause ?? "installation inutilisable"}");
                continue;
            }

            if (componentId == "prism" && resolution.ImmersionLocked)
            {
                continue;
            }

            resolution.StartupOrder.Add(componentId);
        }

        resolution.Satisfiable = resolution.Problems.Count == 0 && resolution.StartupOrder.Count > 0;
        if (resolution.ImmersionLocked)
        {
            resolution.Satisfiable = false;
        }

        return resolution;
    }

    /// <summary>Identifiant de corrélation d'une nouvelle opération.</summary>
    public static string NewCorrelationId() => $"c{Guid.NewGuid().ToString("N")[..12]}";

    /// <summary>
    /// Cause du verrou Immersion pour une installation PRISM donnée, nulle si le verrou est levé.
    /// Absent ou manifeste invalide : jalon G7 ; manifeste lisible : exigences du §11.1, une par une.
    /// Publique : l'interface affiche la même évaluation que la résolution de profil (ADR-006).
    /// </summary>
    public static string? EvaluateImmersionLock(ComponentInstallation? prism)
    {
        if (prism is null)
        {
            return "PRISM n'est pas implémenté (jalon G7 — voir ROADMAP.md)";
        }

        if (!prism.ManifestValid)
        {
            return $"{prism.DetectionCause ?? "installation inutilisable"} (jalon G7 — voir ROADMAP.md)";
        }

        var failures = PrismRequirements.Validate(prism.Manifest!);
        return failures.Count == 0
            ? null
            : $"{string.Join(" ; ", failures)} (jalon G7 — INTEGRATION_CONTRACT.md §11.1)";
    }

    /// <summary>Registre l'état de santé d'une instance et le journalise.</summary>
    public void ApplyHealth(string instanceId, HealthReport report, string? correlationId = null)
    {
        var instance = _registry.Find(instanceId);
        if (instance is null)
        {
            return;
        }

        var previous = instance.Health;
        instance.Health = report;
        if (previous.State != report.State)
        {
            _journal.Info("ComponentState",
                $"{instance.ComponentId} : {StateRules.Name(previous.State)} → {StateRules.Name(report.State)} ({report.Cause})",
                correlationId, instanceId);
        }
    }

    /// <summary>Composants du mode Analyse et de l'Expérience.</summary>
    private static readonly string[] SyndicateComponents = ["syne", "echos"];

    /// <summary>Composants du mode Immersion.</summary>
    private static readonly string[] ImmersionComponents = ["syne", "prism"];

    /// <summary>Composants du type Développement.</summary>
    private static readonly string[] DevelopmentComponents = ["syne-mock", "echos"];

    /// <summary>Construit l'ordre d'arrêt : inverse strict de l'ordre de démarrage (COMPONENTS.md §6).</summary>
    public static IReadOnlyList<string> ShutdownOrder(IReadOnlyList<string> startupOrder)
    {
        var copy = startupOrder.ToList();
        copy.Reverse();
        return copy;
    }

    /// <summary>Agrège la santé globale avec la cause principale (OBSERVABILITY.md §3.2).</summary>
    public (string GlobalState, string MainCause) AggregateHealth(IReadOnlyCollection<string> requiredComponents)
    {
        var instances = _registry.All();
        var input = instances
            .Select(i => (Instance: i, Required: requiredComponents.Contains(i.ComponentId)))
            .Where(x => x.Required || x.Instance.Health.State is not (ComponentState.Absent or ComponentState.Inactif));
        return StateRules.Aggregate(input);
    }
}
