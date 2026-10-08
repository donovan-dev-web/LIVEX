using System.Text.Json;
using Launcher.App.Composition;
using Launcher.Application;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Infrastructure;
using Launcher.Package;
using Launcher.Protocol;
using Launcher.Protocol.Model;
using Xunit;
using Xunit.Sdk;

namespace Launcher.Tests.EndToEnd;

/// <summary>
/// Campagne de bout en bout contre un SYNE réellement publié : le Launcher le lance,
/// suit son avancement, collecte ses sorties et scelle le paquet.
/// </summary>
[Collection("Composition")]
public sealed class PublishedSyneCampaignEndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"livex-published-syne-{Guid.NewGuid():N}");

    [Fact]
    public async Task Launcher_lance_SYNE_publie_collecte_les_donnees_et_scelle_le_paquet()
    {
        using var harness = PublishedSyneHarness.Create(_root, out var published);

        var definition = new ExperimentDefinition
        {
            Id = "EXP-SYNE-PUBLISHED",
            Title = "Acceptation Launcher avec SYNE publié",
            Profile = WellKnownProfiles.SimulationSeule,
            Simulation = "reference",
            RunCount = 1,
            Ticks = 10,
            AgentCount = 10,
            SeedStrategy = SeedStrategy.Derived,
            BaseSeed = 900,
            FailurePolicy = FailurePolicy.Stop,
            MaxRetries = 1,
        };

        var packagePath = harness.Campaigns.CreateCampaign(definition);
        var sealedPath = await harness.Campaigns.ExecuteAsync(packagePath, definition, CancellationToken.None);

        Assert.Equal(packagePath, sealedPath);
        using var reader = new LivexPackageReader(sealedPath);
        Assert.Equal(PackageStates.Sealed, reader.Manifest.State);
        Assert.Equal(1, reader.Manifest.Counts.RunsDone);
        var run = Assert.Single(reader.RunIndex.Runs);
        Assert.Equal(RunStatuses.Termine, run.Status);
        Assert.Equal(900, run.Seed);
        Assert.Equal(0, run.ExitCode);

        var resultBytes = reader.ReadEntry("runs/RUN-0001/data/result.json");
        Assert.NotNull(resultBytes);
        using var result = JsonDocument.Parse(resultBytes!);
        Assert.Equal("reference", result.RootElement.GetProperty("simulation").GetString());
        Assert.Equal(900UL, result.RootElement.GetProperty("seed").GetUInt64());
        Assert.Equal(10UL, result.RootElement.GetProperty("ticks").GetUInt64());
        Assert.Matches("^0x[0-9a-f]{16}$", result.RootElement.GetProperty("stateChecksum").GetString());

        var logs = reader.ReadEntry("runs/RUN-0001/logs/syne.log");
        Assert.NotNull(logs);

        // Régime batch : la cadence est la seule chose que la surcouche de configuration
        // change dans le flux — le stateChecksum, lui, est invariant. On l'observe donc
        // dans la provenance du world_initialized.
        //
        // La valeur exacte est une constante de RunEngineProfile ; l'invariant qui
        // protège la régression est « plus rapide que le défaut du moteur » : sans la
        // surcouche, un run à 10 ticks/s rejoue 1 s par tick et une campagne de 1000
        // ticks dure 100 s au lieu de quelques secondes.
        var streamBytes = reader.ReadEntry("runs/RUN-0001/data/stream.jsonl");
        Assert.NotNull(streamBytes);
        using var worldInitialized = JsonDocument.Parse(
            System.Text.Encoding.UTF8.GetString(streamBytes!).Split('\n', 2)[0]);
        Assert.Equal("world_initialized", worldInitialized.RootElement.GetProperty("type").GetString());
        var batchTicksPerSecond = worldInitialized.RootElement
            .GetProperty("world").GetProperty("ticksPerSecond").GetInt32();
        Assert.True(batchTicksPerSecond > 10,
            $"un run batch ne doit pas être rejoué en temps réel (défaut du moteur : 10) ; lu {batchTicksPerSecond}");

        // La configuration effective est archivée et atteste ce qui a été transmis.
        var resolvedBytes = reader.ReadEntry("runs/RUN-0001/config.resolved.json");
        Assert.NotNull(resolvedBytes);
        using var resolved = JsonDocument.Parse(resolvedBytes!);
        Assert.Equal(
            batchTicksPerSecond,
            resolved.RootElement.GetProperty("engine").GetProperty("configOverlay")
                .GetProperty("simulation").GetProperty("ticksPerSecond").GetInt32());

        Assert.Contains(reader.Manifest.Components,
            component => component.Id == "syne" && component.Version == published.ManifestVersion);
        Assert.True(reader.VerifyRunIntegrity("RUN-0001", out var integrityProblems),
            string.Join("; ", integrityProblems));
    }

    /// <summary>
    /// La progression affichée vient du tic rapporté par le moteur (EXPERIMENTS.md §8).
    /// Vérifié contre un vrai SYNE : la lecture de <c>/api/control/status</c> et son
    /// câblage jusqu'à la campagne ne peuvent pas être inventés par le test.
    /// </summary>
    [Fact]
    public async Task La_progression_du_run_vient_du_tic_rapporte_par_le_moteur()
    {
        using var harness = PublishedSyneHarness.Create(_root, out _);
        // On écoute l'exécuteur que la campagne utilise réellement : en observer un autre
        // ne prouverait rien du câblage.
        var observed = new List<RunTickProgress>();
        harness.Executor.TickProgress += (_, progress) => observed.Add(progress);

        // Assez de ticks pour que la sonde (200 ms) ait plusieurs échantillons : un run
        // de quelques dizaines de ticks s'achève avant le premier sondage.
        var definition = new ExperimentDefinition
        {
            Id = "EXP-SYNE-PROGRESS",
            Title = "Progression d'un run",
            Profile = WellKnownProfiles.SimulationSeule,
            Simulation = "reference",
            RunCount = 1,
            Ticks = 1000,
            AgentCount = 200,
            SeedStrategy = SeedStrategy.Derived,
            BaseSeed = 7,
            FailurePolicy = FailurePolicy.Stop,
            MaxRetries = 1,
        };

        var packagePath = harness.Campaigns.CreateCampaign(definition);
        await harness.Campaigns.ExecuteAsync(packagePath, definition, CancellationToken.None);

        Assert.NotEmpty(observed);
        // Plusieurs échantillons : un seul point ne prouverait pas que la sonde tourne
        // pendant le run, seulement qu'elle a interrogé le moteur une fois.
        Assert.True(observed.Count >= 3,
            $"la sonde d'avancement doit accompagner le run ; {observed.Count} échantillon(s) pour 1000 ticks");
        // L'horizon est connu dès que le run tourne : la barre peut donc afficher un
        // pourcentage honnête. Un échantillon pris pendant que le moteur attend encore
        // (état « ready », aucun tic, aucun horizon) ne prétend pas l'avoir — il signale
        // l'indétermination plutôt que de publier un faux dénominateur.
        var inFlight = observed.Where(progress => progress.Tick > 0).ToArray();
        Assert.NotEmpty(inFlight);
        Assert.All(inFlight, progress => Assert.Equal(1000, progress.MaxTicks));
        Assert.Contains(observed, progress => progress.Tick > 0);
        // L'avancement est monotone : une barre qui recule serait un défaut d'affichage.
        Assert.Equal(
            observed.Select(p => p.Tick).OrderBy(tick => tick).ToArray(),
            observed.Select(p => p.Tick).ToArray());
        // Le moteur a extinctions comprise : AliveCount est remonté, pas supposé.
        Assert.All(observed, progress => Assert.True(progress.AliveCount >= 0));
        Assert.NotEmpty(observed.Select(p => p.State).Distinct());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

/// <summary>Installation SYNE publiée, copie dans une racine de test, et câblage de campagne.</summary>
internal sealed class PublishedSyneHarness : IDisposable
{
    private PublishedSyneHarness(string root) => Root = root;

    /// <summary>Racine des paquets de la campagne.</summary>
    public string Root { get; }

    /// <summary>Version déclarée par le manifeste SYNE.</summary>
    public string ManifestVersion { get; private set; } = string.Empty;

    /// <summary>Moteur de campagnes, prêt à exécuter.</summary>
    public CampaignRunner Campaigns { get; private set; } = null!;

    /// <summary>Construit le banc, ou lève un skip si SYNE n'est pas publié.</summary>
    public static PublishedSyneHarness Create(string root, out PublishedSyneHarness harness)
    {
        harness = new PublishedSyneHarness(root);
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows())
        {
            throw SkipException.ForSkip(
                "The SYNE component manifest declares linux and windows executables only.");
        }

        var publishedSyneRoot = Environment.GetEnvironmentVariable("LIVEX_SYNE_PUBLISHED_ROOT");
        if (string.IsNullOrWhiteSpace(publishedSyneRoot))
        {
            throw SkipException.ForSkip("Requires LIVEX_SYNE_PUBLISHED_ROOT pointing to the published SYNE component.");
        }

        var componentRoot = Path.Combine(root, "components", "syne");
        harness.ManifestVersion = PublishedSyneInstaller.Install(publishedSyneRoot, componentRoot);

        var journal = new SessionFileJournal(Path.Combine(root, "sessions"));
        var clock = new SystemClock();
        var registry = new ServiceRegistry();
        var detector = new ManifestDetector(journal);
        var installation = detector.Inspect(componentRoot);
        Assert.NotNull(installation);
        Assert.True(installation!.ManifestValid, installation.DetectionCause);
        registry.RegisterInstallation(installation);

        var ports = new PortAllocator(5200, 5399, TcpPortProbe.IsFree);
        var orchestration = new OrchestrationService(registry, ports, clock, journal);
        var processManager = new ProcessManager(clock, journal);
        var executor = new ProcessRunExecutor(processManager, orchestration, ports, journal, new HealthProber());
        var packageService = new FileSystemPackageService(Path.Combine(root, "packages"), clock);
        harness.Campaigns = new CampaignRunner(packageService, executor, null, clock, journal);
        harness.Executor = executor;
        return harness;
    }

    /// <summary>Exécuteur de run, pour observer la progression brute du moteur.</summary>
    public ProcessRunExecutor Executor { get; private set; } = null!;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

/// <summary>
/// Installe (par copie) un SYNE publié dans une racine de composant de test :
/// manifeste à la racine, répertoire de l'exécutable reconstitué, droits
/// d'exécution posés. Partagé par les bancs qui ont besoin du vrai moteur.
/// </summary>
internal static class PublishedSyneInstaller
{
    /// <summary>Copie le composant SYNE publié et rend la version déclarée au manifeste.</summary>
    public static string Install(string publishedSourceRoot, string componentRoot)
    {
        var sourceRoot = Path.GetFullPath(publishedSourceRoot);
        var manifestPath = Path.Combine(sourceRoot, "component.json");
        Assert.True(File.Exists(manifestPath), $"SYNE manifest is missing: {manifestPath}");

        Directory.CreateDirectory(componentRoot);
        File.Copy(manifestPath, Path.Combine(componentRoot, "component.json"), overwrite: true);

        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var executableKey = OperatingSystem.IsWindows() ? "windows" : "linux";
        Assert.True(
            manifestDocument.RootElement.GetProperty("executable").TryGetProperty(executableKey, out var executableElement),
            $"SYNE manifest declares no executable for {executableKey}.");
        var executableRelativePath = executableElement.GetString();
        var manifestVersion = manifestDocument.RootElement.GetProperty("version").GetString();
        Assert.False(string.IsNullOrWhiteSpace(executableRelativePath));
        var sourceExecutableDirectory = Path.GetDirectoryName(
            Path.GetFullPath(Path.Combine(sourceRoot, executableRelativePath!)))!;
        Assert.True(Directory.Exists(sourceExecutableDirectory),
            $"SYNE publish directory is missing: {sourceExecutableDirectory}");

        var executableDirectoryRelativePath = Path.GetRelativePath(sourceRoot, sourceExecutableDirectory);
        var installedExecutableDirectory = Path.Combine(componentRoot, executableDirectoryRelativePath);
        Directory.CreateDirectory(installedExecutableDirectory);
        foreach (var sourceFile in Directory.EnumerateFiles(sourceExecutableDirectory))
        {
            var installedFile = Path.Combine(installedExecutableDirectory, Path.GetFileName(sourceFile));
            File.Copy(sourceFile, installedFile, overwrite: true);
            if (!OperatingSystem.IsWindows()
                && string.Equals(Path.GetFileName(sourceFile), Path.GetFileName(executableRelativePath), StringComparison.Ordinal))
            {
                File.SetUnixFileMode(installedFile,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }

        return manifestVersion ?? string.Empty;
    }
}