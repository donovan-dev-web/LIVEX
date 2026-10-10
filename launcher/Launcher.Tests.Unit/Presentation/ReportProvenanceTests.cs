using Launcher.Application;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Package;
using Launcher.Protocol;
using Launcher.Protocol.Model;
using Launcher.Presentation.ViewModel;
using Xunit;

namespace Launcher.Tests.Unit.Presentation;

/// <summary>
/// Façade en mémoire réalisée par un banc de test : expose uniquement ce que la présentation
/// a le droit de connaître (ADR-003 : aucun calcul scientifique dans la présentation).
/// </summary>
public sealed class FakeOrchestrationFacade : IOrchestrationFacade
{
    public event EventHandler<CampaignProgress>? CampaignProgressChanged { add { } remove { } }
    public string? LastProfileId { get; private set; }
    public IReadOnlyCollection<string>? LastExtraComponents { get; private set; }

    /// <summary>Rapport à renvoyer, ou null pour simuler l'absence d'ECHOS.</summary>
    public string? Report { get; set; }
    public string? ReportError { get; set; }
    public IReadOnlyList<PackageLogFileViewModel> RunLogs { get; set; } = [];
    public string RunLogContent { get; set; } = string.Empty;
    public (string PackagePath, string EntryPath, string DestinationPath)? LastRunLogExport { get; private set; }
    public IReadOnlyList<ComponentInstallationOption> Installations { get; set; } = [];
    public string? ActiveInstallationLocation { get; private set; }

    /// <summary>Nombre d'appels de cycle de vie, pour vérifier le câblage des cartes.</summary>
    public List<(string ComponentId, bool Start)> ToggleCalls { get; } = new();

    /// <inheritdoc />
    public IReadOnlyList<ComponentRowViewModel> ComponentSnapshot { get; set; } =
    [
        new ComponentRowViewModel
        {
            Id = "syne",
            RuntimeComponentId = "syne",
            Name = "SYNE",
            Version = "0.1.0-stub",
            Subtitle = "Moteur de simulation multi-agents",
            Technology = ".NET 10",
            Description = "Moteur simulé du banc.",
            Accent = "#1fa8e8",
        },
    ];

    public IReadOnlyList<ComponentRowViewModel> SnapshotComponents() => ComponentSnapshot;

    /// <inheritdoc />
    public (string GlobalState, string MainCause) AggregateHealth(IReadOnlyList<string> requiredComponents) => ("Inactif", "aucun composant démarré");

    /// <inheritdoc />
    public ProfileResolution ResolveProfile(string profileId, SessionKind session, IReadOnlyCollection<string>? extraComponents = null)
    {
        LastProfileId = profileId;
        LastExtraComponents = extraComponents;
        IReadOnlyCollection<string> components = profileId == WellKnownProfiles.Personnalise
            ? extraComponents ?? Array.Empty<string>()
            : ["syne"];
        var resolution = new ProfileResolution { Satisfiable = components.Count > 0 };
        resolution.StartupOrder.AddRange(components);
        return resolution;
    }

    /// <inheritdoc />
    public ReportReadResult ReadEmergenceReport(string packagePath) => new(Report, ReportError);

    /// <inheritdoc />
    public Task<string?> ToggleComponentAsync(string componentId, bool start)
    {
        ToggleCalls.Add((componentId, start));
        return Task.FromResult<string?>(null);
    }

    /// <summary>Résultat renvoyé par StartEnginePrismAsync (scriptable pour les tests PRISM).</summary>
    public PrismEngineStart PrismStartResult { get; set; } =
        new(null, "syne", "http://127.0.0.1:5181", "ws://127.0.0.1:5180/");

    /// <summary>Nombre de démarrages PRISM demandés.</summary>
    public int PrismStartCalls { get; private set; }

    /// <inheritdoc />
    public Task<PrismEngineStart> StartEnginePrismAsync()
    {
        PrismStartCalls++;
        return Task.FromResult(PrismStartResult);
    }

    /// <inheritdoc />
    public ResourceSnapshot SampleResources() => new(double.NaN, double.NaN, 31);

