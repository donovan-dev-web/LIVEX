using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Protocol.Model;

namespace Launcher.Application;

/// <summary>Ports sortants de la couche application (ARCHITECTURE.md §1 : l'application dépend du domaine).</summary>
public interface IPackageService
{
    /// <summary>Crée le paquet d'une campagne et renvoie son chemin.</summary>
    string Create(ExperimentDefinition experiment);

    /// <summary>Clôture un run dans le paquet selon la séquence normative §5.2.</summary>
    void CompleteRun(string packagePath, RunCompletion completion);

    /// <summary>Scelle le paquet d'une campagne.</summary>
    string Seal(string packagePath);

    /// <summary>Marque le paquet comme récupérable après interruption.</summary>
    void MarkRecoverable(string packagePath);

    /// <summary>Écrit le rapport d'émergence et les fichiers d'analyse agrégée (canal 3 — restitution).</summary>
    void WriteAnalysisReport(string packagePath, string emergenceReport, IReadOnlyDictionary<string, byte[]> aggregateFiles);

    /// <summary>Enregistre les versions des composants engagés dans le manifeste (EXPERIMENTS.md §12).</summary>
    void RegisterComponents(string packagePath, IReadOnlyList<Protocol.Model.JsonComponentRef> components);

    /// <summary>Lit l'état du paquet et son index des runs.</summary>
    (string State, RunIndex Index) ReadState(string packagePath);
}

/// <summary>Résultat d'un run exécuté par le moteur.</summary>
public sealed record RunResult(
    string RunId,
    long Seed,
    string Status,
    int Attempt,
    string? Cause,
    IReadOnlyDictionary<string, byte[]> DataFiles,
    IReadOnlyList<(string Name, byte[] Content)> LogFiles,
    long TicksReached,
    TimeSpan Duration,
    int ExitCode = 0,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? EndedAt = null,
    IReadOnlyList<Protocol.Model.JsonComponentRef>? ComponentVersions = null);

/// <summary>Éléments écrits dans le paquet à la clôture d'un run.</summary>
public sealed class RunCompletion
{
    /// <summary>Identifiant du run (RUN-nnnn).</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Entrée d'index à enregistrer (source de reprise).</summary>
    public RunIndexEntry IndexEntry { get; init; } = new();

    /// <summary>Contenu de run.json.</summary>
    public string RunJson { get; init; } = string.Empty;

    /// <summary>Contenu de config.resolved.json.</summary>
    public string ConfigResolvedJson { get; init; } = string.Empty;

    /// <summary>Fichiers de données du run, collectés depuis les composants.</summary>
    public IReadOnlyDictionary<string, byte[]> DataFiles { get; init; } = new Dictionary<string, byte[]>();

    /// <summary>Journaux corrélés au run.</summary>
    public IReadOnlyList<(string Name, byte[] Content)> LogFiles { get; init; } = Array.Empty<(string, byte[])>();

    /// <summary>Événement de journal à ajouter au paquet.</summary>
    public RunJournalEvent? CompletionEvent { get; set; }

    /// <summary>Analyse individuelle d'ECHOS pour ce run, écrite sous analysis/individual/.</summary>
    public IReadOnlyDictionary<string, byte[]> AnalysisFiles { get; init; } = new Dictionary<string, byte[]>();

    /// <summary>Versions des composants engagés, lues de leurs manifestes (EXPERIMENTS.md §12).</summary>
    public IReadOnlyList<Protocol.Model.JsonComponentRef> ComponentVersions { get; init; } = Array.Empty<Protocol.Model.JsonComponentRef>();
}

/// <summary>Exécution d'un run par le moteur (pilotage SYNE, réel ou stub).</summary>
public interface IRunExecutor
{
    /// <summary>
    /// Avancement du run en cours, émis tant qu'il tourne (EXPERIMENTS.md §8). Sans
    /// abonné, l'exécution est identique : la progression est une commodité d'affichage.
    /// </summary>
    event EventHandler<RunTickProgress>? TickProgress;

    /// <summary>Exécute un run et renvoie son résultat normalisé.</summary>
    Task<RunResult> ExecuteAsync(RunSpec spec, CancellationToken cancellationToken);
}

