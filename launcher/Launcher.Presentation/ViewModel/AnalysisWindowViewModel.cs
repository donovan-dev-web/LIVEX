using System.Collections.ObjectModel;
using System.Globalization;
using Launcher.Application;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Launcher.Presentation.ViewModel;

/// <summary>
/// Vue modèle de la fenêtre d'analyse (USER_INTERFACE.md §9.2, ADR-007).
///
/// **Règle de frontière (ADR-003)** : cette vue *présente* ce qu'ECHOS produit.
/// Elle ne calcule aucune valeur scientifique — pas de moyenne, pas d'écart-type,
/// pas d'indice composite. Séries, dernières valeurs, profil de viabilité, catalogue
/// et comparaison multi-run viennent tels quels de l'API REST. Seules des
/// transformations de **rendu** sont faites, qui ne produisent aucune valeur
/// affichée : mise en forme des nombres, troncature des séries à la position de
/// relecture et **trous de données** — un tick non observé reste vide, jamais
/// interpolé, jamais comblé (P0 : le masque de mesure par tick pilote la courbe).
///
/// Le sondage est déclenché par la fenêtre (horloge d'interface, 1 s) et jamais
/// en tâche de fond concurrente : un relevé à la fois, le suivant étant ignoré
/// tant que le précédent n'a pas rendu la main. La relecture d'un run stocké est
/// une simple fenêtre de lecture sur les mêmes séries : elle ne modifie ni SYNE
/// ni les données d'ECHOS.
/// </summary>
public sealed class AnalysisWindowViewModel : ObservableObject
{
    /// <summary>Nombre maximal de séries affichées simultanément sur la courbe.</summary>
    private const int MaxSeries = 6;

    /// <summary>Relations affichées dans le tableau (bornage d'affichage, pas de calcul).</summary>
    private const int EdgeRowLimit = 200;

    /// <summary>Nombre maximal de runs comparés en même temps (lecture seule, pas de calcul).</summary>
    private const int CompareRunLimit = 6;

    /// <summary>Nombre maximal de classes de l'outil de distribution (borne de rendu).</summary>
    private const int MaxDistributionBins = 12;

    /// <summary>Nombre maximal de marqueurs d'événements dessinés sur la courbe (borne de rendu).</summary>
    private const int MaxEventMarkers = 16;

    /// <summary>Sous-écran « viabilité » — premier écran (P3).</summary>
    public const string SectionViability = "viabilite";

    /// <summary>Sous-écran « vue d'ensemble ».</summary>
    public const string SectionOverview = "ensemble";

    /// <summary>Sous-écran « statistiques exactes ».</summary>
    public const string SectionStatistics = "statistiques";

    /// <summary>Sous-écran « comparer des expériences ».</summary>
    public const string SectionCompare = "comparer";

    /// <summary>Sous-écran « confiance entre entités ».</summary>
    public const string SectionRelations = "relations";

    /// <summary>Sous-écran « monde 2D ».</summary>
    public const string SectionWorld = "monde";

    /// <summary>Sous-écran « fiche d'entité ».</summary>
    public const string SectionAgents = "agents";

    private static readonly SKColor[] Palette =
    [
        new(0x2d, 0x9b, 0xf0),
        new(0xe0, 0xa2, 0x5a),
        new(0x2e, 0xcc, 0x71),
        new(0xa0, 0x4c, 0xf0),
        new(0xe0, 0x52, 0x52),
        new(0x5c, 0xc4, 0xff),
    ];

    private readonly IEchosTelemetrySource _source;
    private readonly object _gate = new();
    private int[] _ticks = [];
    private EchosSeries? _lastSeries;
    private string? _seriesRunId;
    private EchosMetricCatalog _catalog = EchosMetricCatalog.Empty;
    private string _statusText = "Connexion à ECHOS…";
    private string _detailText = string.Empty;
    private string _disclaimerText = string.Empty;
    private string _networkText = string.Empty;
    private string _catalogText = string.Empty;
    private bool _hasData;
    private bool _isBusy;
    private bool _pendingRefresh;
    private bool _isLive = true;
    private int _every = 1;
    private EchosRunOption? _selectedRun;
    private DistributionOption? _selectedDistributionOption;
    private IReadOnlyList<EchosEventRow> _runEvents = [];
    private EchosEventFeed? _eventsFeed;
    private bool _showEventMarkers = true;
    private string _eventsText = string.Empty;
    private IReadOnlyList<EchosGroup> _groups = [];
    private string _selectedSectionId = SectionViability;
    private string _statsHeader = string.Empty;
    private string _relationsText = string.Empty;
    private string _worldText = string.Empty;
    private string _worldTickInput = string.Empty;
    private string _agentHeaderText = string.Empty;
    private IReadOnlyList<EchosTrustNode> _trustNodes = [];
    private IReadOnlyList<EchosTrustEdge> _trustEdges = [];
    private EchosWorldDescription? _worldDescription;
    private IReadOnlyList<EchosAgentSnapshot> _worldAgents = [];
    private IReadOnlyList<EchosTickResource> _worldResources = [];
    private AgentOption? _selectedAgent;
    private string _viabilityHeader = string.Empty;
    private string _viabilityCalibration = string.Empty;
    private string _viabilityNote = string.Empty;
    private string _compareHeader = string.Empty;
    private string _compareNote = string.Empty;
    private int _replayTick;
    private bool _isPlaying;
    private int _replaySpeed = 1;

    /// <summary>Initialise la vue modèle sur la source de télémétrie ECHOS.</summary>
    public AnalysisWindowViewModel(IEchosTelemetrySource source)
    {
        _source = source;
        Runs = new ObservableCollection<EchosRunOption>();
        CompareRuns = new ObservableCollection<CompareRunOption>();
        Metrics = new ObservableCollection<MetricOption>();
        Phenomena = new ObservableCollection<PhenomenonRow>();
        Statistics = new ObservableCollection<StatRow>();
        ViabilityRows = new ObservableCollection<ViabilityRow>();
        ExtinctionRows = new ObservableCollection<ChronologyRow>();
        CompareRunRows = new ObservableCollection<CompareRunRow>();
        CompareMetricRows = new ObservableCollection<CompareMetricRow>();
        TrustEdgeRows = new ObservableCollection<TrustEdgeRow>();
        Agents = new ObservableCollection<AgentOption>();
        AgentBeliefs = new ObservableCollection<BeliefRow>();
        AgentTrust = new ObservableCollection<TrustRelationRow>();
        AgentDecisions = new ObservableCollection<DecisionRow>();
        TrendSeries = Array.Empty<ISeries>();
        TrendXAxes = BuildTickAxes();
        TrendYAxes = BuildValueAxes("valeur publiée");
        DistributionOptions = new ObservableCollection<DistributionOption>();
        DistributionSeries = Array.Empty<ISeries>();
        DistributionXAxes = BuildLabelAxes([]);
        DistributionYAxes = BuildValueAxes("observations publiées");
        DistributionText = "aucune métrique sélectionnée pour l'instant";
        PopulationSeries = Array.Empty<ISeries>();
        PopulationXAxes = BuildTickAxes();
        PopulationYAxes = BuildValueAxes("entités vivantes");
        NeedsSeries = Array.Empty<ISeries>();
        NeedsXAxes = BuildTickAxes();
        NeedsYAxes = BuildValueAxes("valeur publiée");
        ResourceSeries = Array.Empty<ISeries>();
        ResourceXAxes = BuildTickAxes();
        ResourceYAxes = BuildValueAxes("unités publiées");
        RefreshCommand = new RelayCommand<string>(_ => StartRefresh());
        ReplayCommand = new RelayCommand<string>(OnReplayCommand);
    }

    /// <summary>Runs disponibles dans la base analytique.</summary>
    public ObservableCollection<EchosRunOption> Runs { get; }

    /// <summary>Run sélectionné ; sa sélection déclenche un relevé.</summary>
    public EchosRunOption? SelectedRun
    {
        get => _selectedRun;
        set
        {
            // Écritures liées à la reconstruction de la liste (la ComboBox ré-écrit
            // SelectedItem quand ItemsSource change, y compris « null » pendant le
            // vide) : même identité de run ⇒ simple mise à jour, aucun relevé —
            // sinon la liste reconstruite relancerait un relevé en boucle infinie.
            if (value is null)
            {
                if (Runs.Count > 0)
                {
                    // Désélection transitoire alors qu'il reste des runs : ignorée,
                    // une liste de choix ne se vide pas d'un geste utilisateur.
                    return;
                }

                SelectRunSilently(null);
                return;
            }

            if (_selectedRun is not null
                && string.Equals(value.RunId, _selectedRun.RunId, StringComparison.Ordinal))
            {
                SelectRunSilently(value);
                return;
            }

            if (SetProperty(ref _selectedRun, value))
            {
                RequestRefresh();
            }
        }
    }

    /// <summary>
    /// Change le run **sans** demander de relevé : employé par la reconstruction de
    /// la liste pendant un relevé — ce relevé en cours lit déjà la sélection qu'il
    /// vient de poser, un second tour serait une chaîne infinie de relevés.
    /// </summary>
    private void SelectRunSilently(EchosRunOption? value)
    {
        if (!SetProperty(ref _selectedRun, value, nameof(SelectedRun)))
        {
            return;
        }
    }

    /// <summary>Runs proposés pour la comparaison multi-run (lecture seule).</summary>
    public ObservableCollection<CompareRunOption> CompareRuns { get; }

    /// <summary>Métriques découvertes, sélectionnables pour la courbe.</summary>
    public ObservableCollection<MetricOption> Metrics { get; }

    /// <summary>Métriques proposées à l'outil de distribution (séries publiées du relevé).</summary>
    public ObservableCollection<DistributionOption> DistributionOptions { get; }

    /// <summary>Métrique choisie pour l'outil de distribution.</summary>
    public DistributionOption? SelectedDistributionOption
    {
        get => _selectedDistributionOption;
        set
        {
            if (SetProperty(ref _selectedDistributionOption, value))
            {
                RebuildDistribution();
            }
        }
    }

    /// <summary>Colonnes de la distribution (comptages par intervalle).</summary>
    public ISeries[] DistributionSeries { get; private set; }

    /// <summary>Axe des intervalles (libellés de classe, jamais un tick).</summary>
    public Axis[] DistributionXAxes { get; private set; }

    /// <summary>Axe des comptages (« observations publiées »).</summary>
    public Axis[] DistributionYAxes { get; private set; }

    /// <summary>Compte-rendu du comptage : type d'observations, intervalles, n et exclus.</summary>
    public string DistributionText { get; private set; }

