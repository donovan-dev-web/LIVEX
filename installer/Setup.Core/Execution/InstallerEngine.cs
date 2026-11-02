// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Setup.Core.Acquisition;
using Setup.Core.CommandLine;
using Setup.Core.Distribution;
using Setup.Core.Journal;
using Setup.Core.Lifecycle;
using Setup.Core.Platform;
using Setup.Core.Planning;
using Setup.Core.Preferences;
using Setup.Core.Preflight;
using Setup.Core.Simulation;
using JournalWriter = Setup.Core.Journal.Journal;

namespace Setup.Core.Execution;

/// <summary>
/// Moteur d'installation et de désinstallation. Trois règles non négociables :
/// <list type="bullet">
/// <item>planifier ne touche jamais au disque ;</item>
/// <item><c>DryRun</c> n'écrit rien, ne télécharge rien, ne lance rien ;</item>
/// <item>un échec réel tente de compenser ce qui a déjà été fait et signale les résidus.</item>
/// </list>
/// </summary>
public sealed class InstallerEngine : IInstallerEngine
{
    private const string LauncherExecutable = "livex-launcher";
    private const string LauncherDirectory = "core/launcher";
    private const string InstallerDirectory = "core/installer";
    private const string IdentityFileName = ".livex-install.json";
    private const string DistributionFileName = ".livex-distribution.json";
    private const string InventoryFileName = ".livex-inventory.json";

    private static readonly string[] LegalFileNames =
        { "LICENSE", "NOTICE", "THIRD-PARTY-NOTICES.md" };

    private readonly IPathPolicy _pathPolicy;
    private readonly IProcessProbe _processProbe;
    private readonly IPrerequisiteProbe _prereq;
    private readonly IFileSync _fileSync;
    private readonly IIntegration? _integration;
    private readonly IInstallationStore? _store;

    public InstallerEngine()
        : this(
            PlatformServices.CreatePathPolicy(),
            PlatformServices.CreateProcessProbe(),
            PlatformServices.CreatePrerequisiteProbe(),
            PlatformServices.CreateFileSync())
    {
    }

    /// <summary>
    /// Constructeur injectable. <paramref name="integration"/> et
    /// <paramref name="store"/> permettent aux tests de vérifier une
    /// installation réelle sur le disque sans toucher au registre, aux
    /// raccourcis ni aux entrées système de la machine.
    /// </summary>
    public InstallerEngine(
        IPathPolicy pathPolicy,
        IProcessProbe processProbe,
        IPrerequisiteProbe prereq,
        IFileSync fileSync,
        IIntegration? integration = null,
        IInstallationStore? store = null)
    {
        _pathPolicy = pathPolicy;
        _processProbe = processProbe;
        _prereq = prereq;
        _fileSync = fileSync;
        _integration = integration;
        _store = store;
    }

    /// <summary>Contexte minimal d'exécution d'une action.</summary>
    private sealed record ActionContext(
        string Product,
        string Version,
        string InstallDir,
        string DataRoot,
        string WorkspaceDir,
        string BundleRoot);

    // ---------------------------------------------------------------- détection

    public Task<DetectionResult> DetectAsync(CancellationToken ct = default)
    {
        var store = PlatformServices.CreateInstallationStore(_pathPolicy.DefaultDataRoot());
        var record = store.Read();
        if (record is not null)
        {
            var valid = IsInstallationValid(record);
            return Task.FromResult(new DetectionResult(
                true,
                record.InstallDir,
                record,
                valid,
                valid ? Array.Empty<string>() : new[] { "Launcher absent du répertoire logiciel." }));
        }

        // Aucun enregistrement : on ne conclut pas « pas installé » sans
        // chercher l'identité dans le répertoire par défaut (ADR-005).
        var defaultDir = _pathPolicy.DefaultInstallDir();
        var identity = Path.Combine(defaultDir, IdentityFileName);
        if (!File.Exists(identity))
        {
            return Task.FromResult(new DetectionResult(false, null, null, false, Array.Empty<string>()));
        }

        try
        {
            var discovered = JsonSerializer.Deserialize<InstallationRecord>(File.ReadAllText(identity));
            if (discovered is null)
            {
                return Task.FromResult(new DetectionResult(true, defaultDir, null, false,
                    new[] { "Identité d'installation illisible." }));
            }

            return Task.FromResult(new DetectionResult(true, discovered.InstallDir, discovered,
                IsInstallationValid(discovered), new[] { "Enregistrement absent : installation adoptée par son identité." }));
        }
        catch
        {
            return Task.FromResult(new DetectionResult(true, defaultDir, null, false,
                new[] { "Identité d'installation illisible." }));
        }
    }

    public Task<DetectionResult> DetectAsync(InstallationRecord? simulatedRecord, CancellationToken ct = default)
    {
        if (simulatedRecord is null)
        {
            return Task.FromResult(new DetectionResult(false, null, null, false, Array.Empty<string>()));
        }

        // En démonstration, le disque n'est pas consulté : l'installation
        // décrite est considérée comme complète et valide.
        return Task.FromResult(new DetectionResult(true, simulatedRecord.InstallDir, simulatedRecord, true, Array.Empty<string>()));
    }

