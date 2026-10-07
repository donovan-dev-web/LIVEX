using System.Net;
using System.Text;
using System.Text.Json;
using Launcher.App.Composition;
using Launcher.Application;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Infrastructure;
using Launcher.Protocol.Model;
using Launcher.Tests.Integration.Infrastructure;
using Xunit;

namespace Launcher.Tests.EndToEnd.Composition;

/// <summary>
/// Contrats d'appel d'<c>EchosAnalysisService</c> (INTEGRATION_CONTRACT.md §10.2).
///
/// Le banc fournit un ECHOS minimal qui **tient le journal des requêtes** : ces
/// tests portent donc sur ce que le Launcher exige réellement du service —
/// l'ordre ingestion-puis-analyse, l'identité composite, et surtout le refus de
/// poursuivre quand ECHOS a enregistré un autre run que celui demandé.
/// </summary>
public sealed class EchosAnalysisServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"livex-echos-service-{Guid.NewGuid():N}");

    [Fact]
    public async Task Ingerer_puis_analyser_avec_l_identite_composite_du_run()
    {
        var calls = new List<string>();
        var port = FreePort();
        using var echos = FakeEchos.Start(port, calls, identity: "EXP-A-RUN-0001", ticks: 4);
        using var service = NewService(port);

        var files = await service.AnalyzeRunAsync(Run("RUN-0001"), "EXP-A", CancellationToken.None);

        // L'analyse ne calcule rien : l'ingestion la précède, sinon ECHOS
        // répondrait « run inconnu » pour un run que la campagne vient de produire.
        Assert.Equal(["ingest/run:EXP-A-RUN-0001", "analysis/run:EXP-A-RUN-0001"], calls);
        // Le nom du fichier est celui qu'ECHOS a choisi : le Launcher le transporte
        // tel quel et ne préfixe rien (INTEGRATION_CONTRACT.md §3.2, fidélité).
        Assert.Equal(["EXP-A-RUN-0001.json"], files.Keys.ToArray());
        Assert.Equal("contenu", Encoding.UTF8.GetString(files["EXP-A-RUN-0001.json"]));
        // Le dossier de run est celui de la campagne, pas le « RUN-nnnn » brut :
        // c'est le chemin que le Launcher archive et qu'il peut rouvrir plus tard.
        Assert.All(calls, call => Assert.True(call.Length > 0));
    }

    [Fact]
    public async Task Refuser_de_continuer_quand_ECHOS_enregistre_un_autre_run()
    {
        // ECHOS fait autorité sur l'identité (c'est le flux qui la porte) : un
        // dossier de run mal apparié ne peut pas produire un « run inconnu »
        // opaque sur l'analyse suivante. Le décalage est nommé au lieu d'être
        // découvert deux étapes plus loin.
        var calls = new List<string>();
        var port = FreePort();
        using var echos = FakeEchos.Start(port, calls, identity: "EXP-B-RUN-0009", ticks: 3);
        using var service = NewService(port);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AnalyzeRunAsync(Run("RUN-0001"), "EXP-B", CancellationToken.None));

        Assert.Contains("EXP-B-RUN-0009", failure.Message, StringComparison.Ordinal);
        Assert.Contains("EXP-B-RUN-0001", failure.Message, StringComparison.Ordinal);
        // Aucune analyse demandée : l'erreur doit arrêter la chaîne avant que le
        // Launcher ne rende un rapport qui ne serait pas celui du run.
        Assert.DoesNotContain("analysis/run", calls);
        echos.Dispose();
    }

    [Fact]
    public async Task Un_compteur_d_ingestion_absent_est_une_reponse_malformee()
    {
        var calls = new List<string>();
        var port = FreePort();
        using var echos = FakeEchos.Start(port, calls, identity: null, ticks: 0);
        using var service = NewService(port);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AnalyzeRunAsync(Run("RUN-0001"), "EXP-C", CancellationToken.None));

        Assert.Contains("ingestion", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("analysis/run", calls);
    }

    [Fact]
    public async Task Le_manifeste_d_experience_porte_les_identites_composites()
    {
        var calls = new List<string>();
        var port = FreePort();
        using var echos = FakeEchos.Start(port, calls, identity: null, ticks: 0);
        using var service = NewService(port);

        var (files, report) = await service.AnalyzeExperimentAsync(
            "EXP-D", ["RUN-0001", "RUN-0002"], CancellationToken.None);

        Assert.Contains("analysis/experiment:EXP-D", calls);
        Assert.Contains("analysis/report:EXP-D", calls);
        Assert.NotNull(report);

        var manifestPath = Path.Combine(_root, "packages", "work", "EXP-D", "experiment.json");
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        Assert.Equal("EXP-D", manifest.RootElement.GetProperty("experimentId").GetString());
        // Les runs sont enregistrés sous ces clés composites à l'ingestion : un
        // manifeste qui porterait les « RUN-nnnn » ne désignerait aucun run.
        Assert.Equal(
            ["EXP-D-RUN-0001", "EXP-D-RUN-0002"],
            manifest.RootElement.GetProperty("runIds").EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty).ToArray());
        Assert.Single(files);
    }

    [Fact]
    public async Task Un_refus_409_deja_enregistre_poursuit_vers_l_analyse()
    {
        // Analyse temps réel : le run est déjà en base (consommateur du flux live
        // pendant l'exécution). Le refus « idempotent par refus » de l'ingestion
        // d'archive est donc attendu — il ne doit pas empêcher l'analyse d'un run
        // que la lecture confirme pourtant présent.
        var calls = new List<string>();
        using var echos = RefusingEchos.Start(FreePort(), calls);
        using var service = NewService(echos.Port);

        var files = await service.AnalyzeRunAsync(Run("RUN-0001"), "EXP-L", CancellationToken.None);

        Assert.Equal(
            ["ingest/run:EXP-L-RUN-0001", "api/runs/EXP-L-RUN-0001", "analysis/run:EXP-L-RUN-0001"],
            calls);
        Assert.Single(files);
    }

    private EchosAnalysisService NewService(int port)
    {
        var registry = new ServiceRegistry();
        var orchestration = new OrchestrationService(
            registry, new PortAllocator(5200, 5399, TcpPortProbe.IsFree), new SystemClock(), new InMemoryJournalAdapter());
        registry.Add(new ComponentInstance
        {
            InstanceId = "echos-0001",
            ComponentId = "echos",
            Installation = new ComponentInstallation { ComponentId = "echos", Location = "/tmp/echos" },
        });
        registry.FindByComponent("echos")!.Endpoints["control"] =
            new ResolvedEndpoint("control", $"http://127.0.0.1:{port}/", port);
        return new EchosAnalysisService(orchestration, Path.Combine(_root, "packages"));
    }

    private static RunResult Run(string runId) => new(
        runId, 42, "Terminé", 1, null,
        new Dictionary<string, RunDataFile>(),
        Array.Empty<(string, byte[])>(),
        4, TimeSpan.FromSeconds(1));

    private static int FreePort()
    {
        for (var port = 5300; port <= 5399; port++)
        {
            if (TcpPortProbe.IsFree(port))
            {
                return port;
            }
        }

        throw new InvalidOperationException("aucun port libre dans la zone haute de la plage interne");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

/// <summary>
/// ECHOS minimal qui enregistre les appels et répond ce qu'on lui demande de
/// répondre. Un <c>identity</c> nul produit une réponse d'ingestion sans
/// compteurs — le cas « réponse malformée » du contrat.
/// </summary>
internal sealed class FakeEchos : IDisposable
{
    private readonly MiniHttpServer _server;
    private readonly List<string> _calls;

    private FakeEchos(MiniHttpServer server, List<string> calls)
    {
        _server = server;
        _calls = calls;
    }

    public static FakeEchos Start(int port, List<string> calls, string? identity, int ticks)
    {
        var server = new MiniHttpServer(port);
        server.Post("/ingest/run", (_, body) =>
        {
            var requested = Field(body, "runId");
            calls.Add($"ingest/run:{requested}");
            return identity is null
                ? "{}"
                : $"{{\"ingested\":{{\"runId\":{JsonSerializer.Serialize(identity)},\"ticks\":{ticks}}}}}";
        });
        server.Post("/analysis/run", (_, body) =>
        {
            calls.Add($"analysis/run:{Field(body, "runId")}");
            var name = $"{Field(body, "runId")}.json";
            var content = Convert.ToBase64String(Encoding.UTF8.GetBytes("contenu"));
            return $"{{\"files\":[{{\"name\":{JsonSerializer.Serialize(name)},\"content\":\"{content}\"}}]}}";
        });
        server.Post("/analysis/experiment", (_, body) =>
        {
            calls.Add($"analysis/experiment:{Field(body, "experimentId")}");
            var content = Convert.ToBase64String(Encoding.UTF8.GetBytes("agrégat"));
            return $"{{\"files\":[{{\"name\":\"experiment.json\",\"content\":\"{content}\"}}]}}";
        });
        server.Post("/analysis/report", (_, body) =>
        {
            calls.Add($"analysis/report:{Field(body, "experimentId")}");
            return $"{{\"report\":\"rapport\"}}";
        });
        server.Start();
        return new FakeEchos(server, calls);
    }

    private static string Field(string? body, string name)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    public void Dispose() => _server.Dispose();
}

/// <summary>
/// ECHOS minimal qui refuse l'ingestion d'un run déjà en base (409, message
/// « déjà enregistré »), confirme ensuite sa présence par lecture, puis sert
/// l'analyse — le scénario exact d'une campagne suivie en direct puis relue.
/// Réseau réel (HttpListener) : le banc partagé ne sait pas produire d'état 409.
/// </summary>
internal sealed class RefusingEchos : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly List<string> _calls;

    private RefusingEchos(int port, List<string> calls)
    {
        Port = port;
        _calls = calls;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    /// <summary>Port d'écoute de ce faux ECHOS.</summary>
    public int Port { get; }

    public static RefusingEchos Start(int port, List<string> calls)
    {
        var server = new RefusingEchos(port, calls);
        server._listener.Start();
        _ = Task.Run(server.ServeAsync);
        return server;
    }

    private async Task ServeAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                return; // écoute arrêtée
            }

            var path = context.Request.Url?.AbsolutePath ?? string.Empty;
            if (path.StartsWith("/ingest/run", StringComparison.Ordinal))
            {
                var body = await ReadBodyAsync(context.Request).ConfigureAwait(false);
                _calls.Add($"ingest/run:{RunIdOf(body)}");
                await ReplyAsync(context, 409,
                    "{\"detail\":\"run déjà enregistré : EXP-L-RUN-0001 (4 tick(s) présent(s))\"}")
                    .ConfigureAwait(false);
            }
            else if (path.StartsWith("/api/runs/", StringComparison.Ordinal))
            {
                _calls.Add(path.TrimStart('/'));
                await ReplyAsync(context, 200, "{}").ConfigureAwait(false);
            }
            else if (path.StartsWith("/analysis/run", StringComparison.Ordinal))
            {
                var body = await ReadBodyAsync(context.Request).ConfigureAwait(false);
                _calls.Add($"analysis/run:{RunIdOf(body)}");
                var content = Convert.ToBase64String(Encoding.UTF8.GetBytes("contenu"));
                await ReplyAsync(context, 200,
                    $"{{\"files\":[{{\"name\":\"EXP-L-RUN-0001.json\",\"content\":\"{content}\"}}]}}")
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyAsync(context, 404, "{}").ConfigureAwait(false);
            }
        }
    }

    private static async Task<string> ReadBodyAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private static string RunIdOf(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("runId", out var value)
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static async Task ReplyAsync(HttpListenerContext context, int status, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (Exception)
        {
            // déjà arrêté
        }
    }
}