    /// <summary>Vrai si les ticks d'événements publiés sont marqués sur la courbe (P3).</summary>
    public bool ShowEventMarkers
    {
        get => _showEventMarkers;
        set
        {
            if (SetProperty(ref _showEventMarkers, value))
            {
                RebuildTrend();
            }
        }
    }

    /// <summary>Compte-rendu des annotations : publié, affiché, types et borne de rendu.</summary>
    public string EventsText
    {
        get => _eventsText;
        private set => SetProperty(ref _eventsText, value);
    }

    /// <summary>Phénomènes émergents détectés sur le run.</summary>
    public ObservableCollection<PhenomenonRow> Phenomena { get; }

    /// <summary>Commande d'actualisation immédiate.</summary>
    public RelayCommand<string> RefreshCommand { get; }

    /// <summary>
    /// Relevé demandé par l'utilisateur (changement de run, de sous-écran, d'entité,
    /// de sélection de comparaison, de tick de relecture, bouton Actualiser).
    /// Si un relevé est en cours, la demande est **rejouée** à la fin de celui-ci :
    /// une sélection pendant un relevé n'est jamais perdue. L'horloge de direct,
    /// elle, appelle <see cref="RefreshAsync"/> qui reste sans effet pendant un
    /// relevé — jamais deux relevés simultanés, jamais de file qui tourne à vide.
    /// </summary>
    private void RequestRefresh()
    {
        lock (_gate)
        {
            if (IsBusy)
            {
                _pendingRefresh = true;
                return;
            }
        }

        _ = RefreshAsync();
    }

    /// <summary>Commande de relecture : play, pause, prev, next, live.</summary>
    public RelayCommand<string> ReplayCommand { get; }

    /// <summary>Vrai si la fenêtre relit ECHOS en continu (1 s).</summary>
    public bool IsLive
    {
        get => _isLive;
        set => SetProperty(ref _isLive, value);
    }

    /// <summary>Pas de sous-échantillonnage des séries (1 = tous les ticks échantillonnés par ECHOS).</summary>
    public int Every
    {
        get => _every;
        set => SetProperty(ref _every, Math.Max(1, value));
    }

    /// <summary>Pas proposés dans la barre d'outils (lecture directe, puis échantillonnée).</summary>
    public IReadOnlyList<int> EveryOptions { get; } = [1, 2, 5, 10];

    /// <summary>Vitesses de relecture proposées (ticks avancés par seconde d'horloge).</summary>
    public IReadOnlyList<int> ReplaySpeedOptions { get; } = [1, 2, 5, 10];

    /// <summary>Sous-écrans proposés par le menu de la fenêtre (USER_INTERFACE.md §9.2).</summary>
    public IReadOnlyList<AnalysisSectionOption> Sections { get; } =
    [
        new(SectionViability, "Viabilité", "Issue, population, besoins, ressources, complétude"),
        new(SectionOverview, "Comportements", "Courbes par tick, communautés, phénomènes détectés"),
        new(SectionStatistics, "Statistiques exactes", "Valeurs publiées par ECHOS, sans calcul local"),
        new(SectionCompare, "Comparer", "Contexte des runs et dispersion publiée"),
        new(SectionRelations, "Relations et groupes", "Confiance entre entités et communautés"),
        new(SectionWorld, "Monde et territoires", "Terrain, ressources, entités, régions"),
        new(SectionAgents, "Entités", "Fiche détaillée d'une entité"),
    ];

    /// <summary>Sous-écran affiché ; son changement déclenche un relevé ciblé.</summary>
    public string SelectedSectionId
    {
        get => _selectedSectionId;
        set
        {
            if (SetProperty(ref _selectedSectionId, value))
            {
                OnPropertyChanged(nameof(SelectedSection));
                RequestRefresh();
            }
        }
    }