    private static bool IsInstallationValid(InstallationRecord record)
    {
        if (!Directory.Exists(record.InstallDir)) return false;
        return File.Exists(LauncherPath(record.InstallDir));
    }

    // ---------------------------------------------------------------- préflight

    public Task<PreflightResult> PreflightAsync(InstallRequest request, CancellationToken ct = default) =>
        Task.Run(() => Preflight(request), ct);

    private PreflightResult Preflight(InstallRequest request)
    {
        var checks = new List<PreflightCheck>();
        var errors = new List<string>();

        void Fail(string name, string message)
        {
            checks.Add(new PreflightCheck(name, PreflightStatus.Fail, message));
            errors.Add(message);
        }

        void Pass(string name, string? message = null) =>
            checks.Add(new PreflightCheck(name, PreflightStatus.Pass, message));

        void Simulated(string name) =>
            checks.Add(new PreflightCheck(name, PreflightStatus.Simulated, "Vérification simulée."));

        // Licence : accepter est une condition de poursuite (spec §6.1).
        if (request.AcceptLicense) Pass("license");
        else Fail("license", "La licence doit être acceptée pour continuer.");

        // Plateforme
        var platform = request.Manifest.Platform;
        var expected = OperatingSystem.IsWindows() ? "win-" : "linux-";
        if (string.IsNullOrWhiteSpace(platform) || !platform.StartsWith(expected, StringComparison.Ordinal))
            Fail("platform", $"Manifeste destiné à « {platform} », incompatible avec cette plateforme.");
        else Pass("platform", platform);

        // Politique de chemins
        if (string.IsNullOrWhiteSpace(request.InstallDir)) Fail("path-policy", "Le répertoire logiciel est requis.");
        else if (_pathPolicy.IsForbiddenInstallDir(request.InstallDir))
            Fail("path-policy", $"Répertoire logiciel interdit : {request.InstallDir}.");
        else Pass("path-policy");

        if (string.IsNullOrWhiteSpace(request.WorkspaceDir))
            Fail("workspace", "L'espace de travail est requis.");
        else if (_pathPolicy.Overlaps(request.InstallDir, request.WorkspaceDir))
            Fail("workspace", "L'espace de travail ne peut pas contenir, ni être contenu dans, le répertoire logiciel.");
        else if (string.Equals(
                     Path.GetFullPath(request.WorkspaceDir),
                     Path.GetFullPath(request.DataRoot),
                     StringComparison.OrdinalIgnoreCase))
            Fail("workspace", "L'espace de travail ne peut pas être la racine de données.");
        else Pass("workspace");

        if (!string.IsNullOrWhiteSpace(request.InstallDir) &&
            !string.IsNullOrWhiteSpace(request.DataRoot) &&
            _pathPolicy.Overlaps(request.InstallDir, request.DataRoot))
            Fail("overlap", "La racine de données ne peut pas contenir, ni être contenue dans, le répertoire logiciel.");
        else Pass("overlap");

        // Droits réels : jamais en simulation.
        if (request.DryRun)
        {
            Simulated("writable");
            Simulated("process");
            Simulated("prerequisites");
        }
        else
        {
            if (_pathPolicy.IsWritable(request.InstallDir) && _pathPolicy.IsWritable(request.WorkspaceDir)
                && _pathPolicy.IsWritable(request.DataRoot)) Pass("writable");
            else Fail("writable", "Les chemins choisis ne sont pas inscriptibles par l'utilisateur courant.");

            if (_processProbe.IsProcessRunningFrom(request.InstallDir))
                Fail("process", "Un processus LIVEX est en cours d'exécution dans le répertoire cible.");
            else Pass("process");

            var prereqIssues = _prereq.Check();
            if (prereqIssues.Count == 0) Pass("prerequisites");
            else Fail("prerequisites", string.Join("; ", prereqIssues));
        }

        // Cohérence du manifeste
        var manifestIssues = ManifestValidator.Validate(request.Manifest);
        if (manifestIssues.Count == 0) Pass("manifest", $"{request.Manifest.Components.Count} composants");
        else Fail("manifest", string.Join("; ", manifestIssues));

        // Dépendances déclarées
        var selected = new HashSet<string>(request.SelectedComponents, StringComparer.Ordinal);
        var missingDependencies = request.Manifest.Components
            .Where(c => selected.Contains(c.Id))
            .SelectMany(c => c.Requires.Select(r => (Component: c.Id, Requires: r)))
            .Where(pair => !selected.Contains(pair.Requires))
            .Select(pair => $"{pair.Component} requiert {pair.Requires}")
            .ToList();
        if (missingDependencies.Count == 0) Pass("requirements");
        else Fail("requirements", string.Join("; ", missingDependencies));

        // Charge utile locale. Dans le monde fictif, le contrôle est informatif :
        // aucune charge utile ne peut exister. Sur une distribution réelle, son
        // absence est un échec, même en dry-run (--check) (INS-F-01, INS-A-37).
        if (request.Simulated)
        {
            checks.Add(new PreflightCheck("payload", PreflightStatus.Info, "Aucune charge utile (simulation)."));
        }
        else
        {
            var payloadComponents = request.Manifest.Components
                .Where(c => selected.Contains(c.Id) && c.Acquisition.Kind == "payload")
                .ToList();
            var archiveComponents = request.Manifest.Components
                .Where(c => selected.Contains(c.Id) && c.Acquisition.Kind == "bundled-archive")
                .ToList();

            if (payloadComponents.Count == 0 && archiveComponents.Count == 0)
            {
                checks.Add(new PreflightCheck("payload", PreflightStatus.Info, "Aucun fichier de charge utile déclaré."));
            }
            else
            {
                var problems = new List<string>();

                foreach (var component in archiveComponents)
                {
                    var archive = ResolveArchive(request, component.Acquisition.Asset!);
                    if (!File.Exists(archive))
                    {
                        problems.Add($"{component.Acquisition.Asset} absent");
                        continue;
                    }

                    var length = new FileInfo(archive).Length;
                    if (component.Acquisition.Size is > 0 && length != component.Acquisition.Size)
                    {
                        problems.Add($"{component.Acquisition.Asset} : taille {length} au lieu de {component.Acquisition.Size}");
                        continue;
                    }

                    if (!string.IsNullOrEmpty(component.Acquisition.Sha256) &&
                        !LooksLikePlaceholder(component.Acquisition.Sha256))
                    {
                        using var stream = File.OpenRead(archive);
                        var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                        if (!string.Equals(actual, component.Acquisition.Sha256, StringComparison.OrdinalIgnoreCase))
                            problems.Add($"{component.Acquisition.Asset} : empreinte invalide");
                    }
                }

                if (request.PayloadRoot is null && payloadComponents.Any(c => c.Files.Count > 0))
                {
                    problems.Add("aucun répertoire payload/ n'accompagne livex-setup");
                }
                else if (request.PayloadRoot is not null)
                {
                    problems.AddRange(payloadComponents
                        .SelectMany(c => c.Files)
                        .Where(f => !File.Exists(Path.Combine(request.PayloadRoot, f.Path)))
                        .Select(f => f.Path));
                }

                if (problems.Count == 0)
                    Pass("payload", $"{archiveComponents.Count + payloadComponents.Count} composant(s)");
                else
                    Fail("payload", "Charge utile incomplète : " + string.Join(", ", problems.Take(5)));
            }
        }

        return new PreflightResult(errors.Count == 0, checks, errors);
    }

