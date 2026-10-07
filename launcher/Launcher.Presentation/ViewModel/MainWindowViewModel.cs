using System.Collections.ObjectModel;
using System.Globalization;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Protocol.Model;
using Launcher.Application;

namespace Launcher.Presentation.ViewModel;

/// <summary>
/// Vue modèle principale. Réconciliation des deux découpages (USER_INTERFACE.md §2.2) :
/// la navigation latérale porte les **neuf écrans V1**, dont sept hérités de la maquette,
/// et le sélecteur de la topbar porte les quatre modes de lancement.
/// L'entrée Immersion n'existe pas : PRISM apparaît comme composant de la pile, pas comme écran.
/// La présentation ne connaît que des abstractions exposées par l'application.
/// </summary>
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IOrchestrationFacade _orchestration;
    private string _selectedNavId = NavAccueilId;
    private string _selectedMode = "Standard";
    private string _selectedSyneEngineMode = "Réel";
    private string _selectedProfile = WellKnownProfiles.SimulationSeule;
    private string _profileStatus = string.Empty;
    private ProfileResolution? _profileResolution;
    private string _globalState = "Inactif";
    private string _globalCause = "aucun composant démarré";
    private string _emergenceReport = string.Empty;
    private string _reportStatus = "aucun paquet ouvert";
    private string _ramPercent = "—";
    private string _cpuPercent = "—";
    private string _storagePercent = "—";
    private double _ramValue;
    private double _cpuValue;
    private double _storageValue;
    private string _campaignTitle = "Nouvelle expérience";
    private string _simulationId = WellKnownSimulations.Reference;
    private string _runCountInput = "1";
    private string _selectedRunMode = "Mono";
    private int _selectedTicksPerSecond = DirectTicksPerSecond;
    private string _tickCountInput = "1000";
    private string _agentCountInput = "50";
    private string _baseSeedInput = "42";
    private string _campaignStatus = "Configurez les paramètres puis démarrez la campagne.";
    private string _campaignProgressText = "Aucune campagne en cours.";
    private double _runTickPercent;
    private bool _hasRunTickProgress;
    private string _runTickText = string.Empty;
    private string _documentationContent = string.Empty;
    private string _documentationStatus = "Aucune documentation embarquée.";
    private string _logStatus = string.Empty;
    private string _runLogContent = string.Empty;
    private string? _runLogPackagePath;
    private DocumentationItemViewModel? _selectedDocumentation;
    private PackageLogFileViewModel? _selectedRunLog;
    private CancellationTokenSource? _campaignCancellation;
    private bool _isCampaignRunning;

    /// <summary>Initialise la vue modèle avec la façade d'orchestration.</summary>
    public MainWindowViewModel(IOrchestrationFacade orchestration)
    {
        _orchestration = orchestration;

        NavigateCommand = new RelayCommand<string>(Navigate);
        ToggleComponentCommand = new RelayCommand<string>(ToggleComponent);
        OpenConsoleCommand = new RelayCommand<string>(OpenConsole);
        OpenAnalysisCommand = new RelayCommand<string>(_ => _analysis?.Invoke());
        StartProfileCommand = new RelayCommand<string>(
            _ => StartSelectedProfile(),
            _ => CanStartProfile);
        StopProfileCommand = new RelayCommand<string>(
            _ => StopSelectedProfile(),
            _ => _profileResolution?.StartupOrder.Count > 0);

        SessionKinds = new ObservableCollection<string> { "Console", "Standard", "Développement", "Personnaliser" };
        CustomComponents =
        [
            new("syne", "SYNE", true, () => OnCustomComponentChanged("syne")),
            new("echos", "ECHOS", false, ResolveSelectedProfile),
            new("prism", "PRISM", false, ResolveSelectedProfile),
        ];
        OpenExperienceCommand = new RelayCommand<string>(OpenExperience);
        CreateCampaignCommand = new RelayCommand<string>(
            async _ => await RunCampaignAsync().ConfigureAwait(true),
            _ => CanCreateCampaign);
        ResumeCampaignCommand = new RelayCommand<string>(
            async packagePath => await RunCampaignAsync(packagePath).ConfigureAwait(true),
            packagePath => !IsCampaignRunning && !string.IsNullOrWhiteSpace(packagePath));
        CancelCampaignCommand = new RelayCommand<string>(
            _ => _campaignCancellation?.Cancel(),
            _ => IsCampaignRunning);
        DocumentationItems = new ObservableCollection<DocumentationItemViewModel>(_orchestration.ListDocumentation());
        if (DocumentationItems.Count > 0)
        {
            SelectedDocumentation = DocumentationItems[0];
        }

        ComponentInstallations = new ObservableCollection<ComponentInstallationSelectionViewModel>(
            _orchestration.ListComponentInstallations()
                .GroupBy(option => option.ComponentId, StringComparer.Ordinal)
                .Select(group => new ComponentInstallationSelectionViewModel(
                    group.Key,
                    group.Key switch
                    {
                        "syne" => "SYNE — moteur réel",
                        "syne-mock" => "SYNE — émulation",
                        _ => group.First().Name,
                    },
                    group.ToArray(),
                    group.FirstOrDefault(option => option.IsActive),
                    SelectComponentInstallation)));
        ResolveSelectedProfile();
    }

    private Action<string, bool>? _lifecycle;
    private Action<string>? _console;
    private Action? _analysis;

    /// <summary>Identifiants stables des entrées de navigation.</summary>
    public const string NavAccueilId = "accueil";
    public const string NavExperiencesId = "experiences";
    public const string NavCampagnesId = "campagnes";
    public const string NavAnalyseId = "analyse";
    public const string NavRapportsId = "rapports";
    public const string NavConfigurationId = "configuration";
    public const string NavLogsId = "logs";
    public const string NavMonitoringId = "monitoring";
    public const string NavDocumentationId = "documentation";

    /// <summary>Identifiant de l'écran actif.</summary>
    public string SelectedNavId
    {
        get => _selectedNavId;
        private set
        {
            if (SetProperty(ref _selectedNavId, value))
            {
                OnPropertyChanged(nameof(SelectedNavTitle));
                OnPropertyChanged(nameof(SelectedNavIndex));
            }
        }
    }

    /// <summary>Titre de l'écran actif, affiché dans le bandeau de la vue.</summary>
    public string SelectedNavTitle => SelectedNavId switch
    {
        NavExperiencesId => "Expériences",
        NavCampagnesId => "Campagnes",
        NavAnalyseId => "Analyse",
        NavRapportsId => "Rapports",
        NavConfigurationId => "Configuration",
        NavLogsId => "Logs",
        NavMonitoringId => "Monitoring",
        NavDocumentationId => "Documentation",
        _ => "Accueil",
    };

    /// <summary>Index de l'entrée active, pour la liaison de la ListBox.</summary>
    public int SelectedNavIndex
    {
        get
        {
            for (var index = 0; index < NavItems.Count; index++)
            {
                if (NavItems[index].Id == SelectedNavId)
                {
                    return index;
                }
            }

            return 0;
        }
        set
        {
            if (value >= 0 && value < NavItems.Count)
            {
                Navigate(NavItems[value].Id);
            }
        }
    }

    /// <summary>Commande de navigation latérale.</summary>
    public RelayCommand<string> NavigateCommand { get; }

    /// <summary>Entrées de la sidebar dans l'ordre V1 ; Monitoring et Documentation complètent la maquette.</summary>
    public IReadOnlyList<NavItemViewModel> NavItems { get; } =
    [
        new(NavAccueilId, "Accueil", true),
        new(NavExperiencesId, "Expériences", false),
        new(NavAnalyseId, "Analyse", false),
        new(NavRapportsId, "Rapports", false),
        new(NavConfigurationId, "Configuration", false),
        new(NavLogsId, "Logs", false),
        new(NavMonitoringId, "Monitoring", false),
        new(NavDocumentationId, "Documentation", false),
    ];

    /// <summary>Commande de cycle de vie d'une carte : démarre si arrêté, arrête si démarré.</summary>
    public RelayCommand<string> ToggleComponentCommand { get; }

    /// <summary>
    /// Commande d'ouverture de la console d'un composant (USER_INTERFACE.md §9) : le paramètre est
    /// l'identifiant d'instance, absent si le composant n'est pas démarré.
    /// </summary>
    public RelayCommand<string> OpenConsoleCommand { get; }

    /// <summary>
    /// Commande d'ouverture de la fenêtre d'analyse native (USER_INTERFACE.md §9.2, ADR-007) :
    /// le Launcher présente l'analyse produite par ECHOS, sans interface web.
    /// </summary>
    public RelayCommand<string> OpenAnalysisCommand { get; }

    private void OpenConsole(string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        _console?.Invoke(instanceId);
    }

    private void ToggleComponent(string? componentId)
    {
        if (componentId is null)
        {
            return;
        }

        var row = Components.FirstOrDefault(component => component.Id == componentId);
        var runtimeComponentId = row?.RuntimeComponentId;
        if (string.IsNullOrWhiteSpace(runtimeComponentId))
        {
            runtimeComponentId = componentId == "syne" ? SelectedSyneComponentId : componentId;
        }

        _lifecycle?.Invoke(runtimeComponentId, row is null || !row.IsRunning);
    }

    /// <summary>Types de session du sélecteur de la topbar (COMPONENTS.md §8).</summary>
    public ObservableCollection<string> SessionKinds { get; }

    /// <summary>Type de session actif. « Standard » est le libellé d'usage de Production.</summary>
    public string SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                OnPropertyChanged(nameof(ModeLabel));
                if (value is "Console" or "Standard")
                {
                    SetSyneEngineMode("Réel", refresh: false);
                }
                else if (value == "Développement")
                {
                    SetSyneEngineMode("Émulé", refresh: false);
                }
                OnPropertyChanged(nameof(CanChooseSyneEngineMode));

                SelectedProfile = value switch
                {
                    "Console" => WellKnownProfiles.Experience,
                    "Développement" => WellKnownProfiles.Developpement,
                    "Personnaliser" => WellKnownProfiles.Personnalise,
                    _ => WellKnownProfiles.SimulationSeule,
                };
                ResolveSelectedProfile();
                RefreshFromRegistry();
            }
        }
    }

    /// <summary>Profils connus, dans l'ordre documenté (COMPONENTS.md §9).</summary>
    public IReadOnlyList<string> Profiles { get; } =
    [
        WellKnownProfiles.SimulationSeule,
        WellKnownProfiles.Analyse,
        WellKnownProfiles.AnalyseAvecTelemetrie,
        WellKnownProfiles.Immersion,
        WellKnownProfiles.Experience,
        WellKnownProfiles.Developpement,
        WellKnownProfiles.Personnalise,
    ];

    /// <summary>Composants configurables du mode Personnaliser.</summary>
    public IReadOnlyList<CustomComponentSelectionViewModel> CustomComponents { get; }
    /// <summary>Modes disponibles pour l'unique rôle SYNE : moteur réel ou émulation.</summary>
    public IReadOnlyList<string> SyneEngineModes { get; } = ["Réel", "Émulé"];

    /// <summary>Variante SYNE ciblée quand aucun moteur n'est déjà actif.</summary>
    public string SelectedSyneEngineMode
    {
        get => _selectedSyneEngineMode;
        set
        {
            if (SelectedMode == "Développement" && value != "Émulé")
            {
                return;
            }

            if (SetSyneEngineMode(value, refresh: false))
            {
                ResolveSelectedProfile();
                RefreshFromRegistry();
            }
        }
    }

    /// <summary>Le profil Développement impose l'émulateur ; les autres modes permettent le choix.</summary>
    public bool CanChooseSyneEngineMode => SelectedMode != "Développement";

    private string SelectedSyneComponentId =>
        SelectedMode == "Développement" || SelectedSyneEngineMode == "Émulé" ? "syne-mock" : "syne";

    /// <summary>Installations disponibles et sélection active par composant.</summary>
    public ObservableCollection<ComponentInstallationSelectionViewModel> ComponentInstallations { get; }

    /// <summary>Nom de l'expérience à créer.</summary>
    public string CampaignTitle { get => _campaignTitle; set => UpdateCampaignField(ref _campaignTitle, value); }
    /// <summary>Identifiant de simulation communiqué au moteur.</summary>
    public string SimulationId { get => _simulationId; set => UpdateCampaignField(ref _simulationId, value); }
    /// <summary>Nombre de runs demandé (mode MultiRun ; le mode Mono force 1).</summary>
    public string RunCountInput { get => _runCountInput; set => UpdateCampaignField(ref _runCountInput, value); }
    /// <summary>Horizon en ticks par run.</summary>
    public string TickCountInput { get => _tickCountInput; set => UpdateCampaignField(ref _tickCountInput, value); }

    /// <summary>Nombre maximal de runs du format (RUN-nnnn sur quatre chiffres).</summary>
    private const int MaxRunCount = 65_535;

    /// <summary>Cadence « direct » : un run Mono se regarde en fenêtre analytique.</summary>
    public const int DirectTicksPerSecond = 10;

    /// <summary>Modes de création d'une expérience (un seul écran, deux rythmes).</summary>
    public IReadOnlyList<string> RunModeOptions { get; } = ["Mono", "MultiRun"];

    /// <summary>Mode choisi : Mono = un run regardable en direct, MultiRun = série batch.</summary>
    public string SelectedRunMode
    {
        get => _selectedRunMode;
        set
        {
            if (SetProperty(ref _selectedRunMode, value))
            {
                OnPropertyChanged(nameof(IsMultiRun));
                // Cadence par défaut du mode : Mono se regarde, MultiRun s'exécute au
                // plus vite. Le choix reste modifiable ensuite dans le même écran.
                SelectedTicksPerSecond = IsMultiRun ? RunEngineProfile.BatchTicksPerSecond : DirectTicksPerSecond;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Vrai en mode MultiRun : le champ « Nombre de runs » n'a de sens que là.</summary>
    public bool IsMultiRun => string.Equals(SelectedRunMode, "MultiRun", StringComparison.Ordinal);

    /// <summary>Vitesses proposées (ticks par seconde) : direct regardable → batch.</summary>
    public IReadOnlyList<int> TicksPerSecondOptions { get; } =
        [DirectTicksPerSecond, 100, RunEngineProfile.BatchTicksPerSecond];

    /// <summary>Vitesse d'exécution choisie, transmise au moteur et archivée dans le paquet.</summary>
    public int SelectedTicksPerSecond
    {
        get => _selectedTicksPerSecond;
        set => SetProperty(ref _selectedTicksPerSecond, value);
    }

    /// <summary>Nombre initial d'agents simulés.</summary>
    public string AgentCountInput { get => _agentCountInput; set => UpdateCampaignField(ref _agentCountInput, value); }
    /// <summary>Graine de base utilisée pour la dérivation reproductible.</summary>
    public string BaseSeedInput { get => _baseSeedInput; set => UpdateCampaignField(ref _baseSeedInput, value); }
    /// <summary>Résultat ou cause du dernier démarrage de campagne.</summary>
    public string CampaignStatus { get => _campaignStatus; private set => SetProperty(ref _campaignStatus, value); }
    /// <summary>Progression détaillée de la campagne courante.</summary>
    public string CampaignProgressText { get => _campaignProgressText; private set => SetProperty(ref _campaignProgressText, value); }
    /// <summary>Avancement du run en cours, en pourcentage des ticks (0 à 100).</summary>
    public double RunTickPercent { get => _runTickPercent; private set => SetProperty(ref _runTickPercent, value); }
    /// <summary>Vrai quand le moteur a rapporté un horizon : sans lui, aucun pourcentage n'est affiché.</summary>
    public bool HasRunTickProgress { get => _hasRunTickProgress; private set => SetProperty(ref _hasRunTickProgress, value); }
    /// <summary>Avancement du run en cours en ticks, tel que rapporté par le moteur.</summary>
    public string RunTickText { get => _runTickText; private set => SetProperty(ref _runTickText, value); }

    /// <summary>
    /// Masque l'avancement du run. Appelé à chaque changement d'état de campagne :
    /// une barre restée du run précédent afficherait un pourcentage trompeur.
    /// </summary>
    private void ResetRunTickProgress()
    {
        HasRunTickProgress = false;
        RunTickPercent = 0;
        RunTickText = string.Empty;
    }
    /// <summary>Commande de création puis d'exécution d'une campagne.</summary>
    public RelayCommand<string> CreateCampaignCommand { get; }
    /// <summary>Commande de reprise d'un paquet récupérable.</summary>
    public RelayCommand<string> ResumeCampaignCommand { get; }
    /// <summary>Commande d'annulation coopérative de la campagne active.</summary>
    public RelayCommand<string> CancelCampaignCommand { get; }
    /// <summary>Indique qu'une campagne est en cours dans cette fenêtre.</summary>
    public bool IsCampaignRunning
    {
        get => _isCampaignRunning;
        private set
        {
            if (SetProperty(ref _isCampaignRunning, value))
            {
                OnPropertyChanged(nameof(CanCreateCampaign));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
    /// <summary>Commande d'ouverture du paquet d'une expérience.</summary>
    public RelayCommand<string> OpenExperienceCommand { get; }
    /// <summary>Documents Markdown embarqués dans le Launcher.</summary>
    public ObservableCollection<DocumentationItemViewModel> DocumentationItems { get; }
    /// <summary>Document affiché dans le lecteur Markdown.</summary>
    public DocumentationItemViewModel? SelectedDocumentation
    {
        get => _selectedDocumentation;
        set
        {
            if (!SetProperty(ref _selectedDocumentation, value) || value is null)
            {
                return;
            }

            try
            {
                DocumentationContent = _orchestration.ReadDocumentation(value.RelativePath);
                DocumentationStatus = value.RelativePath;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                DocumentationContent = string.Empty;
                DocumentationStatus = $"Documentation indisponible : {exception.Message}";
            }
        }
    }
    /// <summary>Contenu du document choisi.</summary>
    public string DocumentationContent { get => _documentationContent; private set => SetProperty(ref _documentationContent, value); }
    /// <summary>État de chargement du document.</summary>
    public string DocumentationStatus { get => _documentationStatus; private set => SetProperty(ref _documentationStatus, value); }
    /// <summary>True quand les paramètres du formulaire passent les validations locales.</summary>
    public bool CanCreateCampaign =>
        !IsCampaignRunning
        && !string.IsNullOrWhiteSpace(CampaignTitle)
        && IsSupportedSimulation
        && int.TryParse(RunCountInput, NumberStyles.None, CultureInfo.InvariantCulture, out var runs) && runs is > 0 and <= MaxRunCount
        && long.TryParse(TickCountInput, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) && ticks > 0
        && int.TryParse(AgentCountInput, NumberStyles.None, CultureInfo.InvariantCulture, out var agents) && agents >= 0
        && long.TryParse(BaseSeedInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
        && SelectedTicksPerSecond is > 0 and <= 100_000;

    private bool IsSupportedSimulation =>
        string.Equals(SimulationId.Trim(), WellKnownSimulations.Reference, StringComparison.Ordinal);

    /// <summary>Profil sélectionné dans la vue Configuration.</summary>
    public string SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                ResolveSelectedProfile();
            }
        }
    }

    /// <summary>
    /// Statut du profil sélectionné, affiché tel quel : « satisfaisable — démarrage : syne → echos »,
    /// ou les causes exactes, verrou Immersion compris, avec le jalon attendu (USER_INTERFACE §3.4).
    /// </summary>
    public string ProfileStatus
    {
        get => _profileStatus;
        private set => SetProperty(ref _profileStatus, value);
    }

    /// <summary>Démarre le profil sélectionné, dans son ordre de démarrage (SYNE en premier).</summary>
    public RelayCommand<string> StartProfileCommand { get; }

    /// <summary>Arrête le profil sélectionné, en ordre inverse strict (COMPONENTS.md §6).</summary>
    public RelayCommand<string> StopProfileCommand { get; }

    /// <summary>Vrai si le profil sélectionné est satisfaisable (bouton Démarrer actif).</summary>
    public bool CanStartProfile => _profileResolution is { Satisfiable: true, StartupOrder.Count: > 0 };

    /// <summary>Vrai si le profil sélectionné a des composants à arrêter (bouton Arrêter actif).</summary>
    public bool CanStopProfile => _profileResolution?.StartupOrder.Count > 0;

    private Func<IReadOnlyList<string>, bool, Task>? _profileLifecycle;

    private void OnCustomComponentChanged(string componentId)
    {
        ResolveSelectedProfile();
    }

    private bool SetSyneEngineMode(string value, bool refresh)
    {
        if (value is not ("Réel" or "Émulé") || !SetProperty(ref _selectedSyneEngineMode, value, nameof(SelectedSyneEngineMode)))
        {
            return false;
        }

        if (refresh)
        {
            RefreshFromRegistry();
        }

        return true;
    }

    /// <summary>Branche l'orchestration réelle du profil (l'application réalise l'action).</summary>
    public void SetProfileLifecycleHandler(Func<IReadOnlyList<string>, bool, Task>? handler) => _profileLifecycle = handler;

    /// <summary>Résout le profil courant contre le registre et met à jour le statut affiché.</summary>
    private void ResolveSelectedProfile()
    {
        var session = SelectedMode switch
        {
            "Console" => SessionKind.Experience,
            "Développement" => SessionKind.Developpement,
            _ => SessionKind.Production,
        };

        var selectedComponents = SelectedMode == "Personnaliser"
            ? CustomComponents.Where(component => component.IsSelected)
                .Select(component => component.Id == "syne" ? SelectedSyneComponentId : component.Id)
                .ToArray()
            : Array.Empty<string>();
        _profileResolution = _orchestration.ResolveProfile(SelectedProfile, session, selectedComponents);
        ProfileStatus = BuildProfileStatus(_profileResolution);
        OnPropertyChanged(nameof(CanStartProfile));
        OnPropertyChanged(nameof(CanStopProfile));
        CommandManager.InvalidateRequerySuggested();
    }

    private static string BuildProfileStatus(ProfileResolution resolution) =>
        resolution.Satisfiable
            ? $"Profil satisfaisable — démarrage : {string.Join(" → ", resolution.StartupOrder.Select(DisplayProfileComponent))}"
            : resolution.Problems.Count > 0
                ? string.Join(" ; ", resolution.Problems)
                : "aucun composant sélectionné (mode Personnalisé)";

    private static string DisplayProfileComponent(string componentId) => componentId switch
    {
        "syne" => "SYNE (réel)",
        "syne-mock" => "SYNE (émulé)",
        "echos" => "ECHOS",
        "prism" => "PRISM",
        _ => componentId,
    };

    private void StartSelectedProfile()
    {
        if (_profileResolution is not { StartupOrder.Count: > 0 } || _profileLifecycle is null)
        {
            return;
        }

        _ = _profileLifecycle.Invoke(_profileResolution.StartupOrder, true);
    }

    private void StopSelectedProfile()
    {
        if (_profileResolution is not { StartupOrder.Count: > 0 } || _profileLifecycle is null)
        {
            return;
        }

        _ = _profileLifecycle.Invoke(OrchestrationService.ShutdownOrder(_profileResolution.StartupOrder), false);
    }

    /// <summary>Libellé « Mode:… » du footer (GUI.md §10).</summary>
    public string ModeLabel => $"Mode:{SelectedMode}";

    /// <summary>État global agrégé, toujours affiché avec sa cause (OBSERVABILITY.md §3.2).</summary>
    public string GlobalState
    {
        get => _globalState;
        private set => SetProperty(ref _globalState, value);
    }

    /// <summary>Cause principale de l'état global, jamais vide sur un état dégradé.</summary>
    public string GlobalCause
    {
        get => _globalCause;
        private set => SetProperty(ref _globalCause, value);
    }

    /// <summary>Pastille du footer : verte sur un système sain, rouge sinon (GUI.md §10).</summary>
    public string SystemDotColor => GlobalState is "Sain" ? "#2ecc71" : GlobalState is "Inactif" ? "#9da6b0" : "#e05252";

    /// <summary>Texte du footer : « Système prêt » ou la cause.</summary>
    public string SystemStatusText => GlobalState is "Sain" or "Inactif" ? "Système prêt" : GlobalState;

    /// <summary>Couleur du texte du footer : vert sur un système sain, ambre sinon.</summary>
    public string SystemTextColor => GlobalState is "Sain" ? "#2ecc71" : "#e8b46a";

    /// <summary>Lignes de l'« État du système » (GUI.md §9.1) et cartes de COMPOSANTS (GUI.md §7).</summary>
    public ObservableCollection<ComponentRowViewModel> Components { get; } = new();

    /// <summary>Ressources échantillonnées (GUI.md §9.2), valeurs textuelles affichées à droite des jauges.</summary>
    public string RamPercent { get => _ramPercent; private set => SetProperty(ref _ramPercent, value); }
    public string CpuPercent { get => _cpuPercent; private set => SetProperty(ref _cpuPercent, value); }
    public string StoragePercent { get => _storagePercent; private set => SetProperty(ref _storagePercent, value); }

    /// <summary>Valeurs numériques des jauges, 0–100 ; NaN rendu comme jauge vide.</summary>
    public double RamValue { get => _ramValue; private set => SetProperty(ref _ramValue, value); }
    public double CpuValue { get => _cpuValue; private set => SetProperty(ref _cpuValue, value); }
    public double StorageValue { get => _storageValue; private set => SetProperty(ref _storageValue, value); }

    /// <summary>Dernières expériences, issues des paquets de la racine (GUI.md §8).</summary>
    public ObservableCollection<ExperienceRowViewModel> Experiences { get; } = new();

    /// <summary>Journal récent, lignes brutes de session (GUI.md §9.3, OBSERVABILITY.md §7).</summary>
    public ObservableCollection<LogEntryViewModel> Logs { get; } = new();
    /// <summary>Journaux des runs du paquet sélectionné dans la vue Logs.</summary>
    public ObservableCollection<PackageLogFileViewModel> RunLogFiles { get; } = new();
    /// <summary>Journal de run affiché.</summary>
    public PackageLogFileViewModel? SelectedRunLog
    {
        get => _selectedRunLog;
        set
        {
            if (!SetProperty(ref _selectedRunLog, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanExportRunLog));
            if (value is null || _runLogPackagePath is null)
            {
                RunLogContent = string.Empty;
                return;
            }

            try
            {
                RunLogContent = _orchestration.ReadPackageRunLog(_runLogPackagePath, value.EntryPath);
                LogStatus = $"{value.RunId} — {value.Name}";
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                RunLogContent = string.Empty;
                LogStatus = $"Lecture du journal du run impossible : {exception.Message}";
            }
        }
    }
    /// <summary>Contenu du journal sélectionné, affiché en lecture seule.</summary>
    public string RunLogContent { get => _runLogContent; private set => SetProperty(ref _runLogContent, value); }
    public bool CanExportRunLog => SelectedRunLog is not null;
    /// <summary>Emplacement des journaux persistants de session.</summary>
    public string SessionLogDirectory => _orchestration.SessionLogDirectory;
    /// <summary>Statut de consultation ou d'export du journal complet.</summary>
    public string LogStatus { get => _logStatus; private set => SetProperty(ref _logStatus, value); }

    /// <summary>
    /// Rapport d'émergence affiché, tel que produit par ECHOS. Aucune présentation ajoutée
    /// ne porte d'information (USER_INTERFACE.md §3.1 : fidélité au fichier produit).
    /// </summary>
    public string EmergenceReport
    {
        get => _emergenceReport;
        private set => SetProperty(ref _emergenceReport, value);
    }

    /// <summary>Statut de provenance du rapport affiché : fichier d'origine ou absence explicite.</summary>
    public string ReportStatus
    {
        get => _reportStatus;
        private set => SetProperty(ref _reportStatus, value);
    }

    /// <summary>Dernier paquet ouvert, pour les écrans d'analyse.</summary>
    public string? OpenedPackagePath { get; private set; }

    /// <summary>Réactualise l'état, la pile, les ressources et le journal depuis la façade.</summary>
    public void RefreshFromRegistry()
    {
        Components.Clear();
        var snapshot = _orchestration.SnapshotComponents();
        var syneRows = snapshot.Where(row => row.Id is "syne" or "syne-mock").ToArray();
        if (syneRows.Length > 0)
        {
            var engine = syneRows.FirstOrDefault(row => row.IsRunning)
                ?? syneRows.FirstOrDefault(row => row.Id == SelectedSyneComponentId)
                ?? syneRows[0];
            Components.Add(new ComponentRowViewModel
            {
                Id = "syne",
                RuntimeComponentId = engine.Id,
                InstanceId = engine.InstanceId,
                Name = "SYNE",
                Version = engine.Version,
                Subtitle = engine.Id == "syne-mock" ? "Moteur — émulation" : "Moteur — réel",
                Technology = engine.Technology,
                Description = "Moteur de simulation multi-agents.",
                Accent = engine.Accent,
                IsAvailable = engine.IsAvailable,
            });
            Components[^1].Update(engine.StateLabel, engine.Cause, engine.ProcessId);
        }

        foreach (var row in snapshot.Where(row => row.Id is not ("syne" or "syne-mock")))
        {
            Components.Add(row);
        }

        var (state, cause) = _orchestration.AggregateHealth(_profileResolution?.StartupOrder ?? ["syne"]);
        GlobalState = state;
        GlobalCause = cause;
        OnPropertyChanged(nameof(SystemDotColor));
        OnPropertyChanged(nameof(SystemStatusText));
        OnPropertyChanged(nameof(SystemTextColor));

        var resources = _orchestration.SampleResources();
        RamValue = double.IsNaN(resources.RamPercent) ? 0 : resources.RamPercent;
        CpuValue = double.IsNaN(resources.CpuPercent) ? 0 : resources.CpuPercent;
        StorageValue = double.IsNaN(resources.StoragePercent) ? 0 : resources.StoragePercent;
        RamPercent = FormatPercent(resources.RamPercent);
        CpuPercent = FormatPercent(resources.CpuPercent);
        StoragePercent = FormatPercent(resources.StoragePercent);

        Logs.Clear();
        foreach (var line in _orchestration.RecentLogs())
        {
            Logs.Add(line);
        }

        // État des composants changé : la satisfiabilité du profil affiché aussi (ADR-006 : verrou évalué).
        ResolveSelectedProfile();
    }

    /// <summary>Recharge les expériences depuis la racine des paquets.</summary>
    public void RefreshExperiences()
    {
        Experiences.Clear();
        foreach (var row in _orchestration.ListExperiences())
        {
            Experiences.Add(row);
        }
    }

    /// <summary>Affiche la progression reportée par le moteur de campagnes.</summary>
    public void UpdateCampaignProgress(CampaignProgress progress)
    {
        CampaignProgressText = $"{progress.CurrentRunId} — graine {progress.CurrentSeed} — {progress.RunsDone}/{progress.RunsTotal} terminé(s), {progress.RunsFailed} échec(s)";

        // Avancement du run, rapporté par le moteur (§8). Sans horizon rapporté, on ne
        // montre aucun pourcentage : une barre à 0 % pour un moteur muet serait un mensonge.
        if (progress.CurrentRunProgress is not { } tick)
        {
            ResetRunTickProgress();
            return;
        }

        RunTickText = tick.MaxTicks is { } reportedMax && reportedMax > 0
            ? $"tick {tick.Tick} / {reportedMax} — {tick.AliveCount} agent(s) vivant(s) — {tick.State}"
            : $"tick {tick.Tick} — {tick.AliveCount} agent(s) vivant(s) — {tick.State}";
        if (tick.MaxTicks is { } total && total > 0)
        {
            RunTickPercent = Math.Clamp(tick.Tick * 100d / total, 0d, 100d);
            HasRunTickProgress = true;
        }
        else
        {
            RunTickPercent = 0;
            HasRunTickProgress = false;
        }
    }

    /// <summary>Navigue vers une entrée de la sidebar.</summary>
    private void Navigate(string? id)
    {
        if (id is null)
        {
            return;
        }

        SelectedNavId = id;
        if (id is NavAccueilId or NavExperiencesId or NavCampagnesId)
        {
            RefreshExperiences();
        }
    }

    private void OpenExperience(string? packagePath)
    {
        if (!string.IsNullOrWhiteSpace(packagePath))
        {
            OpenPackage(packagePath);
        }
    }

    private void UpdateCampaignField(ref string field, string value)
    {
        if (SetProperty(ref field, value))
        {
            OnPropertyChanged(nameof(CanCreateCampaign));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void SelectComponentInstallation(string componentId, ComponentInstallationOption option)
    {
        try
        {
            _orchestration.SetActiveInstallation(componentId, option.Location);
            foreach (var choice in ComponentInstallations.Where(choice => choice.ComponentId == componentId))
            {
                choice.UpdateActive(option.Location);
            }

            RefreshFromRegistry();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            var activeLocation = _orchestration.ListComponentInstallations()
                .FirstOrDefault(option => option.ComponentId == componentId && option.IsActive)?.Location;
            if (activeLocation is not null)
            {
                ComponentInstallations.FirstOrDefault(choice => choice.ComponentId == componentId)?.UpdateActive(activeLocation);
            }

            LogStatus = $"Sélection d'installation impossible : {exception.Message}";
        }
    }

    /// <summary>Détecte, enregistre et recharge une installation sélectionnée dans le système de fichiers.</summary>
    public void AddComponentInstallation(string location)
    {
        try
        {
            _ = _orchestration.RegisterComponentInstallation(location);
            ReloadComponentInstallations();
            RefreshFromRegistry();
            LogStatus = $"Installation enregistrée : {location}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            LogStatus = $"Installation non enregistrée : {exception.Message}";
        }
    }

    private void ReloadComponentInstallations()
    {
        var refreshed = _orchestration.ListComponentInstallations()
            .GroupBy(option => option.ComponentId, StringComparer.Ordinal)
            .Select(group => new ComponentInstallationSelectionViewModel(
                group.Key,
                group.First().Name,
                group.ToArray(),
                group.FirstOrDefault(option => option.IsActive),
                SelectComponentInstallation))
            .ToArray();
        ComponentInstallations.Clear();
        foreach (var component in refreshed)
        {
            ComponentInstallations.Add(component);
        }
    }

    private async Task RunCampaignAsync(string? resumePackagePath = null)
    {
        if (resumePackagePath is null && !IsSupportedSimulation)
        {
            CampaignStatus = $"Scénario non pris en charge : SYNE V1 accepte uniquement « {WellKnownSimulations.Reference} ».";
            return;
        }

        if (resumePackagePath is null && !CanCreateCampaign)
        {
            CampaignStatus = "Paramètres invalides : vérifiez le nom, la simulation, le nombre de runs, les ticks, les agents et la graine.";
            return;
        }

        ExperimentDefinition? definition = null;
        if (resumePackagePath is null)
        {
            definition = new ExperimentDefinition
            {
                Id = $"EXP-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}",
                Title = CampaignTitle.Trim(),
                Profile = WellKnownProfiles.Experience,
                Simulation = SimulationId.Trim(),
                // Mono = un run, toujours : le champ « Nombre de runs » ne sert qu'en MultiRun.
                RunCount = IsMultiRun
                    ? int.Parse(RunCountInput, NumberStyles.None, CultureInfo.InvariantCulture)
                    : 1,
                Ticks = long.Parse(TickCountInput, NumberStyles.None, CultureInfo.InvariantCulture),
                // Cadence d'exécution demandée au moteur (10 = direct regardable,
                // 1000 = batch) — archivée dans config.resolved.json du paquet.
                TicksPerSecond = SelectedTicksPerSecond,
                AgentCount = int.Parse(AgentCountInput, NumberStyles.None, CultureInfo.InvariantCulture),
                BaseSeed = long.Parse(BaseSeedInput, NumberStyles.Integer, CultureInfo.InvariantCulture),
                SeedStrategy = SeedStrategy.Derived,
                FailurePolicy = FailurePolicy.Continue,
            };
        }

        using var cancellation = new CancellationTokenSource();
        _campaignCancellation = cancellation;
        IsCampaignRunning = true;
        CampaignStatus = resumePackagePath is null ? "Création et exécution de la campagne…" : "Reprise de la campagne…";
        CampaignProgressText = "Préparation du premier run…";
        ResetRunTickProgress();
        try
        {
            var packagePath = resumePackagePath is null
                ? await _orchestration.RunCampaignAsync(
                    definition ?? throw new InvalidOperationException("la définition de campagne n'a pas été créée"),
                    cancellation.Token).ConfigureAwait(true)
                : await _orchestration.ResumeCampaignAsync(resumePackagePath, cancellation.Token).ConfigureAwait(true);
            CampaignStatus = $"Campagne terminée et scellée : {Path.GetFileName(packagePath)}";
            CampaignProgressText = "Tous les runs sont terminés.";
            ResetRunTickProgress();
            RefreshExperiences();
        }
        catch (OperationCanceledException)
        {
            CampaignStatus = "Campagne annulée ; le paquet reste récupérable et peut être repris.";
            CampaignProgressText = "La reprise relancera uniquement les runs non terminés.";
            ResetRunTickProgress();
            RefreshExperiences();
        }
        catch (CampaignStoppedException stopped)
        {
            // Politique « stop » : le runner a déjà marqué le paquet récupérable et nommé le
            // run fautif. Sans cette branche, l'exception traverserait la commande et laisserait
            // l'écran afficher « Reprise de la campagne… » indéfiniment.
            CampaignStatus = $"Campagne interrompue : {stopped.Message}";
            CampaignProgressText = $"Paquet récupérable {Path.GetFileName(stopped.PackagePath)} — la reprise relancera le run fautif.";
            ResetRunTickProgress();
            RefreshExperiences();
        }
        catch (Exception exception)
        {
            // Filet : aucun type d'incident ne doit rester silencieux, y compris ceux que le
            // filtre de cas prévus ne couvrait pas — une fin de campagne muette se lit comme un gel.
            CampaignStatus = $"Campagne interrompue : {exception.Message}";
            CampaignProgressText = "Consultez le paquet récupérable et les journaux pour reprendre ou diagnostiquer.";
            ResetRunTickProgress();
            RefreshExperiences();
        }
        finally
        {
            _campaignCancellation = null;
            IsCampaignRunning = false;
        }
    }

    /// <summary>Ouvre un paquet .livexp en lecture et affiche son rapport, tel qu'ECHOS l'a écrit.</summary>
    public void OpenPackage(string packagePath)
    {
        OpenedPackagePath = packagePath;
        SelectedNavId = NavAnalyseId;
        var result = _orchestration.ReadEmergenceReport(packagePath);
        if (result.ErrorMessage is not null)
        {
            EmergenceReport = string.Empty;
            ReportStatus = $"{Path.GetFileName(packagePath)} — lecture du paquet impossible : {result.ErrorMessage}";
            return;
        }

        if (result.Content is null)
        {
            // Sans ECHOS, aucun rapport n'est produit : l'absence est affichée, jamais remplacée.
            EmergenceReport = string.Empty;
            ReportStatus = $"{Path.GetFileName(packagePath)} — rapport d'émergence non encore produit (ECHOS indisponible ou campagne en cours)";
            return;
        }

        EmergenceReport = result.Content;
        ReportStatus = $"{Path.GetFileName(packagePath)} — rapport produit par ECHOS, affiché tel quel";
    }

    /// <summary>Expose un résultat de dialogue de fichier dans l'écran de rapport.</summary>
    public void SetReportStatus(string status) => ReportStatus = status;

    /// <summary>Expose un résultat d'action sur les journaux dans l'écran Logs.</summary>
    public void SetLogStatus(string status) => LogStatus = status;

    /// <summary>Charge les entrées stdout/stderr archivées d'un paquet.</summary>
    public void LoadPackageRunLogs(string packagePath)
    {
        _runLogPackagePath = packagePath;
        RunLogFiles.Clear();
        RunLogContent = string.Empty;
        SelectedRunLog = null;
        try
        {
            foreach (var file in _orchestration.ListPackageRunLogs(packagePath))
            {
                RunLogFiles.Add(file);
            }

            if (RunLogFiles.Count > 0)
            {
                SelectedRunLog = RunLogFiles[0];
                LogStatus = $"{RunLogFiles.Count} journal(aux) trouvé(s) dans {Path.GetFileName(packagePath)}";
            }
            else
            {
                LogStatus = $"Aucun journal de processus dans {Path.GetFileName(packagePath)}.";
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            RunLogFiles.Clear();
            LogStatus = $"Lecture des journaux du paquet impossible : {exception.Message}";
        }
    }

    /// <summary>Exporte tous les journaux de session persistés dans un fichier NDJSON.</summary>
    public async Task ExportSessionLogsAsync(string destinationPath)
    {
        try
        {
            await _orchestration.ExportSessionLogsAsync(destinationPath).ConfigureAwait(true);
            LogStatus = $"Journaux de session exportés : {destinationPath}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            LogStatus = $"Export des journaux impossible : {exception.Message}";
        }
    }

    /// <summary>Exporte le journal de run sélectionné sans extraire le paquet.</summary>
    public async Task ExportSelectedRunLogAsync(string destinationPath)
    {
        if (_runLogPackagePath is null || SelectedRunLog is null)
        {
            LogStatus = "Sélectionnez un journal de run avant l'export.";
            return;
        }

        try
        {
            await _orchestration.ExportPackageRunLogAsync(
                _runLogPackagePath,
                SelectedRunLog.EntryPath,
                destinationPath).ConfigureAwait(true);
            LogStatus = $"Journal du run exporté : {destinationPath}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            LogStatus = $"Export du journal du run impossible : {exception.Message}";
        }
    }

    /// <summary>Branche les actions de cycle de vie réalisées par l'application (G5 : session complète).</summary>
    public void SetLifecycleHandler(Action<string, bool>? handler) => _lifecycle = handler;

    /// <summary>Branche l'ouverture de console réalisée par l'application (USER_INTERFACE.md §9).</summary>
    public void SetConsoleHandler(Action<string>? handler) => _console = handler;

    /// <summary>Branche l'ouverture de la fenêtre d'analyse réalisée par l'application (ADR-007).</summary>
    public void SetAnalysisHandler(Action? handler) => _analysis = handler;

    private static string FormatPercent(double value) => double.IsNaN(value) ? "—" : $"{value:0} %";
}

/// <summary>Une entrée de la sidebar (GUI.md §5.1).</summary>
public sealed class NavItemViewModel(string Id, string Title, bool Selected)
{
    /// <summary>Identifiant stable de l'entrée.</summary>
    public string Id { get; } = Id;

    /// <summary>Libellé affiché.</summary>
    public string Title { get; } = Title;

    /// <summary>Vrai uniquement pour « Accueil » à l'ouverture ; la sélection réelle est portée par le VM.</summary>
    public bool InitiallySelected { get; } = Selected;
}

/// <summary>Entrée du lecteur de documentation.</summary>
public sealed record DocumentationItemViewModel(string RelativePath, string Title);

/// <summary>Un composant sélectionnable en mode Personnaliser.</summary>
public sealed class CustomComponentSelectionViewModel : ObservableObject
{
    private readonly Action _onChanged;
    private bool _isSelected;

    public CustomComponentSelectionViewModel(string id, string name, bool isSelected, Action onChanged)
    {
        Id = id;
        Name = name;
        _isSelected = isSelected;
        _onChanged = onChanged;
    }

    public string Id { get; }
    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _onChanged();
            }
        }
    }
}

/// <summary>Une ligne de composant : colonne d'état (GUI.md §9.1) et carte de COMPOSANTS (GUI.md §7).</summary>
public sealed class ComponentRowViewModel : ObservableObject
{
    private string _stateLabel = string.Empty;
    private string _cause = string.Empty;
    private int? _processId;

    /// <summary>Identifiant de type (« syne », « echos », « prism »).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Identifiant réel du processus contrôlé ; peut être SYNE ou son émulateur.</summary>
    public string RuntimeComponentId { get; init; } = string.Empty;

    /// <summary>Identifiant d'instance en cours, null si le composant n'est pas démarré ; ouvre la console.</summary>
    public string? InstanceId { get; init; }

    /// <summary>Vrai si une console peut être ouverte : il faut une instance vivante.</summary>
    public bool CanOpenConsole => !string.IsNullOrEmpty(InstanceId);

    /// <summary>Nom affichable du composant.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Version déclarée.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Sous-titre de la carte, selon le composant.</summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>Technologie affichée sur la carte.</summary>
    public string Technology { get; init; } = string.Empty;

    /// <summary>Description de la carte, deux lignes.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Couleur d'accent du composant (GUI.md §3.1).</summary>
    public string Accent { get; init; } = "#9aa0aa";

    /// <summary>Vrai si une installation détectée et son manifeste permettent un démarrage.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>Actions permises selon la disponibilité et l'état courant.</summary>
    public bool CanStart => IsAvailable && !IsRunning;
    public bool CanStop => IsRunning;

    /// <summary>Le bouton bascule démarrer/arrêter est actif si l'une des deux actions est permise.</summary>
    public bool CanToggle => CanStart || CanStop;

    /// <summary>Libellé français de l'état, jamais la couleur seule (USER_INTERFACE.md §6).</summary>
    public string StateLabel { get => _stateLabel; private set => SetProperty(ref _stateLabel, value); }

    /// <summary>Cause principale, obligatoire sur un état autre qu'Inactif.</summary>
    public string Cause { get => _cause; private set => SetProperty(ref _cause, value); }

    /// <summary>PID du processus, masqué si arrêté.</summary>
    public int? ProcessId { get => _processId; private set => SetProperty(ref _processId, value); }

    /// <summary>Vrai si le composant est considéré démarré (Actif, Prêt, Démarrage, Suspendu).</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Couleur d'état (GUI.md §3.1) : vert actif, bleu démarrage, rouge défaillant, gris arrêté.</summary>
    public string StateColor { get; private set; } = "#9da6b0";

    /// <summary>Libellé du bouton principal : « Arrêter » si démarré, « Démarrer » sinon.</summary>
    public string ToggleLabel => IsRunning ? "Arrêter" : "Démarrer";

    /// <summary>Met à jour l'état affiché et recalcule les dérivés.</summary>
    public void Update(string stateLabel, string cause, int? processId)
    {
        StateLabel = stateLabel;
        Cause = cause;
        ProcessId = processId;
        IsRunning = stateLabel is "Actif" or "Prêt" or "Démarrage" or "Suspendu";
        StateColor = stateLabel switch
        {
            "Actif" or "Prêt" => "#2ecc71",
            "Démarrage" => "#2d9bf0",
            "Défaillant" => "#e05252",
            _ => "#9da6b0",
        };
        OnPropertyChanged(nameof(ToggleLabel));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanToggle));
        OnPropertyChanged(nameof(StateColor));
    }
}

/// <summary>Une ligne du tableau « Dernières expériences » (GUI.md §8).</summary>
public sealed class ExperienceRowViewModel
{
    /// <summary>Chemin du paquet source à ouvrir.</summary>
    public string PackagePath { get; init; } = string.Empty;

    /// <summary>Identifiant d'expérience.</summary>
    public string ExperimentId { get; init; } = string.Empty;

    /// <summary>Nom affiché.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Date de création, localisée.</summary>
    public string Date { get; init; } = string.Empty;

    /// <summary>Durée ou progression (« 5 runs », « 10 000 ticks »…).</summary>
    public string Duration { get; init; } = string.Empty;

    /// <summary>Statut affiché : Terminée, En cours, Récupérable.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Vrai si le statut est vert (terminée).</summary>
    public bool IsDone => Status == "Terminée";

    /// <summary>Vrai si le statut est bleu (en cours).</summary>
    public bool IsRunning => Status == "En cours";

    /// <summary>Vrai si le statut est ambre (récupérable).</summary>
    public bool IsRecoverable => Status == "Récupérable";
}

/// <summary>Une entrée du panneau « Logs récents » (GUI.md §9.3).</summary>
public sealed class LogEntryViewModel
{
    /// <summary>Niveau : Info, Warn, Error.</summary>
    public string Level { get; init; } = "Info";

    /// <summary>Message lisible.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Horodatage affiché, localisé.</summary>
    public string Timestamp { get; init; } = string.Empty;

    /// <summary>Couleur du niveau (GUI.md §3.1) : bleu INFO, ambre WARN, rouge ERROR.</summary>
    public string LevelColor => Level switch
    {
        "Warn" => "#f0a05a",
        "Error" => "#e05252",
        _ => "#2d9bf0",
    };
}

/// <summary>
/// Façade d'orchestration exposée à la présentation (l'application réalise cette interface
/// en adaptant le domaine). Garde la frontière : la présentation ne dépend jamais de
/// l'infrastructure ni des services concrets.
/// </summary>
public interface IOrchestrationFacade
{
    /// <summary>Progression reçue de l'exécuteur de campagnes.</summary>
    event EventHandler<CampaignProgress>? CampaignProgressChanged;
    /// <summary>Lignes de composants pour la colonne d'état et les cartes.</summary>
    IReadOnlyList<ComponentRowViewModel> SnapshotComponents();

    /// <summary>Agrégation de santé globale : état et cause principale.</summary>
    (string GlobalState, string MainCause) AggregateHealth(IReadOnlyList<string> requiredComponents);

    /// <summary>Résolution d'un profil.</summary>
    ProfileResolution ResolveProfile(string profileId, SessionKind session, IReadOnlyCollection<string>? extraComponents = null);

    /// <summary>
    /// Lit le rapport d'émergence produit par ECHOS dans un paquet .livexp (ADR-003 :
    /// le Launcher présente le fichier, sans le recalculer). Nul s'il n'est pas encore produit.
    /// </summary>
    ReportReadResult ReadEmergenceReport(string packagePath);

    /// <summary>Démarre ou arrête un composant de la pile (cycle de vie réel, INTEGRATION_CONTRACT.md §5).</summary>
    Task<string?> ToggleComponentAsync(string componentId, bool start);

    /// <summary>Échantillonne les ressources locales (GUI.md §9.2), sans métrique scientifique.</summary>
    ResourceSnapshot SampleResources();

    /// <summary>Lit les dernières lignes du journal de session (OBSERVABILITY.md §7).</summary>
    IReadOnlyList<LogEntryViewModel> RecentLogs();

    /// <summary>Répertoire contenant les journaux persistants de session.</summary>
    string SessionLogDirectory { get; }

    /// <summary>Exporte l'historique complet des journaux de session en NDJSON.</summary>
    Task ExportSessionLogsAsync(string destinationPath);

    /// <summary>Liste les fichiers de journal de run d'un paquet sans les extraire.</summary>
    IReadOnlyList<PackageLogFileViewModel> ListPackageRunLogs(string packagePath);

    /// <summary>Lit un fichier de journal de run autorisé du paquet.</summary>
    string ReadPackageRunLog(string packagePath, string entryPath);

    /// <summary>Exporte un fichier de journal de run autorisé du paquet.</summary>
    Task ExportPackageRunLogAsync(string packagePath, string entryPath, string destinationPath);

    /// <summary>Liste les installations valides et l'installation active par composant.</summary>
    IReadOnlyList<ComponentInstallationOption> ListComponentInstallations();

    /// <summary>Sélectionne une installation préalablement détectée du composant.</summary>
    void SetActiveInstallation(string componentId, string location);

    /// <summary>Valide, enregistre et rend persistante une installation locale.</summary>
    ComponentInstallationOption RegisterComponentInstallation(string location);

    /// <summary>Liste les expériences connues, depuis la racine des paquets.</summary>
    IReadOnlyList<ExperienceRowViewModel> ListExperiences();

    /// <summary>Exécute une campagne et renvoie le paquet scellé.</summary>
    Task<string> RunCampaignAsync(ExperimentDefinition definition, CancellationToken cancellationToken);

    /// <summary>Reprend un paquet vivant ou récupérable depuis sa définition enregistrée.</summary>
    Task<string> ResumeCampaignAsync(string packagePath, CancellationToken cancellationToken);

    /// <summary>Liste les documents Markdown distribués avec le Launcher.</summary>
    IReadOnlyList<DocumentationItemViewModel> ListDocumentation();

    /// <summary>Lit un document identifié par son chemin relatif dans la liste.</summary>
    string ReadDocumentation(string relativePath);
}

/// <summary>Échantillon de ressources locales affiché dans la colonne d'état.</summary>
public sealed record ResourceSnapshot(double RamPercent, double CpuPercent, double StoragePercent);

/// <summary>Résultat de lecture distinct de l'absence attendue du rapport.</summary>
public sealed record ReportReadResult(string? Content, string? ErrorMessage);

/// <summary>Fichier de journal d'un run stocké dans un paquet LIVEX.</summary>
public sealed record PackageLogFileViewModel(string RunId, string Name, string EntryPath);

/// <summary>Installation détectée destinée au sélecteur de configuration.</summary>
public sealed record ComponentInstallationOption(
    string ComponentId,
    string Name,
    string Version,
    string Location,
    bool IsActive);

/// <summary>Sélecteur des installations valides d'un composant.</summary>
public sealed class ComponentInstallationSelectionViewModel : ObservableObject
{
    private readonly Action<string, ComponentInstallationOption> _select;
    private ComponentInstallationOption? _selected;

    public ComponentInstallationSelectionViewModel(
        string componentId,
        string name,
        IReadOnlyList<ComponentInstallationOption> options,
        ComponentInstallationOption? selected,
        Action<string, ComponentInstallationOption> select)
    {
        ComponentId = componentId;
        Name = name;
        Options = options;
        _selected = selected;
        _select = select;
    }

    public string ComponentId { get; }
    public string Name { get; }
    public IReadOnlyList<ComponentInstallationOption> Options { get; }
    public ComponentInstallationOption? Selected
    {
        get => _selected;
        set
        {
            if (value is not null && SetProperty(ref _selected, value))
            {
                _select(ComponentId, value);
            }
        }
    }

    public void UpdateActive(string location)
    {
        var selected = Options.FirstOrDefault(option =>
            string.Equals(option.Location, location, StringComparison.OrdinalIgnoreCase));
        if (selected is not null && !Equals(_selected, selected))
        {
            SetProperty(ref _selected, selected, nameof(Selected));
        }
    }
}
