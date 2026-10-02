using System.Text.Json;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Infrastructure;
using Launcher.Presentation.ViewModel;
using Launcher.Protocol.Model;
using Xunit;

namespace Launcher.Tests.EndToEnd;

/// <summary>
/// G5 — session complète (ROADMAP.md §6.6) : détection des composants, profil satisfiable,
/// demande d'analyse à ECHOS par l'API du contrat §10.1, collecte des artefacts et
/// présentation du rapport d'émergence tel qu'ECHOS l'a écrit. Aucune interface web
/// n'est ouverte : la preuve est le journal des demandes HTTP tenu par le stub analyste.
/// Un analyste arrêté ne dégrade pas le moteur : la campagne suivante se scelle sans rapport.
/// </summary>
[Collection("Composition")]
public sealed class SessionCompleteEndToEndTests : IDisposable
{
    private readonly string _root;
    private readonly string _componentsParent;
    private readonly string _packagesRoot;
    private readonly string _dataRoot;
    private readonly int _echosPort;

    public SessionCompleteEndToEndTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"livexp-g5-{Guid.NewGuid():N}");
        _componentsParent = Path.Combine(_root, "components");
        _packagesRoot = Path.Combine(_root, "packages");
        _dataRoot = Path.Combine(_root, "data");
        _echosPort = StubInstall.FreePort();

        StubInstall.Install(_componentsParent, "syne", "Stub.Syne", SyneManifest);
        StubInstall.Install(_componentsParent, "echos", "Stub.Echos", EchosManifest(_echosPort));
    }

    [Fact]
    public async Task Session_complete_rapport_fidele_sans_interface_web()
    {
        using var composition = new Launcher.App.Composition.LauncherComposition(
            _packagesRoot, _componentsParent, _dataRoot);

        // 1. Détection et profil : SYNE + ECHOS détectés par manifeste, profil satisfiable.
        var profile = composition.Orchestration.ResolveProfile(WellKnownProfiles.Analyse);
        Assert.True(profile.Satisfiable, string.Join(" ; ", profile.Problems));
        Assert.Equal(["syne", "echos"], profile.StartupOrder);

        // 2. Démarrage d'ECHOS par le cycle de vie réel de la pile (INTEGRATION_CONTRACT.md §5),
        //    avec les arguments communs — jamais les arguments de lot de SYNE (§3.1 / §3.3).
        var startError = await composition.Facade.ToggleComponentAsync("echos", true);
        Assert.Null(startError);

        var instance = composition.Orchestration.Registry.FindByComponent("echos");
        Assert.NotNull(instance);
        var control = instance!.Endpoints["control"];
        Assert.Equal(_echosPort, control.Port);
        await StubInstall.WaitForHealthyAsync(control.Url);

        var instanceWork = Path.Combine(_dataRoot, "workspace", "sessions", instance.InstanceId);
        var echosArgs = File.ReadAllLines(Path.Combine(instanceWork, "args.txt"));
        Assert.Contains("--control-port", echosArgs);
        Assert.DoesNotContain("--seed", echosArgs);
        Assert.DoesNotContain("--ticks", echosArgs);
        Assert.DoesNotContain("--autostart", echosArgs);

        // 3. Campagne de bout en bout : deux runs exécutés par le vrai binaire Stub.Syne.
        var definition = Definition("EXP-G5-001");
        var packagePath = composition.Campaigns.CreateCampaign(definition);
        var sealedPath = await composition.Campaigns.ExecuteAsync(packagePath, definition, CancellationToken.None);

        using var reader = new Launcher.Package.LivexPackageReader(sealedPath);
        Assert.Equal(PackageStates.Sealed, reader.Manifest.State);
        if (reader.Manifest.Counts.RunsDone != 2)
        {
            var detail = string.Join(" | ", reader.RunIndex.Runs.Select(run => $"{run.RunId}={run.Status}:{run.Cause}"));
            var failureLog = reader.ReadEntry("runs/RUN-0001/logs/failure.txt");
            var stderr = failureLog is null ? "(aucun journal d'échec)" : System.Text.Encoding.UTF8.GetString(failureLog);
            Assert.Fail($"campagne incomplète : {reader.Manifest.Counts.RunsDone}/2 — index : {detail} — stderr : {stderr}");
        }

        // 4. Preuve que tout est passé par l'API HTTP d'analyse (§10.1), sans interface web :
        //    le stub tient le journal de ses demandes, deux analyses de run, l'agrégation et le rapport.
        var requests = File.ReadAllText(Path.Combine(instanceWork, "analysis", "requests.log"));
        Assert.Equal(2, CountOccurrences(requests, $"run {definition.Id}"));
        Assert.Contains($"experiment {definition.Id}", requests);
        Assert.Contains($"report {definition.Id}", requests);

        // 5. Fidélité : le rapport archivé est exactement celui qu'ECHOS renvoie à l'instant (§3.2).
        var expected = await PostReportAsync(control.Url, definition.Id);
        var archived = reader.ReadEmergenceReport();
        Assert.NotNull(archived);
        Assert.Equal(expected, archived);
        Assert.Contains($"expérience : {definition.Id}", archived);

        // 6. Présentation : la vue affiche le rapport tel qu'il est, sans interprétation (G5).
        var viewModel = new MainWindowViewModel(composition.Facade);
        viewModel.OpenPackage(sealedPath);
        Assert.Equal(archived, viewModel.EmergenceReport);
        Assert.Contains("tel quel", viewModel.ReportStatus);

        // 7. Mode défaillant : ECHOS arrêté, la campagne suivante se déroule et se scelle
        //    entièrement — moteur intact, absence de rapport affichée, jamais un rapport approximatif.
        var stopError = await composition.Facade.ToggleComponentAsync("echos", false);
        Assert.Null(stopError);
        Assert.Null(composition.Orchestration.Registry.FindByComponent("echos"));

        var second = Definition("EXP-G5-002");
        var secondPath = composition.Campaigns.CreateCampaign(second);
        var secondSealed = await composition.Campaigns.ExecuteAsync(secondPath, second, CancellationToken.None);

        using var secondReader = new Launcher.Package.LivexPackageReader(secondSealed);
        Assert.Equal(PackageStates.Sealed, secondReader.Manifest.State);
        Assert.Equal(2, secondReader.Manifest.Counts.RunsDone);
        Assert.Null(secondReader.ReadEmergenceReport());
    }

    /// <summary>Appel direct de GenerateReport (§10.1) : c'est la référence de fidélité.</summary>
    private async Task<string> PostReportAsync(string controlUrl, string experimentId)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var body = JsonSerializer.Serialize(new
        {
            experimentId,
            experimentPath = PackagesWorkPath(experimentId),
        });
        var response = await client.PostAsync(new Uri(new Uri(controlUrl), "/analysis/report"),
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.True(response.IsSuccessStatusCode, $"generatereport refusé : {(int)response.StatusCode}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("report").GetString()!;
    }

    private string PackagesWorkPath(string experimentId) => Path.Combine(_packagesRoot, "work", experimentId);

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static ExperimentDefinition Definition(string id) => new()
    {
        Id = id,
        Title = "Session complète G5",
        Profile = WellKnownProfiles.Analyse,
        Simulation = "ecosystem_01",
        RunCount = 2,
        Ticks = 3,
        SeedStrategy = SeedStrategy.Derived,
        BaseSeed = 4242,
        FailurePolicy = FailurePolicy.Continue,
    };

    private const string SyneManifest =
        """
        {
          "schema": 1,
          "id": "syne",
          "name": "SYNE",
          "type": "engine",
          "version": "0.1.0-stub",
          "executable": { "windows": "Stub.Syne.exe", "linux": "Stub.Syne", "path": "Stub.Syne" },
          "capabilities": ["headless", "seed", "tickLimit", "export", "pause"],
          "endpoints": { "control": { "transport": "http" } },
          "health": { "probe": "http", "path": "/health/ready", "intervalMs": 1000 },
          "timeouts": { "startupMs": 30000, "shutdownMs": 15000 },
          "contributesTo": ["analyse", "immersion"]
        }
        """;

    private static string EchosManifest(int port) =>
        $$"""
        {
          "schema": 1,
          "id": "echos",
          "name": "ECHOS",
          "type": "analysis",
          "version": "0.1.0-stub",
          "executable": { "windows": "Stub.Echos.exe", "linux": "Stub.Echos", "path": "Stub.Echos" },
          "capabilities": ["headless"],
          "endpoints": { "control": { "transport": "http", "port": {{port}} } },
          "timeouts": { "startupMs": 30000, "shutdownMs": 15000 },
          "contributesTo": ["analyse"]
        }
        """;

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