/// <summary>Spécification d'exécution d'un run, résolue par l'application.</summary>
public sealed class RunSpec
{
    /// <summary>Identifiant de campagne.</summary>
    public string ExperimentId { get; init; } = string.Empty;

    /// <summary>Identifiant du run (RUN-nnnn).</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Graine dérivée du run.</summary>
    public long Seed { get; init; }

    /// <summary>Horizon de ticks.</summary>
    public long Ticks { get; init; }

    /// <summary>
    /// Vitesse d'exécution demandée au moteur (ticks par seconde). 0 = valeur non
    /// spécifiée : le profil moteur applique son plancher batch
    /// (<c>RunEngineProfile.BatchTicksPerSecond</c>). Une valeur basse (10) rend le
    /// run regardable en direct, une valeur haute (1000) l'exécute au plus vite.
    /// </summary>
    public int TicksPerSecond { get; init; }

    /// <summary>Nombre d'agents initiaux demandé au moteur.</summary>
    public int AgentCount { get; init; } = 50;

    /// <summary>Identifiant de la simulation à charger.</summary>
    public string Simulation { get; init; } = string.Empty;

    /// <summary>Numéro de tentative (politique retry).</summary>
    public int Attempt { get; init; } = 1;

    /// <summary>Répertoire de travail du run, où le composant écrit ses sorties.</summary>
    public string WorkDirectory { get; init; } = string.Empty;

    /// <summary>Jeton de session, transmis par variable d'environnement.</summary>
    public string SessionToken { get; init; } = string.Empty;
}

/// <summary>Demande d'analyse à ECHOS (ADR-003 : le Launcher demande, ECHOS produit).</summary>
public interface IAnalysisService
{
    /// <summary>Demande l'analyse d'un run. Renvoie les fichiers produits.</summary>
    Task<IReadOnlyDictionary<string, byte[]>> AnalyzeRunAsync(RunResult run, string experimentId, CancellationToken cancellationToken);

    /// <summary>Demande l'analyse agrégée et le rapport d'émergence en fin de campagne.</summary>
    Task<(IReadOnlyDictionary<string, byte[]> AggregateFiles, string? EmergenceReport)> AnalyzeExperimentAsync(
        string experimentId,
        IReadOnlyList<string> runIds,
        CancellationToken cancellationToken);
}

/// <summary>
/// Lecture de l'analyse ECHOS pour la fenêtre d'analyse native (ADR-007). Le Launcher
/// **présente** : aucune méthode de ce port ne calcule une valeur scientifique, il
/// transporte ce que l'API REST d'ECHOS publie déjà (ADR-003).
/// Tout échec lève une erreur explicite — l'absence de données est affichée, jamais
/// approximée.
/// </summary>
public interface IEchosTelemetrySource
{
    /// <summary>Liste des runs enregistrés dans la base analytique.</summary>
    Task<IReadOnlyList<EchosRunSummary>> ReadRunsAsync(CancellationToken cancellationToken);

    /// <summary>Séries de métriques d'un run, sous-échantillonnées de `every` ticks.</summary>
    Task<EchosSeries> ReadSeriesAsync(string? runId, int every, CancellationToken cancellationToken);

    /// <summary>Catalogue versionné des métriques (définitions, unités, limites).</summary>
    Task<EchosMetricCatalog> ReadCatalogAsync(CancellationToken cancellationToken);

    /// <summary>Profil de viabilité d'un run : population, besoins, ressources, complétude.</summary>
    Task<EchosViability> ReadViabilityAsync(string runId, int every, CancellationToken cancellationToken);

    /// <summary>Synthèse multi-runs (contexte de contrôle + dispersion descriptive).</summary>
    Task<EchosExperimentSummary> ReadExperimentSummaryAsync(
        IReadOnlyList<string> runIds, CancellationToken cancellationToken);

    /// <summary>Communautés observées au dernier tick (graphe de relations).</summary>
    Task<EchosNetwork> ReadNetworkAsync(string? runId, CancellationToken cancellationToken);

    /// <summary>Phénomènes émergents détectés sur le run.</summary>
    Task<EchosPhenomena> ReadPhenomenaAsync(string? runId, CancellationToken cancellationToken);