    /// <inheritdoc />
    public IReadOnlyList<LogEntryViewModel> RecentLogs() => [];
    public string SessionLogDirectory => Path.Combine(Path.GetTempPath(), "livex-test-logs");
    public Task ExportSessionLogsAsync(string destinationPath) => Task.CompletedTask;
    public IReadOnlyList<PackageLogFileViewModel> ListPackageRunLogs(string packagePath) => RunLogs;
    public string ReadPackageRunLog(string packagePath, string entryPath) => RunLogContent;
    public Task ExportPackageRunLogAsync(string packagePath, string entryPath, string destinationPath)
    {
        LastRunLogExport = (packagePath, entryPath, destinationPath);
        return Task.CompletedTask;
    }
    public IReadOnlyList<ComponentInstallationOption> ListComponentInstallations() => Installations;
    public void SetActiveInstallation(string componentId, string location) => ActiveInstallationLocation = location;
    public ComponentInstallationOption RegisterComponentInstallation(string location) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public IReadOnlyList<ExperienceRowViewModel> ListExperiences() =>
    [
        new ExperienceRowViewModel
        {
            ExperimentId = "EXP-001",
            Name = "Test Emergence 03",
            Date = "27 sept. 2026 14:32",
            Duration = "1 run",
            Status = "Terminée",
        },
    ];

    /// <summary>Dernière définition soumise à la création — vérifie Mono/MultiRun et la cadence.</summary>
    public ExperimentDefinition? LastDefinition { get; private set; }

    public Task<string> RunCampaignAsync(ExperimentDefinition definition, CancellationToken cancellationToken)
    {
        LastDefinition = definition;
        return Task.FromResult(string.Empty);
    }

    public Task<string> ResumeCampaignAsync(string packagePath, CancellationToken cancellationToken) =>
        Task.FromResult(packagePath);

    public IReadOnlyList<DocumentationItemViewModel> ListDocumentation() =>
        [new DocumentationItemViewModel("docs-launcher/README.md", "README")];

    public string ReadDocumentation(string relativePath) => $"# {relativePath}";
}

