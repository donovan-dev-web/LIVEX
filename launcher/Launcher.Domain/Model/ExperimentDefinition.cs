using Launcher.Protocol;

namespace Launcher.Domain.Model;

/// <summary>
/// Définition déclarative d'une campagne (EXPERIMENTS.md §3). Complète et autosuffisante :
/// le fichier suffit à rejouer la campagne, sans le Launcher, si le moteur est disponible.
/// </summary>
public sealed class ExperimentDefinition : ISchemaVersioned
{
    /// <summary>Version de schéma.</summary>
    public int Schema { get; set; } = PackageConstants.SchemaVersion;

    /// <summary>Identifiant stable de la campagne (ex. « EXP-2026-001 »).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Libellé affichable.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Profil d'exécution (COMPONENTS.md §9).</summary>
    public string Profile { get; set; } = WellKnownProfiles.Analyse;

    /// <summary>Identifiant de la simulation à charger par le moteur.</summary>
    public string Simulation { get; set; } = string.Empty;

    /// <summary>Nombre de runs.</summary>
    public int RunCount { get; set; }

    /// <summary>Horizon de ticks par run.</summary>
    public long Ticks { get; set; }

    /// <summary>
    /// Vitesse d'exécution transmise au moteur (ticks par seconde) : 10 pour un run
    /// regardable en direct, 1000 pour l'exécution batch la plus rapide. Archivée
    /// dans le paquet (config.resolved.json) — le run atteste de sa cadence réelle.
    /// </summary>
    public int TicksPerSecond { get; set; } = 1_000;

    /// <summary>Nombre d'agents initiaux demandé au moteur.</summary>
    public int AgentCount { get; set; } = 50;

    /// <summary>Stratégie de graines. Le Launcher n'invente jamais de graine.</summary>
    public SeedStrategy SeedStrategy { get; set; } = SeedStrategy.Derived;

    /// <summary>Graine de base.</summary>
    public long BaseSeed { get; set; }

    /// <summary>Graines explicites, si la stratégie le prévoit.</summary>
    public List<long>? ExplicitSeeds { get; set; }

    /// <summary>Politique d'échec.</summary>
    public FailurePolicy FailurePolicy { get; set; } = FailurePolicy.Continue;

    /// <summary>Nombre maximal de tentatives par run, pour la politique retry.</summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>Note libre de l'utilisateur.</summary>
    public string? Notes { get; set; }

    /// <summary>Valide la définition : refuse tout champ incohérent avant création du paquet.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Id))
        {
            problems.Add("identifiant de campagne manquant");
        }

        if (RunCount <= 0)
        {
            problems.Add("nombre de runs doit être strictement positif");
        }
        else if (RunCount > 65535)
        {
            problems.Add("nombre de runs au-delà de la limite du format (65 535, RUN-nnnn sur quatre chiffres)");
        }

        if (Ticks <= 0)
        {
            problems.Add("horizon de ticks doit être strictement positif");
        }

        if (TicksPerSecond <= 0 || TicksPerSecond > 100_000)
        {
            problems.Add("ticks par seconde doit être dans [1, 100000] (10 = direct, 1000 = batch)");
        }

        if (AgentCount < 0)
        {
            problems.Add("nombre d'agents doit être positif ou nul");
        }

        if (string.IsNullOrWhiteSpace(Simulation))
        {
            problems.Add("identifiant de simulation manquant");
        }

        if (!string.IsNullOrWhiteSpace(Id)
            && (Id.Length > 128
                || !char.IsAsciiLetterOrDigit(Id[0])
                || Id.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-')))
        {
            problems.Add("identifiant de campagne invalide (lettres, chiffres, point, tiret ou souligné uniquement)");
        }

        if (SeedStrategy is SeedStrategy.Explicit or SeedStrategy.Random
            && (ExplicitSeeds is null || ExplicitSeeds.Count < RunCount))
        {
            problems.Add(SeedStrategy == SeedStrategy.Explicit
                ? "stratégie « explicit » : la liste de graines doit couvrir tous les runs"
                : "stratégie « random » : les graines tirées doivent être enregistrées et couvrir tous les runs (DATA_FLOW.md §4.3)");
        }

        if (FailurePolicy == FailurePolicy.Retry && MaxRetries < 1)
        {
            problems.Add("politique « retry » : maxRetries doit être au moins 1");
        }

        return problems;
    }

    /// <summary>Dérive la graine du run donné (indexé à partir de 0).</summary>
    public long SeedFor(int runNumber) => SeedDeriver.Derive(SeedStrategy, BaseSeed, runNumber, ExplicitSeeds);
}

/// <summary>Statuts d'exécution d'une campagne.</summary>
public static class ExperimentStates
{
    /// <summary>Créée, aucun run lancé.</summary>
    public const string Planifiee = "Planifiee";
    /// <summary>Runs en cours d'exécution.</summary>
    public const string EnCours = "EnCours";
    /// <summary>Interrompue : paquet récupérable, reprise possible.</summary>
    public const string Interrompue = "Interrompue";
    /// <summary>Terminée : paquet scellé.</summary>
    public const string Terminee = "Terminee";
    /// <summary>Annulée par l'utilisateur : paquet scellé documentant l'interruption.</summary>
    public const string Annulee = "Annulee";
}
