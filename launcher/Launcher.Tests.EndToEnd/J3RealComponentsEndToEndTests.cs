using System.IO.Compression;
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
/// J3 (étape 4 de <c>ROADMAP-V01.md</c>) — parcours réel de bout en bout :
/// campagne contre un SYNE publié et un ECHOS réellement installé, interruption
/// après un run, reprise sans rejouer le run terminé, analyse par l'API ECHOS et
/// rapport archivé à l'identique dans le paquet scellé. Aucun stub ne peut
/// produire ces preuves : les deux composants sont de vrais exécutables, ECHOS
/// étant lancé par son propre <c>echos-launcher</c> avec sa base dédiée.
/// </summary>
[Collection("Composition")]
public sealed class J3RealComponentsEndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"livexp-j3-{Guid.NewGuid():N}");
    private readonly string _componentsParent;
    private readonly string _packagesRoot;
    private readonly string _dataRoot;
    private string? _echosRoot;

    public J3RealComponentsEndToEndTests()
    {
        _componentsParent = Path.Combine(_root, "components");
        _packagesRoot = Path.Combine(_root, "packages");
        _dataRoot = Path.Combine(_root, "data");
    }

    /// <summary>
    /// Parcours complet : campagne 3 runs, interruption après le run 1, reprise,
    /// analyse réelle ECHOS, rapport fidèle, intégrité et frontières du paquet.
    /// </summary>
    [Fact]
    public async Task J3_SYNE_publie_et_ECHOS_reel_interruption_reprise_analyse_et_rapport()
    {
        RequireRealComponents();
        using var composition = new LauncherComposition(_packagesRoot, _componentsParent, _dataRoot);

        // 1. ECHOS réel démarré par le cycle de vie du Launcher et prêt à servir.
        Assert.Null(await composition.Facade.ToggleComponentAsync("echos", true));
        var instance = composition.Orchestration.Registry.FindByComponent("echos");
        Assert.NotNull(instance);
        var controlUrl = instance!.Endpoints["control"].Url;
        await StubInstall.WaitForHealthyAsync(controlUrl);

        // 2. Campagne réelle : 3 runs contre le SYNE publié.
        var definition = Definition("EXP-J3-A");
        var packagePath = composition.Campaigns.CreateCampaign(definition);

        // 3. Interruption après le premier run, par l'annulation réelle du campagne.
        using var cts = new CancellationTokenSource();
        var cancelled = false;
        composition.Campaigns.Progress += (_, progress) =>
        {
            if (!cancelled && progress.RunsDone >= 1)
            {
                cancelled = true;
                composition.Campaigns.Cancel(cts);
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            composition.Campaigns.ExecuteAsync(packagePath, definition, cts.Token));

        // 4. Paquet récupérable : RUN-0001 terminé dans l'index, aucun autre run joué —
        //    lu par le même service que celui de la reprise.
        var packages = new FileSystemPackageService(_packagesRoot, new SystemClock());
        var (state, interrupted) = packages.ReadState(packagePath);
        Assert.Equal(PackageStates.Recoverable, state);
        var first = interrupted.Runs.Single(run => run.RunId == "RUN-0001");
        Assert.Equal(RunStatuses.Termine, first.Status);
        Assert.DoesNotContain(interrupted.Runs,
            run => run.RunId == "RUN-0002" && run.Status == RunStatuses.Termine);
        var firstBeforeResume = JsonSerializer.Serialize(first);

        // 5. Reprise : le run terminé n'est pas rejoué, les deux autres s'exécutent.
        var sealedPath = await composition.Campaigns.ExecuteAsync(
            packagePath, definition, CancellationToken.None);
        using var reader = new LivexPackageReader(sealedPath);
        Assert.Equal(PackageStates.Sealed, reader.Manifest.State);
        Assert.Equal(3, reader.Manifest.Counts.RunsDone);

        var firstAfterResume = reader.RunIndex.Runs.Single(run => run.RunId == "RUN-0001");
        Assert.Equal(firstBeforeResume, JsonSerializer.Serialize(firstAfterResume));
        Assert.Equal(1, reader.Journal()
            .Count(line => line.Event == "run_completed" && line.RunId == "RUN-0001"));
        Assert.Contains("reprise : 1 run(s) déjà terminé(s), jamais rejoué(s)",
            ReadSessionJournal());

        // 6. Chaque run est analysé par ECHOS réel (journal du paquet), et ECHOS porte
        //    effectivement les trois runs sous leur identité composite.
        var runIds = new[] { "RUN-0001", "RUN-0002", "RUN-0003" };
        foreach (var runId in runIds)
        {
            var completed = reader.Journal()
                .Single(line => line.Event == "run_completed" && line.RunId == runId);
            Assert.Contains("analyse individuelle collectée", completed.Message);
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var runsJson = await http.GetStringAsync(new Uri(new Uri(controlUrl), "/api/runs"));
        foreach (var runId in runIds)
        {
            Assert.Contains($"{definition.Id}-{runId}", runsJson, StringComparison.Ordinal);
        }

        // 7. Rapport archivé = rapport que produit ECHOS à l'instant (fidélité octet à
        //    octet) ; l'en-tête prouve que le rapport vient du vrai moteur d'analyse.
        var archivedReport = reader.ReadEmergenceReport();
        Assert.NotNull(archivedReport);
        var freshReport = await PostReportAsync(controlUrl, definition.Id);
        Assert.Equal(freshReport, archivedReport);
        Assert.Contains($"# ECHOS experiment `{definition.Id}`", archivedReport);
        Assert.Contains("**Runs analysed:** 3", archivedReport);

        // 8. Intégrité des runs et frontières du paquet : empreintes vérifiées, aucune
        //    entrée hors des chemins autorisés (runs/RUN-xxxx/, ni '..', ni absolu).
        foreach (var runId in runIds)
        {
            Assert.True(reader.VerifyRunIntegrity(runId, out var problems),
                $"{runId} : {string.Join("; ", problems)}");
        }

        using (var archive = ZipFile.OpenRead(sealedPath))
        {
            var entryNames = archive.Entries.Select(entry => entry.FullName).ToArray();
            Assert.All(entryNames, name =>
            {
                Assert.DoesNotContain("..", name, StringComparison.Ordinal);
                Assert.False(name.StartsWith('/'), $"entrée absolue : {name}");
            });
            Assert.All(
                entryNames.Where(name => name.StartsWith("runs/", StringComparison.Ordinal)),
                name => Assert.Matches("^(runs/index\\.json|runs/RUN-[0-9]{4}/)", name));
        }

        // 9. Les versions des composants réels sont consignées dans le manifeste du
        //    paquet : le moteur qui a tourné et l'analyste qui a produit le rapport.
        var syneVersion = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(_componentsParent, "syne", "component.json")))
            .RootElement.GetProperty("version").GetString();
        Assert.Contains(reader.Manifest.Components,
            component => component.Id == "syne" && component.Version == syneVersion);
        var echosVersion = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(_componentsParent, "echos", "component.json")))
            .RootElement.GetProperty("version").GetString();
        Assert.Contains(reader.Manifest.Components,
            component => component.Id == "echos" && component.Version == echosVersion);
    }

    /// <summary>
    /// Contrôle des erreurs composant par composant (J3) : ECHOS tombe entre les deux
    /// moitiés de la campagne — SYNE finit son run, les données de simulation restent
    /// intactes, et l'absence de rapport est consignée avec sa cause, jamais approximée.
    /// </summary>
    [Fact]
    public async Task J3_echec_ECHOS_ne_supprime_rien_et_laisse_la_campagne_se_terminer()
    {
        RequireRealComponents();
        using var composition = new LauncherComposition(_packagesRoot, _componentsParent, _dataRoot);

        Assert.Null(await composition.Facade.ToggleComponentAsync("echos", true));
        var instance = composition.Orchestration.Registry.FindByComponent("echos");
        Assert.NotNull(instance);
        await StubInstall.WaitForHealthyAsync(instance!.Endpoints["control"].Url);

        var definition = Definition("EXP-J3-B", runCount: 2);
        var packagePath = composition.Campaigns.CreateCampaign(definition);

        using var cts = new CancellationTokenSource();
        var cancelled = false;
        composition.Campaigns.Progress += (_, progress) =>
        {
            if (!cancelled && progress.RunsDone >= 1)
            {
                cancelled = true;
                composition.Campaigns.Cancel(cts);
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            composition.Campaigns.ExecuteAsync(packagePath, definition, cts.Token));

        // L'analyste tombe entre les deux moitiés de la campagne.
        Assert.Null(await composition.Facade.ToggleComponentAsync("echos", false));
        Assert.Null(composition.Orchestration.Registry.FindByComponent("echos"));

        // Reprise : SYNE continue sans ECHOS, le second run termine, le paquet se scelle.
        var sealedPath = await composition.Campaigns.ExecuteAsync(
            packagePath, definition, CancellationToken.None);
        using var reader = new LivexPackageReader(sealedPath);
        Assert.Equal(PackageStates.Sealed, reader.Manifest.State);
        Assert.Equal(2, reader.Manifest.Counts.RunsDone);

        // Données de simulation intactes des deux côtés de la panne.
        foreach (var runId in new[] { "RUN-0001", "RUN-0002" })
        {
            Assert.True(reader.VerifyRunIntegrity(runId, out var problems),
                $"{runId} : {string.Join("; ", problems)}");
            Assert.NotNull(reader.ReadEntry($"runs/{runId}/data/stream.jsonl"));
        }

        // Erreur bornée au composant concerné : analyse collectée pour le run 1,
        // marquée indisponible pour le run 2 — qui reste terminé.
        var run1 = reader.Journal()
            .Single(line => line.Event == "run_completed" && line.RunId == "RUN-0001");
        Assert.Contains("analyse individuelle collectée", run1.Message);
        var run2 = reader.Journal()
            .Single(line => line.Event == "run_completed" && line.RunId == "RUN-0002");
        Assert.Contains("analyse individuelle indisponible", run2.Message);

        // L'absence de rapport est consignée, jamais approximée.
        Assert.Null(reader.ReadEmergenceReport());
        Assert.Contains("rapport d'émergence indisponible", ReadSessionJournal());
    }

    /// <summary>
    /// Prérequis J3 : Linux, SYNE publié (<c>LIVEX_SYNE_PUBLISHED_ROOT</c>) et
    /// installation ECHOS (<c>LIVEX_ECHOS_ROOT</c> ou dépôt courant, avec son venv).
    /// Un prérequis manquant donne un skip explicite : un banc vert sans composants
    /// réels ne prouverait rien pour J3.
    /// </summary>
    private void RequireRealComponents()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows())
        {
            throw SkipException.ForSkip(
                "J3 : les manifestes déclarent les exécutables linux et windows uniquement.");
        }

        var syneRoot = Environment.GetEnvironmentVariable("LIVEX_SYNE_PUBLISHED_ROOT");
        if (string.IsNullOrWhiteSpace(syneRoot))
        {
            throw SkipException.ForSkip(
                "LIVEX_SYNE_PUBLISHED_ROOT manquant : SYNE publié requis (dotnet publish -c Release).");
        }

        _echosRoot = DiscoverEchosRoot();
        if (_echosRoot is null)
        {
            throw SkipException.ForSkip(
                "ECHOS introuvable : définir LIVEX_ECHOS_ROOT ou exécuter depuis le dépôt " +
                "(installation : INTEGRATION_CONTRACT.md §10.3).");
        }

        var venvPython = OperatingSystem.IsWindows()
            ? Path.Combine(_echosRoot, ".venv", "Scripts", "python.exe")
            : Path.Combine(_echosRoot, ".venv", "bin", "python");
        if (!File.Exists(venvPython))
        {
            throw SkipException.ForSkip(
                $"venv ECHOS absent ({Path.Combine(_echosRoot, ".venv")}) — installation requise " +
                "(INTEGRATION_CONTRACT.md §10.3).");
        }

        // Base d'analyse dédiée au banc : héritée par le processus ECHOS, jamais partagée
        // avec une autre instance (launcher.py laisse prioritaire la valeur de l'opérateur).
        Environment.SetEnvironmentVariable(
            "ECHOS_ANALYTICS_DB", Path.Combine(_root, "echos-analytics.sqlite"));

        PublishedSyneInstaller.Install(syneRoot, Path.Combine(_componentsParent, "syne"));
        InstallEchos();
    }

    /// <summary>Trouve le composant ECHOS : variable d'environnement, sinon dépôt courant.</summary>
    private static string? DiscoverEchosRoot()
    {
        var configured = Environment.GetEnvironmentVariable("LIVEX_ECHOS_ROOT");
        if (!string.IsNullOrWhiteSpace(configured)
            && File.Exists(Path.Combine(configured, "component.json")))
        {
            return Path.GetFullPath(configured);
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var nested = Path.Combine(directory.FullName, "echos");
            if (File.Exists(Path.Combine(nested, "component.json"))
                && File.Exists(Path.Combine(nested, "echos-launcher")))
            {
                return nested;
            }
            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Installation ECHOS hermétique : manifeste et lanceur copiés dans le banc, code et
    /// venv liés à leur installation d'origine — le processus tourne avec son répertoire
    /// de travail dans le banc, sans dupliquer les 212 Mo du venv.
    /// </summary>
    private void InstallEchos()
    {
        var destination = Path.Combine(_componentsParent, "echos");
        Directory.CreateDirectory(destination);
        File.Copy(Path.Combine(_echosRoot!, "component.json"),
            Path.Combine(destination, "component.json"), overwrite: true);        var launcher = Path.Combine(destination, "echos-launcher");
        File.Copy(Path.Combine(_echosRoot!, "echos-launcher"), launcher, overwrite: true);
        // Le manifeste ECHOS pointe sur echos-launcher.cmd sous Windows : le fichier
        // doit être présent dans l'installation pour que la détection soit honnête.
        File.Copy(Path.Combine(_echosRoot!, "echos-launcher.cmd"),
            Path.Combine(destination, "echos-launcher.cmd"), overwrite: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(launcher,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        LinkDirectory(Path.Combine(destination, "echos"), Path.Combine(_echosRoot!, "echos"));
        LinkDirectory(Path.Combine(destination, ".venv"), Path.Combine(_echosRoot!, ".venv"));
    }

    /// <summary>
    /// Lien de répertoire : symbolique sous Linux, junction sous Windows
    /// (un lien symbolique de répertoire exége le privilège SeCreateSymbolicLink,
    /// qu'un hôte d'exécution CI n'a pas toujours ; la junction est équivalente
    /// pour un lien local et ne demande aucun privilège).
    /// </summary>
    private static void LinkDirectory(string linkPath, string targetPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return;
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            ArgumentList = { "/c", "mklink", "/J", linkPath, targetPath },
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("mklink /J n'a pas pu être lancé.");
        process.WaitForExit();
        Assert.True(Directory.Exists(linkPath), $"junction refusée : {linkPath} → {targetPath}");
    }

    private static ExperimentDefinition Definition(string id, int runCount = 3) => new()
    {
        Id = id,
        Title = "J3 — campagne réelle de bout en bout",
        Profile = WellKnownProfiles.Analyse,
        Simulation = "reference",
        RunCount = runCount,
        Ticks = 50,
        AgentCount = 10,
        SeedStrategy = SeedStrategy.Derived,
        BaseSeed = 20261008,
        FailurePolicy = FailurePolicy.Stop,
        MaxRetries = 1,
    };

    /// <summary>Journal de session du banc (data/sessions), concaténé.</summary>
    private string ReadSessionJournal()
    {
        var directory = Path.Combine(_dataRoot, "sessions");
        if (!Directory.Exists(directory))
        {
            return string.Empty;
        }

        return string.Join("\n", Directory.EnumerateFiles(directory).Select(File.ReadAllText));
    }

    /// <summary>Appel direct de GenerateReport (§10.1) : c'est la référence de fidélité.</summary>
    private async Task<string> PostReportAsync(string controlUrl, string experimentId)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var body = JsonSerializer.Serialize(new
        {
            experimentId,
            experimentPath = Path.Combine(_packagesRoot, "work", experimentId),
        });
        var response = await client.PostAsync(new Uri(new Uri(controlUrl), "/analysis/report"),
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.True(response.IsSuccessStatusCode,
            $"analysis/report refusé : {(int)response.StatusCode} — " +
            await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("report").GetString()!;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Les liens symboliques sont retirés avant la suppression : jamais suivre vers
        // l'installation d'origine (code et venv ECHOS du dépôt).
        foreach (var name in new[] { "echos", ".venv" })
        {
            var path = Path.Combine(_componentsParent, "echos", name);
            try
            {
                if (Directory.Exists(path) && new DirectoryInfo(path).LinkTarget is not null)
                {
                    Directory.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        Environment.SetEnvironmentVariable("ECHOS_ANALYTICS_DB", null);
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