    /// <summary>Description de monde et observation au tick demandé (vue 2D).</summary>
    Task<EchosWorld> ReadWorldAsync(string? runId, int? tick, CancellationToken cancellationToken);

    /// <summary>Graphe de confiance observé (nœuds et arêtes publiés tels quels).</summary>
    Task<EchosTrustGraph> ReadTrustGraphAsync(string? runId, int? tick, CancellationToken cancellationToken);

    /// <summary>Détail d'un run : métadonnées, métriques exactes, provenance, phénomènes.</summary>
    Task<EchosRunDetail> ReadRunDetailAsync(string runId, CancellationToken cancellationToken);

    /// <summary>Croyances et relations d'une entité au tick le plus récent observé.</summary>
    Task<EchosAgentProfile> ReadAgentProfileAsync(string runId, string agentId, CancellationToken cancellationToken);

    /// <summary>Traces de décision du run (tri tick puis entité), pour la fiche agent.</summary>
    Task<IReadOnlyList<EchosDecision>> ReadDecisionsAsync(string runId, CancellationToken cancellationToken);

    /// <summary>
    /// Journal d'événements borné du run (`GET /api/runs/{id}/events`) — annotations
    /// de la vue temporelle : ticks publiés, jamais interprétés (P3).
    /// </summary>
    Task<EchosEventFeed> ReadEventsAsync(string runId, CancellationToken cancellationToken);
}

/// <summary>Métadonnées d'un run telles que produites par ECHOS (`/api/runs`).</summary>
public sealed record EchosRunSummary(
    string RunId,
    long Seed,
    int TicksCount,
    int FirstTick,
    int LastTick,
    string Outcome,
    int? ExtinctionTick,
    string? ConservationLevel = null,
    string Version = "",
    int? AgentContextEvery = null)
{
    /// <summary>Statut de lecture : un run éteint ne se relit pas comme un run en direct.</summary>
    public string RunState => Outcome switch
    {
        "extinct" => "terminé (extinction observée)",
        "surviving" => "en cours ou sans extinction observée",
        _ => "inconnu",
    };
}

/// <summary>Séries d'un run (`/api/runs/{id}/metrics`).</summary>
public sealed class EchosSeries
{
    /// <summary>Identifiant du run résolu par ECHOS.</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Ticks échantillonnés, ordre croissant ; les tableaux de valeurs suivent le même ordre.</summary>
    public IReadOnlyList<int> Ticks { get; init; } = Array.Empty<int>();

    /// <summary>Valeurs par moteur puis par métrique, alignées sur <see cref="Ticks"/>.
    /// `null` = tick sans observation pour cette métrique : le trou reste un trou,
    /// jamais une valeur décalée ni un zéro inventé.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<double?>>> Values { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<double?>>>();

    /// <summary>Dernière valeur par moteur et métrique.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Latest { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, double>>();

    /// <summary>
    /// Provenance par métrique : `false` signale un repli neutre (fenêtre de données
    /// absente), **pas** une valeur observée — elle ne doit jamais s'afficher comme telle.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> Measured { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, bool>>();

    /// <summary>
    /// Provenance **alignée par tick** (même longueur que <see cref="Ticks"/> et que
    /// chaque série) : `null` = tick sans observation, `false` = repli neutre,
    /// `true` = mesuré. C'est le masque qui doit piloter la courbe (P0).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<bool?>>> MeasuredByTick { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<bool?>>>();

    /// <summary>Ticks absents entre le premier et le dernier tick observé (lacunes).</summary>
    public IReadOnlyList<int> MissingTicks { get; init; } = Array.Empty<int>();

    /// <summary>Nombre total de ticks manquants (au-delà du détail publié).</summary>
    public int MissingTicksCount { get; init; }

    /// <summary>Dernier tick du run, nul si le run est vide.</summary>
    public int? LatestTick { get; init; }
}

/// <summary>Graphe de communautés observé (`/api/groups`).</summary>
public sealed class EchosNetwork
{
    /// <summary>Identifiant du run.</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Tick de l'observation ; -1 si aucune observation de groupes.</summary>
    public int Tick { get; init; } = -1;