    // ---------------------------------------------------------------- plan

    public Task<InstallPlan> PlanAsync(InstallRequest request, CancellationToken ct = default) =>
        Task.Run(() => Plan(request), ct);

    private InstallPlan Plan(InstallRequest request)
    {
        var actions = new List<PlanAction>();
        var warnings = new List<string>();
        var step = 0;
        long bytesToWrite = 0;
        long bytesToDownload = 0;

        var installDir = request.InstallDir;
        var selected = new HashSet<string>(request.SelectedComponents, StringComparer.Ordinal);

        // Arborescence (aucune création ici : ce sont des actions du plan).
        actions.Add(new CreateDirectoryAction(installDir, ++step));
        actions.Add(new CreateDirectoryAction(Path.Combine(installDir, LauncherDirectory), ++step));
        actions.Add(new CreateDirectoryAction(Path.Combine(installDir, InstallerDirectory), ++step));
        actions.Add(new CreateDirectoryAction(Path.Combine(installDir, "components"), ++step));
        actions.Add(new CreateDirectoryAction(Path.Combine(installDir, "contracts"), ++step));
        actions.Add(new CreateDirectoryAction(Path.Combine(installDir, "assets"), ++step));
        actions.Add(new CreateDirectoryAction(Path.Combine(installDir, "plugins"), ++step));
        actions.Add(new CreateDirectoryAction(request.DataRoot, ++step));
        actions.Add(new CreateDirectoryAction(Path.Combine(request.DataRoot, "logs"), ++step));
        actions.Add(new CreateDirectoryAction(Path.Combine(request.DataRoot, "cache", "downloads"), ++step));
        actions.Add(new CreateDirectoryAction(request.WorkspaceDir, ++step));

        // Fichiers de la charge utile historique, limités aux composants sélectionnés.
        var selectedFiles = request.Manifest.Components
            .Where(component => selected.Contains(component.Id) && component.Acquisition.Kind == "payload")
            .SelectMany(component => component.Files)
            .DistinctBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in selectedFiles)
        {
            var target = Path.Combine(installDir, file.Path);
            var source = request.PayloadRoot is null ? null : Path.Combine(request.PayloadRoot, file.Path);
            actions.Add(new WriteFileAction(target, source, file.Sha256, file.Size, ++step));
            bytesToWrite += file.Size;
        }

        var inventory = new List<FileEntry>(selectedFiles);

