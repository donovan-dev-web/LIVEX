namespace Launcher.Domain.Model;

/// <summary>Profils d'exécution prédéfinis (COMPONENTS.md §9).</summary>
public static class WellKnownProfiles
{
    /// <summary>SYNE seul, sans analyse ni observation.</summary>
    public const string SimulationSeule = "simulation-seule";
    /// <summary>SYNE + ECHOS : analyse et rapport.</summary>
    public const string Analyse = "analyse";
    /// <summary>SYNE + ECHOS complet, interface comprise.</summary>
    public const string AnalyseAvecTelemetrie = "analyse-avec-telemetrie";
    /// <summary>SYNE + PRISM (verrouillé tant que PRISM est absent).</summary>
    public const string Immersion = "immersion";
    /// <summary>Campagne multi-run et rapports.</summary>
    public const string Experience = "experience";
    /// <summary>Diagnostic et injection de panne, stubs possibles.</summary>
    public const string Developpement = "developpement";
    /// <summary>Sélection manuelle libre.</summary>
    public const string Personnalise = "personnalise";
}

/// <summary>Résultat d'une résolution de profil (COMPONENTS.md §6).</summary>
public sealed class ProfileResolution
{
    /// <summary>Le profil est satisfiable avec les installations détectées.</summary>
    public bool Satisfiable { get; set; }

    /// <summary>Le mode Immersion est demandé mais verrouillé (PRISM non implémenté).</summary>
    public bool ImmersionLocked { get; set; }

    /// <summary>Le type de session Développement autorise la substitution par les stubs.</summary>
    public bool StubsAllowed { get; set; }

    /// <summary>Composants retenus, dans l'ordre de démarrage (SYNE en premier).</summary>
    public List<string> StartupOrder { get; } = new();

    /// <summary>Raisons explicites de non-satisfaction, affichées telles quelles.</summary>
    public List<string> Problems { get; } = new();
}

/// <summary>Dépendances obligatoires entre composants, pour la complétion de sélection.</summary>
public static class ComponentDependencies
{
    /// <summary>Composants requis par le composant donné. Le moteur est requis par tous les modes.</summary>
    public static IReadOnlyList<string> RequiredBy(string componentId) => componentId switch
    {
        "echos" => new[] { "syne" },
        "prism" => new[] { "syne" },
        "syne" => Array.Empty<string>(),
        _ => Array.Empty<string>(),
    };
}

/// <summary>Politique d'échec d'une campagne (EXPERIMENTS.md §7).</summary>
public enum FailurePolicy
{
    /// <summary>Le premier échec interrompt la campagne.</summary>
    Stop,
    /// <summary>La campagne se poursuit, les échecs sont consignés. Usage par défaut.</summary>
    Continue,
    /// <summary>Un échec est réessayé jusqu'à maxRetries.</summary>
    Retry,
    /// <summary>L'échec est attendu et ne produit pas d'incident.</summary>
    Tolerate,
}

/// <summary>Stratégie de dérivation des graines (EXPERIMENTS.md §5, DATA_FLOW.md §4.3).</summary>
public enum SeedStrategy
{
    /// <summary>graine(n) = baseSeed + n. Stratégie V0.1 par défaut.</summary>
    Derived,
    /// <summary>Liste de graines donnée dans la définition.</summary>
    Explicit,
    /// <summary>graine(n) = hash(baseSeed, n), dispersion large.</summary>
    DerivedHashed,
    /// <summary>Graines tirées puis enregistrées. Reproductibilité partielle seulement.</summary>
    Random,
}