    /// <summary>Communautés détectées.</summary>
    public IReadOnlyList<EchosGroup> Groups { get; init; } = Array.Empty<EchosGroup>();
}

/// <summary>Une communauté : étiquette (plus petit membre) et membres triés.</summary>
public sealed record EchosGroup(string Label, IReadOnlyList<string> Members, int Size);

/// <summary>Phénomènes émergents détectés (`/api/emergent-phenomena`).</summary>
public sealed class EchosPhenomena
{
    /// <summary>Identifiant du run.</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Tick de la dernière observation ; -1 si aucune.</summary>
    public int Tick { get; init; } = -1;

    /// <summary>Phénomènes détectés, identifiant croissant.</summary>
    public IReadOnlyList<EchosPhenomenon> Detected { get; init; } = Array.Empty<EchosPhenomenon>();

    /// <summary>Disclaimer méthodologique porté par ECHOS, affiché tel quel.</summary>
    public string Disclaimer { get; init; } = string.Empty;
}

/// <summary>Un signal déclencheur : métrique, valeur observée et seuil de la règle.</summary>
public sealed record EchosSignal(string Metric, double Value, double Threshold);

/// <summary>
/// Un phénomène détecté par ECHOS, tel que publié. L'identifiant reste stable
/// (clé de contrat) ; `Label`/`Description` portent la requalification honnête du
/// libellé, et `Signals` la trace exacte du déclenchement (P3).
/// </summary>
public sealed record EchosPhenomenon(
    string Identifier,
    int Occurrences,
    int FirstTick,
    int LastTick,
    string Label = "",
    string Description = "",
    IReadOnlyList<EchosSignal>? Signals = null)
{
    /// <summary>Libellé affiché, à défaut l'identifiant technique.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Label) ? Identifier : Label;

    /// <summary>Signaux déclencheurs, jamais nuls (liste vide si absents).</summary>
    public IReadOnlyList<EchosSignal> Triggers => Signals ?? Array.Empty<EchosSignal>();
}

/// <summary>
/// Réponse de `/api/world` : description de monde publiée au démarrage du run et
/// observation au tick demandé (ADR-007 — vue 2D de l'observation).
/// </summary>
public sealed class EchosWorld
{
    /// <summary>Identifiant du run.</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Tick de l'observation ; -1 si aucune observation d'entités.</summary>
    public int Tick { get; init; } = -1;

    /// <summary>Tick de publication de la description de monde ; -1 si absente.</summary>
    public int WorldTick { get; init; } = -1;

    /// <summary>Description de monde (terrain, obstacles, ressources, régions) ; absente si non publiée.</summary>
    public EchosWorldDescription? Description { get; init; }

    /// <summary>Entités observées au tick retenu.</summary>
    public IReadOnlyList<EchosAgentSnapshot> Agents { get; init; } = Array.Empty<EchosAgentSnapshot>();

    /// <summary>Communautés observées au tick retenu.</summary>
    public IReadOnlyList<EchosGroup> Groups { get; init; } = Array.Empty<EchosGroup>();

    /// <summary>Réserves du tick (position et quantité), publiées telles qu'observées.</summary>
    public IReadOnlyList<EchosTickResource> Resources { get; init; } = Array.Empty<EchosTickResource>();
}

/// <summary>Description statique du monde, publiée une fois dans `world_initialized`.</summary>
public sealed record EchosWorldDescription(
    int Width,
    int Height,
    double CellSize,
    IReadOnlyList<EchosWorldCell> Cells,
    IReadOnlyList<EchosObstacle> Obstacles,
    IReadOnlyList<EchosWorldResource> Resources,
    IReadOnlyList<EchosRegion> Regions);

/// <summary>Cellule de terrain : coordonnées grille, type et praticabilité.</summary>
public sealed record EchosWorldCell(int X, int Y, string TerrainType, bool Walkable);

/// <summary>Obstacle circulaire publié dans la description de monde.</summary>
public sealed record EchosObstacle(string Id, double X, double Y, double Radius);

/// <summary>Ressource initiale publiée dans la description de monde.</summary>
public sealed record EchosWorldResource(string Id, string Kind, double X, double Y, double Quantity);

