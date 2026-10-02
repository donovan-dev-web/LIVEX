using Launcher.Application;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Presentation.ViewModel;
using Launcher.Protocol.Model;
using Xunit;

namespace Launcher.Tests.Unit.Presentation;

/// <summary>
/// Sélection de profil dans la vue Configuration (G7, COMPONENTS.md §9) : le statut affiché
/// est la résolution du domaine — verrou Immersion compris, avec sa cause exacte et son
/// jalon — ; le démarrage suit l'ordre de résolution, l'arrêt en inverse strict (§6).
/// Le verrou est réévalué à chaque rafraîchissement, jamais mémorisé (ADR-006).
/// </summary>
public sealed class ProfileSelectionTests
{
    [Fact]
    public void Profil_satisfiable_affiche_l_ordre_de_demarrage()
    {
        var facade = new ScriptedProfileFacade();
        facade.Script(WellKnownProfiles.Analyse, Satisfiable("syne", "echos"));
        var viewModel = new MainWindowViewModel(facade);

        viewModel.SelectedProfile = WellKnownProfiles.Analyse;

        Assert.Equal("Profil satisfaisable — démarrage : SYNE (réel) → ECHOS", viewModel.ProfileStatus);
    }

    [Fact]
    public void Verrou_immersion_affiche_la_cause_exacte_et_le_jalon()
    {
        var facade = new ScriptedProfileFacade();
        var locked = new ProfileResolution
        {
            Satisfiable = false,
            ImmersionLocked = true,
        };
        locked.Problems.Add("mode Immersion verrouillé : capacité « renderCadence » absente du manifeste (jalon G7 — INTEGRATION_CONTRACT.md §11.1)");
        facade.Script(WellKnownProfiles.Immersion, locked);
        var viewModel = new MainWindowViewModel(facade);

        viewModel.SelectedProfile = WellKnownProfiles.Immersion;

        Assert.Contains("capacité « renderCadence » absente du manifeste", viewModel.ProfileStatus);
        Assert.Contains("G7", viewModel.ProfileStatus);
        Assert.Contains("§11.1", viewModel.ProfileStatus);
    }

    [Fact]
    public void Demarrage_dans_l_ordre_arret_en_ordre_inverse_strict()
    {
        var facade = new ScriptedProfileFacade();
        facade.Script(WellKnownProfiles.Experience, Satisfiable("syne", "echos", "prism"));
        var viewModel = new MainWindowViewModel(facade);
        viewModel.SelectedProfile = WellKnownProfiles.Experience;

        var calls = new List<(IReadOnlyList<string> Order, bool Start)>();
        viewModel.SetProfileLifecycleHandler((order, start) =>
        {
            calls.Add((order, start));
            return Task.CompletedTask;
        });

        viewModel.StartProfileCommand.Execute(WellKnownProfiles.Experience);
        viewModel.StopProfileCommand.Execute(WellKnownProfiles.Experience);

        Assert.Equal(2, calls.Count);
        Assert.True(calls[0].Start);
        Assert.Equal(["syne", "echos", "prism"], calls[0].Order);
        Assert.False(calls[1].Start);
        Assert.Equal(["prism", "echos", "syne"], calls[1].Order);
    }

    [Fact]
    public void Demarrage_refuse_tant_que_le_profil_n_est_pas_satisfiable()
    {
        var facade = new ScriptedProfileFacade();
        facade.Script(WellKnownProfiles.Analyse, Satisfiable("syne", "echos"));
        var viewModel = new MainWindowViewModel(facade);

        // Par défaut, simulation-seule n'est pas scripté : profil non satisfiable, commande inactive.
        Assert.False(viewModel.StartProfileCommand.CanExecute(WellKnownProfiles.SimulationSeule));

        viewModel.SelectedProfile = WellKnownProfiles.Analyse;
        Assert.True(viewModel.StartProfileCommand.CanExecute(WellKnownProfiles.Analyse));
        Assert.True(viewModel.StopProfileCommand.CanExecute(WellKnownProfiles.Analyse));
    }

