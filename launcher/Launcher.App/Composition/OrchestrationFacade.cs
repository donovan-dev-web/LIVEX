using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Application;
using Launcher.Infrastructure;
using Launcher.Package;
using Launcher.Protocol;
using Launcher.Protocol.Model;
using Launcher.Presentation.ViewModel;

namespace Launcher.App.Composition;

/// <summary>
/// Façade d'orchestration réalisée par l'application (ARCHITECTURE.md : la composition
/// appartient à App). Adapte domaine + infrastructure au contrat de la présentation :
/// cycle de vie réel des composants de la pile (INTEGRATION_CONTRACT.md §5), ressources
/// locales, journaux récents, liste des expériences.
/// </summary>
public sealed class OrchestrationFacade : IOrchestrationFacade, IDisposable
{
    private const string SessionTokenPrefix = "lxt-";
    private const int RecentLogCount = 30;
    private static readonly string[] KnownComponents = ["syne", "syne-mock", "echos", "prism"];

    private readonly OrchestrationService _orchestration;
    private readonly IProcessManager _processes;
    private readonly PortAllocator _ports;
    private readonly ISessionJournal _journal;
    private readonly string _packagesRoot;
    private readonly string _workspaceRoot;
    private readonly string _journalDirectory;
    private readonly string _documentationRoot;
    private readonly string _activeInstallationsPath;
    private readonly CampaignRunner _campaigns;
    private readonly string _sessionToken = $"{SessionTokenPrefix}{Guid.NewGuid().ToString("N")[..16]}";
    private readonly object _gate = new();
    private readonly Dictionary<string, ManagedInstance> _managed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _stopping = new(StringComparer.Ordinal);
    private readonly HashSet<string> _starting = new(StringComparer.Ordinal);
    private readonly object _recentLogsGate = new();
    private string? _recentLogsSignature;
    private IReadOnlyList<LogEntryViewModel> _recentLogs = Array.Empty<LogEntryViewModel>();

    // Échantillonnage CPU : delta de temps processeur entre deux appels.
    private TimeSpan _lastProcessorTime = TimeSpan.Zero;
    private DateTime _lastCpuSample = DateTime.MinValue;

    /// <summary>Journalise l'opération de cycle de vie la plus récente, pour la vue et le journal.</summary>
    public event EventHandler<string>? LifecycleReported;

    /// <summary>
    /// Levé quand une instance de composant vient de démarrer : c'est le signal d'ouverture
    /// automatique de sa console de logs (USER_INTERFACE.md §9).
    /// </summary>
    public event EventHandler<ComponentStartedEventArgs>? ComponentStarted;

    /// <summary>Initialise la façade avec les dépendances concrètes de la composition.</summary>
    public OrchestrationFacade(
        OrchestrationService orchestration,
        IProcessManager processes,
        PortAllocator ports,
        ISessionJournal journal,
        string packagesRoot,
        string workspaceRoot,
        string journalDirectory,
        string documentationRoot,
        CampaignRunner campaigns)
    {
        _orchestration = orchestration;
        _processes = processes;
        _ports = ports;
        _journal = journal;
        _packagesRoot = packagesRoot;
        _workspaceRoot = workspaceRoot;
        _journalDirectory = journalDirectory;
        _documentationRoot = documentationRoot;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _activeInstallationsPath = string.IsNullOrWhiteSpace(userProfile)
            ? string.Empty
            : Path.Combine(userProfile, ".livex", "active-components.json");
        _campaigns = campaigns;
        _campaigns.Progress += OnCampaignProgress;
        LoadActiveInstallations();
    }

    /// <inheritdoc />
    public event EventHandler<CampaignProgress>? CampaignProgressChanged;

    /// <summary>Liste les documents Markdown embarqués, par chemin relatif stable.</summary>
    public IReadOnlyList<DocumentationItemViewModel> ListDocumentation()
    {
        if (!Directory.Exists(_documentationRoot))
        {
            return [];
        }

        return Directory.EnumerateFiles(_documentationRoot, "*.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_documentationRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new DocumentationItemViewModel(path, Path.GetFileNameWithoutExtension(path)))
            .ToList();
    }

    /// <summary>Lit un document de la liste embarquée, sans accepter de chemin absolu.</summary>
    public string ReadDocumentation(string relativePath)
    {
        var root = Path.GetFullPath(_documentationRoot);
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        var relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative == ".." || !string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("chemin de documentation invalide", nameof(relativePath));
        }

        return File.ReadAllText(path);
    }