/// <summary>
/// Tests de la vue principale : provenance du rapport (ADR-003), navigation à neuf entrées,
/// modes de lancement et cycle de vie des cartes.
/// </summary>
public sealed class ReportProvenanceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"livexp-prov-{Guid.NewGuid():N}");

    public ReportProvenanceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Le rapport affiché est fidèle, octet pour octet, au fichier écrit par ECHOS.</summary>
    [Fact]
    public void Rapport_affiche_fidele_au_fichier_echos()
    {
        var reportContent = "# Rapport d'émergence\n\n## Synthèse statistique\n\nProduit par ECHOS — version 0.9.0.\n";
        var path = Path.Combine(_directory, "EXP-PROV.livexp");
        WritePackageWithReport(path, reportContent);

        var facade = new FakeOrchestrationFacade
        {
            Report = ReadReportFromPackage(path),
        };
        var viewModel = new MainWindowViewModel(facade);

        viewModel.OpenPackage(path);

        Assert.Equal(reportContent, viewModel.EmergenceReport);
        Assert.Contains("produit par ECHOS", viewModel.ReportStatus);
        Assert.Equal(MainWindowViewModel.NavAnalyseId, viewModel.SelectedNavId);
    }

    /// <summary>Sans ECHOS, l'absence est affichée explicitement, jamais remplacée par une approximation.</summary>
    [Fact]
    public void Absence_de_rapport_affichee_explicitement()
    {
        var path = Path.Combine(_directory, "EXP-SANS.livexp");
        WritePackageWithReport(path, null);

        var viewModel = new MainWindowViewModel(new FakeOrchestrationFacade { Report = null });

        viewModel.OpenPackage(path);

        Assert.Equal(string.Empty, viewModel.EmergenceReport);
        Assert.Contains("non encore produit", viewModel.ReportStatus);
    }

    [Fact]
    public void Erreur_de_lecture_du_paquet_est_distincte_de_l_absence_de_rapport()
    {
        var viewModel = new MainWindowViewModel(new FakeOrchestrationFacade
        {
            ReportError = "paquet corrompu",
        });

        viewModel.OpenPackage(Path.Combine(_directory, "EXP-CORROMPU.livexp"));

        Assert.Empty(viewModel.EmergenceReport);
        Assert.Contains("lecture du paquet impossible", viewModel.ReportStatus);
        Assert.Contains("paquet corrompu", viewModel.ReportStatus);
    }

    /// <summary>
    /// La navigation expose les écrans dans leur ordre (USER_INTERFACE.md) : le mode PRISM
    /// a son écran dédié, entre Expériences et Analyse. « Campagnes » n'existe plus :
    /// expériences et campagnes ne font qu'un écran.
    /// </summary>
    [Fact]
    public void Navigation_neuf_entrees_dans_l_ordre_v1()
    {
        var viewModel = new MainWindowViewModel(new FakeOrchestrationFacade());

        Assert.Equal(9, viewModel.NavItems.Count);
        Assert.Equal(
            ["Accueil", "Expériences", "PRISM", "Analyse", "Rapports", "Configuration", "Logs", "Monitoring", "Documentation"],
            viewModel.NavItems.Select(item => item.Title).ToList());
        Assert.DoesNotContain(viewModel.NavItems, item => item.Id == MainWindowViewModel.NavCampagnesId);

        Assert.Equal(MainWindowViewModel.NavAccueilId, viewModel.SelectedNavId);
        Assert.Equal(0, viewModel.SelectedNavIndex);
        viewModel.NavigateCommand.Execute(MainWindowViewModel.NavAnalyseId);
        Assert.Equal(MainWindowViewModel.NavAnalyseId, viewModel.SelectedNavId);
        Assert.Equal(3, viewModel.SelectedNavIndex);
        Assert.Equal("Analyse", viewModel.SelectedNavTitle);
    }

    /// <summary>
    /// Mode PRISM : le démarrage pilote le moteur (posture pilotée), expose les points
    /// d'accès du projet Unreal et verrouille l'écran Expériences (parcours exclusifs) ;
    /// l'arrêt rouvre l'Expérience.
    /// </summary>
    [Fact]
    public async Task Mode_prism_pilote_le_moteur_et_verrouille_lexperience()
    {
        var facade = new FakeOrchestrationFacade
        {
            PrismStartResult = new PrismEngineStart(null, "syne", "http://127.0.0.1:5181", "ws://127.0.0.1:5180/"),
        };
        var viewModel = new MainWindowViewModel(facade) { CampaignTitle = "Campagne verrouillée" };

        // Au départ : pas de mode PRISM, l'Expérience est accessible.
        Assert.False(viewModel.IsPrismRunning);
        Assert.False(viewModel.IsExperienceLocked);
        Assert.True(viewModel.CanCreateCampaign);

        // Démarrage PRISM : moteur piloté, points d'accès affichés, Expérience verrouillée.
        viewModel.StartPrismCommand.Execute("start");
        await Task.Delay(1);

        Assert.Equal(1, facade.PrismStartCalls);
        Assert.True(viewModel.IsPrismRunning);
        Assert.True(viewModel.IsExperienceLocked);
        Assert.Equal("http://127.0.0.1:5181", viewModel.PrismControlUrl);
        Assert.Equal("ws://127.0.0.1:5180/", viewModel.PrismObserveUrl);
        Assert.False(viewModel.CanCreateCampaign);

        // Arrêt : le moteur s'arrête, l'Expérience se rouvre.
        viewModel.StopPrismCommand.Execute("stop");
        await Task.Delay(1);

        Assert.Equal(("syne", false), Assert.Single(facade.ToggleCalls));
        Assert.False(viewModel.IsPrismRunning);
        Assert.False(viewModel.IsExperienceLocked);
        Assert.True(viewModel.CanCreateCampaign);
    }

    /// <summary>
    /// Un seul écran, champs explicites : 1 run par défaut et cadence directe (10 ticks/s,
    /// regardable en fenêtre d'analyse) ; le champ « Nombre de runs » pilote la série, la
    /// vitesse reste libre. La cadence choisie part dans la définition — donc dans le
    /// `config.resolved.json` du paquet.
    /// </summary>
    [Fact]
    public async Task Creation_defaut_un_seul_run_et_cadence_directe_puis_serie()
    {
        var facade = new FakeOrchestrationFacade();
        var viewModel = new MainWindowViewModel(facade) { CampaignTitle = "Essai direct" };

        Assert.Equal(MainWindowViewModel.DirectTicksPerSecond, viewModel.SelectedTicksPerSecond);
        Assert.True(viewModel.CanCreateCampaign);

        // Par défaut : un seul run, cadence directe.
        viewModel.CreateCampaignCommand.Execute("start");
        await Task.Delay(1);

        var definition = Assert.IsType<ExperimentDefinition>(facade.LastDefinition);
        Assert.Equal(1, definition.RunCount);
        Assert.Equal(MainWindowViewModel.DirectTicksPerSecond, definition.TicksPerSecond);

        // Série : le champ « Nombre de runs » pilote le compte, la vitesse passe en batch.
        viewModel.RunCountInput = "3";
        viewModel.SelectedTicksPerSecond = Launcher.Application.RunEngineProfile.BatchTicksPerSecond;
        viewModel.CreateCampaignCommand.Execute("start");
        await Task.Delay(1);

        definition = Assert.IsType<ExperimentDefinition>(facade.LastDefinition);
        Assert.Equal(3, definition.RunCount);
        Assert.Equal(Launcher.Application.RunEngineProfile.BatchTicksPerSecond, definition.TicksPerSecond);
        Assert.Empty(definition.Validate());
    }

    /// <summary>La vitesse reste libre : on peut regarder un run, ou accélérer une série.</summary>
    [Fact]
    public void La_cadence_de_lexecution_est_choisissable()
    {
        var viewModel = new MainWindowViewModel(new FakeOrchestrationFacade());

        Assert.Equal([10, 100, 1000], viewModel.TicksPerSecondOptions);
        viewModel.SelectedTicksPerSecond = 100;
        Assert.Equal(100, viewModel.SelectedTicksPerSecond);
        Assert.True(viewModel.CanCreateCampaign);

        viewModel.SelectedTicksPerSecond = 1000;
        Assert.Equal(1000, viewModel.SelectedTicksPerSecond);
        Assert.True(viewModel.CanCreateCampaign);
    }

    /// <summary>
    /// Le profil Immersion n'est pas une entrée de navigation (il vit dans le sélecteur de
    /// session) ; en revanche le MODE PRISM — posture pilotée du moteur — a son écran dédié.
    /// </summary>
    [Fact]
    public void Immersion_n_est_pas_une_entree_de_navigation()
    {
        var viewModel = new MainWindowViewModel(new FakeOrchestrationFacade());

        Assert.DoesNotContain(viewModel.NavItems, item => item.Id == "immersion");
        Assert.Contains(viewModel.NavItems, item => item.Id == MainWindowViewModel.NavPrismId);
        Assert.Equal("Standard", viewModel.SelectedMode);
        Assert.Equal("Mode:Standard", viewModel.ModeLabel);
    }

    [Fact]
    public void Modes_V1_selectionnent_les_profils_et_le_mode_personnalise_propage_les_composants()
    {
        var facade = new FakeOrchestrationFacade();
        var viewModel = new MainWindowViewModel(facade);

        Assert.Equal(["Console", "Standard", "Développement", "Personnaliser"], viewModel.SessionKinds);
        viewModel.SelectedMode = "Console";
        Assert.Equal(WellKnownProfiles.Experience, facade.LastProfileId);

        viewModel.SelectedMode = "Développement";
        Assert.Equal(WellKnownProfiles.Developpement, facade.LastProfileId);

        viewModel.SelectedMode = "Personnaliser";
        viewModel.CustomComponents.Single(component => component.Id == "echos").IsSelected = true;
        Assert.Equal(WellKnownProfiles.Personnalise, facade.LastProfileId);
        Assert.Equal(["syne-mock", "echos"], facade.LastExtraComponents);
    }

    [Fact]
    public void Personnaliser_sans_composant_ne_propose_pas_de_demarrage()
    {
        var viewModel = new MainWindowViewModel(new FakeOrchestrationFacade())
        {
            SelectedMode = "Personnaliser",
        };
        foreach (var component in viewModel.CustomComponents)
        {
            component.IsSelected = false;
        }

        Assert.False(viewModel.CanStartProfile);
    }

    [Fact]
    public void Personnaliser_propose_un_seul_composant_syne_avec_choix_de_moteur()
    {
        var facade = new FakeOrchestrationFacade();
        var viewModel = new MainWindowViewModel(facade)
        {
            SelectedMode = "Personnaliser",
        };
        viewModel.SelectedSyneEngineMode = "Émulé";

        Assert.Equal(["syne", "echos", "prism"], viewModel.CustomComponents.Select(component => component.Id));
        Assert.Equal(["syne-mock"], facade.LastExtraComponents);
    }

    [Fact]
    public void Dashboard_etat_systeme_regroupent_syne_reel_et_emule()
    {
        var facade = new FakeOrchestrationFacade
        {
            ComponentSnapshot =
            [
                new ComponentRowViewModel
                {
                    Id = "syne",
                    Name = "SYNE",
                    Version = "1.0.0",
                    Accent = "#1fa8e8",
                    IsAvailable = true,
                },
                new ComponentRowViewModel
                {
                    Id = "syne-mock",
                    Name = "SYNE — mock",
                    Version = "0.1.0",
                    Accent = "#1fa8e8",
                    IsAvailable = true,
                },
                new ComponentRowViewModel { Id = "echos", Name = "ECHOS" },
                new ComponentRowViewModel { Id = "prism", Name = "PRISM" },
            ],
        };
        facade.ComponentSnapshot[1].Update("Prêt", "émulateur actif", 1234);
        var viewModel = new MainWindowViewModel(facade);

        viewModel.RefreshFromRegistry();

        Assert.Equal(["syne", "echos", "prism"], viewModel.Components.Select(component => component.Id));
        var syne = viewModel.Components[0];
        Assert.Equal("syne-mock", syne.RuntimeComponentId);
        Assert.Equal("Moteur — émulation", syne.Subtitle);
        Assert.Equal("Prêt", syne.StateLabel);
        Assert.Equal(1234, syne.ProcessId);
    }

    [Fact]
    public void Carte_syne_demarre_la_variante_emule_selectionnee()
    {
        var facade = new FakeOrchestrationFacade
        {
            ComponentSnapshot =
            [
                new ComponentRowViewModel { Id = "syne", IsAvailable = true },
                new ComponentRowViewModel { Id = "syne-mock", IsAvailable = true },
            ],
        };
        var viewModel = new MainWindowViewModel(facade)
        {
            SelectedMode = "Personnaliser",
            SelectedSyneEngineMode = "Émulé",
        };
        viewModel.SetLifecycleHandler((componentId, start) => facade.ToggleComponentAsync(componentId, start));

        viewModel.ToggleComponentCommand.Execute("syne");

        Assert.Equal(("syne-mock", true), Assert.Single(facade.ToggleCalls));
    }

    [Fact]
    public void Documentation_chargee_par_le_lecteur_embarque()
    {
        var viewModel = new MainWindowViewModel(new FakeOrchestrationFacade());

        Assert.Equal("# docs-launcher/README.md", viewModel.DocumentationContent);
        Assert.Equal("docs-launcher/README.md", viewModel.DocumentationStatus);
    }

    [Fact]
    public async Task Export_logs_affiche_le_statut_de_succes()
    {
        var destination = Path.Combine(_directory, "session.ndjson");
        var viewModel = new MainWindowViewModel(new FakeOrchestrationFacade());

        await viewModel.ExportSessionLogsAsync(destination);

        Assert.Contains(destination, viewModel.LogStatus);
    }

    /// <summary>Le bouton d'une carte démarre un composant arrêté, puis l'arrête (cycle de vie réel).</summary>
    [Fact]
    public void Carte_composant_bascule_le_cycle_de_vie()
    {
        var facade = new FakeOrchestrationFacade();
        var viewModel = new MainWindowViewModel(facade);
        viewModel.SetLifecycleHandler((componentId, start) => facade.ToggleComponentAsync(componentId, start));
        viewModel.RefreshFromRegistry();

        var syne = viewModel.Components.Single(component => component.Id == "syne");
        Assert.False(syne.IsRunning);

        viewModel.ToggleComponentCommand.Execute("syne");
        Assert.Single(facade.ToggleCalls);
        Assert.Equal(("syne", true), facade.ToggleCalls[0]);
    }

    /// <summary>Les expériences rechargées depuis la racine alimentent le tableau de l'accueil.</summary>
    [Fact]
    public void Experiences_rechargees_depuis_la_facade()
    {
        var viewModel = new MainWindowViewModel(new FakeOrchestrationFacade());

        viewModel.RefreshExperiences();

        var experience = Assert.Single(viewModel.Experiences);
        Assert.Equal("Test Emergence 03", experience.Name);
        Assert.True(experience.IsDone);
    }

    private static void WritePackageWithReport(string path, string? reportContent)
    {
        var definition = new ExperimentDefinition
        {
            Id = "EXP-PROV-001",
            Title = "Campagne provenance",
            Profile = WellKnownProfiles.Analyse,
            Simulation = "ecosystem_01",
            RunCount = 1,
            Ticks = 10,
            SeedStrategy = SeedStrategy.Derived,
            BaseSeed = 7,
            FailurePolicy = FailurePolicy.Continue,
        };
        using var writer = LivexPackageWriter.CreateNew(path, definition, "0.1.0-test", DateTimeOffset.UnixEpoch);
        writer.CompleteRun(
            "RUN-0001",
            new RunIndexEntry { RunId = "RUN-0001", Status = RunStatuses.Termine, Seed = 7 },
            "{\"runId\":\"RUN-0001\"}",
            "{\"config\":\"resolved\"}",
            new Dictionary<string, RunDataFile>(),
            Array.Empty<(string, byte[])>(),
            JournalLine.Pack(DateTimeOffset.UnixEpoch, "run_completed", "run terminé", "RUN-0001"));
        if (reportContent is not null)
        {
            writer.WriteEntry(PackageConstants.EmergenceReportEntry, reportContent);
        }
    }

    private static string? ReadReportFromPackage(string path)
    {
        using var reader = new LivexPackageReader(path);
        return reader.ReadEmergenceReport();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    [Fact]
    public async Task Journaux_de_run_peuvent_etre_parcourus_et_exportes()
    {
        const string packagePath = "/tmp/example.livexp";
        const string entryPath = "runs/RUN-0001/logs/stdout.log";
        var facade = new FakeOrchestrationFacade
        {
            RunLogs = [new PackageLogFileViewModel("RUN-0001", "stdout.log", entryPath)],
            RunLogContent = "sortie de simulation\n",
        };
        var viewModel = new MainWindowViewModel(facade);

        viewModel.LoadPackageRunLogs(packagePath);
        Assert.Single(viewModel.RunLogFiles);
        Assert.Equal(facade.RunLogContent, viewModel.RunLogContent);
        Assert.True(viewModel.CanExportRunLog);

        await viewModel.ExportSelectedRunLogAsync("/tmp/run.log");

        Assert.Equal((packagePath, entryPath, "/tmp/run.log"), facade.LastRunLogExport);
    }

    [Fact]
    public void Installation_active_peut_etre_changee_depuis_configuration()
    {
        var facade = new FakeOrchestrationFacade
        {
            Installations =
            [
                new ComponentInstallationOption("syne", "SYNE", "1.0.0", "/opt/syne-1", true),
                new ComponentInstallationOption("syne", "SYNE", "2.0.0", "/opt/syne-2", false),
            ],
        };
        var viewModel = new MainWindowViewModel(facade);

        var component = Assert.Single(viewModel.ComponentInstallations);
        Assert.Equal("/opt/syne-1", component.Selected?.Location);
        component.Selected = component.Options[1];

        Assert.Equal("/opt/syne-2", facade.ActiveInstallationLocation);
        Assert.Equal("/opt/syne-2", component.Selected?.Location);
    }
}