/// <summary>Région rectangulaire publiée dans la description de monde.</summary>
public sealed record EchosRegion(string Id, int X, int Y, int Width, int Height);

/// <summary>Entité observée au tick de la vue 2D.</summary>
public sealed record EchosAgentSnapshot(
    string Id,
    double X,
    double Y,
    double Energy,
    double Hunger,
    double Thirst,
    string? Action,
    string? Group);

/// <summary>Réserve observée à un tick : type, position, quantité.</summary>
public sealed record EchosTickResource(string Type, double X, double Y, double Quantity, double? Capacity);

/// <summary>Réponse de `/api/trust-graph` : nœuds et arêtes observés.</summary>
public sealed class EchosTrustGraph
{
    /// <summary>Identifiant du run.</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Tick de l'observation ; -1 si aucune.</summary>
    public int Tick { get; init; } = -1;

    /// <summary>Entités du graphe.</summary>
    public IReadOnlyList<EchosTrustNode> Nodes { get; init; } = Array.Empty<EchosTrustNode>();

    /// <summary>Relations de confiance publiées par les entités.</summary>
    public IReadOnlyList<EchosTrustEdge> Edges { get; init; } = Array.Empty<EchosTrustEdge>();
}

/// <summary>Un nœud du graphe de confiance.</summary>
public sealed record EchosTrustNode(
    string Id,
    double? X,
    double? Y,
    double? Energy,
    double? Hunger,
    double? Thirst,
    string? Action,
    string? Group);

/// <summary>Une relation publiée : source, cible et niveau de confiance (0–1).</summary>
public sealed record EchosTrustEdge(string Source, string Target, double? Weight);

/// <summary>Réponse de `/api/runs/{id}` : détail complet pour l'écran de statistiques.</summary>
public sealed class EchosRunDetail
{
    /// <summary>Identifiant du run.</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Graine du run.</summary>
    public long Seed { get; init; }

    /// <summary>Nombre de ticks enregistrés.</summary>
    public int TicksCount { get; init; }

    /// <summary>Premier et dernier tick observés.</summary>
    public int FirstTick { get; init; }

    /// <summary>Dernier tick observé.</summary>
    public int LastTick { get; init; }

    /// <summary>Issue de population publiée.</summary>
    public string Outcome { get; init; } = string.Empty;

    /// <summary>Tick d'extinction, si observé.</summary>
    public int? ExtinctionTick { get; init; }

    /// <summary>Métriques exactes au dernier tick, par moteur puis par métrique.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Metrics { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, double>>();

    /// <summary>Provenance des métriques : `false` = repli neutre, pas une observation.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> Measured { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, bool>>();

    /// <summary>Phénomènes détectés, triés par identifiant.</summary>
    public IReadOnlyList<EchosPhenomenon> Detected { get; init; } = Array.Empty<EchosPhenomenon>();

    /// <summary>Disclaimer méthodologique publié avec les phénomènes.</summary>
    public string Disclaimer { get; init; } = string.Empty;
}

/// <summary>Réponse `/api/beliefs/{id}` + `/api/relationships/{id}` : fiche d'une entité.</summary>
public sealed class EchosAgentProfile
{
    /// <summary>Identifiant de l'entité.</summary>
    public string AgentId { get; init; } = string.Empty;

    /// <summary>Tick de l'observation ; -1 si absente.</summary>
    public int Tick { get; init; } = -1;

    /// <summary>Croyances publiées (sujet, prédicat, valeur, confiance).</summary>
    public IReadOnlyList<EchosBelief> Beliefs { get; init; } = Array.Empty<EchosBelief>();

    /// <summary>Relations de confiance déclarées par l'entité.</summary>
    public IReadOnlyList<EchosTrustRelation> Trust { get; init; } = Array.Empty<EchosTrustRelation>();
}

/// <summary>Une croyance publiée pour une entité.</summary>
public sealed record EchosBelief(string Subject, string Predicate, string Value, double Confidence);

/// <summary>Une relation de confiance déclarée par une entité vers une paire.</summary>
public sealed record EchosTrustRelation(string PeerId, double Trust);

/// <summary>Une trace de décision publiée par ECHOS.</summary>
public sealed record EchosDecision(
    int Tick,
    string AgentId,
    string ChosenAction,
    double Utility,
    string Cause);

