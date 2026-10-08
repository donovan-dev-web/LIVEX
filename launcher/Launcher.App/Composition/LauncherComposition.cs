using System.Net.Sockets;
using Launcher.Application;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Infrastructure;
using Launcher.Protocol.Model;

namespace Launcher.App.Composition;

/// <summary>
/// Composition racine (ARCHITECTURE.md §1 : la composition appartient à l'application).
/// Réalise le graphe : infrastructure concrète → domaine → cas d'usage, plus la façade
/// adaptateur consommée par la présentation.
/// </summary>
public sealed class LauncherComposition : IDisposable
{
    /// <summary>Plage interne LIVEX (NETWORK.md §6.2, §12) : ports des instances et du Launcher lui-même.</summary>
    private const int PortRangeFrom = 5200;

    /// <summary>Borne haute de la plage interne LIVEX.</summary>
    private const int PortRangeTo = 5399;

    /// <summary>Cadence de la boucle de sondes (OBSERVABILITY.md §4) : « health.intervalMs » la précise par composant.</summary>
    private static readonly TimeSpan ProbeTick = TimeSpan.FromSeconds(1);

    /// <summary>Délai par défaut entre deux sondes d'un composant sans « health.intervalMs ».</summary>
    private const int DefaultProbeIntervalMs = 1000;

    private readonly ServiceRegistry _registry;
    private readonly OrchestrationService _orchestration;
    private readonly SessionFileJournal _journal;
    private readonly ManifestDetector _detector;
    private readonly ProcessManager _processes;
    private readonly ComponentLogBuffer _logs;
    private readonly PortAllocator _ports;
    private readonly CampaignRunner _campaigns;
    private readonly EchosAnalysisService _analysis;
    private readonly EchosTelemetryService _telemetry;
    private readonly LauncherHttpSurface? _surface;
    private readonly HealthProber _prober = new();
    private readonly CancellationTokenSource _supervisionStop = new();
    private readonly Task _supervision;
    private readonly string _packagesRoot;
    private readonly string _componentsRoot;
    private readonly string _dataRoot;
    private bool _disposed;

    /// <summary>
    /// Vrai : les sources standard (application, LIVEX_HOME, profil utilisateur, registre)
    /// font foi. Faux : la composition est hermétique et ne connaît que la racine explicite
    /// donnée à la construction — c'est ce qui garde les bancs de tests et les installations
    /// portables indépendants de l'état de la machine.
    /// </summary>
    private readonly bool _detectStandardSources;
    private OrchestrationFacade? _facade;

    /// <summary>Initialise la composition complète du Launcher sur les emplacements par défaut.</summary>
    public LauncherComposition()
        : this(
            Path.Combine(WorkspaceLayout.UserDataRoot(), "packages"),
            Path.Combine(AppContext.BaseDirectory, "components"),
            WorkspaceLayout.UserDataRoot(),
            detectStandardSources: true)
    {
    }

    /// <summary>Initialise la composition avec des racines explicites (bancs de tests, installation portable).</summary>
    public LauncherComposition(string packagesRoot, string componentsRoot)
        : this(packagesRoot, componentsRoot, WorkspaceLayout.UserDataRoot(), detectStandardSources: false)
    {
    }

    /// <summary>Initialise la composition avec des racines de données explicites (bancs de tests hermétiques).</summary>
    public LauncherComposition(string packagesRoot, string componentsRoot, string dataRoot)
        : this(packagesRoot, componentsRoot, dataRoot, detectStandardSources: false)
    {
    }

    private LauncherComposition(string packagesRoot, string componentsRoot, string dataRoot, bool detectStandardSources)
    {
        _packagesRoot = packagesRoot;
        _componentsRoot = componentsRoot;
        _dataRoot = dataRoot;
        _detectStandardSources = detectStandardSources;
        _journal = new SessionFileJournal(Path.Combine(dataRoot, "sessions"));
        _registry = new ServiceRegistry();
        _ports = new PortAllocator(PortRangeFrom, PortRangeTo, TcpPortProbe.IsFree);
        _orchestration = new OrchestrationService(_registry, _ports, new SystemClock(), _journal);
        _detector = new ManifestDetector(_journal);
        _processes = new ProcessManager(new SystemClock(), _journal);
        _logs = new ComponentLogBuffer(_processes);

        var packageService = new FileSystemPackageService(packagesRoot, new SystemClock());
        _analysis = new EchosAnalysisService(_orchestration, packagesRoot);
        var runExecutor = new ProcessRunExecutor(_processes, _orchestration, _ports, _journal, _prober, _analysis);
        _telemetry = new EchosTelemetryService(_orchestration);
        _campaigns = new CampaignRunner(packageService, runExecutor, _analysis, new SystemClock(), _journal);

        // En production, prendre en compte les racines utilisateur/installation et le registre.
        // Les constructeurs explicites restent hermétiques pour les tests et les installations portables.
        var installations = DetectInstallations();
        foreach (var installation in installations)
        {
            _orchestration.Registry.RegisterInstallation(installation);
        }

        _surface = StartHttpSurface(packagesRoot);

        // Supervision (OBSERVABILITY.md §4) : sondes tirées par le Launcher, états transités
        // par StateRules uniquement. Sans elle, un composant démarré resterait « Démarrage ».
        _supervision = Task.Run(() => SuperviseLoopAsync(_supervisionStop.Token));
    }

