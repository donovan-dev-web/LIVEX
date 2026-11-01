// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.App.CommandLine;
using Setup.Core.Distribution;
using Setup.Core.Execution;
using Setup.Core.Lifecycle;
using Setup.Core.Planning;
using Setup.Core.Platform;
using Setup.Core.Preflight;
using Setup.Core.Simulation;

namespace Setup.App.Services;

/// <summary>Opération de maintenance proposée par l'assistant.</summary>
public enum MaintenanceActionKind
{
    Update,
    Repair,
    ManageComponents,
    ChangeWorkspace,
}

/// <summary>
/// Contexte d'exécution de l'assistant : distribution réelle si un manifeste
/// accompagne <c>livex-setup</c>, sinon monde fictif de Setup.Core. Toutes les
/// opérations passent par le même moteur ; en mode fictif, <c>DryRun</c> est
/// vrai et le moteur simule sans écrire.
/// </summary>
public sealed class InstallerSession
{
    private readonly AppOptions _options;
    private readonly IPathPolicy _pathPolicy = PlatformServices.CreatePathPolicy();
    private readonly InstallerEngine _engine = new();
    private InstallationRecord? _record;

    private InstallerSession(
        AppOptions options,
        DistributionManifest manifest,
        bool isDemo,
        DemoScenario scenario,
        string? loadWarning)
    {
        _options = options;
        Manifest = manifest;
        IsDemo = isDemo;
        Scenario = scenario;
        LoadWarning = loadWarning;
    }

    public DistributionManifest Manifest { get; private set; }

    public bool IsDemo { get; private set; }

    public DemoScenario Scenario { get; private set; }

    public string? LoadWarning { get; private set; }

    public string DefaultInstallDir => _pathPolicy.DefaultInstallDir();

    public string DefaultDataRoot => _pathPolicy.DefaultDataRoot();

    public string DefaultRootDir => _pathPolicy.DefaultRootDir();

    public string DefaultWorkspaceDir => _pathPolicy.DefaultWorkspaceDir();

    /// <summary>Répertoire qui porte la distribution (manifeste, archives de composants).</summary>
    public string BundleRoot => AppContext.BaseDirectory;

    /// <summary>
    /// Exécutable d'installation à conserver dans l'installation. Nul quand le
    /// processus courant n'est pas <c>livex-setup</c> (tests, outillage).
    /// </summary>
    public string? SetupSourcePath
    {
        get
        {
            var process = Environment.ProcessPath;
            return process is not null &&
                   string.Equals(
                       Path.GetFileNameWithoutExtension(process),
                       "livex-setup",
                       StringComparison.OrdinalIgnoreCase)
                ? process
                : null;
        }
    }

    /// <summary>Sous-répertoire logiciel dérivé d'une racine.</summary>
    public static string InstallDirFor(string root) => Path.Combine(root, "software");

    /// <summary>Sous-répertoire de données dérivé d'une racine.</summary>
    public static string DataRootFor(string root) => Path.Combine(root, "data");

    /// <summary>Sous-répertoire d'espace de travail dérivé d'une racine.</summary>
    public static string WorkspaceDirFor(string root) => Path.Combine(root, "workspace");

    public bool NoNetwork => _options.NoNetwork;

    public string? OfflineAssetsDir => _options.Core.OfflineAssetsDir;

    public InstallationRecord? Detected => _record;

    /// <summary>Inventaire de l'installation courante (fictif en mode démonstration).</summary>
    public IReadOnlyList<FileEntry> Inventory => CurrentInventory((_record ?? BuildRecord()).InstallDir);

    public static InstallerSession Create(AppOptions options)
    {
        if (options.Demo)
        {
            return new InstallerSession(
                options,
                DemoWorld.CreateManifest(options.DemoScenario),
                isDemo: true,
                options.DemoScenario,
                "Mode monde fictif : aucune installation réelle ne sera effectuée.");
        }

        if (DistributionLoader.TryLoad(DistributionLoader.ResolvePath(), out var manifest, out var error) && manifest is not null)
        {
            return new InstallerSession(options, manifest, isDemo: false, DemoScenario.FreshInstall, null);
        }

        return new InstallerSession(
            options,
            DemoWorld.CreateManifest(options.DemoScenario),
            isDemo: true,
            options.DemoScenario,
            $"Aucune distribution réelle détectée ({error}). Le monde fictif est utilisé : aucune écriture ne sera effectuée.");
    }

    public void SetDemoScenario(DemoScenario scenario)
    {
        if (!IsDemo)
        {
            return;
        }

        Scenario = scenario;
        Manifest = DemoWorld.CreateManifest(scenario);
        _record = null;
    }

    /// <summary>
    /// Bascule une distribution réelle vers le monde fictif, sans redémarrer :
    /// le manifeste est remplacé et toute opération devient strictement sèche.
    /// </summary>
    public void EnterDemo(DemoScenario scenario = DemoScenario.FreshInstall)
    {
        IsDemo = true;
        Scenario = scenario;
        Manifest = DemoWorld.CreateManifest(scenario);
        _record = null;
        LoadWarning = "Mode monde fictif activé : aucune installation, aucun téléchargement, aucune écriture.";
    }