        // Composants livrés sous forme d'archive unique : une seule extraction
        // remplace des milliers de copies fichier par fichier.
        foreach (var component in request.Manifest.Components)
        {
            if (!selected.Contains(component.Id)) continue;
            if (component.Requires.Any(r => !selected.Contains(r)))
            {
                warnings.Add($"Composant ignoré : {component.Id} requiert un composant non sélectionné.");
                continue;
            }

            if (component.Acquisition.Kind == "bundled-archive" && component.Acquisition.Asset is not null)
            {
                var archive = ResolveArchive(request, component.Acquisition.Asset);
                var destination = ComponentDestination(installDir, component);
                var entries = ReadArchiveEntries(archive, component.InstallRoot);
                actions.Add(new CreateDirectoryAction(destination, ++step));
                actions.Add(new ExtractArchiveAction(
                    archive, destination, ++step,
                    Entries: entries.Select(e => e.Path).ToList(),
                    ComponentId: component.Id));
                bytesToWrite += component.Acquisition.Size ?? 0;
                inventory.AddRange(entries);
            }
        }

        // Modules facultatifs acquis hors distribution.
        foreach (var component in request.Manifest.Components)
        {
            if (!selected.Contains(component.Id)) continue;
            if (component.Requires.Any(r => !selected.Contains(r)))
            {
                continue;
            }

            switch (component.Acquisition.Kind)
            {
                case "release-asset" when component.Acquisition.Url is not null:
                {
                    var asset = component.Acquisition.Asset ?? component.Id + ".zip";
                    var sha = component.Acquisition.Sha256 ?? string.Empty;
                    var size = component.Acquisition.Size ?? 0;
                    bytesToDownload += size;

                    if (component.Id == "prism-ldk")
                    {
                        // L'archive du LDK n'est jamais extraite (ARCHITECTURE §5).
                        var destination = Path.Combine(installDir, "plugins", "prism-ldk", asset);
                        actions.Add(new CreateDirectoryAction(Path.GetDirectoryName(destination)!, ++step));
                        actions.Add(new DownloadAssetAction(destination, component.Acquisition.Url, sha, size, ++step));
                        inventory.Add(new FileEntry(
                            Path.GetRelativePath(installDir, destination).Replace('\\', '/'), size, sha));
                    }
                    else
                    {
                        var cachePath = Path.Combine(request.DataRoot, "cache", "downloads", sha);
                        var destination = ComponentDestination(installDir, component);
                        actions.Add(new DownloadAssetAction(cachePath, component.Acquisition.Url, sha, size, ++step));
                        actions.Add(new CreateDirectoryAction(destination, ++step));
                        actions.Add(new ExtractArchiveAction(cachePath, destination, ++step, ComponentId: component.Id));
                    }

                    break;
                }

                case "unavailable":
                    warnings.Add($"Composant indisponible : {component.Id} ({component.Acquisition.Reason}).");
                    break;
            }
        }

        // Copie de maintenance : l'installateur installé reste disponible pour
        // désinstaller ou réparer sans retélécharger quoi que ce soit.
        var setupSource = request.SetupSourcePath ?? ResolveSetupSource();
        if (!request.Simulated && setupSource is not null && File.Exists(setupSource))
        {
            var setupTarget = Path.Combine(installDir, SetupExecutableName());
            actions.Add(new WriteFileAction(setupTarget, setupSource, null, null, ++step));
            inventory.Add(new FileEntry(SetupExecutableName(), new FileInfo(setupSource).Length, string.Empty));
        }

        // Mentions légales : conservées à la racine, à côté de l'exécutable.
        foreach (var name in LegalFileNames)
        {
            var source = Path.Combine(BundleRoot(request), name);
            if (!File.Exists(source)) continue;
            actions.Add(new WriteFileAction(Path.Combine(installDir, name), source, null, null, ++step));
            inventory.Add(new FileEntry(name, new FileInfo(source).Length, string.Empty));
        }

        // Manifeste d'origine conservé dans core/installer pour la maintenance.
        actions.Add(new WriteFileAction(
            Path.Combine(installDir, InstallerDirectory, "distribution.json"), null, null, null, ++step,
            Content: Serialize(request.Manifest)));