    /// <summary>Sous-écran sélectionné dans le menu (liaison aller-retour).</summary>
    public AnalysisSectionOption? SelectedSection
    {
        get => Sections.FirstOrDefault(option => string.Equals(option.Id, _selectedSectionId, StringComparison.Ordinal));
        set
        {
            if (value is not null && !string.Equals(value.Id, _selectedSectionId, StringComparison.Ordinal))
            {
                SelectedSectionId = value.Id;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Viabilité (P1/P3) — profil multi-dimensionnel publié par ECHOS.
    // -----------------------------------------------------------------------

    /// <summary>En-tête de la viabilité : run, issue publiée et extinction éventuelle.</summary>
    public string ViabilityHeader
    {
        get => _viabilityHeader;
        private set => SetProperty(ref _viabilityHeader, value);
    }

    /// <summary>Faits publiés par ECHOS (population, complétude, conservation…), sans calcul local.</summary>
    public ObservableCollection<ViabilityRow> ViabilityRows { get; }

    /// <summary>Mesures du rapport de calibration post-run, signalées comme telles.</summary>
    public string ViabilityCalibration
    {
        get => _viabilityCalibration;
        private set => SetProperty(ref _viabilityCalibration, value);
    }

    /// <summary>Note de lecture prudente affichée sous le profil de viabilité.</summary>
    public string ViabilityNote
    {
        get => _viabilityNote;
        private set => SetProperty(ref _viabilityNote, value);
    }

    /// <summary>Chronologie descriptive des observations avant extinction, publiée telle quelle.</summary>
    public ObservableCollection<ChronologyRow> ExtinctionRows { get; }

    /// <summary>Série de population (panneau par unité, jamais mélangée aux autres).</summary>
    public ISeries[] PopulationSeries { get; private set; }

    /// <summary>Axe des ticks du panneau population.</summary>
    public Axis[] PopulationXAxes { get; private set; }

    /// <summary>Axe nommé du panneau population.</summary>
    public Axis[] PopulationYAxes { get; private set; }

    /// <summary>Séries de besoins (énergie, faim, soif — même unité, même panneau).</summary>
    public ISeries[] NeedsSeries { get; private set; }

    /// <summary>Axe des ticks du panneau besoins.</summary>
    public Axis[] NeedsXAxes { get; private set; }

    /// <summary>Axe nommé du panneau besoins.</summary>
    public Axis[] NeedsYAxes { get; private set; }

    /// <summary>Séries de réserves (nourriture, eau — même unité, même panneau).</summary>
    public ISeries[] ResourceSeries { get; private set; }

    /// <summary>Axe des ticks du panneau ressources.</summary>
    public Axis[] ResourceXAxes { get; private set; }

    /// <summary>Axe nommé du panneau ressources.</summary>
    public Axis[] ResourceYAxes { get; private set; }

    // -----------------------------------------------------------------------
    // Comparaison multi-run (P3) — agrégats calculés par ECHOS, jamais ici.
    // -----------------------------------------------------------------------

    /// <summary>En-tête de la comparaison : nombre de runs retenus.</summary>
    public string CompareHeader
    {
        get => _compareHeader;
        private set => SetProperty(ref _compareHeader, value);
    }

    /// <summary>Remarque de méthode publiée par ECHOS, affichée telle quelle.</summary>
    public string CompareNote
    {
        get => _compareNote;
        private set => SetProperty(ref _compareNote, value);
    }

    /// <summary>Contexte de contrôle des runs comparés (version, graine, issue…).</summary>
    public ObservableCollection<CompareRunRow> CompareRunRows { get; }

    /// <summary>Valeurs publiées par run et dispersion descriptive publiée par ECHOS.</summary>
    public ObservableCollection<CompareMetricRow> CompareMetricRows { get; }

    // -----------------------------------------------------------------------
    // Relecture (P3) — fenêtre de lecture sur les séries déjà publiées.
    // -----------------------------------------------------------------------

    /// <summary>Tick de relecture courant (borne basse de la fenêtre de lecture).</summary>
    public double ReplayPosition
    {
        get => _ticks.Length == 0 ? 0 : Math.Clamp(_replayTick, _ticks[0], _ticks[^1]);
        set
        {
            if (_ticks.Length == 0)
            {
                return;
            }

            SetReplayTick((int)Math.Round(value));
        }
    }

    /// <summary>Premier tick publié (borne du curseur de relecture).</summary>
    public double ReplayMinimum => _ticks.Length == 0 ? 0 : _ticks[0];

    /// <summary>Dernier tick publié (borne du curseur de relecture).</summary>
    public double ReplayMaximum => _ticks.Length == 0 ? 0 : _ticks[^1];

    /// <summary>Vrai si le curseur est revenu au direct (dernier tick publié).</summary>
    public bool IsAtLive => _ticks.Length == 0 || _replayTick >= _ticks[^1];

    /// <summary>Vrai pendant la lecture automatique de la relecture.</summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    /// <summary>Vitesse de relecture : ticks avancés à chaque seconde d'horloge.</summary>
    public int ReplaySpeed
    {
        get => _replaySpeed;
        set => SetProperty(ref _replaySpeed, Math.Max(1, value));
    }

    /// <summary>Compte-rendu de la relecture : tick courant, dernier tick publié.</summary>
    public string ReplayText => _ticks.Length == 0
        ? "aucun tick publié pour ce run"
        : IsAtLive
            ? $"direct — dernier tick {_ticks[^1]}"
            : $"relecture — tick {_replayTick} / {_ticks[^1]}";

    // -----------------------------------------------------------------------
    // Courbe temporelle (ticks réels, trous visibles).
    // -----------------------------------------------------------------------

    /// <summary>Séries de la courbe temporelle (un point par tick publié, trou = null).</summary>
    public ISeries[] TrendSeries { get; private set; }

    /// <summary>Axe des ticks réels de la courbe temporelle.</summary>
    public Axis[] TrendXAxes { get; private set; }

    /// <summary>Axe des valeurs de la courbe temporelle.</summary>
    public Axis[] TrendYAxes { get; private set; }

    /// <summary>Communautés observées, rendues par le contrôle de graphe.</summary>
    public IReadOnlyList<EchosGroup> Groups
    {
        get => _groups;
        private set => SetProperty(ref _groups, value);
    }

    /// <summary>Catalogue versionné des métriques (définitions, unités, statuts).</summary>
    public EchosMetricCatalog Catalog
    {
        get => _catalog;
        private set => SetProperty(ref _catalog, value);
    }

    /// <summary>État du catalogue : version publiée, ou absence explicite.</summary>
    public string CatalogText
    {
        get => _catalogText;
        private set => SetProperty(ref _catalogText, value);
    }

    /// <summary>Statistiques exactes au dernier tick, par moteur (provenance comprise).</summary>
    public ObservableCollection<StatRow> Statistics { get; }

    /// <summary>En-tête des statistiques : métadonnées exactes du run sélectionné.</summary>
    public string StatsHeader
    {
        get => _statsHeader;
        private set => SetProperty(ref _statsHeader, value);
    }

    /// <summary>Compte-rendu du graphe de confiance : tick, nœuds, arêtes publiés.</summary>
    public string RelationsText
    {
        get => _relationsText;
        private set => SetProperty(ref _relationsText, value);
    }

    /// <summary>Nœuds du graphe de confiance observé.</summary>
    public IReadOnlyList<EchosTrustNode> TrustNodes
    {
        get => _trustNodes;
        private set => SetProperty(ref _trustNodes, value);
    }

    /// <summary>Relations de confiance observées.</summary>
    public IReadOnlyList<EchosTrustEdge> TrustEdges
    {
        get => _trustEdges;
        private set
        {
            if (SetProperty(ref _trustEdges, value))
            {
                RebuildTrustEdgeRows(value);
            }
        }
    }

    /// <summary>Relations mises en forme pour le tableau du sous-écran confiance.</summary>
    public ObservableCollection<TrustEdgeRow> TrustEdgeRows { get; }

    /// <summary>Description statique du monde (terrain, obstacles, ressources, régions).</summary>
    public EchosWorldDescription? WorldDescription
    {
        get => _worldDescription;
        private set => SetProperty(ref _worldDescription, value);
    }

    /// <summary>Entités observées pour la vue 2D et la liste des fiches.</summary>
    public IReadOnlyList<EchosAgentSnapshot> WorldAgents
    {
        get => _worldAgents;
        private set => SetProperty(ref _worldAgents, value);
    }

    /// <summary>Réserves observées au tick de la vue 2D.</summary>
    public IReadOnlyList<EchosTickResource> WorldResources
    {
        get => _worldResources;
        private set => SetProperty(ref _worldResources, value);
    }

    /// <summary>Compte-rendu de la vue 2D : tick, entités, réserves, monde.</summary>
    public string WorldText
    {
        get => _worldText;
        private set => SetProperty(ref _worldText, value);
    }

    /// <summary>Tick demandé pour la vue 2D ; vide ou invalide = dernier observé.</summary>
    public string WorldTickInput
    {
        get => _worldTickInput;
        set => SetProperty(ref _worldTickInput, value ?? string.Empty);
    }

    /// <summary>Entités proposées dans la liste des fiches.</summary>
    public ObservableCollection<AgentOption> Agents { get; }

    /// <summary>Entité dont la fiche est affichée ; sa sélection déclenche un relevé.</summary>
    public AgentOption? SelectedAgent
    {
        get => _selectedAgent;
        set
        {
            // Écritures liées à la reconstruction de la liste (la ListBox ré-écrit
            // SelectedItem quand ItemsSource change, y compris « null » pendant le
            // vide) : même entité ou désélection transitoire ⇒ simple mise à jour,
            // aucun relevé — sinon la liste reconstruite enchaînerait les relevés.
            if (value is null)
            {
                if (Agents.Count > 0)
                {
                    // Désélection transitoire alors qu'il reste des entités : ignorée.
                    return;
                }

                SelectAgentSilently(null);
                return;
            }

            if (_selectedAgent is not null
                && string.Equals(value.Id, _selectedAgent.Id, StringComparison.Ordinal))
            {
                SelectAgentSilently(value);
                return;
            }

            if (SetProperty(ref _selectedAgent, value))
            {
                OnPropertyChanged(nameof(AgentHeaderText));
                RequestRefresh();
            }
        }
    }

    /// <summary>
    /// Pose l'entité observée pendant un relevé sans demander de relevé : ce relevé
    /// lit la fiche juste après la reconstruction de la liste.
    /// </summary>
    private void SelectAgentSilently(AgentOption? value)
    {
        if (!SetProperty(ref _selectedAgent, value, nameof(SelectedAgent)))
        {
            return;
        }

        OnPropertyChanged(nameof(AgentHeaderText));
    }

    /// <summary>En-tête de la fiche : entité, tick d'observation, groupe, état.</summary>
    public string AgentHeaderText
    {
        get => _agentHeaderText;
        private set => SetProperty(ref _agentHeaderText, value);
    }

    /// <summary>Croyances publiées pour l'entité sélectionnée.</summary>
    public ObservableCollection<BeliefRow> AgentBeliefs { get; }

    /// <summary>Relations de confiance déclarées par l'entité sélectionnée.</summary>
    public ObservableCollection<TrustRelationRow> AgentTrust { get; }

    /// <summary>Dernières décisions publiées pour l'entité sélectionnée.</summary>
    public ObservableCollection<DecisionRow> AgentDecisions { get; }

    /// <summary>Compte-rendu factuel du dernier relevé : source, run, état.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Détail du run : ticks, dernier tick, lacunes, pas de lecture.</summary>
    public string DetailText
    {
        get => _detailText;
        private set => SetProperty(ref _detailText, value);
    }

    /// <summary>Disclaimer méthodologique publié par ECHOS, affiché tel quel.</summary>
    public string DisclaimerText
    {
        get => _disclaimerText;
        private set => SetProperty(ref _disclaimerText, value);
    }

    /// <summary>État du graphe de communautés : tick de l'observation et nombre de groupes.</summary>
    public string NetworkText
    {
        get => _networkText;
        private set => SetProperty(ref _networkText, value);
    }

    /// <summary>Vrai si un relevé a produit des données à afficher.</summary>
    public bool HasData
    {
        get => _hasData;
        private set => SetProperty(ref _hasData, value);
    }

    /// <summary>Vrai pendant un relevé : les suivants sont ignorés, jamais empilés.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>
    /// Vrai pendant l'ouverture du sélecteur de run : l'horloge de direct diffère le
    /// relevé pour que la liste ne se reconstruisse pas sous le doigt de l'utilisateur
    /// (reconstruire la liste referme la liste déroulante).
    /// </summary>
    public bool IsRunSelectorOpen { get; set; }

    /// <summary>
    /// Relevé complet : liste des runs, séries du run sélectionné, réseau, phénomènes.
    /// Un échec est **affiché**, jamais comblé (ADR-003 : pas d'approximation locale).
    /// </summary>
    public async Task RefreshAsync()
    {
        lock (_gate)
        {
            if (IsBusy)
            {
                // Un seul relevé à la fois : l'horloge de direct est sans effet pendant
                // un relevé en cours (jamais empilé). Les demandes explicites de
                // l'utilisateur passent par RequestRefresh, qui les rejoue à la fin.
                return;
            }

            IsBusy = true;
        }

        try
        {
            var runs = await _source.ReadRunsAsync(CancellationToken.None);
            SyncRuns(runs);
            await EnsureCatalogAsync();

            if (SelectedRun is null)
            {
                ApplyEmpty("aucun run enregistré dans la base analytique");
                return;
            }

            var runId = SelectedRun.RunId;

            // Les séries sont relues à chaque relevé : le curseur de relecture et le
            // compte-rendu de ticks restent disponibles sur tous les sous-écrans.
            var series = await _source.ReadSeriesAsync(runId, Every, CancellationToken.None);
            ApplySeries(series);

            switch (SelectedSectionId)
            {
                case SectionViability:
                    await RefreshViabilityAsync(runId);
                    break;
                case SectionStatistics:
                    await RefreshStatisticsAsync(runId);
                    break;
                case SectionCompare:
                    await RefreshCompareAsync();
                    break;
                case SectionRelations:
                    await RefreshRelationsAsync(runId);
                    break;
                case SectionWorld:
                    await RefreshWorldAsync(runId);
                    break;
                case SectionAgents:
                    await RefreshWorldAsync(runId);
                    await RefreshAgentAsync(runId);
                    break;
                default:
                    await RefreshOverviewAsync(runId);
                    break;
            }
        }
        catch (Exception exception)
        {
            StatusText = $"ECHOS indisponible — {exception.Message}";
            HasData = false;
        }
        finally
        {
            bool restart;
            lock (_gate)
            {
                IsBusy = false;
                restart = _pendingRefresh;
                _pendingRefresh = false;
            }

            if (restart)
            {
                _ = RefreshAsync();
            }
        }
    }

    /// <summary>Avance d'un pas de relecture ; appelée par l'horloge d'interface (1 s).</summary>
    public void AdvanceReplay()
    {
        if (!IsPlaying || _ticks.Length == 0)
        {
            return;
        }

        var index = ReplayIndex + ReplaySpeed;
        if (index >= _ticks.Length - 1)
        {
            ReplayPosition = _ticks[^1];
            IsPlaying = false;
        }
        else
        {
            ReplayPosition = _ticks[index];
        }
    }

    /// <summary>
    /// Catalogue des métriques : lu une fois, relu tant qu'il reste vide. Une
    /// indisponibilité est **affichée** (les noms techniques restent lisibles),
    /// jamais simulée.
    /// </summary>
    private async Task EnsureCatalogAsync()
    {
        if (Catalog.Metrics.Count > 0)
        {
            return;
        }

        try
        {
            var catalog = await _source.ReadCatalogAsync(CancellationToken.None);
            Catalog = catalog;
            CatalogText = catalog.Metrics.Count == 0
                ? "catalogue des métriques vide — seuls les noms techniques sont affichés"
                : $"catalogue {catalog.Version} · {catalog.Metrics.Count} fiche(s)";
        }
        catch (Exception exception)
        {
            Catalog = EchosMetricCatalog.Empty;
            CatalogText = $"catalogue des métriques indisponible — {exception.Message}";
        }
    }

    /// <summary>Viabilité : issue, population, besoins, ressources, complétude (P1/P3).</summary>
    private async Task RefreshViabilityAsync(string runId)
    {
        var viability = await _source.ReadViabilityAsync(runId, Every, CancellationToken.None);

        ViabilityHeader = viability.RunId +
            $" · issue publiée : {DescribeOutcome(viability.Outcome)}" +
            (viability.ExtinctionTick is { } extinction ? $" · extinction au tick {extinction}" : string.Empty);

        ViabilityRows.Clear();
        ViabilityRows.Add(new ViabilityRow("Issue publiée", viability.Outcome, "statut publié par ECHOS"));
        ViabilityRows.Add(new ViabilityRow(
            "État du run",
            DescribeRunState(viability),
            "issue + complétude publiées ; ECHOS ne distingue pas « en cours » d'« interrompu »"));
        ViabilityRows.Add(new ViabilityRow(
            "Population initiale",
            FormatNumber(viability.PopulationInitial),
            "entités vivantes au premier tick"));
        ViabilityRows.Add(new ViabilityRow(
            "Population finale",
            FormatNumber(viability.PopulationFinal),
            "entités vivantes au dernier tick observé"));
        ViabilityRows.Add(new ViabilityRow(
            "Minimum observé",
            FormatNumber(viability.PopulationMinimum),
            "plus bas niveau de population observé"));
        ViabilityRows.Add(new ViabilityRow(
            "Premier tick à population nulle",
            FormatNumber(viability.ExtinctionTick),
            "absent : aucune extinction observée"));
        ViabilityRows.Add(new ViabilityRow(
            "Ticks manquants",
            FormatNumber(viability.MissingTickCount),
            "lacunes de l'archive entre le premier et le dernier tick"));
        ViabilityRows.Add(new ViabilityRow(
            "Niveau de conservation",
            string.IsNullOrWhiteSpace(viability.ConservationLevel) ? "—" : viability.ConservationLevel,
            "métadonnée persistée par le pipeline ECHOS"));

        if (viability.CalibrationAvailable)
        {
            ViabilityCalibration = "Rapport de calibration post-run disponible (mesures ci-dessous).";
            if (viability.EnergySlopePerTick is { } slope)
            {
                ViabilityRows.Add(new ViabilityRow(
                    "Pente d'énergie par tick",
                    FormatNumber(slope),
                    "rapport post-run, fenêtre de calibration publiée"));
            }

            if (viability.HungryDecisions is { } hungry)
            {
                ViabilityRows.Add(new ViabilityRow(
                    "Décisions sous faim > 70",
                    FormatNumber(hungry),
                    "rapport post-run, comptage publié par ECHOS"));
            }
        }
        else
        {
            ViabilityCalibration = "Rapport de calibration post-run indisponible pour ce run.";
        }

        ViabilityNote = "Profil multi-dimensionnel : aucune de ces valeurs n'est agrégée en un score " +
            "dans cette fenêtre. Absence d'une ligne = donnée non publiée, jamais valeur nulle.";

        ExtinctionRows.Clear();
        foreach (var row in viability.ExtinctionChronology)
        {
            ExtinctionRows.Add(new ChronologyRow(
                row.Tick,
                row.Type,
                row.AgentId ?? "—",
                row.Action ?? "—",
                row.Cause ?? "—"));
        }

        PopulationSeries = BuildPointSeries("Population", viability.PopulationSeries, Palette[2]);
        NeedsSeries =
        [
            .. BuildPointSeries("Énergie", viability.EnergySeries, Palette[0]),
            .. BuildPointSeries("Faim", viability.HungerSeries, Palette[4]),
            .. BuildPointSeries("Soif", viability.ThirstSeries, Palette[5]),
        ];
        ResourceSeries =
        [
            .. BuildPointSeries("Nourriture", viability.FoodSeries, Palette[1]),
            .. BuildPointSeries("Eau", viability.WaterSeries, Palette[3]),
        ];

        StatusText = $"ECHOS · {viability.RunId} · relevé à " +
            DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        HasData = ViabilityRows.Count > 0;
        RaiseChartsChanged();
    }

    /// <summary>
    /// Vue d'ensemble : communautés et phénomènes, tels que publiés (ADR-003).
    /// Les séries ont déjà été lues et appliquées par le relevé courant.
    /// </summary>
    private async Task RefreshOverviewAsync(string runId)
    {
        var network = await _source.ReadNetworkAsync(runId, CancellationToken.None);
        Groups = network.Groups;
        NetworkText = network.Tick >= 0
            ? $"{network.Groups.Count} communauté(s) observée(s) au tick {network.Tick}"
            : "aucune observation de groupes pour ce run";

        var phenomena = await _source.ReadPhenomenaAsync(runId, CancellationToken.None);
        Phenomena.Clear();
        foreach (var phenomenon in phenomena.Detected)
        {
            Phenomena.Add(new PhenomenonRow(
                phenomenon.Identifier,
                phenomenon.DisplayName,
                phenomenon.Description,
                phenomenon.Occurrences,
                phenomenon.FirstTick,
                phenomenon.LastTick,
                DescribeTriggers(phenomenon)));
        }

        DisclaimerText = phenomena.Disclaimer;
        await RefreshEventsAsync(runId);
        StatusText = $"ECHOS · {runId} · relevé à " +
            DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        HasData = true;
    }

    /// <summary>Statistiques exactes : métadonnées, métriques et provenance publiées.</summary>
    private async Task RefreshStatisticsAsync(string runId)
    {
        var detail = await _source.ReadRunDetailAsync(runId, CancellationToken.None);
        Statistics.Clear();
        foreach (var (engine, metrics) in detail.Metrics)
        {
            foreach (var (metric, value) in metrics)
            {
                var measured = !detail.Measured.TryGetValue(engine, out var flags)
                    || !flags.TryGetValue(metric, out var flag)
                    || flag;
                Statistics.Add(new StatRow(
                    engine,
                    metric,
                    FormatNumber(value),
                    measured,
                    LookupStatus(engine, metric),
                    LookupHelp(engine, metric)));
            }
        }

        StatsHeader = detail.RunId + $" · graine {detail.Seed} · {detail.TicksCount} tick(s)" +
            $" · {detail.Outcome}" +
            (detail.ExtinctionTick is { } extinction ? $" · extinction au tick {extinction}" : string.Empty);
        DisclaimerText = detail.Disclaimer;
        StatusText = $"ECHOS · {detail.RunId} · relevé à " +
            DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        HasData = Statistics.Count > 0;
    }

    /// <summary>
    /// Comparaison multi-run : contexte de contrôle et dispersion publiés par ECHOS.
    /// Aucun agrégat n'est calculé dans cette fenêtre.
    /// </summary>
    private async Task RefreshCompareAsync()
    {
        var runIds = CompareRuns
            .Where(option => option.IsSelected)
            .Select(option => option.RunId)
            .Take(CompareRunLimit)
            .ToList();

        CompareRunRows.Clear();
        CompareMetricRows.Clear();
        if (runIds.Count < 2)
        {
            CompareHeader = "sélectionnez au moins deux runs dans la liste";
            CompareNote = string.Empty;
            StatusText = "comparaison indisponible — moins de deux runs sélectionnés";
            HasData = false;
            return;
        }

        var summary = await _source.ReadExperimentSummaryAsync(runIds, CancellationToken.None);
        CompareHeader = $"{summary.Runs.Count} run(s) comparé(s) · dispersion publiée par ECHOS";
        CompareNote = summary.Note;

        foreach (var run in summary.Runs)
        {
            CompareRunRows.Add(new CompareRunRow(
                run.RunId,
                string.IsNullOrWhiteSpace(run.Version) ? "—" : run.Version,
                string.IsNullOrWhiteSpace(run.Seed) ? "—" : run.Seed,
                run.Outcome,
                $"{run.FirstTick} → {run.LastTick} ({run.TicksCount})",
                FormatNumber(run.ExtinctionTick),
                string.IsNullOrWhiteSpace(run.ConservationLevel) ? "—" : run.ConservationLevel));
        }

        foreach (var metric in summary.Metrics)
        {
            var values = string.Join(" · ", metric.Values.Select(pair =>
                $"{pair.Key} = {FormatNumber(pair.Value)}"));
            CompareMetricRows.Add(new CompareMetricRow(
                metric.Engine,
                metric.Metric,
                values,
                metric.RunsObserved,
                FormatNumber(metric.Min),
                FormatNumber(metric.Max),
                FormatNumber(metric.Mean),
                FormatNumber(metric.Spread)));
        }

        StatusText = $"ECHOS · comparaison de {summary.Runs.Count} run(s) · relevé à " +
            DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        HasData = CompareRunRows.Count > 0;
    }

    /// <summary>Confiance et communautés : graphe publié, communautés observées.</summary>
    private async Task RefreshRelationsAsync(string runId)
    {
        // Le curseur de relecture pilote le graphe : au direct, le tick courant du
        // flux ; en relecture, le tick affiché — l'évolution des relations sociales
        // se visualise ainsi tick par tick, sans attente la fin du run.
        var graph = await _source.ReadTrustGraphAsync(runId, ViewTick, CancellationToken.None);
        TrustNodes = graph.Nodes;
        TrustEdges = graph.Edges;
        RelationsText = graph.Tick >= 0
            ? $"{graph.Nodes.Count} entité(s), {graph.Edges.Count} relation(s) publiée(s) au tick {graph.Tick}"
            : "aucune observation d'entités pour ce run";

        var network = await _source.ReadNetworkAsync(runId, CancellationToken.None);
        Groups = network.Groups;
        NetworkText = network.Tick >= 0
            ? $"{network.Groups.Count} communauté(s) observée(s) au tick {network.Tick}"
            : "aucune observation de groupes pour ce run";

        StatusText = $"ECHOS · {runId} · relevé à " +
            DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        HasData = graph.Nodes.Count > 0;
    }

    /// <summary>Monde 2D : description, entités et réserves au tick demandé.</summary>
    private async Task RefreshWorldAsync(string runId)
    {
        int? requested = int.TryParse(
                WorldTickInput.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tick) && tick >= 0
            ? tick
            // Sinon le curseur de relecture : la vue 2D montre le tick affiché,
            // pas seulement le dernier publié (positions visibles à chaque tick).
            : ViewTick;
        var world = await _source.ReadWorldAsync(runId, requested, CancellationToken.None);
        WorldDescription = world.Description;
        WorldAgents = world.Agents;
        WorldResources = world.Resources;
        WorldText = world.Tick < 0
            ? "aucune observation d'entités pour ce run"
            : $"{world.Agents.Count} entité(s), {world.Resources.Count} réserve(s) au tick {world.Tick}" +
                (world.Description is { } description
                    ? $" · monde {description.Width}×{description.Height}"
                    : " · description de monde indisponible");
        RebuildAgentRows(world.Agents);
        StatusText = $"ECHOS · {runId} · relevé à " +
            DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        HasData = world.Tick >= 0;
    }

    /// <summary>Fiche d'entité : croyances, relations et dernières décisions publiées.</summary>
    private async Task RefreshAgentAsync(string runId)
    {
        if (SelectedAgent is not { } agent)
        {
            AgentHeaderText = "aucune entité sélectionnée";
            AgentBeliefs.Clear();
            AgentTrust.Clear();
            AgentDecisions.Clear();
            return;
        }

        try
        {
            var profile = await _source.ReadAgentProfileAsync(runId, agent.Id, CancellationToken.None);
            AgentBeliefs.Clear();
            foreach (var belief in profile.Beliefs)
            {
                AgentBeliefs.Add(new BeliefRow(
                    belief.Subject, belief.Predicate, belief.Value, FormatNumber(belief.Confidence)));
            }

            AgentTrust.Clear();
            foreach (var relation in profile.Trust)
            {
                AgentTrust.Add(new TrustRelationRow(relation.PeerId, FormatNumber(relation.Trust)));
            }

            var decisions = await _source.ReadDecisionsAsync(runId, CancellationToken.None);
            AgentDecisions.Clear();
            foreach (var decision in decisions
                         .Where(candidate => string.Equals(candidate.AgentId, agent.Id, StringComparison.Ordinal))
                         .TakeLast(12)
                         .Reverse())
            {
                AgentDecisions.Add(new DecisionRow(
                    decision.Tick, decision.ChosenAction, FormatNumber(decision.Utility), decision.Cause));
            }

            AgentHeaderText = $"Entité {agent.Id}" +
                (profile.Tick >= 0 ? $" · contexte lu au tick {profile.Tick}" : string.Empty) +
                DescribeContextAge(runId, profile.Tick) +
                (string.IsNullOrWhiteSpace(agent.Group) ? string.Empty : $" · groupe {agent.Group}") +
                $" · énergie {FormatNumber(agent.Energy)}" +
                (string.IsNullOrWhiteSpace(agent.Action) ? string.Empty : $" · {agent.Action}");
        }
        catch (Exception exception)
        {
            // Une fiche indisponible n'invalide pas le reste du relevé : l'absence est affichée.
            AgentHeaderText = $"ECHOS indisponible — {exception.Message}";
            AgentBeliefs.Clear();
            AgentTrust.Clear();
            AgentDecisions.Clear();
        }
    }

    /// <summary>Reconstruit la liste des fiches depuis l'observation de monde.</summary>
    private void RebuildAgentRows(IReadOnlyList<EchosAgentSnapshot> agents)
    {
        var previous = SelectedAgent?.Id;
        Agents.Clear();
        foreach (var agent in agents)
        {
            Agents.Add(new AgentOption(agent.Id, agent.X, agent.Y, agent.Energy, agent.Action, agent.Group));
        }

        // Sélection silencieuse (voir SelectAgentSilently) : la liste est reconstruite
        // à chaque relevé, une sélection qui relancerait un relevé en chaînerait.
        SelectAgentSilently(
            Agents.FirstOrDefault(option => option.Id == previous) ?? Agents.FirstOrDefault());
    }

    /// <summary>Met en forme les relations publiées pour le tableau (rendu seul).</summary>
    private void RebuildTrustEdgeRows(IReadOnlyList<EchosTrustEdge> edges)
    {
        TrustEdgeRows.Clear();
        foreach (var edge in edges.Take(EdgeRowLimit))
        {
            TrustEdgeRows.Add(new TrustEdgeRow(
                edge.Source,
                edge.Target,
                edge.Weight is { } weight ? FormatNumber(weight) : "—"));
        }
    }

    private void SyncRuns(IReadOnlyList<EchosRunSummary> runs)
    {
        var known = Runs.Select(option => option.RunId).ToHashSet(StringComparer.Ordinal);
        if (runs.Count == Runs.Count && runs.All(run => known.Contains(run.RunId)))
        {
            foreach (var option in Runs)
            {
                option.Update(runs.First(run => run.RunId == option.RunId));
            }

            SyncCompareRuns();
            return;
        }

        var previous = SelectedRun?.RunId;
        Runs.Clear();
        foreach (var run in runs.OrderByDescending(run => run.LastTick))
        {
            Runs.Add(new EchosRunOption(run));
        }

        RebuildCompareRuns();
        // Sélection silencieuse : le relevé en cours (celui qui reconstruit la liste)
        // lit cette sélection juste après — redemander un relevé enchaînerait les tours.
        SelectRunSilently(Runs.FirstOrDefault(option => option.RunId == previous) ?? Runs.FirstOrDefault());
    }

    /// <summary>Reconstruit la sélection de comparaison en conservant les coches existantes.</summary>
    private void RebuildCompareRuns()
    {
        var previous = CompareRuns
            .Where(option => option.IsSelected)
            .Select(option => option.RunId)
            .ToHashSet(StringComparer.Ordinal);
        CompareRuns.Clear();
        for (var index = 0; index < Runs.Count; index++)
        {
            var run = Runs[index];
            var option = new CompareRunOption(run.RunId, run.Label)
            {
                // Premier relevé : les trois runs les plus récents sont présélectionnés.
                IsSelected = previous.Count == 0
                    ? index < Math.Min(3, Runs.Count)
                    : previous.Contains(run.RunId),
            };
            option.PropertyChanged += (_, _) => OnCompareSelectionChanged();
            CompareRuns.Add(option);
        }
    }

    /// <summary>Met à jour les options de comparaison sans changer la coche de l'utilisateur.</summary>
    private void SyncCompareRuns()
    {
        if (CompareRuns.Count != Runs.Count
            || CompareRuns.Select(option => option.RunId)
                .Intersect(Runs.Select(run => run.RunId), StringComparer.Ordinal)
                .Count() != Runs.Count)
        {
            RebuildCompareRuns();
            return;
        }

        for (var index = 0; index < Runs.Count; index++)
        {
            CompareRuns[index].Label = Runs[index].Label;
        }
    }

    private void OnCompareSelectionChanged()
    {
        if (string.Equals(SelectedSectionId, SectionCompare, StringComparison.Ordinal))
        {
            RequestRefresh();
        }
    }

    private void ApplyEmpty(string reason)
    {
        TrendSeries = Array.Empty<ISeries>();
        _lastSeries = null;
        _ticks = [];
        _seriesRunId = null;
        _replayTick = 0;
        Metrics.Clear();
        DistributionOptions.Clear();
        SelectedDistributionOption = null;
        DistributionText = "aucune métrique sélectionnée pour l'instant";
        _runEvents = [];
        _eventsFeed = null;
        EventsText = string.Empty;
        Phenomena.Clear();
        Groups = [];
        DetailText = string.Empty;
        NetworkText = string.Empty;
        Statistics.Clear();
        StatsHeader = string.Empty;
        ViabilityRows.Clear();
        ExtinctionRows.Clear();
        ViabilityHeader = string.Empty;
        ViabilityCalibration = string.Empty;
        ViabilityNote = string.Empty;
        PopulationSeries = Array.Empty<ISeries>();
        NeedsSeries = Array.Empty<ISeries>();
        ResourceSeries = Array.Empty<ISeries>();
        CompareRunRows.Clear();
        CompareMetricRows.Clear();
        CompareHeader = string.Empty;
        CompareNote = string.Empty;
        TrustNodes = [];
        TrustEdges = [];
        RelationsText = string.Empty;
        WorldDescription = null;
        WorldAgents = [];
        WorldResources = [];
        WorldText = string.Empty;
        Agents.Clear();
        AgentBeliefs.Clear();
        AgentTrust.Clear();
        AgentDecisions.Clear();
        AgentHeaderText = string.Empty;
        StatusText = reason;
        HasData = false;
        RaiseReplayChanged();
        RaiseChartsChanged();
    }

    private void ApplySeries(EchosSeries series)
    {
        // Le direct est une décision d'affichage : si le curseur était au bord du
        // flux publié, il suit les nouveaux ticks — sinon la fenêtre reste figée
        // sur l'état précédent alors que des points sont dessinés au-delà.
        var wasLive = _ticks.Length == 0 || _replayTick >= _ticks[^1];
        _ticks = series.Ticks.ToArray();
        _lastSeries = series;

        if (!string.Equals(_seriesRunId, series.RunId, StringComparison.Ordinal))
        {
            // Changement de run : la relecture repart au direct (dernier tick publié)
            // et le journal d'événements du run précédent est abandonné — jamais
            // reporté sur les courbes d'un autre run.
            _seriesRunId = series.RunId;
            _replayTick = _ticks.Length == 0 ? 0 : _ticks[^1];
            IsPlaying = false;
            _runEvents = [];
            _eventsFeed = null;
            EventsText = string.Empty;
            RaiseReplayChanged();
        }
        else if (_ticks.Length > 0)
        {
            _replayTick = wasLive
                ? _ticks[^1]
                : Math.Clamp(_replayTick, _ticks[0], _ticks[^1]);
            RaiseReplayChanged();
        }

        // Découvertes des métriques : on conserve l'ordre stable renvoyé par ECHOS
        // et on écarte celles qu'il signale comme non mesurées (repli neutre).
        var discovered = new List<MetricOption>();
        foreach (var (engine, metrics) in series.Values)
        {
            foreach (var (metric, values) in metrics)
            {
                if (values.Count == 0)
                {
                    continue;
                }

                var measured = !series.Measured.TryGetValue(engine, out var engineMeasured)
                    || !engineMeasured.TryGetValue(metric, out var flag)
                    || flag;
                discovered.Add(new MetricOption(
                    engine, metric, measured, ResolveLatest(series, engine, metric, values, measured),
                    LookupHelp(engine, metric)));
            }
        }

        var previousSelection = Metrics.Where(option => option.IsSelected).Select(option => option.Key)
            .ToHashSet(StringComparer.Ordinal);
        Metrics.Clear();
        foreach (var option in discovered)
        {
            // Au premier relevé, on présélectionne les métriques mesurées ;
            // ensuite la sélection de l'utilisateur fait foi, même vide.
            if (previousSelection.Count == 0)
            {
                option.IsSelected = discovered.Count(item => item.Measured) <= MaxSeries
                    || discovered.Take(MaxSeries).Any(item => item.Key == option.Key);
            }
            else
            {
                option.IsSelected = previousSelection.Contains(option.Key);
            }

            option.PropertyChanged += (_, _) => OnMetricSelectionChanged();
            Metrics.Add(option);
        }

        foreach (var option in Metrics)
        {
            option.NotifyLatest(series);
        }

        RebuildDistributionOptions();
        RebuildTrend();

        DetailText =
            $"{series.RunId} · {series.Ticks.Count} point(s) affiché(s) · " +
            $"dernier tick {series.LatestTick?.ToString(CultureInfo.InvariantCulture) ?? "—"}" +
            (series.MissingTicksCount > 0
                ? $" · {series.MissingTicksCount} tick(s) manquant(s)"
                : " · aucune lacune de tick") +
            (Every > 1 ? $" · pas de lecture : {Every} tick(s)" : string.Empty);
        RaiseChartsChanged();
    }

    /// <summary>
    /// Dernière valeur publiée : la valeur d'ECHOS fait foi ; à défaut, la dernière
    /// observation non nulle de la série. Une valeur jamais observée vaut zéro et
    /// reste signalée « non mesurée ».
    /// </summary>
    private static double ResolveLatest(
        EchosSeries series, string engine, string metric, IReadOnlyList<double?> values, bool measured)
    {
        if (series.Latest.TryGetValue(engine, out var engineLatest)
            && engineLatest.TryGetValue(metric, out var published))
        {
            return published;
        }

        if (measured)
        {
            for (var index = values.Count - 1; index >= 0; index--)
            {
                if (values[index] is { } value)
                {
                    return value;
                }
            }
        }

        return 0d;
    }

    private void StartRefresh() => RequestRefresh();

    private void OnReplayCommand(string? action)
    {
        switch (action)
        {
            case "play":
                if (_ticks.Length > 0 && !IsAtLive)
                {
                    IsPlaying = true;
                }

                break;
            case "pause":
                IsPlaying = false;
                break;
            case "prev":
                if (ReplayIndex > 0)
                {
                    ReplayPosition = _ticks[ReplayIndex - 1];
                }

                break;
            case "next":
                if (ReplayIndex < _ticks.Length - 1)
                {
                    ReplayPosition = _ticks[ReplayIndex + 1];
                }

                break;
            case "live":
                if (_ticks.Length > 0)
                {
                    IsPlaying = false;
                    ReplayPosition = _ticks[^1];
                }

                break;
        }
    }

    /// <summary>Tick affiché par les vues liées au temps (monde, graphe de confiance) ;
    /// null tant qu'aucune série n'est publiée (lecture du dernier tick par ECHOS).</summary>
    private int? ViewTick => _ticks.Length == 0 ? null : _replayTick;

    /// <summary>Index de la position de relecture dans les ticks publiés.</summary>
    private int ReplayIndex
    {
        get
        {
            if (_ticks.Length == 0)
            {
                return 0;
            }

            var index = Array.FindIndex(_ticks, candidate => candidate >= _replayTick);
            return index < 0 ? _ticks.Length - 1 : index;
        }
    }

    /// <summary>
    /// Déplace le curseur de relecture. La fenêtre de lecture ne fait que tronquer
    /// les séries déjà publiées : aucune donnée n'est lue, écrite ni interpolée.
    /// </summary>
    private void SetReplayTick(int tick)
    {
        if (_ticks.Length == 0)
        {
            return;
        }

        var clamped = Math.Clamp(tick, _ticks[0], _ticks[^1]);
        if (_replayTick == clamped)
        {
            return;
        }

        _replayTick = clamped;
        var atLive = IsAtLive;
        if (atLive)
        {
            // « Suivre le direct » et « Relire ce run » sont deux états distincts (P3).
            IsPlaying = false;
            IsLive = true;
        }
        else
        {
            IsLive = false;
        }

        RaiseReplayChanged();
        RebuildTrend();

        if (string.Equals(SelectedSectionId, SectionWorld, StringComparison.Ordinal)
            || string.Equals(SelectedSectionId, SectionAgents, StringComparison.Ordinal)
            || string.Equals(SelectedSectionId, SectionRelations, StringComparison.Ordinal))
        {
            // Synchronisation des panneaux : la vue 2D et le graphe de confiance
            // lisent le tick du curseur.
            WorldTickInput = clamped.ToString(CultureInfo.InvariantCulture);
            RequestRefresh();
        }
    }

    private void RaiseReplayChanged()
    {
        // Ordre important : la valeur d'abord, puis les bornes (maximum avant minimum).
        // Sinon le curseur voit une fenêtre [min > max] invalide, se cale sur le plancher
        // et renvoie cette valeur à la vue modèle, déplaçant le curseur au premier tick.
        OnPropertyChanged(nameof(ReplayPosition));
        OnPropertyChanged(nameof(ReplayMaximum));
        OnPropertyChanged(nameof(ReplayMinimum));
        OnPropertyChanged(nameof(IsAtLive));
        OnPropertyChanged(nameof(ReplayText));
    }

    private void OnMetricSelectionChanged()
    {
        // La sélection ne modifie que la vue : les séries restent celles d'ECHOS.
        if (!HasData)
        {
            return;
        }

        RebuildTrend();
    }

    /// <summary>
    /// Reconstruit la courbe depuis le dernier relevé déjà lu : la sélection de
    /// l'utilisateur et la fenêtre de relecture sont des décisions d'affichage,
    /// aucune nouvelle lecture, aucun calcul local.
    /// </summary>
    private void RebuildTrend()
    {
        if (_lastSeries is null)
        {
            TrendSeries = Array.Empty<ISeries>();
            OnPropertyChanged(nameof(TrendSeries));
            RebuildDistribution();
            return;
        }

        var selected = Metrics.Where(option => option.IsSelected).Take(MaxSeries).ToList();
        var markers = BuildEventMarkers(selected);
        TrendSeries = BuildTrend(selected, _lastSeries, _ticks, ReplayIndex)
            .Concat(markers)
            .ToArray();
        OnPropertyChanged(nameof(TrendSeries));
        UpdateEventsText(markers.Length);
        RebuildDistribution();
    }

    /// <summary>
    /// Marqueurs d'événements (P3 — annotations de la vue temporelle) : une
    /// ligne verticale par tick publié portant au moins un événement.
    ///
    /// Le marqueur ne dit **pas** ce qui s'est passé : il signale qu'un
    /// événement existe à ce tick, et son type — la cause reste dans le journal
    /// publié. Borné à <see cref="MaxEventMarkers"/> marqueurs, limité à la
    /// fenêtre affichée, tracé sur l'étendue déjà dessinée : aucune valeur n'est
    /// produite (transformation de rendu, ADR-003).
    /// </summary>
    private ISeries[] BuildEventMarkers(IReadOnlyList<MetricOption> selected)
    {
        if (!ShowEventMarkers || _runEvents.Count == 0 || _ticks.Length == 0 || _lastSeries is null)
        {
            return [];
        }

        double? lowest = null;
        double? highest = null;
        var window = Math.Min(ReplayIndex, _ticks.Length - 1);
        foreach (var option in selected)
        {
            if (!_lastSeries.Values.TryGetValue(option.Engine, out var metrics)
                || !metrics.TryGetValue(option.Metric, out var values))
            {
                continue;
            }

            for (var index = 0; index <= window && index < values.Count; index++)
            {
                if (values[index] is not { } value)
                {
                    continue;
                }

                lowest = lowest is null ? value : Math.Min(lowest.Value, value);
                highest = highest is null ? value : Math.Max(highest.Value, value);
            }
        }

        if (lowest is null || highest is null)
        {
            return [];
        }

        // Série plate : une marge minimale évite des marqueurs invisibles.
        var padding = highest.Value > lowest.Value
            ? 0d
            : Math.Max(Math.Abs(lowest.Value) * 0.05, 0.5);
        var bottom = lowest.Value - padding;
        var top = highest.Value + padding;

        var from = _ticks[0];
        var to = _ticks[window];
        return _runEvents
            .Where(row => row.Tick >= from && row.Tick <= to)
            .GroupBy(row => row.Tick)
            .OrderBy(group => group.Key)
            .Take(MaxEventMarkers)
            .Select(group => (ISeries)new LineSeries<ObservablePoint?>
            {
                Name = $"t={group.Key} · {group.First().Type}",
                Values =
                [
                    new ObservablePoint(group.Key, bottom),
                    new ObservablePoint(group.Key, top),
                ],
                Stroke = new SolidColorPaint(EventColor(group.First().Type))
                {
                    StrokeThickness = 1.5f,
                },
                Fill = null,
                GeometryFill = null,
                GeometryStroke = null,
                LineSmoothness = 0,
            })
            .ToArray();
    }

    /// <summary>Couleur d'un marqueur par type d'événement (rendu seulement).</summary>
    private static SKColor EventColor(string type) => type switch
    {
        "agent_died" => new SKColor(0xf8, 0x51, 0x49),
        "group_formed" or "group_dissolved" => new SKColor(0xd2, 0xa8, 0xff),
        _ => new SKColor(0x8b, 0x94, 0x9e),
    };

    /// <summary>Journal d'événements du run : publié, borné, jamais interprété.</summary>
    private async Task RefreshEventsAsync(string runId)
    {
        try
        {
            var feed = await _source.ReadEventsAsync(runId, CancellationToken.None);
            _eventsFeed = feed;
            _runEvents = feed.Events;
        }
        catch (Exception exception)
        {
            // L'absence du journal est affichée : la courbe reste lisible sans lui.
            _eventsFeed = null;
            _runEvents = [];
            EventsText = $"journal d'événements indisponible — {exception.Message}";
        }

        RebuildTrend();
    }

    /// <summary>
    /// Compte-rendu des marqueurs : le texte annonce **ce qui est dessiné**
    /// (marqueurs dans la fenêtre affichée) sur ce qui est publié (total du
    /// journal) — jamais l'inverse, pour qu'une borne de rendu ne passe pas pour
    /// une intégralité.
    /// </summary>
    private void UpdateEventsText(int shown)
    {
        if (_eventsFeed is null)
        {
            return; // le relevé d'événements a déjà affiché son indisponibilité
        }

        if (_eventsFeed.Total == 0)
        {
            EventsText = "aucun événement publié pour ce run";
            return;
        }

        EventsText = $"{shown} marqueur(s) affiché(s) sur {_eventsFeed.Total} événement(s) publié(s)" +
            $" · {_eventsFeed.Types.Count} type(s) : " +
            string.Join(", ", _eventsFeed.Types.Select(kind => $"{kind.Type} ({kind.Count})")) +
            " · marqueur = tick publié, sans interprétation" +
            (shown < _eventsFeed.Events.Count ? $" · borné à {MaxEventMarkers} marqueurs" : string.Empty);
    }

    /// <summary>
    /// Outil de distribution (P3 — l'histogramme de la vue d'ensemble a été
    /// retiré, il est réintroduit ici comme **outil** explicite) : comptage des
    /// valeurs **publiées** de la métrique choisie, sur la fenêtre affichée.
    ///
    /// Le comptage est une transformation de **rendu** bornée (ADR-003) : aucune
    /// valeur n'est produite, seule une fréquence de classe est dessinée. Les
    /// valeurs non mesurées et les ticks sans observation sont **exclus** et
    /// comptés séparément — jamais convertis en zéro observé.
    /// </summary>
    private void RebuildDistribution()
    {
        if (_lastSeries is null || SelectedDistributionOption is not { } option)
        {
            DistributionSeries = Array.Empty<ISeries>();
            DistributionXAxes = BuildLabelAxes([]);
            DistributionText = "aucune métrique sélectionnée pour l'instant";
            RaiseDistributionChanged();
            return;
        }

        if (!_lastSeries.Values.TryGetValue(option.Engine, out var metrics)
            || !metrics.TryGetValue(option.Metric, out var values)
            || values.Count == 0)
        {
            DistributionSeries = Array.Empty<ISeries>();
            DistributionXAxes = BuildLabelAxes([]);
            DistributionText = $"{option.Label} — aucune série publiée pour ce run";
            RaiseDistributionChanged();
            return;
        }

        var mask = _lastSeries.MeasuredByTick.TryGetValue(option.Engine, out var engineMask)
            && engineMask.TryGetValue(option.Metric, out var metricMask)
            ? metricMask
            : null;
        var window = Math.Min(ReplayIndex, values.Count - 1);
        var samples = new List<double>(window + 1);
        var excluded = 0;
        for (var index = 0; index <= window; index++)
        {
            var flag = mask is null || index >= mask.Count ? null : mask[index];
            if (values[index] is { } value && flag != false)
            {
                samples.Add(value);
            }
            else
            {
                // Repli neutre ou tick sans observation : exclu et compté, jamais compté comme 0.
                excluded++;
            }
        }

        if (samples.Count == 0)
        {
            DistributionSeries = Array.Empty<ISeries>();
            DistributionXAxes = BuildLabelAxes([]);
            DistributionText = $"{option.Label} — aucune observation publiée dans la fenêtre affichée " +
                $"({excluded} valeur(s) exclue(s) : non mesurée ou tick sans observation)";
            RaiseDistributionChanged();
            return;
        }

        var minimum = samples.Min();
        var maximum = samples.Max();
        var counts = new List<double>();
        var labels = new List<string>();
        var width = 0d;
        if (samples.Count >= 2 && maximum > minimum)
        {
            var bins = Math.Clamp((int)Math.Ceiling(Math.Log2(samples.Count)) + 1, 2, MaxDistributionBins);
            width = (maximum - minimum) / bins;
            var counters = new double[bins];
            foreach (var sample in samples)
            {
                var slot = Math.Min(bins - 1, (int)((sample - minimum) / width));
                counters[slot]++;
            }

            counts.AddRange(counters);
            for (var bin = 0; bin < bins; bin++)
            {
                labels.Add(FormatNumber(minimum + (bin * width)) + "–" + FormatNumber(minimum + ((bin + 1) * width)));
            }
        }
        else
        {
            // Toutes les observations publiées sont identiques : un seul intervalle.
            counts.Add(samples.Count);
            labels.Add(FormatNumber(minimum));
        }

        DistributionSeries =
        [
            new ColumnSeries<double>
            {
                Name = option.Metric,
                Values = counts,
                Fill = new SolidColorPaint(new SKColor(0x00, 0x78, 0xd7)),
                Stroke = null,
            },
        ];
        DistributionXAxes = BuildLabelAxes(labels);
        DistributionText = $"{option.Label} · {samples.Count} observation(s) publiée(s) · " +
            $"{counts.Count} intervalle(s)" +
            (width > 0 ? $" de largeur {FormatNumber(width)}" : string.Empty) +
            $" · {excluded} valeur(s) exclue(s) (non mesurée ou tick sans observation) · " +
            "comptage de classe sur la fenêtre affichée, aucune extrapolation";
        RaiseDistributionChanged();
    }

    /// <summary>Reconstruit la liste déroulante de l'outil de distribution (séries publiées).</summary>
    private void RebuildDistributionOptions()
    {
        var previous = SelectedDistributionOption?.Key;
        DistributionOptions.Clear();
        if (_lastSeries is null)
        {
            SelectedDistributionOption = null;
            return;
        }

        foreach (var (engine, metrics) in _lastSeries.Values)
        {
            foreach (var metric in metrics.Keys)
            {
                DistributionOptions.Add(new DistributionOption(engine, metric, $"{metric} ({engine})"));
            }
        }

        SelectedDistributionOption = DistributionOptions.FirstOrDefault(
                option => string.Equals(option.Key, previous, StringComparison.Ordinal))
            ?? DistributionOptions.FirstOrDefault();
    }

    private void RaiseDistributionChanged()
    {
        OnPropertyChanged(nameof(DistributionSeries));
        OnPropertyChanged(nameof(DistributionXAxes));
        OnPropertyChanged(nameof(DistributionYAxes));
        OnPropertyChanged(nameof(DistributionText));
    }

    /// <summary>Axe catégoriel : un libellé par classe de l'outil de distribution.</summary>
    private static Axis[] BuildLabelAxes(IReadOnlyList<string> labels) =>
    [
        new Axis
        {
            Name = "intervalle",
            NamePaint = new SolidColorPaint(new SKColor(0x9a, 0xa0, 0xaa)),
            Labels = labels.ToList(),
            LabelsPaint = new SolidColorPaint(new SKColor(0x9a, 0xa0, 0xaa)),
            TextSize = 12,
            LabelsRotation = 20,
            SeparatorsPaint = new SolidColorPaint(new SKColor(0x2a, 0x2e, 0x37)),
            MinLimit = null,
        },
    ];

    /// <summary>
    /// Courbe : un point par tick publié, en coordonnée de tick réelle. Un tick
    /// sans observation (valeur nulle ou masque de mesure à `false`) devient un
    /// **trou** : la ligne se interrompt, jamais interpolée (P0).
    /// </summary>
    private static ISeries[] BuildTrend(
        IReadOnlyList<MetricOption> selected, EchosSeries series, IReadOnlyList<int> ticks, int lastIndex)
    {
        var list = new List<ISeries>();
        var color = 0;
        foreach (var option in selected)
        {
            if (!series.Values.TryGetValue(option.Engine, out var engine)
                || !engine.TryGetValue(option.Metric, out var values))
            {
                continue;
            }

            IReadOnlyList<bool?>? mask = null;
            if (series.MeasuredByTick.TryGetValue(option.Engine, out var engineMask)
                && engineMask.TryGetValue(option.Metric, out var publishedMask)
                && publishedMask is not null)
            {
                mask = publishedMask;
            }

            var limit = Math.Min(Math.Min(values.Count, ticks.Count), lastIndex + 1);
            var points = new ObservablePoint?[limit];
            for (var index = 0; index < limit; index++)
            {
                if (values[index] is { } value
                    && (mask is null
                        ? option.Measured
                        : index < mask.Count && mask[index] == true))
                {
                    points[index] = new ObservablePoint(ticks[index], value);
                }
                else
                {
                    points[index] = null;
                }
            }

            list.Add(new LineSeries<ObservablePoint?>
            {
                Name = option.Label,
                Values = points,
                Stroke = new SolidColorPaint(Palette[color % Palette.Length], 2f),
                Fill = null,
                GeometryStroke = new SolidColorPaint(Palette[color % Palette.Length], 2f),
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometrySize = 5,
                LineSmoothness = 0,
            });
            color++;
        }

        return list.ToArray();
    }

    /// <summary>Panneau d'une série publiée (tick réel en abscisse, jamais interpolé).</summary>
    private static ISeries[] BuildPointSeries(string name, IReadOnlyList<EchosPoint> points, SKColor color)
    {
        if (points.Count == 0)
        {
            return Array.Empty<ISeries>();
        }

        var values = new ObservablePoint?[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            values[index] = new ObservablePoint(points[index].Tick, points[index].Value);
        }

        return
        [
            new LineSeries<ObservablePoint?>
            {
                Name = name,
                Values = values,
                Stroke = new SolidColorPaint(color, 2f),
                Fill = null,
                GeometryStroke = new SolidColorPaint(color, 2f),
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometrySize = 4,
                LineSmoothness = 0,
            },
        ];
    }

    /// <summary>Texte des signaux déclencheurs publié derrière chaque phénomène.</summary>
    private string DescribeTriggers(EchosPhenomenon phenomenon)
    {
        if (phenomenon.Triggers.Count == 0)
        {
            return "aucun signal publié avec cette détection";
        }

        return string.Join(" · ", phenomenon.Triggers.Select(signal =>
            $"{signal.Metric} = {FormatNumber(signal.Value)} (seuil {FormatNumber(signal.Threshold)})"));
    }

    /// <summary>Fiche du catalogue « que regarder ? » pour une métrique, ou absence vide.</summary>
    private string LookupHelp(string engine, string metric) => LookupDoc(engine, metric)?.Help ?? string.Empty;

    /// <summary>Statut publié par le catalogue (« exploratoire » n'est jamais masqué).</summary>
    private string LookupStatus(string engine, string metric) => LookupDoc(engine, metric)?.StatusLabel ?? "—";

    /// <summary>Fiche du catalogue pour une métrique, par moteur puis par identifiant.</summary>
    private EchosMetricDoc? LookupDoc(string engine, string metric) =>
        Catalog.Metrics.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, metric, StringComparison.Ordinal)
                && string.Equals(candidate.Engine, engine, StringComparison.Ordinal))
            ?? Catalog.Metrics.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, metric, StringComparison.Ordinal));

    /// <summary>Issue de population en clair, sans réinterpréter le statut publié.</summary>
    private static string DescribeOutcome(string outcome) => outcome switch
    {
        "extinct" => "extinction observée",
        "surviving" => "survie observée",
        _ => string.IsNullOrWhiteSpace(outcome) ? "inconnue" : outcome,
    };

    /// <summary>
    /// État du run construit **uniquement** à partir des faits publiés (issue,
    /// dernier tick, lacunes). ECHOS ne publie pas l'état d'activité du flux :
    /// la distinction « en cours » / « interrompu » est une lacune explicite du
    /// contrat, affichée comme telle — jamais devinée (P3).
    /// </summary>
    private static string DescribeRunState(EchosViability viability)
    {
        var state = viability.Outcome switch
        {
            "extinct" => viability.ExtinctionTick is { } extinction
                ? $"terminé — extinction observée au tick {extinction}"
                : "terminé — extinction observée",
            "surviving" => viability.LastTick > 0
                ? $"aucune extinction observée jusqu'au tick {viability.LastTick} — run possiblement en cours"
                : "aucune extinction observée",
            _ => "inconnu — aucune issue publiée",
        };

        return viability.MissingTickCount > 0
            ? $"{state} · incomplet : {viability.MissingTickCount} tick(s) manquant(s)"
            : state;
    }

    /// <summary>
    /// Âge affiché d'un contexte échantillonné : cadence publiée
    /// (``conservation.sampledDetails.agentContextEvery``) et écart au dernier
    /// tick observé — deux métadonnées affichées côte à côte, jamais
    /// interpolées ni converties en valeur mesurée (P3).
    /// </summary>
    private string DescribeContextAge(string runId, int contextTick)
    {
        var age = string.Empty;
        var cadence = Runs.FirstOrDefault(option => option.RunId == runId)?.AgentContextEvery;
        if (cadence is { } every)
        {
            age = $" · cadence {every} tick(s)";
            if (every > 1)
            {
                age += $" (âge ≤ {every - 1})";
            }
        }

        if (contextTick >= 0 && _lastSeries?.LatestTick is { } latest && latest > contextTick)
        {
            age += $" · {latest - contextTick} tick(s) derrière le dernier ({latest})";
        }

        return age;
    }

    private static Axis[] BuildTickAxes() =>
    [
        new Axis
        {
            Name = "tick",
            NamePaint = new SolidColorPaint(new SKColor(0x9a, 0xa0, 0xaa)),
            Labeler = value => value.ToString("0", CultureInfo.InvariantCulture),
            LabelsPaint = new SolidColorPaint(new SKColor(0x9a, 0xa0, 0xaa)),
            TextSize = 12,
            LabelsRotation = 0,
            SeparatorsPaint = new SolidColorPaint(new SKColor(0x2a, 0x2e, 0x37)),
            MinLimit = null,
        },
    ];

    private static Axis[] BuildValueAxes(string name) =>
    [
        new Axis
        {
            Name = name,
            NamePaint = new SolidColorPaint(new SKColor(0x9a, 0xa0, 0xaa)),
            LabelsPaint = new SolidColorPaint(new SKColor(0x9a, 0xa0, 0xaa)),
            TextSize = 12,
            SeparatorsPaint = new SolidColorPaint(new SKColor(0x2a, 0x2e, 0x37)),
            MinLimit = null,
        },
    ];

    private static string FormatNumber(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatNumber(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string FormatNumber(double? value) => value is { } published
        ? FormatNumber(published)
        : "—";

    private static string FormatNumber(int? value) => value is { } published
        ? FormatNumber(published)
        : "—";

    private void RaiseChartsChanged()
    {
        OnPropertyChanged(nameof(TrendSeries));
        OnPropertyChanged(nameof(TrendXAxes));
        OnPropertyChanged(nameof(TrendYAxes));
        OnPropertyChanged(nameof(PopulationSeries));
        OnPropertyChanged(nameof(NeedsSeries));
        OnPropertyChanged(nameof(ResourceSeries));
        OnPropertyChanged(nameof(Groups));
        OnPropertyChanged(nameof(NetworkText));
    }
}

/// <summary>Run proposé dans le sélecteur, avec ses métadonnées d'issue de population.</summary>
public sealed class EchosRunOption : ObservableObject
{
    private EchosRunSummary _summary;

    /// <summary>Construit l'option depuis la métadonnée ECHOS.</summary>
    public EchosRunOption(EchosRunSummary summary) => _summary = summary;

    /// <summary>Identifiant du run.</summary>
    public string RunId => _summary.RunId;

    /// <summary>Libellé affiché : identifiant, ticks et issue.</summary>
    public string Label => $"{_summary.RunId} · {_summary.LastTick} tick(s) · {_summary.Outcome}";

    /// <summary>Dernier tick observé.</summary>
    public int LastTick => _summary.LastTick;

    /// <summary>Cadence publiée du contexte ``agents`` (ticks), si le run la publie.</summary>
    public int? AgentContextEvery => _summary.AgentContextEvery;

    /// <summary>
    /// Met à jour la métadonnée sans changer d'identité de sélection — le libellé
    /// notifié se rafraîchit dans le sélecteur sans que l'utilisateur perde le run
    /// choisi (le compte de ticks avance en direct).
    /// </summary>
    public void Update(EchosRunSummary summary)
    {
        _summary = summary;
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(LastTick));
        OnPropertyChanged(nameof(AgentContextEvery));
    }
}