    /// <summary>
    /// Démarre la surface HTTP d'observabilité du Launcher (NETWORK.md §4.1 : /registry ;
    /// OBSERVABILITY.md §4 : /health, /info, /metrics) sur un port libre de la plage interne.
    /// Non bloquante : si aucun port n'est disponible, l'application démarre sans surface,
    /// l'échec étant journalisé.
    /// </summary>
    private LauncherHttpSurface? StartHttpSurface(string packagesRoot)
    {
        for (var port = PortRangeFrom; port <= PortRangeTo; port++)
        {
            if (!TcpPortProbe.IsFree(port))
            {
                continue;
            }

            try
            {
                var surface = new LauncherHttpSurface(
                    _orchestration,
                    () => LauncherHttpSurface.BuildRegistryJson(_orchestration.Registry.All()),
                    port,
                    packagesRoot);
                _journal.Info("HttpSurface", $"surface HTTP d'observabilité démarrée sur 127.0.0.1:{port}");
                return surface;
            }
            catch (SocketException)
            {
                // Course entre le pré-vol et le bind : essayer le port suivant.
            }
        }

        _journal.Warn("HttpSurface", "plage interne 5200–5399 épuisée : surface HTTP d'observabilité non démarrée");
        return null;
    }

    /// <summary>Service d'orchestration du domaine.</summary>
    public OrchestrationService Orchestration => _orchestration;

    /// <summary>Détecteur de composants.</summary>
    public ManifestDetector Detector => _detector;

    /// <summary>Cas d'usage campagne.</summary>
    public CampaignRunner Campaigns => _campaigns;

    /// <summary>Journal de session.</summary>
    public ISessionJournal Journal => _journal;

    /// <summary>
    /// Tampon des lignes de sortie des composants, source des fenêtres console natives
    /// (USER_INTERFACE.md §9). Nourri par le gestionnaire de processus, lu par l'interface.
    /// </summary>
    public IComponentLogSource Logs => _logs;

    /// <summary>
    /// Lecture de la télémétrie ECHOS pour la fenêtre d'analyse native (ADR-007) :
    /// le Launcher présente ce que l'API REST d'ECHOS publie, sans rien recalculer.
    /// </summary>
    public IEchosTelemetrySource EchosTelemetry => _telemetry;

    /// <summary>
    /// Racines de détection de cette composition : les sources standard en production,
    /// la seule racine explicite sinon. Une redétection doit retrouver exactement ce que la
    /// construction a trouvé — sinon un rafraîchissement ferait apparaître des composants
    /// absents de l'installation hermétique (et les ferait disparaître du même coup).
    /// </summary>
    private IReadOnlyList<ComponentInstallation> DetectInstallations() => _detectStandardSources
        ? _detector.Detect()
        : _detector.DetectFromRoots([_componentsRoot]);

    /// <summary>Détecte les composants et installe le registre.</summary>
    public void DetectComponents() => _orchestration.Adopt(DetectInstallations());

    /// <summary>Surface HTTP d'observabilité, si elle a pu démarrer (NETWORK.md §4.1).</summary>
    public LauncherHttpSurface? Surface => _surface;

    /// <summary>Port d'écoute de la surface HTTP, dans la plage interne 5200–5399.</summary>
    public int? HttpPort => _surface?.Port;

    /// <summary>
    /// Façade adaptateur pour la présentation : cycle de vie réel, ressources, journaux,
    /// expériences. Créée à la demande, une seule fois.
    /// </summary>
    public OrchestrationFacade Facade =>
        _facade ??= new OrchestrationFacade(
            _orchestration,
            _processes,
            _ports,
            _journal,
            _packagesRoot,
            Path.Combine(_dataRoot, "workspace"),
            Path.Combine(_dataRoot, "sessions"),
            Path.Combine(AppContext.BaseDirectory, "docs"),
            _campaigns);