        // Préférences : seul le chemin de l'espace de travail est écrit.
        actions.Add(new MergeSettingsAction(
            Path.Combine(request.DataRoot, "settings.json"),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["workspace"] = request.WorkspaceDir },
            ++step));

        // Identité, manifeste d'origine, inventaire.
        actions.Add(new WriteFileAction(
            Path.Combine(installDir, IdentityFileName), null, null, null, ++step,
            Content: Serialize(new InstallationRecord(
                request.Manifest.Product,
                request.Manifest.Version,
                installDir,
                request.DataRoot,
                request.WorkspaceDir,
                DateTimeOffset.UtcNow))));

        actions.Add(new WriteFileAction(
            Path.Combine(installDir, DistributionFileName), null, null, null, ++step,
            Content: Serialize(request.Manifest)));

        actions.Add(new WriteFileAction(
            Path.Combine(installDir, InventoryFileName), null, null, null, ++step,
            Content: Serialize(InstallationInventory.Create(
                request.Manifest.Product,
                request.Manifest.Version,
                Guid.NewGuid().ToString("N"),
                inventory))));

        // Intégration système : menu, bureau (option), association .livexp.
        var launcherPath = LauncherPath(installDir);
        var shortcutIcon = Path.Combine(installDir, "assets", OperatingSystem.IsWindows() ? "livex.ico" : "livex.svg");
        actions.Add(new CreateShortcutAction(
            _pathPolicy.StartMenuShortcutPath("LIVEX"), launcherPath, false, ++step,
            IconPath: shortcutIcon));
        if (request.CreateDesktopShortcut)
            actions.Add(new CreateShortcutAction(
                _pathPolicy.DesktopShortcutPath("LIVEX"), launcherPath, true, ++step,
                IconPath: shortcutIcon));

        var packageIcon = Path.Combine(installDir, "assets", OperatingSystem.IsWindows() ? "livexp.ico" : "livexp.svg");
        actions.Add(new AssociatePackageAction(launcherPath, packageIcon, ++step));

        actions.Add(new WriteRegistrationAction("HKCU", "LIVEX", installDir, ++step));

        return new InstallPlan(
            InstallOperation.Install,
            actions,
            Array.Empty<string>(),
            bytesToWrite,
            bytesToDownload,
            warnings,
            RequiresConfirmation: false);
    }

    public Task<InstallPlan> PlanMaintenanceAsync(MaintenanceRequest request, CancellationToken ct = default) =>
        Task.Run(() => MaintenancePlanner.Plan(request), ct);

    // ---------------------------------------------------------------- exécution

    public async Task<ExecutionResult> ExecuteAsync(
        InstallPlan plan,
        InstallRequest request,
        IProgress<ProgressReport>? progress = null,
        CancellationToken ct = default)
    {
        if (request.DryRun)
        {
            return await SimulationExecutor.ExecuteAsync(plan, request, progress, ct).ConfigureAwait(false);
        }

        var context = new ActionContext(
            request.Manifest.Product,
            request.Manifest.Version,
            request.InstallDir,
            request.DataRoot,
            request.WorkspaceDir,
            BundleRoot(request));

        // Toute l'écriture s'exécute hors du fil d'interface : la progression
        // est ainsi peinte pendant que les fichiers sont copiés.
        return await Task.Run(
                () => RunActionsAsync(plan.Actions, plan.BytesToWrite, plan.Warnings, context, progress, ct),
                ct)
            .ConfigureAwait(false);
    }

    public async Task<ExecutionResult> ExecuteUninstallAsync(
        UninstallPlan plan,
        UninstallRequest request,
        IProgress<ProgressReport>? progress = null,
        CancellationToken ct = default)
    {
        if (request.DryRun)
        {
            return await SimulationExecutor.ExecuteAsync(plan, request, progress, ct).ConfigureAwait(false);
        }

        var record = request.Record;
        var context = new ActionContext(
            record.Product,
            record.Version,
            record.InstallDir,
            record.DataRoot,
            record.WorkspacePath ?? Path.Combine(record.DataRoot, "workspace"),
            AppContext.BaseDirectory);

        var warnings = plan.Warnings.Concat(plan.Residuals).ToList();
        return await Task.Run(
                () => RunActionsAsync(plan.Actions, 0, warnings, context, progress, ct),
                ct)
            .ConfigureAwait(false);
    }

    private async Task<ExecutionResult> RunActionsAsync(
        IReadOnlyList<PlanAction> actions,
        long bytesTotal,
        IReadOnlyList<string> planWarnings,
        ActionContext context,
        IProgress<ProgressReport>? progress,
        CancellationToken ct)
    {
        var journal = new JournalWriter(Path.Combine(context.DataRoot, "logs"));
        var integration = _integration ?? PlatformServices.CreateIntegration();
        var store = _store ?? PlatformServices.CreateInstallationStore(context.DataRoot);
        var createdFiles = new List<string>();
        var createdDirectories = new List<string>();
        var warnings = new List<string>(planWarnings);
        var residuals = new List<string>();
        var affected = new List<string>();
        var total = actions.Count;
        var current = 0;
        var lastProgressTick = 0L;

        void Report(ProgressReport report, bool force = false)
        {
            var now = Environment.TickCount64;
            if (!force && now - lastProgressTick < 50)
            {
                return;
            }

            lastProgressTick = now;
            progress?.Report(report);
        }

        try
        {
            foreach (var action in actions)
            {
                ct.ThrowIfCancellationRequested();
                current++;

                var bytesBefore = total == 0 ? 0 : bytesTotal * (current - 1) / total;
                var bytesPerAction = total == 0 ? 0 : bytesTotal / total;
                // Limité par le seuil de 50 ms : un rapport forcé par action noie le fil d'interface.
                Report(new ProgressReport(
                    action.GetType().Name, ComponentIdOf(action), current, total,
                    bytesBefore, bytesTotal, action.TargetPath, PlanActionLabel.Of(action)));

                void SubProgress(double fraction, string? entry)
                {
                    var bytes = bytesBefore + (long)(bytesPerAction * fraction);
                    Report(new ProgressReport(
                        action.GetType().Name, ComponentIdOf(action), current, total,
                        bytes, bytesTotal, entry ?? action.TargetPath, PlanActionLabel.Of(action),
                        Math.Clamp(fraction, 0, 1)));
                }

                Apply(action, context, integration, store, createdFiles, createdDirectories, warnings, SubProgress, ct);

                affected.Add(action.TargetPath);
                journal.Write(JournalLevel.Info, "execute", PlanActionLabel.Of(action), "ok", path: action.TargetPath);
            }

            Report(new ProgressReport(
                "complete", null, total, total, bytesTotal, bytesTotal, null, "complete", 1),
                force: true);

            return new ExecutionResult(true, warnings, residuals, journal.LogPath, Simulated: false, affected);
        }
        catch (OperationCanceledException)
        {
            journal.Write(JournalLevel.Warn, "execute", "run", "cancelled");
            Rollback(createdFiles, createdDirectories, residuals);
            warnings.Add("Opération annulée.");
            // INS-F-95 : une opération annulée sort en 2, comme un préflight en échec.
            return new ExecutionResult(false, warnings, residuals, journal.LogPath, Simulated: false, affected,
                ExitCodes.PreflightFailed);
        }
        catch (Exception ex)
        {
            journal.Write(JournalLevel.Error, "execute", "run", "failed", code: ex.GetType().Name);
            Rollback(createdFiles, createdDirectories, residuals);
            residuals.Insert(0, ex.Message);
            var hint = ex is OptionalAssetUnavailableException
                ? ExitCodes.OptionalAssetUnavailable
                : (int?)null;
            return new ExecutionResult(false, warnings, residuals, journal.LogPath, Simulated: false, affected, hint);
        }
    }

    private static string? ComponentIdOf(PlanAction action) => action switch
    {
        ExtractArchiveAction extract => extract.ComponentId,
        _ => null,
    };

    private void Apply(
        PlanAction action,
        ActionContext context,
        IIntegration integration,
        IInstallationStore store,
        ICollection<string> createdFiles,
        ICollection<string> createdDirectories,
        ICollection<string> warnings,
        Action<double, string?> subProgress,
        CancellationToken ct)
    {
        switch (action)
        {
            case CreateDirectoryAction a:
                if (!Directory.Exists(a.TargetPath))
                {
                    Directory.CreateDirectory(a.TargetPath);
                    createdDirectories.Add(a.TargetPath);
                }
                break;

            case WriteFileAction w:
                EnsureParent(w.TargetPath);
                if (w.Content is not null)
                {
                    File.WriteAllText(w.TargetPath, w.Content, new UTF8Encoding(false));
                    createdFiles.Add(w.TargetPath);
                    _fileSync.Flush(w.TargetPath);
                }
                else
                {
                    CopyVerified(w.SourcePath, w.TargetPath, w.ExpectedSha256);
                    createdFiles.Add(w.TargetPath);
                }
                break;

            case ReplaceFileAction r:
                EnsureParent(r.TargetPath);
                if (r.Content is not null)
                {
                    File.WriteAllText(r.TargetPath, r.Content, new UTF8Encoding(false));
                    _fileSync.Flush(r.TargetPath);
                }
                else
                {
                    CopyVerified(r.SourcePath, r.TargetPath, r.ExpectedSha256);
                }
                break;

            case DeleteFileAction d:
                DeleteFile(d.TargetPath);
                break;

            case DeleteDirectoryAction dd:
                DeleteDirectory(dd, warnings);
                break;

            case DownloadAssetAction dl:
                EnsureParent(dl.TargetPath);
                try
                {
                    var downloader = PlatformServices.CreateHttpDownloader();
                    downloader.DownloadAsync(dl.Url, dl.TargetPath, dl.ExpectedSize, dl.ExpectedSha256, null, ct)
                        .GetAwaiter().GetResult();
                    VerifyDownloadedAsset(dl);
                }
                catch (Exception ex) when (ex is not OptionalAssetUnavailableException)
                {
                    // Réseau, 404, empreinte : l'actif facultatif manque, le
                    // socle déjà installé reste intact (ADR-008).
                    throw new OptionalAssetUnavailableException(
                        $"Actif facultatif indisponible : {dl.Url} ({ex.Message})", ex);
                }

                createdFiles.Add(dl.TargetPath);
                break;

            case ExtractArchiveAction ex:
                var extractor = PlatformServices.CreateArchiveExtractor();
                var entryCount = ex.Entries?.Count ?? 0;
                var perEntry = new Progress<int>(value =>
                {
                    if (entryCount == 0)
                    {
                        return;
                    }

                    var index = Math.Clamp(value, 0, entryCount);
                    subProgress((double)index / entryCount, index < entryCount ? ex.Entries![index] : null);
                });
                extractor.ExtractAsync(ex.ArchivePath, ex.DestinationDirectory, ct, perEntry)
                    .GetAwaiter().GetResult();
                subProgress(1, null);
                break;

            case CreateShortcutAction sc:
            {
                var icon = sc.IconPath ?? IconPathFor(sc.TargetExePath);
                if (sc.IsDesktop) integration.CreateDesktopShortcut("LIVEX", sc.TargetExePath, icon);
                else integration.CreateStartMenuShortcut("LIVEX", sc.TargetExePath, icon);
                break;
            }

            case AssociatePackageAction ap:
                integration.AssociatePackage(ap.LauncherPath, ap.IconPath);
                break;

            case RemovePackageAssociationAction:
                integration.RemovePackageAssociation();
                break;

            case RemoveShortcutsAction rs:
                integration.RemoveShortcuts(rs.Name);
                break;

            case WriteRegistrationAction:
                integration.WriteUninstallEntry(
                    context.Product,
                    context.Version,
                    context.InstallDir,
                    $"\"{Path.Combine(context.InstallDir, SetupExecutableName())}\" --uninstall");
                store.Write(new InstallationRecord(
                    context.Product,
                    context.Version,
                    context.InstallDir,
                    context.DataRoot,
                    context.WorkspaceDir,
                    DateTimeOffset.UtcNow));
                break;

            case RemoveRegistrationAction:
                integration.RemoveUninstallEntry();
                store.Delete();
                break;

            case MergeSettingsAction ms:
                var settings = WorkspaceSettings.Read(ms.SettingsPath);
                foreach (var change in ms.Changes)
                {
                    if (change.Key == "workspace" && change.Value is string workspace)
                        settings.WorkspacePath = workspace;
                    else
                        settings.Extra[change.Key] = change.Value;
                }
                settings.Write(ms.SettingsPath);
                break;

            case RemoveSettingsAction rs2:
                if (File.Exists(rs2.SettingsPath)) File.Delete(rs2.SettingsPath);
                break;

            case UpdateInventoryAction ui:
                MergeInventory(ui);
                break;
        }
    }

    /// <summary>
    /// Un actif téléchargé est vérifié après coup : l'empreinte du manifeste
    /// fait foi, et un actif non conforme est supprimé du cache avant d'être
    /// signalé comme une erreur bloquante (ARCHITECTURE §7.2).
    /// </summary>
    private static void VerifyDownloadedAsset(DownloadAssetAction action)
    {
        if (string.IsNullOrEmpty(action.ExpectedSha256) || LooksLikePlaceholder(action.ExpectedSha256)) return;

        using var stream = File.OpenRead(action.TargetPath);
        var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (string.Equals(actual, action.ExpectedSha256, StringComparison.OrdinalIgnoreCase)) return;

        try { File.Delete(action.TargetPath); } catch { /* le fichier restera signalé par l'erreur */ }
        throw new InvalidDataException($"Actif non conforme, supprimé du cache : « {action.TargetPath} ».");
    }

    private static void DeleteDirectory(DeleteDirectoryAction action, ICollection<string> warnings)
    {
        if (!Directory.Exists(action.TargetPath)) return;
        try
        {
            Directory.Delete(action.TargetPath, action.Recursive);
        }
        catch (IOException)
        {
            // Un répertoire non récursif peut contenir des fichiers étrangers :
            // on les conserve et on le signale, au lieu d'échouer.
            if (action.Recursive) throw;
            warnings.Add($"Répertoire conservé car il contient des fichiers étrangers : {action.TargetPath}");
        }
        catch (UnauthorizedAccessException)
        {
            if (action.Recursive) throw;
            warnings.Add($"Répertoire conservé faute de droits : {action.TargetPath}");
        }
    }

    /// <summary>
    /// Supprime un fichier. L'exécutable de l'installateur peut être en cours
    /// d'exécution depuis l'installation (désinstallation) : il est d'abord
    /// renommé pour que le répertoire puisse ensuite être supprimé.
    /// </summary>
    private static void DeleteFile(string path)
    {
        if (!File.Exists(path)) return;

        try
        {
            File.Delete(path);
        }
        catch (UnauthorizedAccessException)
        {
            if (!IsCurrentProcess(path)) throw;
            var parked = path + ".old";
            try
            {
                if (File.Exists(parked)) File.Delete(parked);
                File.Move(path, parked);
            }
            catch
            {
                // Le fichier restera signalé par le répertoire non supprimé.
            }
        }
    }

    private static bool IsCurrentProcess(string path)
    {
        var process = Environment.ProcessPath;
        return process is not null &&
               string.Equals(Path.GetFullPath(process), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
    }

    private static void MergeInventory(UpdateInventoryAction action)
    {
        InstallationInventory? existing = null;
        if (File.Exists(action.InventoryPath))
        {
            try
            {
                existing = JsonSerializer.Deserialize<InstallationInventory>(File.ReadAllText(action.InventoryPath));
            }
            catch
            {
                existing = null;
            }
        }

        var entries = existing?.Files.ToList() ?? new List<InventoryEntry>();
        var removed = new HashSet<string>(action.Removed, StringComparer.OrdinalIgnoreCase);
        entries.RemoveAll(entry => removed.Contains(entry.Path));

        foreach (var added in action.Added)
        {
            entries.RemoveAll(entry => string.Equals(entry.Path, added.Path, StringComparison.OrdinalIgnoreCase));
            entries.Add(new InventoryEntry(added.Path, added.Size, added.Sha256));
        }

        var inventory = new InstallationInventory(
            action.Product,
            action.Version,
            existing?.InstallId ?? Guid.NewGuid().ToString("N"),
            existing?.InstalledAt ?? DateTimeOffset.UtcNow,
            entries);

        EnsureParent(action.InventoryPath);
        File.WriteAllText(action.InventoryPath, Serialize(inventory), new UTF8Encoding(false));
    }

    private static string? IconPathFor(string launcherPath)
    {
        try
        {
            var launcherDirectory = Path.GetDirectoryName(launcherPath);
            var installRoot = launcherDirectory is null ? null : Directory.GetParent(launcherDirectory)?.Parent?.FullName;
            if (installRoot is null) return null;
            var candidate = Path.Combine(installRoot, "assets", OperatingSystem.IsWindows() ? "livex.ico" : "livex.svg");
            return File.Exists(candidate) ? candidate : null;
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureParent(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
    }

    private static void CopyVerified(string? source, string target, string? expectedSha256)
    {
        if (source is null || !File.Exists(source))
            throw new FileNotFoundException($"Charge utile absente : « {source ?? target} ».", source ?? target);

        if (!string.IsNullOrEmpty(expectedSha256) && !LooksLikePlaceholder(expectedSha256))
        {
            using var stream = File.OpenRead(source);
            var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Empreinte invalide : « {source} ».");
        }

        File.Copy(source, target, overwrite: true);
    }

    /// <summary>Empreinte manifestement factice (démonstration) : pas de vérification.</summary>
    private static bool LooksLikePlaceholder(string hash) =>
        hash.Length != 64 || hash.Distinct().Count() <= 2;

    private void Rollback(IEnumerable<string> createdFiles, IEnumerable<string> createdDirectories, ICollection<string> residuals)
    {
        foreach (var file in createdFiles.Reverse())
        {
            try { if (File.Exists(file)) File.Delete(file); }
            catch (Exception ex) { residuals.Add($"Résidu non supprimé : {file} ({ex.Message})"); }
        }

        foreach (var dir in createdDirectories.Reverse())
        {
            try
            {
                if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
            catch (Exception ex)
            {
                residuals.Add($"Répertoire non supprimé : {dir} ({ex.Message})");
            }
        }
    }

    private static string ComponentDestination(string installDir, ComponentDescriptor component)
    {
        var root = component.InstallRoot;
        if (!string.IsNullOrWhiteSpace(root) && !string.Equals(root, "root", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(installDir, root);

        return component.Id switch
        {
            "prism-ldk" => Path.Combine(installDir, "plugins", "prism-ldk"),
            "docs" => Path.Combine(installDir, "docs"),
            "launcher" => Path.Combine(installDir, LauncherDirectory),
            _ => Path.Combine(installDir, "components", component.Id)
        };
    }

    private static string BundleRoot(InstallRequest request) =>
        request.BundleRoot ?? AppContext.BaseDirectory;

    private static string ResolveArchive(InstallRequest request, string asset) =>
        Path.IsPathRooted(asset) ? asset : Path.Combine(BundleRoot(request), asset);

    /// <summary>Liste les entrées d'une archive ZIP sans rien extraire (pour l'inventaire).</summary>
    private static IReadOnlyList<FileEntry> ReadArchiveEntries(string archivePath, string? installRoot)
    {
        var entries = new List<FileEntry>();
        if (!File.Exists(archivePath) || !archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return entries;
        }

        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            var prefix = string.IsNullOrWhiteSpace(installRoot) || installRoot == "root"
                ? string.Empty
                : installRoot.Replace('\\', '/').TrimEnd('/') + "/";

            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue; // répertoire
                }

                var relative = prefix + entry.FullName.Replace('\\', '/');
                entries.Add(new FileEntry(relative, entry.Length, string.Empty));
            }
        }
        catch (InvalidDataException)
        {
            // Archive illisible : le préflight l'aura déjà signalée.
        }

        return entries;
    }

    internal static string LauncherPath(string installDir) =>
        Path.Combine(installDir, LauncherDirectory,
            OperatingSystem.IsWindows() ? LauncherExecutable + ".exe" : LauncherExecutable);

    internal static string SetupExecutableName() =>
        OperatingSystem.IsWindows() ? "livex-setup.exe" : "livex-setup";

    /// <summary>
    /// Source de la copie de maintenance : l'exécutable courant seulement s'il
    /// s'agit bien de livex-setup, sinon une copie voisine du manifeste.
    /// </summary>
    private static string? ResolveSetupSource()
    {
        var process = Environment.ProcessPath;
        if (process is not null &&
            string.Equals(Path.GetFileNameWithoutExtension(process), "livex-setup", StringComparison.OrdinalIgnoreCase))
        {
            return process;
        }

        var sibling = Path.Combine(AppContext.BaseDirectory, SetupExecutableName());
        return File.Exists(sibling) ? sibling : null;
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
}