/// <summary>Run cochable pour la comparaison multi-run (aucune valeur n'est calculée ici).</summary>
public sealed class CompareRunOption(string runId, string label) : ObservableObject
{
    private bool _isSelected;
    private string _label = label;

    /// <summary>Identifiant du run.</summary>
    public string RunId { get; } = runId;

    /// <summary>Libellé affiché (identifiant, ticks, issue).</summary>
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    /// <summary>Vrai si le run est inclus dans la comparaison publiée.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>Une métrique découverte dans les séries, sélectionnable pour les vues.</summary>
public sealed class MetricOption : ObservableObject
{
    private bool _isSelected;
    private double _latest;
    private EchosSeries? _series;

    /// <summary>Construit l'option à partir du moteur et de la métrique.</summary>
    public MetricOption(string engine, string metric, bool measured, double latest, string help = "")
    {
        Engine = engine;
        Metric = metric;
        Measured = measured;
        _latest = latest;
        Help = help;
    }

    /// <summary>Moteur producteur (ex. <c>SocialComplexityMetrics</c>).</summary>
    public string Engine { get; }

    /// <summary>Nom de la métrique (ex. <c>ClusteringCoefficient</c>).</summary>
    public string Metric { get; }

    /// <summary>Étiquette affichée : métrique, abrégée du moteur si utile.</summary>
    public string Label => Metric;

