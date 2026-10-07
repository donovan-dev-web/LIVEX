using Launcher.Application;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Infrastructure;
using Launcher.Protocol.Model;
using Launcher.Tests.Integration.Infrastructure;
using Xunit;

namespace Launcher.Tests.EndToEnd;

/// <summary>
/// Scénarios de bout en bout (TESTING.md §8) : une campagne est créée, exécutée par le vrai
/// binaire Stub.Syne détecté par manifeste, scellée puis relue. Le Launcher exerce ici
/// toutes ses couches : détection → orchestration → processus → paquet → scellement.
/// </summary>
[Collection("Composition")]
public sealed class CampaignEndToEndTests : IDisposable
{
    private readonly string _root;
    private readonly string _componentsRoot;
    private readonly string _componentsParent;

    public CampaignEndToEndTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"livexp-e2e-{Guid.NewGuid():N}");
        _componentsParent = Path.Combine(_root, "components");
        _componentsRoot = Path.Combine(_componentsParent, "syne");

        // Installe un composant simulé : manifeste + l'ensemble des fichiers de l'application
        // (un apphost seul ne suffit pas : il exige .dll, .deps.json et .runtimeconfig.json à côté).
        Directory.CreateDirectory(_componentsRoot);
        var stubSource = StubLocator.StubSynePath();
        var stubSourceDirectory = Path.GetDirectoryName(stubSource)!;
        foreach (var file in Directory.GetFiles(stubSourceDirectory, "Stub.Syne*"))
        {
            var target = Path.Combine(_componentsRoot, Path.GetFileName(file));
            File.Copy(file, target, overwrite: true);
            if (!OperatingSystem.IsWindows() && Path.GetExtension(file) is "." or "")
            {
                File.SetUnixFileMode(target,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }

        var manifest = """
            {
              "schema": 1,
              "id": "syne",
              "name": "SYNE",
              "type": "engine",
              "version": "0.1.0-stub",
              "executable": { "path": "Stub.Syne" },
              "capabilities": ["headless", "seed", "tickLimit", "export", "pause"],
              "endpoints": { "control": { "transport": "http" } },
              "health": { "probe": "http", "path": "/health/ready", "intervalMs": 1000 },
              "timeouts": { "startupMs": 30000, "shutdownMs": 15000 },
              "contributesTo": ["analyse", "immersion"]
            }
            """;
        File.WriteAllText(Path.Combine(_componentsRoot, "component.json"), manifest);
    }

    private (Launcher.App.Composition.LauncherComposition Composition, ManifestDetector Detector, string PackagesRoot) BuildComposition()
    {
        var detector = new ManifestDetector(new InMemoryJournalAdapter());
        var installation = detector.Inspect(_componentsRoot);
        Assert.NotNull(installation);
        Assert.Equal("syne", installation!.ComponentId);

        var packagesRoot = Path.Combine(_root, "packages");
        var dataRoot = Path.Combine(_root, "data");
        var composition = new Launcher.App.Composition.LauncherComposition(packagesRoot, _componentsParent, dataRoot);
        return (composition, detector, packagesRoot);
    }

    private static ExperimentDefinition Definition(int runs) => new()
    {
        Id = "EXP-E2E-001",
        Title = "Campagne de bout en bout",
        Profile = WellKnownProfiles.Analyse,
        Simulation = "ecosystem_01",
        RunCount = runs,
        Ticks = 3,
        SeedStrategy = SeedStrategy.Derived,
        BaseSeed = 900,
        AgentCount = 7,
        FailurePolicy = FailurePolicy.Continue,
    };

    /// <summary>Campagne nominale de bout en bout : création, exécution des runs par le stub réel, scellement, relecture.</summary>
    [Fact]
    public async Task Campagne_nominale_avec_moteur_simule_reel()
    {
        var (composition, _, packagesRoot) = BuildComposition();
        composition.DetectComponents();

        var definition = Definition(2);
        var packagePath = composition.Campaigns.CreateCampaign(definition);

        string failure = string.Empty;
        try
        {
            var sealedPath = await composition.Campaigns.ExecuteAsync(packagePath, definition, CancellationToken.None);
            Assert.Equal(packagePath, sealedPath);
            using var reader = new Launcher.Package.LivexPackageReader(sealedPath);
            Assert.Equal(PackageStates.Sealed, reader.Manifest.State);
            Assert.Equal(2, reader.Manifest.Counts.RunsDone);

            // Les données du run viennent du vrai binaire, pas d'un doublon du Launcher.
            var resultData = reader.ReadEntry("runs/RUN-0001/data/result.json");
            Assert.NotNull(resultData);
            Assert.Contains("900", System.Text.Encoding.UTF8.GetString(resultData));
            Assert.Contains("\"agents\":7", System.Text.Encoding.UTF8.GetString(resultData));

            // Le journal du paquet trace la campagne de bout en bout.
            Assert.Contains(reader.Journal(), line => line.Event == "package_sealed");
            return;
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
        }

        // Diagnostic : état du paquet après échec de la campagne.
        var detail = string.Empty;
        try
        {
            using var diagnosticReader = new Launcher.Package.LivexPackageReader(packagePath);
            detail = string.Join(" | ", diagnosticReader.RunIndex.Runs.Select(r => $"{r.RunId}={r.Status}:{r.Cause}"));
        }
        catch (Exception readException)
        {
            detail = $"paquet illisible : {readException.Message}";
        }

        Assert.Fail($"campagne en échec : {failure} — index : {detail}");
    }

    /// <summary>Le profil « simulation-seule » est satisfiable ; l'Immersion reste verrouillée avec sa raison ; un profil exigeant un composant absent est refusé explicitement.</summary>
    [Fact]
    public async Task Profils_resolus_et_verrou_immersion()
    {
        var (composition, _, _) = BuildComposition();
        composition.DetectComponents();

        var engineOnly = composition.Orchestration.ResolveProfile(WellKnownProfiles.SimulationSeule);
        Assert.True(engineOnly.Satisfiable, string.Join(" ; ", engineOnly.Problems));
        Assert.Equal(["syne"], engineOnly.StartupOrder);

        var immersion = composition.Orchestration.ResolveProfile(WellKnownProfiles.Immersion);
        Assert.True(immersion.ImmersionLocked);
        Assert.False(immersion.Satisfiable);
        Assert.Contains(immersion.Problems, p => p.Contains("G7"));

        // Aucune sélection silencieuse : un profil exigeant ECHOS est explicitement non satisfiable.
        var analysis = composition.Orchestration.ResolveProfile(WellKnownProfiles.Analyse);
        Assert.False(analysis.Satisfiable);
        Assert.Contains(analysis.Problems, p => p.Contains("echos"));
        await Task.CompletedTask;
    }

    /// <summary>
    /// Une composition construite avec des racines explicites reste hermétique après une
    /// redétection : aucun composant hors de sa racine n'entre dans le registre. Sans cette
    /// règle, DetectComponents() lisait les sources standard de la machine (~/.livex, registre
    /// utilisateur) et un composant installé ailleurs rendait un profil satisfiable là où la
    /// composition ne contient que le moteur simulé.
    /// </summary>
    [Fact]
    public void Redetection_reste_hermétique_à_la_racine_explicite()
    {
        var (composition, _, _) = BuildComposition();
        try
        {
            composition.DetectComponents();

            foreach (var componentId in new[] { "syne", "syne-mock", "echos", "prism" })
            {
                Assert.All(
                    composition.Orchestration.Registry.GetInstallations(componentId),
                    installation => Assert.StartsWith(_componentsParent, installation.Location));
            }
        }
        finally
        {
            composition.Dispose();
        }
    }

    /// <summary>La vérification d'environnement --check passe sur un poste où le composant simulé est installé.</summary>
    [Fact]
    public async Task Verification_environnement_detecte_le_composant_simule()
    {
        var (composition, _, _) = BuildComposition();
        composition.DetectComponents();
        Assert.True(composition.Orchestration.Registry.GetActiveInstallation("syne")!.ManifestValid);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Export_journaux_inclut_l_historique_de_session()
    {
        var (composition, _, _) = BuildComposition();
        composition.Journal.Info("Test", "entrée à exporter");
        var destination = Path.Combine(_root, "export", "session.ndjson");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        await composition.Facade.ExportSessionLogsAsync(destination);

        var exported = await File.ReadAllTextAsync(destination);
        Assert.Contains("entrée à exporter", exported);
        Assert.Equal(Path.Combine(_root, "data", "sessions"), composition.Facade.SessionLogDirectory);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