    /// <summary>
    /// Boucle de supervision (OBSERVABILITY.md §4) : interroge le point de santé déclaré au
    /// manifeste (« health.path », « health.intervalMs ») de chaque instance dotée d'un point
    /// de contrôle, et fait transiter l'état par <see cref="StateRules"/> — Démarrage → Prêt
    /// sur réponse, trois sondes manquées = perte de contact, délai de démarrage selon
    /// « timeouts.startupMs ». Résiliente : un cycle en échec est rejoué, jamais fatal.
    /// </summary>
    private async Task SuperviseLoopAsync(CancellationToken cancellationToken)
    {
        var lastProbed = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(ProbeTick, cancellationToken).ConfigureAwait(false);
                var at = DateTimeOffset.UtcNow;
                var live = _orchestration.Registry.All();
                foreach (var instance in live)
                {
                    try
                    {
                        var interval = TimeSpan.FromMilliseconds(
                            instance.Installation.Manifest?.Health?.IntervalMs ?? DefaultProbeIntervalMs);
                        if (lastProbed.TryGetValue(instance.InstanceId, out var previous)
                            && at - previous < interval)
                        {
                            continue;
                        }

                        lastProbed[instance.InstanceId] = at;
                        await ProbeInstanceAsync(instance, at, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception)
                    {
                        // Une sonde en échec ne met jamais la supervision à l'arrêt (§8 : un silence est un état).
                    }
                }

                // Purge des instances disparues : la mémoire suit le registre, jamais l'inverse.
                var liveIds = live.Select(instance => instance.InstanceId).ToHashSet(StringComparer.Ordinal);
                foreach (var stale in lastProbed.Keys.Where(id => !liveIds.Contains(id)).ToList())
                {
                    lastProbed.Remove(stale);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Sonde une instance : succès → Prêt ou confirmation ; échec → décompte ou délai de démarrage.</summary>
    private async Task ProbeInstanceAsync(ComponentInstance instance, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (!instance.Endpoints.TryGetValue("control", out var control))
        {
            return; // Instances de run ou composants sans point de contrôle : hors périmètre de la sonde.
        }

        var health = instance.Installation.Manifest?.Health;
        if (health?.Probe is { } probe && probe != "http")
        {
            return; // Sonde fichier ou exotique : non implémentée, l'état reste tel quel.
        }

        var path = string.IsNullOrWhiteSpace(health?.Path) ? "/health/ready" : health!.Path!;
        var (ready, cause) = await _prober.ProbeReadyAsync(new Uri(control.Url), cancellationToken, path).ConfigureAwait(false);

        if (ready)
        {
            var current = instance.Health;
            var next = current.State is ComponentState.Demarrage or ComponentState.Inactif or ComponentState.Defaillant
                ? StateRules.Ready(at, ComponentState.Pret, $"prêt (sonde {path})")
                : StateRules.Confirmed(current, at);
            _orchestration.ApplyHealth(instance.InstanceId, next);
            return;
        }

        var state = instance.Health;
        if (state.State == ComponentState.Demarrage)
        {
            // Le délai de démarrage déclaré au manifeste prime (COMPONENTS.md §4.1) :
            // avant son dépassement, l'absence de réponse reste « Démarrage », pas une panne.
            var startupMs = instance.Installation.Manifest?.Timeouts?.StartupMs ?? 30000;
            var startedAt = instance.StartedAt ?? at;
            if (at - startedAt > TimeSpan.FromMilliseconds(startupMs))
            {
                _orchestration.ApplyHealth(instance.InstanceId,
                    StateRules.Timeout(ComponentState.Demarrage, cause ?? "sonde sans réponse", at));
            }

            return;
        }

        if (state.State is ComponentState.Pret or ComponentState.Actif or ComponentState.Suspendu)
        {
            _orchestration.ApplyHealth(instance.InstanceId, StateRules.Missed(state, at));
        }
    }

    /// <summary>Libère la supervision, la surface HTTP, la façade et les processus lancés par le Launcher.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _supervisionStop.Cancel();
        try
        {
            _supervision.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _facade?.Dispose();
        _surface?.Dispose();
        _telemetry.Dispose();
        _analysis.Dispose();
        _logs.Dispose();
        _journal.Dispose();
    }
}