    /// <summary>Composants dont tous les fichiers figurent déjà dans l'inventaire.</summary>
    public IReadOnlyList<string> InstalledComponentIds
    {
        get
        {
            var record = _record ?? BuildRecord();
            var paths = CurrentInventory(record.InstallDir)
                .Select(entry => entry.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return Manifest.Components
                .Where(component => IsComponentInstalled(component, paths))
                .Select(component => component.Id)
                .ToList();
        }
    }

    /// <summary>
    /// Un composant est présent quand tous ses fichiers listés figurent dans l'inventaire.
    /// Le manifeste public ne liste pas les fichiers des archives : on se rabat alors sur
    /// la présence d'au moins un fichier sous sa racine d'installation.
    /// </summary>
    private static bool IsComponentInstalled(ComponentDescriptor component, HashSet<string> paths)
    {
        if (component.Files.Count > 0)
        {
            return component.Files.All(file => paths.Contains(file.Path));
        }

        var root = component.InstallRoot;
        if (string.IsNullOrWhiteSpace(root) || string.Equals(root, "root", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var prefix = root.Replace('\\', '/').TrimEnd('/') + "/";
        return paths.Any(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<DetectionResult> DetectAsync(CancellationToken ct = default)
    {
        _record = null;

        var result = IsDemo
            ? await _engine.DetectAsync(
                Scenario == DemoScenario.ExistingInstall
                    ? DemoWorld.CreateExistingInstallation(DefaultInstallDir, DefaultDataRoot, DefaultWorkspaceDir)
                    : null,
                ct)
            : await _engine.DetectAsync(ct);

        _record = result.Record;
        return result;
    }

    public Task<PreflightResult> PreflightAsync(InstallRequest request, CancellationToken ct = default) =>
        _engine.PreflightAsync(request, ct);

    public Task<InstallPlan> PlanAsync(InstallRequest request, CancellationToken ct = default) =>
        _engine.PlanAsync(request, ct);

    public Task<ExecutionResult> ExecuteAsync(
        InstallPlan plan, InstallRequest request,
        IProgress<ProgressReport>? progress = null, CancellationToken ct = default) =>
        _engine.ExecuteAsync(plan, request, progress, ct);

    /// <summary>
    /// Maintenance planifiée par <see cref="MaintenancePlanner"/> : mise à jour
    /// (refus du downgrade, composants installés seulement), réparation (fichiers
    /// manquants de l'inventaire) ou gestion des composants. En mode fictif, le
    /// plan est calculé puis exécuté en simulation, sans aucune écriture.
    /// </summary>
    public async Task<ExecutionResult> ExecuteMaintenanceAsync(
        MaintenanceActionKind action, InstallRequest request,
        IReadOnlyList<string>? addComponents = null,
        IReadOnlyList<string>? removeComponents = null,
        IProgress<ProgressReport>? progress = null, CancellationToken ct = default)
    {
        var record = _record ?? BuildRecord();
        var maintenance = new MaintenanceRequest(
            record,
            action switch
            {
                MaintenanceActionKind.Repair => MaintenanceOperation.Repair,
                MaintenanceActionKind.ManageComponents => MaintenanceOperation.ManageComponents,
                MaintenanceActionKind.ChangeWorkspace => MaintenanceOperation.ChangeWorkspace,
                _ => MaintenanceOperation.Update,
            },
            Manifest,
            AddComponents: addComponents,
            RemoveComponents: removeComponents,
            NewWorkspacePath: action == MaintenanceActionKind.ChangeWorkspace ? request.WorkspaceDir : null,
            Inventory: CurrentInventory(record.InstallDir),
            PayloadRoot: request.PayloadRoot,
            DryRun: request.DryRun,
            BundleRoot: request.BundleRoot);

        var plan = await _engine.PlanMaintenanceAsync(maintenance, ct).ConfigureAwait(false);
        return await _engine.ExecuteAsync(plan, request, progress, ct).ConfigureAwait(false);
    }

    /// <summary>Désinstallation par portée, planifiée puis exécutée par Setup.Core.</summary>
    public async Task<ExecutionResult> ExecuteUninstallAsync(
        UninstallScope scope, bool purgeResearch,
        IProgress<ProgressReport>? progress = null, CancellationToken ct = default)
    {
        var record = _record ?? BuildRecord();
        var inventory = CurrentInventory(record.InstallDir);

        var plan = UninstallPlanner.Plan(new UninstallRequest(
            record,
            scope,
            inventory,
            WorkspaceDeletionConfirmed: scope == UninstallScope.SoftwareConfigAndWorkspace,
            PurgeResearch: purgeResearch,
            DryRun: IsDemo));

        return await _engine.ExecuteUninstallAsync(plan, new UninstallRequest(
            record,
            scope,
            inventory,
            WorkspaceDeletionConfirmed: scope == UninstallScope.SoftwareConfigAndWorkspace,
            PurgeResearch: purgeResearch,
            DryRun: IsDemo), progress, ct).ConfigureAwait(false);
    }

    private InstallationRecord BuildRecord() => new(
        Manifest.Product,
        Manifest.Version,
        DefaultInstallDir,
        DefaultDataRoot,
        DefaultWorkspaceDir,
        DateTimeOffset.UtcNow);

    private IReadOnlyList<FileEntry> CurrentInventory(string installDir) =>
        IsDemo ? DemoWorld.CreateInventory() : InventoryStore.ReadEntries(installDir);
}