    /// <summary>Vrai si ECHOS signale une mesure réelle (sinon repli neutre).</summary>
    public bool Measured { get; }

    /// <summary>Fiche du catalogue « que regarder ? » (vide si le catalogue est absent).</summary>
    public string Help { get; }

    /// <summary>Info-bulle « que regarder ? » ; null = aucune fiche au catalogue.</summary>
    public string? Tip => string.IsNullOrEmpty(Help) ? null : Help;

    /// <summary>Clef stable pour la sélection.</summary>
    public string Key => $"{Engine}/{Metric}";

    /// <summary>Séries du dernier relevé, pour reconstruire la courbe sans relecture.</summary>
    public EchosSeries? Series => _series;

    /// <summary>Vrai si la métrique est affichée sur la courbe.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Dernière valeur publiée par ECHOS.</summary>
    public double Latest
    {
        get => _latest;
        private set => SetProperty(ref _latest, value);
    }

    /// <summary>Dernière valeur sous forme de libellé.</summary>
    public string LatestLabel => Measured
        ? Latest.ToString("0.###", CultureInfo.InvariantCulture)
        : "non mesurée";

    /// <summary>Rattache les séries du relevé (reconstruction sans nouvel appel).</summary>
    public void NotifyLatest(EchosSeries series)
    {
        _series = series;
        if (series.Latest.TryGetValue(Engine, out var engine)
            && engine.TryGetValue(Metric, out var value))
        {
            Latest = value;
        }

        OnPropertyChanged(nameof(LatestLabel));
    }
}

/// <summary>Une ligne du tableau des phénomènes émergents, avec ses signaux publiés.</summary>
public sealed record PhenomenonRow(
    string Identifier,
    string DisplayName,
    string Description,
    int Occurrences,
    int FirstTick,
    int LastTick,
    string Triggers);

/// <summary>Un sous-écran de la fenêtre d'analyse (menu de navigation).</summary>
public sealed record AnalysisSectionOption(string Id, string Title, string Description);

/// <summary>Une ligne du tableau des statistiques exactes : valeur publiée et provenance.</summary>
public sealed record StatRow(string Engine, string Metric, string Display, bool Measured, string Status, string Help)
{
    /// <summary>Provenance affichée : mesurée, ou repli neutre signalé comme tel.</summary>
    public string Provenance => Measured ? "mesurée" : "repli neutre";

