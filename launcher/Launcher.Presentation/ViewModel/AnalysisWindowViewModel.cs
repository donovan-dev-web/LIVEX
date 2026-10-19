// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : Vue modèle de la fenêtre d'analyse.

using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Launcher.Application;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Launcher.Presentation.ViewModel;

/// <summary>
/// Vue modèle de la fenêtre d'analyse.
///
/// **Règle de frontière ** : cette vue *présente* ce qu'ECHOS produit.
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
public sealed partial class AnalysisWindowViewModel : ObservableObject
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

    /// <summary>Sous-écran « chaîne causale » (trace de décision reconstruite par ECHOS).</summary>
    public const string SectionCausality = "causalite";

    /// <summary>Profondeur maximale acceptée par ECHOS pour une chaîne causale.</summary>
    public const int MaxCausalDepth = 12;

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
    private IReadOnlyList<NativeGroupRow> _nativeGroups = [];
    private string _nativeGroupsText = string.Empty;
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
    private string _worldResourcesText = string.Empty;
    private string _causalHeader = string.Empty;
    private string _causalText = string.Empty;
    private string _causalNote = string.Empty;
    private string _causalTickInput = string.Empty;
    private int _causalDepth = 7;

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
        WorldResourceRows = new ObservableCollection<TickResourceRow>();
        CausalNodes = new ObservableCollection<CausalNodeRow>();
        CausalCycles = new ObservableCollection<CausalCycleRow>();
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

    /// <summary>Sous-écrans proposés par le menu de la fenêtre.</summary>
    public IReadOnlyList<AnalysisSectionOption> Sections { get; } =
    [
        new(SectionViability, "Viabilité", "Issue, population, besoins, ressources, complétude"),
        new(SectionOverview, "Comportements", "Courbes par tick, communautés, phénomènes détectés"),
        new(SectionStatistics, "Statistiques exactes", "Valeurs publiées par ECHOS, sans calcul local"),
        new(SectionCompare, "Comparer", "Contexte des runs et dispersion publiée"),
        new(SectionRelations, "Relations et groupes", "Confiance entre entités et communautés"),
        new(SectionWorld, "Monde observé", "Terrain, gisements, réserves, entités en mouvement"),
        new(SectionAgents, "Entités", "Fiche détaillée d'une entité"),
        new(SectionCausality, "Chaîne causale", "Action → perception : trace de décision et boucles"),
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

    /// <summary>
    /// Réserves du tick, mises en forme pour le panneau latéral de la vue 2D.
    /// Le contrat SYNE 0.5 les agrège **par type** : sans position publiée, elles
    /// sont listées ici et ne sont pas projetées sur la carte (jamais à l'origine).
    /// </summary>
    public ObservableCollection<TickResourceRow> WorldResourceRows { get; }

    /// <summary>Compte-rendu des réserves : publiées, localisées, quantités absentes.</summary>
    public string WorldResourcesText
    {
        get => _worldResourcesText;
        private set => SetProperty(ref _worldResourcesText, value);
    }

    // -----------------------------------------------------------------------
    // Chaîne causale (A0-2/A0-3 : décision et preuves auditables).
    // -----------------------------------------------------------------------

    /// <summary>En-tête : entité, tick de la trace et profondeur servie.</summary>
    public string CausalHeader
    {
        get => _causalHeader;
        private set => SetProperty(ref _causalHeader, value);
    }

    /// <summary>Couches de la chaîne, de l'action vers la perception.</summary>
    public ObservableCollection<CausalNodeRow> CausalNodes { get; }

    /// <summary>Boucles signalées par ECHOS (rétroaction ou action répétée).</summary>
    public ObservableCollection<CausalCycleRow> CausalCycles { get; }

    /// <summary>Compte-rendu de lecture : profondeur demandée/servie et troncature.</summary>
    public string CausalText
    {
        get => _causalText;
        private set => SetProperty(ref _causalText, value);
    }

    /// <summary>Note de méthode affichée sous la chaîne (ce que la trace ne prouve pas).</summary>
    public string CausalNote
    {
        get => _causalNote;
        private set => SetProperty(ref _causalNote, value);
    }

    /// <summary>Tick demandé pour la chaîne ; vide = dernier tick tracé par ECHOS.</summary>
    public string CausalTickInput
    {
        get => _causalTickInput;
        set => SetProperty(ref _causalTickInput, value ?? string.Empty);
    }

    /// <summary>
    /// Profondeur demandée (couches affichées) ; bornée à 12 comme ECHOS. La
    /// modification ne déclenche pas de relevé : elle est prise en compte au
    /// relevé suivant (« Reconstruire » ou entrée dans le sous-écran), ce qui
    /// évite une lecture à chaque écriture de la liste déroulante.
    /// </summary>
    public int CausalDepth
    {
        get => _causalDepth;
        set => SetProperty(ref _causalDepth, Math.Clamp(value, 1, MaxCausalDepth));
    }

    /// <summary>Profondeurs proposées (les 7 couches publiées, puis les plafonds ECHOS).</summary>
    public IReadOnlyList<int> CausalDepthOptions { get; } = [3, 5, 7, 10, 12];

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

}

/// <summary>Une boucle signalée par ECHOS : couche, libellé et récurrence publiés.</summary>
public sealed record CausalCycleRow(string Layer, string Label, string Recurrence);