/// <summary>Une entrée de la chronologie descriptive avant l'extinction.</summary>
public sealed record EchosEventRow(
    int Tick,
    string Type,
    string? AgentId,
    string? Action,
    string? Cause);

/// <summary>Un type d'événement publié, avec son comptage.</summary>
public sealed record EchosEventKind(string Type, int Count);

/// <summary>
/// Journal d'événements publié, **borné** : `Total` est le nombre réel de lignes
/// du filtre, `Limit` le plafond de la réponse — une troncature n'est jamais
/// présentée comme l'intégralité du journal.
/// </summary>
public sealed record EchosEventFeed
{
    /// <summary>Identifiant du run résolu par ECHOS.</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Nombre réel d'événements correspondant au filtre.</summary>
    public int Total { get; init; }

    /// <summary>Plafond appliqué par le service.</summary>
    public int Limit { get; init; }

    /// <summary>Types disponibles, triés, avec comptage.</summary>
    public IReadOnlyList<EchosEventKind> Types { get; init; } = Array.Empty<EchosEventKind>();

    /// <summary>Événements détaillés (tick, type, entité, action, cause).</summary>
    public IReadOnlyList<EchosEventRow> Events { get; init; } = Array.Empty<EchosEventRow>();
}

// ---------------------------------------------------------------------------
// Catalogue de métriques (P1 — ECHOS publie les définitions, le Launcher
// les restitue : formatage, axes et filtrage uniquement).
// ---------------------------------------------------------------------------

/// <summary>Catalogue versionné des métriques (`/api/metrics/catalog`).</summary>
public sealed class EchosMetricCatalog
{
    /// <summary>Version sémantique du catalogue (toute rupture de formule l'augmente).</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Fiches de métriques, dans l'ordre publié par ECHOS.</summary>
    public IReadOnlyList<EchosMetricDoc> Metrics { get; init; } = Array.Empty<EchosMetricDoc>();

    /// <summary>Catalogue vide (API indisponible) : les noms techniques restent affichables.</summary>
    public static EchosMetricCatalog Empty { get; } = new();
}

/// <summary>Une fiche de métrique : ce qui est observé, calculé, sa portée et sa limite.</summary>
public sealed record EchosMetricDoc(
    string Id,
    string Engine,
    string Label,
    string Unit,
    string Domain,
    string Definition,
    string Calculation,
    string Population,
    string Window,
    string Direction,
    string Status,
    IReadOnlyList<string> States,
    string Warning,
    string Visual,
    IReadOnlyList<string> RenamedFrom)
{
    /// <summary>Texte « que regarder ? » affiché en info-bulle.</summary>
    public string Help =>
        $"{Definition}\nCalcul : {Calculation}\nPopulation : {Population} · Fenêtre : {Window}\n" +
        $"Plage : {Domain} · Unité : {Unit}\nLecture : {Direction}" +
        (string.IsNullOrWhiteSpace(Warning) ? string.Empty : $"\nLimite : {Warning}");

    /// <summary>Statut affiché en clair (« exploratoire » n'est jamais masqué).</summary>
    public string StatusLabel => Status switch
    {
        "exploratory" => "exploratoire",
        "suspended" => "suspendu",
        _ => "mesuré",
    };
}

// ---------------------------------------------------------------------------
// Viabilité (P1/P3) — profil multi-dimensionnel, jamais un score opaque.
// ---------------------------------------------------------------------------

/// <summary>Profil de viabilité d'un run (`/api/runs/{id}/viability`).</summary>
public sealed class EchosViability
{
    /// <summary>Identifiant du run.</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Issue de population publiée (``surviving``/``extinct``).</summary>
    public string Outcome { get; init; } = string.Empty;

    /// <summary>Premier tick observé d'une population nulle.</summary>
    public int? ExtinctionTick { get; init; }

    /// <summary>Population initiale publiée.</summary>
    public int? PopulationInitial { get; init; }

    /// <summary>Population au dernier tick observé.</summary>
    public int? PopulationFinal { get; init; }

    /// <summary>Minimum d'entités vivantes observé.</summary>
    public int? PopulationMinimum { get; init; }