    /// <summary>Info-bulle « que regarder ? » ; null = aucune fiche au catalogue.</summary>
    public string? Tip => string.IsNullOrEmpty(Help) ? null : Help;
}

/// <summary>Une ligne du profil de viabilité : fait publié, valeur et note de lecture.</summary>
public sealed record ViabilityRow(string Label, string Value, string Note);

/// <summary>Une entrée de la chronologie publiée avant l'extinction.</summary>
public sealed record ChronologyRow(int Tick, string Type, string AgentId, string Action, string Cause);

/// <summary>Métrique proposée à l'outil de distribution (clé de série publiée).</summary>
public sealed record DistributionOption(string Engine, string Metric, string Label)
{
    /// <summary>Clé stable de sélection : moteur puis identifiant.</summary>
    public string Key => $"{Engine}/{Metric}";
}

/// <summary>Contexte de contrôle d'un run comparé, publié par ECHOS.</summary>
public sealed record CompareRunRow(
    string RunId,
    string Version,
    string Seed,
    string Outcome,
    string Ticks,
    string Extinction,
    string Conservation);

/// <summary>Valeurs publiées d'une métrique sur les runs comparés et sa dispersion publiée.</summary>
public sealed record CompareMetricRow(
    string Engine,
    string Metric,
    string Values,
    int RunsObserved,
    string Min,
    string Max,
    string Mean,
    string Spread);

/// <summary>Une relation mise en forme pour le tableau du sous-écran confiance.</summary>
public sealed record TrustEdgeRow(string Source, string Target, string Display);

/// <summary>Une entité sélectionnable dans la liste des fiches.</summary>
public sealed class AgentOption(string Id, double X, double Y, double Energy, string? Action, string? Group)
{
    /// <summary>Identifiant de l'entité.</summary>
    public string Id { get; } = Id;