    /// <inheritdoc />
    public async Task<string> RunCampaignAsync(ExperimentDefinition definition, CancellationToken cancellationToken)
    {
        var packagePath = _campaigns.CreateCampaign(definition);
        return await _campaigns.ExecuteAsync(packagePath, definition, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> ResumeCampaignAsync(string packagePath, CancellationToken cancellationToken)
    {
        ExperimentDefinition definition;
        using (var reader = new LivexPackageReader(packagePath))
        {
            var experimentBytes = reader.ReadEntry(PackageConstants.ExperimentEntry)
                ?? throw new InvalidDataException("définition experiment.json absente du paquet");
            definition = System.Text.Json.JsonSerializer.Deserialize<ExperimentDefinition>(experimentBytes, ContractJson.Options)
                ?? throw new InvalidDataException("définition experiment.json invalide");
        }

        return await _campaigns.ExecuteAsync(packagePath, definition, cancellationToken).ConfigureAwait(false);
    }

    private void OnCampaignProgress(object? sender, CampaignProgress progress) =>
        CampaignProgressChanged?.Invoke(this, progress);

    /// <inheritdoc />
    public IReadOnlyList<ComponentRowViewModel> SnapshotComponents()
    {
        var rows = new List<ComponentRowViewModel>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var componentId in KnownComponents)
        {
            rows.Add(BuildRow(componentId, _orchestration.Registry.FindByComponent(componentId)));
            seen.Add(componentId);
        }

        foreach (var instance in _orchestration.Registry.All())
        {
            if (!seen.Contains(instance.ComponentId))
            {
                rows.Add(BuildRow(instance.ComponentId, instance));
                seen.Add(instance.ComponentId);
            }
        }

        return rows;
    }

    /// <inheritdoc />
    public (string GlobalState, string MainCause) AggregateHealth(IReadOnlyList<string> requiredComponents) =>
        _orchestration.AggregateHealth(requiredComponents);

    /// <inheritdoc />
    public ProfileResolution ResolveProfile(string profileId, SessionKind session, IReadOnlyCollection<string>? extraComponents = null) =>
        _orchestration.ResolveProfile(profileId, session, extraComponents);

    /// <inheritdoc />
    public ReportReadResult ReadEmergenceReport(string packagePath)
    {
        try
        {
            using var reader = new LivexPackageReader(packagePath);
            return new ReportReadResult(reader.ReadEmergenceReport(), null);
        }
        catch (Exception exception) when (exception is IOException
            or CorruptedPackageException
            or SchemaNotSupportedException
            or UnsafeEntryPathException)
        {
            return new ReportReadResult(null, exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<string?> ToggleComponentAsync(string componentId, bool start) =>
        start ? await StartComponentAsync(componentId).ConfigureAwait(false) : await StopComponentAsync(componentId).ConfigureAwait(false);

    /// <summary>
    /// Dossier des journaux stdout/stderr d'une instance (sessions/&lt;instance&gt;/logs), celui
    /// que la fenêtre console propose d'ouvrir. Chemin construit, non lu : il n'est pas
    /// exigé que le dossier existe déjà.
    /// </summary>
    public string LogsDirectoryFor(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || instanceId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || instanceId.Contains("..", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return Path.Combine(_workspaceRoot, "sessions", instanceId, "logs");
    }

    /// <inheritdoc />
    public ResourceSnapshot SampleResources()
    {
        var ram = SystemRamPercent();
        var cpu = LauncherCpuPercent();
        var storage = DiskPercent();
        return new ResourceSnapshot(ram, cpu, storage);
    }

    /// <inheritdoc />
    public IReadOnlyList<LogEntryViewModel> RecentLogs()
    {
        // Le sampler de l'interface relit ce journal toutes les 800 ms : sans mémoire de la
        // dernière lecture, chaque cycle relirait en entier les fichiers d'une journée entière
        // et désérialiserait des milliers d'événements inchangés. La signature (chemin + taille)
        // suffit : le journal de session ne fait qu'ajouter des lignes.
        lock (_recentLogsGate)
        {
            try
            {
                var root = _journalDirectory;
                if (!Directory.Exists(root))
                {
                    return _recentLogs;
                }

                var files = Directory.EnumerateFiles(root, "launcher-session-*.ndjson")
                    .OrderDescending(StringComparer.Ordinal)
                    .Take(3)
                    .Select(path => (Path: path, Length: new FileInfo(path).Length))
                    .ToList();
                var signature = string.Join('|', files.Select(file => $"{file.Path}:{file.Length}"));
                if (string.Equals(signature, _recentLogsSignature, StringComparison.Ordinal))
                {
                    return _recentLogs;
                }

                var entries = new List<(DateTimeOffset Ts, SessionEvent Event)>();
                foreach (var (path, _) in files)
                {
                    // Lecture partagée : le journal est encore ouvert en écriture par le Launcher
                    // — sous Windows, File.ReadLines échouerait en violation de partage et le
                    // panneau resterait sans jamais s'actualiser.
                    foreach (var line in SessionFileJournal.ReadSharedLines(path))
                    {
                        try
                        {
                            var @event = System.Text.Json.JsonSerializer.Deserialize<SessionEvent>(line, ContractJson.Compact);
                            if (@event is not null && DateTimeOffset.TryParse(@event.Ts, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var ts))
                            {
                                entries.Add((ts, @event));
                            }
                        }
                        catch (System.Text.Json.JsonException)
                        {
                        }
                    }
                }

                _recentLogs = entries
                    .OrderByDescending(entry => entry.Ts)
                    .Take(RecentLogCount)
                    .Select(entry => new LogEntryViewModel
                    {
                        Level = entry.Event.Level,
                        Message = entry.Event.Message,
                        Timestamp = entry.Ts.ToLocalTime().ToString("dd MMM yyyy HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture),
                    })
                    .ToList();
                _recentLogsSignature = signature;
                return _recentLogs;
            }
            catch (IOException)
            {
                // Un fichier momentanément illisible ne vide pas le panneau : la dernière vue reste.
                return _recentLogs;
            }
        }
    }

    /// <inheritdoc />
    public string SessionLogDirectory => _journalDirectory;

    /// <inheritdoc />
    public async Task ExportSessionLogsAsync(string destinationPath)
    {
        var files = Directory.Exists(_journalDirectory)
            ? Directory.EnumerateFiles(_journalDirectory, "launcher-session-*.ndjson")
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray()
            : [];
        if (files.Length == 0)
        {
            throw new FileNotFoundException("aucun journal de session n'est disponible", _journalDirectory);
        }

        var fullDestination = Path.GetFullPath(destinationPath);
        if (files.Any(path => string.Equals(Path.GetFullPath(path), fullDestination, StringComparison.Ordinal)))
        {
            throw new ArgumentException("la destination ne peut pas remplacer un journal de session", nameof(destinationPath));
        }

        var contents = new StringBuilder();
        foreach (var file in files)
        {
            var text = await SessionFileJournal.ReadSharedTextAsync(file).ConfigureAwait(false);
            contents.Append(text);
            if (text.Length > 0 && text[^1] is not '\n' and not '\r')
            {
                contents.AppendLine();
            }
        }

        await File.WriteAllTextAsync(fullDestination, contents.ToString(), new UTF8Encoding(false)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IReadOnlyList<PackageLogFileViewModel> ListPackageRunLogs(string packagePath)
    {
        using var reader = new LivexPackageReader(packagePath);
        return reader.ListRunLogEntries()
            .Select(entryPath =>
            {
                var segments = entryPath.Split('/');
                return new PackageLogFileViewModel(segments[1], segments[^1], entryPath);
            })
            .ToArray();
    }

    /// <inheritdoc />
    public string ReadPackageRunLog(string packagePath, string entryPath)
    {
        using var reader = new LivexPackageReader(packagePath);
        return reader.ReadRunLog(entryPath);
    }

    /// <inheritdoc />
    public async Task ExportPackageRunLogAsync(string packagePath, string entryPath, string destinationPath)
    {
        var fullDestination = Path.GetFullPath(destinationPath);
        var fullPackagePath = Path.GetFullPath(packagePath);
        if (string.Equals(
                fullDestination,
                fullPackagePath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new ArgumentException("le journal ne peut pas remplacer son paquet source", nameof(destinationPath));
        }

        var destinationDirectory = Path.GetDirectoryName(fullDestination)!;
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = Path.Combine(destinationDirectory, $".{Path.GetFileName(fullDestination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using var reader = new LivexPackageReader(packagePath);
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await reader.CopyRunLogToAsync(entryPath, destination).ConfigureAwait(false);
            }

            File.Move(temporaryPath, fullDestination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ComponentInstallationOption> ListComponentInstallations() =>
        KnownComponents
            .SelectMany(componentId => _orchestration.Registry.GetInstallations(componentId)
                .Where(installation => installation.ManifestValid && installation.BinaryPresent)
                .Select(installation => new ComponentInstallationOption(
                    componentId,
                    installation.Manifest!.Name,
                    installation.Manifest.Version,
                    installation.Location,
                    string.Equals(
                        _orchestration.Registry.GetActiveInstallation(componentId)?.Location,
                        installation.Location,
                        StringComparison.OrdinalIgnoreCase))))
            .ToArray();

    /// <inheritdoc />
    public void SetActiveInstallation(string componentId, string location)
    {
        var installation = _orchestration.Registry.GetInstallations(componentId)
            .FirstOrDefault(candidate => string.Equals(candidate.Location, location, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("installation non détectée", nameof(location));
        PersistActiveInstallation(componentId, installation.Location);
        _orchestration.Registry.SetActiveInstallation(componentId, installation);
        _journal.Info("InstallationSelection", $"{componentId} utilise {installation.Location}");
    }

    /// <inheritdoc />
    public ComponentInstallationOption RegisterComponentInstallation(string location)
    {
        var fullPath = Path.GetFullPath(location);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"répertoire d'installation introuvable : {fullPath}");
        }

        if (fullPath.IndexOfAny(['\r', '\n']) >= 0)
        {
            throw new ArgumentException("un chemin contenant un retour à la ligne ne peut pas être enregistré", nameof(location));
        }

        var detector = new ManifestDetector(_journal);
        var installation = detector.Inspect(fullPath)
            ?? throw new InvalidOperationException("aucun component.json trouvé dans ce répertoire");
        if (!KnownComponents.Contains(installation.ComponentId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"type de composant inconnu : {installation.ComponentId}");
        }

        if (!installation.ManifestValid || !installation.BinaryPresent)
        {
            throw new InvalidOperationException(installation.DetectionCause ?? "installation invalide ou exécutable absent");
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            throw new InvalidOperationException("répertoire utilisateur indisponible : installation non persistable");
        }

        var registryDirectory = Path.Combine(userProfile, ".livex");
        var registryPath = Path.Combine(registryDirectory, "components.registry");
        var registryRoot = Directory.GetParent(fullPath)?.FullName
            ?? throw new InvalidOperationException("le répertoire d'installation ne peut pas être enregistré");
        var roots = File.Exists(registryPath)
            ? File.ReadAllLines(registryPath).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => Path.GetFullPath(line.Trim()))
            : [];
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!roots.Any(root => string.Equals(
                root,
                registryRoot,
                pathComparison)))
        {
            Directory.CreateDirectory(registryDirectory);
            File.AppendAllLines(registryPath, [registryRoot]);
        }

        _orchestration.Registry.RegisterInstallation(installation);
        _ = _orchestration.Registry.GetActiveInstallation(installation.ComponentId);
        _journal.Info("InstallationRegistered", $"{installation.ComponentId} détecté dans {fullPath}");
        return new ComponentInstallationOption(
            installation.ComponentId,
            installation.Manifest!.Name,
            installation.Manifest.Version,
            installation.Location,
            true);
    }

    private void LoadActiveInstallations()
    {
        if (string.IsNullOrEmpty(_activeInstallationsPath) || !File.Exists(_activeInstallationsPath))
        {
            return;
        }

        try
        {
            var selections = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(_activeInstallationsPath),
                ContractJson.Options) ?? throw new InvalidDataException("fichier de sélection vide");
            foreach (var (componentId, location) in selections)
            {
                var installation = _orchestration.Registry.GetInstallations(componentId)
                    .FirstOrDefault(candidate => candidate.ManifestValid
                        && string.Equals(candidate.Location, location, StringComparison.OrdinalIgnoreCase));
                if (installation is null)
                {
                    _journal.Warn("InstallationSelection", $"sélection persistée indisponible pour {componentId}: {location}");
                    continue;
                }

                _orchestration.Registry.SetActiveInstallation(componentId, installation);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _journal.Warn("InstallationSelection", $"lecture des sélections persistées impossible : {exception.Message}", null);
        }
    }

    private void PersistActiveInstallation(string componentId, string location)
    {
        if (string.IsNullOrEmpty(_activeInstallationsPath))
        {
            throw new InvalidOperationException("répertoire utilisateur indisponible : sélection non persistable");
        }

        var selections = new Dictionary<string, string>(StringComparer.Ordinal);
        if (File.Exists(_activeInstallationsPath))
        {
            try
            {
                selections = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(_activeInstallationsPath),
                    ContractJson.Options) ?? throw new InvalidDataException("fichier de sélection vide");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"fichier de sélection invalide : {exception.Message}", exception);
            }
        }

        selections[componentId] = location;
        var directory = Path.GetDirectoryName(_activeInstallationsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{_activeInstallationsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(selections, ContractJson.Options), new UTF8Encoding(false));
            File.Move(temporaryPath, _activeInstallationsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ExperienceRowViewModel> ListExperiences()
    {
        var rows = new List<(DateTimeOffset Created, ExperienceRowViewModel Row)>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(_packagesRoot, "*.livexp"))
            {
                try
                {
                    using var reader = new LivexPackageReader(path);
                    var manifest = reader.Manifest;
                    var index = reader.RunIndex;
                    var title = manifest.Experiment.Title is { Length: > 0 } name ? name : manifest.Experiment.Id;
                    var created = DateTimeOffset.TryParse(manifest.CreatedAt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)
                        ? parsed
                        : DateTimeOffset.MinValue;
                    // La condition d'une campagne vivante est toujours « En cours » : aucun run
                    // exécuté encore, runs en cours, ou tous exécutés en attente d'analyse et de
                    // scellement. Seul le scellement fait « Terminée » (GUI.md §8).
                    var status = manifest.State switch
                    {
                        PackageStates.Sealed => "Terminée",
                        PackageStates.Recoverable => "Récupérable",
                        _ => "En cours",
                    };
                    rows.Add((created, new ExperienceRowViewModel
                    {
                        ExperimentId = manifest.Experiment.Id,
                        PackagePath = path,
                        Name = title,
                        Date = created.ToLocalTime().ToString("dd MMM yyyy HH:mm", System.Globalization.CultureInfo.CurrentCulture),
                        Duration = $"{manifest.Counts.Runs} runs",
                        Status = status,
                    }));
                }
                catch (Exception exception) when (exception is IOException
                    or CorruptedPackageException
                    or SchemaNotSupportedException
                    or UnsafeEntryPathException)
                {
                    // Un paquet illisible ne doit jamais vider la liste : il est simplement ignoré.
                }
            }
        }
        catch (IOException)
        {
        }

        return rows
            .OrderByDescending(entry => entry.Created)
            .Select(entry => entry.Row)
            .ToList();
    }

    /// <summary>Arrête proprement les composants lancés par le Launcher.</summary>
    public void Dispose()
    {
        _campaigns.Progress -= OnCampaignProgress;
        List<ManagedInstance> managed;
        lock (_gate)
        {
            managed = _managed.Values.ToList();
        }

        if (managed.Count == 0)
        {
            return;
        }

        try
        {
            // Arrêt simultané : chaque composant reçoit son propre délai de grâce déclaré au
            // manifeste, et la fermeture attend le plus long des délais — jamais leur somme.
            var stops = managed.Select(instance => Task.Run(async () =>
            {
                try
                {
                    await _processes.StopAsync(instance.InstanceId, instance.ProcessId, instance.Control, _sessionToken,
                        instance.Installation.ShutdownGrace, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Fermeture en cours : l'échec d'un arrêt gracieux ne doit pas empêcher les autres.
                }
            })).ToArray();
            var bound = TimeSpan.FromMilliseconds(
                managed.Max(instance => instance.Installation.ShutdownGrace.TotalMilliseconds) + 2000);
            Task.WhenAll(stops).Wait(bound);
        }
        catch (AggregateException)
        {
        }
    }

    private async Task<string?> StartComponentAsync(string componentId)
    {
        lock (_gate)
        {
            if (_managed.ContainsKey(componentId) || !_starting.Add(componentId))
            {
                return $"{DisplayName(componentId)} est déjà démarré";
            }

            var alternativeId = componentId switch
            {
                "syne" => "syne-mock",
                "syne-mock" => "syne",
                _ => null,
            };
            if (alternativeId is not null
                && (_managed.ContainsKey(alternativeId) || _starting.Contains(alternativeId)))
            {
                _starting.Remove(componentId);
                return $"{DisplayName(alternativeId)} est déjà démarré : SYNE réel et émulé ne peuvent pas fonctionner simultanément";
            }
        }

        try
        {
            return await StartComponentCoreAsync(componentId).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _starting.Remove(componentId);
            }
        }
    }

    private async Task<string?> StartComponentCoreAsync(string componentId)
    {
        var installation = _orchestration.Registry.GetActiveInstallation(componentId);
        if (installation is null)
        {
            return $"composant « {componentId} » non détecté : rien à démarrer";
        }

        if (!installation.ManifestValid)
        {
            return $"composant « {componentId} » : {installation.DetectionCause}";
        }

        var manifest = installation.Manifest!;
        var endpointDefinitions = manifest.Endpoints ?? new Dictionary<string, JsonEndpoint>();
        var declaredPort = endpointDefinitions.TryGetValue("control", out var controlEndpoint)
            ? controlEndpoint.Port
            : null;

        var instanceId = _orchestration.Registry.NextInstanceId(componentId);
        ResolvedEndpoint control;
        var resolvedEndpoints = new Dictionary<string, ResolvedEndpoint>(StringComparer.Ordinal);
        try
        {
            control = _ports.Resolve(componentId, instanceId, "control", declaredPort);
            resolvedEndpoints.Add("control", control);
            foreach (var (kind, endpoint) in endpointDefinitions)
            {
                if (kind == "control" || endpoint.Enabled == false)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(endpoint.LaunchArgument)
                    && (endpoint.LaunchArgument[0] != '-'
                        || endpoint.LaunchArgument.Any(char.IsWhiteSpace)))
                {
                    throw new InvalidOperationException($"argument de lancement invalide pour le point d'accès « {kind} »");
                }

                var resolved = _ports.Resolve(componentId, instanceId, kind, endpoint.Port);
                if (string.Equals(endpoint.Transport, "websocket", StringComparison.OrdinalIgnoreCase))
                {
                    resolved = resolved with { Url = $"ws://127.0.0.1:{resolved.Port}/" };
                }

                resolvedEndpoints.Add(kind, resolved);
            }
        }
        catch (PortUnavailableException exception)
        {
            _ports.Release(instanceId);
            return exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            _ports.Release(instanceId);
            return exception.Message;
        }
        var workDirectory = Path.Combine(_workspaceRoot, "sessions", instanceId);
        var logsDirectory = Path.Combine(workDirectory, "logs");
        Directory.CreateDirectory(logsDirectory);

        var correlationId = OrchestrationService.NewCorrelationId();

        // Arguments communs (INTEGRATION_CONTRACT.md §3.1) pour tout composant, puis les
        // arguments déclarés au manifeste. Aucun argument propre à SYNE n'est passé à ECHOS
        // ni à PRISM : les arguments de lot du §3.3 ne vont qu'au moteur (§3.3).
        var arguments = new List<string>
        {
            "--headless",
            "--instance-id", instanceId,
            "--control-port", control.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--work-dir", workDirectory,
            "--log-dir", logsDirectory,
            CorrelationHeaders.CorrelationIdArgument, correlationId,
        };

        if (manifest.Arguments is { Count: > 0 } declaredArguments)
        {
            arguments.AddRange(declaredArguments);
        }

        foreach (var (kind, endpoint) in endpointDefinitions)
        {
            if (kind != "control"
                && !string.IsNullOrWhiteSpace(endpoint.LaunchArgument)
                && resolvedEndpoints.TryGetValue(kind, out var resolved))
            {
                arguments.Add(endpoint.LaunchArgument);
                arguments.Add(resolved.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        // Moteur : type « engine » ou capacités de lot déclarées (seed + tickLimit).
        var isEngine = string.Equals(manifest.Type, "engine", StringComparison.Ordinal)
            || (manifest.Capabilities.Contains("seed", StringComparer.Ordinal)
                && manifest.Capabilities.Contains("tickLimit", StringComparer.Ordinal));
        if (isEngine)
        {
            var seed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0x7FFFFFFF;
            arguments.AddRange([
                "--simulation", WellKnownSimulations.Reference,
                "--seed", seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--ticks", "1000000",
                "--autostart",
            ]);
        }

        // État de sortie partagé entre le thread de démarrage et la publication de fin de
        // processus : la notification est asynchrone et peut arriver avant que l'instance
        // existe pour le registre.
        ProcessExitedEventArgs? observedExit = null;
        var exitRegistered = false;
        var exitProcessed = false;

        void OnExited(object? sender, ProcessExitedEventArgs args)
        {
            if (args.InstanceId != instanceId)
            {
                return;
            }

            lock (_gate)
            {
                observedExit = args;
            }

            FinishExit();
        }

        // Nettoyage d'une fin de processus, exactement une fois : publié par l'événement
        // quand l'instance est enregistrée, sinon pris en charge par le thread de démarrage.
        void FinishExit()
        {
            ProcessExitedEventArgs exit;
            lock (_gate)
            {
                if (exitProcessed || observedExit is null || !exitRegistered)
                {
                    return;
                }

                exitProcessed = true;
                exit = observedExit;
            }

            _processes.Exited -= OnExited;
            _orchestration.Registry.Remove(instanceId);
            _ports.Release(instanceId);
            lock (_gate)
            {
                if (_managed.TryGetValue(componentId, out var current) && current.InstanceId == instanceId)
                {
                    _managed.Remove(componentId);
                }
            }

            var stoppingNow = false;
            lock (_gate)
            {
                stoppingNow = _stopping.Contains(instanceId);
            }

            if (!stoppingNow)
            {
                // Fin non demandée : le composant passe Défaillant avec la cause normalisée (COMPONENTS.md §4.1).
                var at = DateTimeOffset.UtcNow;
                _orchestration.ApplyHealth(instanceId, StateRules.ProcessLost(at, exit.ExitCode, exit.Outcome));
                LifecycleReported?.Invoke(this, $"{DisplayName(componentId)} terminé de façon inattendue (code {exit.ExitCode})");
            }
        }

        // Abonnement avant StartAsync : un composant qui s'arrête dans la foulée (port repris
        // entre le pré-vol et le bind, exécutable défaillant) publie sa fin de façon
        // asynchrone — un abonnement postérieur perd cette unique notification et laisse
        // l'instance bloquée en « Démarrage » jusqu'au délai de démarrage du manifeste.
        _processes.Exited += OnExited;

        var startedAt = DateTimeOffset.UtcNow;
        int processId;
        try
        {
            processId = await _processes.StartAsync(new ProcessLaunchSpec
            {
                InstanceId = instanceId,
                ExecutablePath = ResolveExecutable(installation),
                WorkingDirectory = workDirectory,
                Arguments = arguments,
                Environment = new Dictionary<string, string>
                {
                    [CorrelationHeaders.SessionTokenEnvVar] = _sessionToken,
                    [CorrelationHeaders.CorrelationIdEnvVar] = correlationId,
                    [CorrelationHeaders.InstallRootEnvVar] = installation.Location,
                },
                StdOutLogPath = Path.Combine(logsDirectory, "stdout.log"),
                StdErrLogPath = Path.Combine(logsDirectory, "stderr.log"),
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _processes.Exited -= OnExited;
            _ports.Release(instanceId);
            return $"démarrage impossible : {exception.Message}";
        }

        var instance = new ComponentInstance
        {
            InstanceId = instanceId,
            ComponentId = componentId,
            Installation = installation,
            ProcessId = processId,
            StartedAt = startedAt,
            Health = StateRules.Ready(startedAt, ComponentState.Demarrage, "démarrage en cours"),
        };

        // Point de contrôle résolu publié au registre : c'est lui qui fait foi pour
        // les appels suivants (INTEGRATION_CONTRACT.md §10.1, NETWORK.md §4.1).
        foreach (var (kind, endpoint) in resolvedEndpoints)
        {
            instance.Endpoints[kind] = endpoint;
        }
        _orchestration.Registry.Add(instance);

        var managed = new ManagedInstance(instanceId, processId, new Uri(control.Url), installation, startedAt);
        lock (_gate)
        {
            _managed[componentId] = managed;
        }

        ProcessExitedEventArgs? earlyExit;
        lock (_gate)
        {
            exitRegistered = true;
            earlyExit = observedExit;
        }

        // Sortie survenue pendant le lancement : le nettoyage est mené ici (le thread de
        // démarrage est le seul à pouvoir le faire à ce stade) et le démarrage est refusé
        // explicitement — sans cela l'interface afficherait une carte « Démarrage » qui ne
        // finirait jamais, et le port resterait réservé.
        FinishExit();
        if (earlyExit is not null)
        {
            return $"{DisplayName(componentId)} s'est arrêté immédiatement après le lancement (code {earlyExit.ExitCode}, {earlyExit.Outcome})";
        }

        _journal.Info("ComponentStart", $"{instanceId} démarré (PID {processId})", correlationId, instanceId);
        LifecycleReported?.Invoke(this, $"{DisplayName(componentId)} démarré (PID {processId})");
        try
        {
            ComponentStarted?.Invoke(this, new ComponentStartedEventArgs
            {
                InstanceId = instanceId,
                ComponentId = componentId,
                DisplayName = DisplayName(componentId),
            });
        }
        catch (Exception exception)
        {
            // Un observateur (ouverture de console) en échec ne condamne pas le démarrage.
            _journal.Warn("ComponentStart", $"{instanceId} : observateur de démarrage en échec — {exception.Message}", correlationId, instanceId);
        }

        return null;
    }

    private async Task<string?> StopComponentAsync(string componentId)
    {
        ManagedInstance? managed;
        lock (_gate)
        {
            _managed.TryGetValue(componentId, out managed);
        }

        if (managed is null)
        {
            return $"{DisplayName(componentId)} n'est pas démarré";
        }

        lock (_gate)
        {
            _stopping.Add(managed.InstanceId);
        }

        try
        {
            var exit = await _processes.StopAsync(managed.InstanceId, managed.ProcessId, managed.Control, _sessionToken, managed.Installation.ShutdownGrace, CancellationToken.None)
                .ConfigureAwait(false);

            // Nettoyage assuré même si l'événement de sortie a déjà consommé l'instance.
            _orchestration.Registry.Remove(managed.InstanceId);

            _ports.Release(managed.InstanceId);
            lock (_gate)
            {
                if (_managed.TryGetValue(componentId, out var current) && current.InstanceId == managed.InstanceId)
                {
                    _managed.Remove(componentId);
                }
            }

            _journal.Info("ComponentStop", $"{managed.InstanceId} arrêté ({exit.Outcome})", null, managed.InstanceId);
            LifecycleReported?.Invoke(this, $"{DisplayName(componentId)} arrêté");
            return null;
        }
        finally
        {
            lock (_gate)
            {
                _stopping.Remove(managed.InstanceId);
            }
        }
    }

    private ComponentRowViewModel BuildRow(string componentId, ComponentInstance? instance)
    {
        var installation = _orchestration.Registry.GetActiveInstallation(componentId);
        var isLaunchable = IsLaunchable(installation);
        var row = new ComponentRowViewModel
        {
            Id = componentId,
            InstanceId = instance?.InstanceId,
            Name = DisplayName(componentId),
            Version = instance?.Installation.Manifest?.Version ?? installation?.Manifest?.Version ?? "—",
            Subtitle = SubtitleOf(componentId),
            Technology = TechnologyOf(componentId),
            Description = componentId == "prism" ? PrismDescription(installation) : DescriptionOf(componentId),
            Accent = AccentOf(componentId),
            IsAvailable = isLaunchable,
        };
        row.Update(
            instance is not null ? StateRules.Name(instance.Health.State)
                : installation is null ? "Absent"
                : isLaunchable ? "Arrêté"
                : "Indisponible",
            instance?.Health.Cause ?? installation?.DetectionCause ?? string.Empty,
            instance?.ProcessId);
        return row;
    }

    private static string ResolveExecutable(ComponentInstallation installation)
    {
        var manifest = installation.Manifest!;
        var executable = OperatingSystem.IsWindows()
            ? manifest.executable.Windows ?? manifest.executable.Path
            : manifest.executable.Linux ?? manifest.executable.Path;
        var path = Path.GetFullPath(Path.Combine(installation.Location, executable!));
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"binaire du composant absent : {path}");
        }

        return path;
    }

    private static bool IsLaunchable(ComponentInstallation? installation)
    {
        if (installation?.ManifestValid != true)
        {
            return false;
        }

        try
        {
            _ = ResolveExecutable(installation);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Mémoire système : /proc/meminfo sous Linux, repli lanceur ailleurs.</summary>
    private static double SystemRamPercent()
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    {
                        var total = ParseMemInfoKb(line);
                        var available = 0L;
                        foreach (var inner in File.ReadLines("/proc/meminfo"))
                        {
                            if (inner.StartsWith("MemAvailable:", StringComparison.Ordinal))
                            {
                                available = ParseMemInfoKb(inner);
                                break;
                            }
                        }

                        return total > 0 ? 100.0 * (total - available) / total : double.NaN;
                    }
                }
            }

            var gc = GC.GetGCMemoryInfo();
            return gc.TotalAvailableMemoryBytes > 0
                ? 100.0 * Environment.WorkingSet / gc.TotalAvailableMemoryBytes
                : double.NaN;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return double.NaN;
        }
    }

    private static long ParseMemInfoKb(string line)
    {
        var digits = new string(line.SkipWhile(char.IsWhiteSpace)
            .SkipWhile(character => !char.IsDigit(character))
            .TakeWhile(char.IsDigit).ToArray());
        return long.TryParse(digits, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    /// <summary>CPU du Launcher : delta de temps processeur entre deux échantillons.</summary>
    private double LauncherCpuPercent()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;
            var cpuTime = process.TotalProcessorTime;
            if (_lastCpuSample == DateTime.MinValue)
            {
                _lastProcessorTime = cpuTime;
                _lastCpuSample = now;
                return double.NaN;
            }

            var wall = (now - _lastCpuSample).TotalSeconds;
            var used = (cpuTime - _lastProcessorTime).TotalSeconds;
            _lastProcessorTime = cpuTime;
            _lastCpuSample = now;
            if (wall <= 0)
            {
                return double.NaN;
            }

            var cores = Math.Max(1, Environment.ProcessorCount);
            return Math.Clamp(100.0 * used / (wall * cores), 0, 100);
        }
        catch (InvalidOperationException)
        {
            return double.NaN;
        }
    }

    /// <summary>Stockage : espace utilisé sur le volume hébergeant les paquets.</summary>
    private double DiskPercent()
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(_packagesRoot));
            if (root is null)
            {
                return double.NaN;
            }

            var drive = new DriveInfo(root);
            var total = drive.TotalSize;
            return total > 0 ? 100.0 * (total - drive.AvailableFreeSpace) / total : double.NaN;
        }
        catch (Exception exception) when (exception is ArgumentException or DriveNotFoundException or IOException or UnauthorizedAccessException)
        {
            return double.NaN;
        }
    }

    private static string DisplayName(string componentId) => componentId switch
    {
        "syne" => "SYNE",
        "syne-mock" => "SYNE — mock",
        "echos" => "ECHOS",
        "prism" => "PRISM",
        _ => componentId,
    };

    private static string SubtitleOf(string componentId) => componentId switch
    {
        "syne" => "Moteur de simulation multi-agents",
        "syne-mock" => "Moteur de simulation simulé pour le développement",
        "echos" => "Analyse et observation des mondes simulés",
        "prism" => "Immersion dans le monde simulé",
        _ => string.Empty,
    };

    private static string TechnologyOf(string componentId) => componentId switch
    {
        "syne" => ".NET 10 — Simulation.Console",
        "syne-mock" => "Mock SYNE — exécution de développement",
        "echos" => "Python — API REST",
        "prism" => "Unreal Engine — PRISM-LDK",
        _ => string.Empty,
    };

    private static string DescriptionOf(string componentId) => componentId switch
    {
        "syne" => "Fait vivre des entités autonomes dans un monde déterministe, tick après tick.",
        "syne-mock" => "Remplace SYNE pour tester l'orchestration sans moteur de production.",
        "echos" => "Collecte les flux de simulation et produit analyses et rapports d'émergence.",
        "prism" => "Rend le monde en immersion. Non implémenté — verrouillé au jalon G7 (ADR-006).",
        _ => string.Empty,
    };

    /// <summary>
    /// Description de PRISM, évaluée à chaque instantané : le verrou est la conséquence du
    /// manifeste détecté, jamais une constante (ADR-006). Levé → la description est nue ;
    /// tenu → la cause exacte et le jalon sont affichés sur la carte même (USER_INTERFACE §3.4).
    /// </summary>
    private static string PrismDescription(ComponentInstallation? installation)
    {
        var lockCause = OrchestrationService.EvaluateImmersionLock(installation);
        return lockCause is null
            ? "Rend le monde en immersion."
            : $"Rend le monde en immersion — verrouillé : {lockCause}";
    }

    private static string AccentOf(string componentId) => componentId switch
    {
        "syne" => "#1fa8e8",
        "echos" => "#a04cf0",
        "prism" => "#f0902d",
        _ => "#9aa0aa",
    };

    private sealed record ManagedInstance(string InstanceId, int ProcessId, Uri Control, ComponentInstallation Installation, DateTimeOffset StartedAt);
}

/// <summary>Démarrage observé d'une instance de composant (USER_INTERFACE.md §9 : ouverture de console).</summary>
public sealed class ComponentStartedEventArgs : EventArgs
{
    /// <summary>Identifiant d'instance démarrée.</summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>Identifiant de type de composant.</summary>
    public string ComponentId { get; init; } = string.Empty;

    /// <summary>Nom affichable du composant.</summary>
    public string DisplayName { get; init; } = string.Empty;
}