    /// <summary>Série population (tick, vivants).</summary>
    public IReadOnlyList<EchosPoint> PopulationSeries { get; init; } = Array.Empty<EchosPoint>();

    /// <summary>Série énergie moyenne.</summary>
    public IReadOnlyList<EchosPoint> EnergySeries { get; init; } = Array.Empty<EchosPoint>();

    /// <summary>Série faim moyenne.</summary>
    public IReadOnlyList<EchosPoint> HungerSeries { get; init; } = Array.Empty<EchosPoint>();

    /// <summary>Série soif moyenne.</summary>
    public IReadOnlyList<EchosPoint> ThirstSeries { get; init; } = Array.Empty<EchosPoint>();

    /// <summary>Régime des réserves (food), si le schéma le publie.</summary>
    public IReadOnlyList<EchosPoint> FoodSeries { get; init; } = Array.Empty<EchosPoint>();

    /// <summary>Régime des réserves (water), si le schéma le publie.</summary>
    public IReadOnlyList<EchosPoint> WaterSeries { get; init; } = Array.Empty<EchosPoint>();

    /// <summary>Ticks manquants entre le premier et le dernier observé.</summary>
    public int MissingTickCount { get; init; }

    /// <summary>Niveau de conservation configuré pour ce run (``base``/``sampled_details``/``high_fidelity``).</summary>
    public string ConservationLevel { get; init; } = string.Empty;

    /// <summary>
    /// Cadence publiée du contexte ``agents`` (``conservation.sampledDetails.agentContextEvery``),
    /// en ticks — l'âge d'un contexte lu vaut au plus ``AgentContextEvery − 1`` (P3).
    /// ``null`` si la métadonnée n'est pas publiée : la cadence n'est jamais devinée.
    /// </summary>
    public int? AgentContextEvery { get; init; }

    /// <summary>Dernier tick publié par ECHOS pour ce run (méadonnées du run).</summary>
    public int LastTick { get; init; }

    /// <summary>Vrai si le rapport de calibration post-run est disponible.</summary>
    public bool CalibrationAvailable { get; init; }

    /// <summary>Pente d'énergie par tick sur la fenêtre du rapport (vide si absent).</summary>
    public double? EnergySlopePerTick { get; init; }

    /// <summary>Nombre de décisions prises sous faim moyenne > 70.</summary>
    public int? HungryDecisions { get; init; }

    /// <summary>Chronologie descriptive des observations avant l'extinction.</summary>
    public IReadOnlyList<EchosEventRow> ExtinctionChronology { get; init; } = Array.Empty<EchosEventRow>();
}

/// <summary>Un point de série (tick réel, valeur) — jamais interpolé.</summary>
public sealed record EchosPoint(double Tick, double Value);

// ---------------------------------------------------------------------------
// Comparaison d'expériences (P3) — agrégats calculés par ECHOS.
// ---------------------------------------------------------------------------

/// <summary>Synthèse multi-runs (`/api/experiments/summary`).</summary>
public sealed class EchosExperimentSummary
{
    /// <summary>Runs comparés, dans l'ordre demandé.</summary>
    public IReadOnlyList<EchosCompareRun> Runs { get; init; } = Array.Empty<EchosCompareRun>();

    /// <summary>Dispersions descriptives par moteur puis métrique.</summary>
    public IReadOnlyList<EchosCompareMetric> Metrics { get; init; } = Array.Empty<EchosCompareMetric>();

    /// <summary>Remarque de méthode publiée par ECHOS, affichée telle quelle.</summary>
    public string Note { get; init; } = string.Empty;
}

/// <summary>Contexte de contrôle d'un run comparé.</summary>
public sealed record EchosCompareRun(
    string RunId,
    string Version,
    string Seed,
    string Outcome,
    int TicksCount,
    int FirstTick,
    int LastTick,
    int? ExtinctionTick,
    string ConservationLevel);

/// <summary>Valeurs d'une métrique sur les runs comparés + dispersion descriptive.</summary>
public sealed record EchosCompareMetric(
    string Engine,
    string Metric,
    IReadOnlyDictionary<string, double?> Values,
    int RunsObserved,
    double? Min,
    double? Max,
    double? Mean,
    double? Spread);