    /// <summary>Position publiée au tick observé.</summary>
    public double X { get; } = X;

    /// <summary>Position publiée au tick observé.</summary>
    public double Y { get; } = Y;

    /// <summary>Énergie publiée.</summary>
    public double Energy { get; } = Energy;

    /// <summary>Action en cours publiée, si renseignée.</summary>
    public string? Action { get; } = Action;

    /// <summary>Communauté à laquelle l'entité appartient, si observée.</summary>
    public string? Group { get; } = Group;

    /// <summary>Libellé affiché : identifiant, groupe et action.</summary>
    public string Label => Id + (string.IsNullOrWhiteSpace(Group) ? string.Empty : $" · groupe {Group}") +
        (string.IsNullOrWhiteSpace(Action) ? string.Empty : $" · {Action}");
}

/// <summary>Une croyance publiée, avec sa confiance brute mise en forme.</summary>
public sealed record BeliefRow(string Subject, string Predicate, string Value, string Confidence);

/// <summary>Une relation de confiance publiée pour l'entité sélectionnée.</summary>
public sealed record TrustRelationRow(string PeerId, string Trust);

/// <summary>Une décision publiée pour l'entité sélectionnée.</summary>
public sealed record DecisionRow(int Tick, string Action, string Utility, string Cause);