    [Fact]
    public void Rafraichissement_reevalue_le_verrou_sans_memoire()
    {
        var facade = new ScriptedProfileFacade();
        facade.Script(WellKnownProfiles.Immersion, Satisfiable("syne", "prism"));
        var viewModel = new MainWindowViewModel(facade);
        viewModel.SelectedProfile = WellKnownProfiles.Immersion;
        Assert.Contains("satisfaisable", viewModel.ProfileStatus);

        // PRISM non conforme détecté entre deux rafraîchissements : le statut suit (évalué, pas codé en dur).
        var locked = new ProfileResolution { ImmersionLocked = true };
        locked.Problems.Add("mode Immersion verrouillé : PRISM ne se déclare pas porteur du mode Immersion (jalon G7 — INTEGRATION_CONTRACT.md §11.1)");
        facade.Script(WellKnownProfiles.Immersion, locked);

        viewModel.RefreshFromRegistry();

        Assert.Contains("PRISM ne se déclare pas porteur du mode Immersion", viewModel.ProfileStatus);
    }

    [Fact]
    public void Monitoring_agrege_les_composants_du_profil_actif_notamment_le_mock()
    {
        var facade = new ScriptedProfileFacade();
        facade.Script(WellKnownProfiles.Developpement, Satisfiable("syne-mock", "echos"));
        var viewModel = new MainWindowViewModel(facade)
        {
            SelectedMode = "Développement",
        };

        viewModel.RefreshFromRegistry();

        Assert.Equal(["syne-mock", "echos"], facade.LastRequiredComponents);
    }

    private static ProfileResolution Satisfiable(params string[] startupOrder)
    {
        var resolution = new ProfileResolution { Satisfiable = true };
        resolution.StartupOrder.AddRange(startupOrder);
        return resolution;
    }

    /// <summary>Façade scriptée : une résolution par profil, tout le reste reste inert.</summary>
    private sealed class ScriptedProfileFacade : IOrchestrationFacade
    {
        private readonly Dictionary<string, ProfileResolution> _script = new(StringComparer.Ordinal);

        public event EventHandler<CampaignProgress>? CampaignProgressChanged { add { } remove { } }
        public IReadOnlyList<string> LastRequiredComponents { get; private set; } = [];

        public void Script(string profileId, ProfileResolution resolution) => _script[profileId] = resolution;

        public IReadOnlyList<ComponentRowViewModel> SnapshotComponents() => [];

        public (string GlobalState, string MainCause) AggregateHealth(IReadOnlyList<string> requiredComponents) =>
            Aggregate(requiredComponents);

        private (string GlobalState, string MainCause) Aggregate(IReadOnlyList<string> requiredComponents)
        {
            LastRequiredComponents = requiredComponents;
            return ("Inactif", "aucun composant démarré");
        }

        public ProfileResolution ResolveProfile(string profileId, SessionKind session, IReadOnlyCollection<string>? extraComponents = null) =>
            _script.TryGetValue(profileId, out var resolution)
                ? resolution
                : new ProfileResolution
                {
                    Satisfiable = false,
                    Problems = { "composant « syne » non détecté : profil non satisfiable" },
                };

        public ReportReadResult ReadEmergenceReport(string packagePath) => new(null, null);

        public Task<string?> ToggleComponentAsync(string componentId, bool start) => Task.FromResult<string?>(null);

        public ResourceSnapshot SampleResources() => new(double.NaN, double.NaN, 31);

        public IReadOnlyList<LogEntryViewModel> RecentLogs() => [];
        public string SessionLogDirectory => Path.Combine(Path.GetTempPath(), "livex-test-logs");
        public Task ExportSessionLogsAsync(string destinationPath) => Task.CompletedTask;
        public IReadOnlyList<PackageLogFileViewModel> ListPackageRunLogs(string packagePath) => [];
        public string ReadPackageRunLog(string packagePath, string entryPath) => string.Empty;
        public Task ExportPackageRunLogAsync(string packagePath, string entryPath, string destinationPath) => Task.CompletedTask;
        public IReadOnlyList<ComponentInstallationOption> ListComponentInstallations() => [];
        public void SetActiveInstallation(string componentId, string location) => throw new NotSupportedException();
        public ComponentInstallationOption RegisterComponentInstallation(string location) => throw new NotSupportedException();

        public IReadOnlyList<ExperienceRowViewModel> ListExperiences() => [];

        public Task<string> RunCampaignAsync(ExperimentDefinition definition, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task<string> ResumeCampaignAsync(string packagePath, CancellationToken cancellationToken) =>
            Task.FromResult(packagePath);

        public IReadOnlyList<DocumentationItemViewModel> ListDocumentation() => [];

        public string ReadDocumentation(string relativePath) => string.Empty;
    }
}
