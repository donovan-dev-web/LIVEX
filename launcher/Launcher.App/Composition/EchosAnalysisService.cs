using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Application;
using Launcher.Domain;
using Launcher.Domain.Model;

namespace Launcher.App.Composition;

/// <summary>
/// Réalise <see cref="IAnalysisService"/> par HTTP contre le port de contrôle d'ECHOS
/// (INTEGRATION_CONTRACT.md §10.1 — le Launcher demande, ECHOS produit, ADR-003).
/// Joignabilité : si une instance ECHOS tourne, son port résolu fait foi ; sinon le port
/// déclaré au manifeste ; sinon la valeur par défaut 5000 (NETWORK.md §6.2).
/// Tout échec — refus, délai dépassé, réponse malformée — lève une erreur explicite :
/// l'absence d'analyse est consignée par la campagne, jamais approximée.
/// </summary>
public sealed class EchosAnalysisService : IAnalysisService, IDisposable
{
    /// <summary>Port de contrôle ECHOS par défaut (NETWORK.md §6.2).</summary>
    public const int DefaultControlPort = 5000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly OrchestrationService _orchestration;
    private readonly string _packagesRoot;
    private readonly HttpClient _client;

    /// <summary>Initialise le service sur la composition et la racine des paquets.</summary>
    public EchosAnalysisService(OrchestrationService orchestration, string packagesRoot)
    {
        _orchestration = orchestration;
        _packagesRoot = packagesRoot;
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, byte[]>> AnalyzeRunAsync(RunResult run, string experimentId, CancellationToken cancellationToken)
    {
        var runPath = RunPath(experimentId, run.RunId);
        var identity = RunIdentity.For(experimentId, run.RunId);

        // L'analyse ne calcule rien : elle lit ce qui est enregistré. Le run vient
        // d'être produit par SYNE et rien ne l'a encore observé, donc on l'ingère
        // d'abord depuis le flux archivé. Sans cette étape ECHOS répondrait
        // « run inconnu » alors que la campagne vient de tourner.
        await IngestAsync(identity, runPath, cancellationToken).ConfigureAwait(false);

        var body = await PostJsonAsync("analysis/run",
            new AnalysisRequest { ExperimentId = experimentId, RunId = identity, RunPath = runPath },
            cancellationToken).ConfigureAwait(false);
        return DecodeFiles(body);
    }

    private async Task IngestAsync(string identity, string runPath, CancellationToken cancellationToken)
    {
        string body;
        try
        {
            body = await PostJsonAsync("ingest/run",
                new AnalysisRequest { RunId = identity, RunPath = runPath },
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("(409)", StringComparison.Ordinal)
            && exception.Message.Contains("déjà enregistré", StringComparison.Ordinal))
        {
            // Analyse temps réel : le run a déjà été enregistré par le consommateur
            // du flux live pendant son exécution. Le refus « idempotent par refus »
            // est donc attendu — il reste à constater que le run est bien présent
            // avant de demander son analyse, sinon l'absence serait découverte
            // plus tard sous forme de « run inconnu ».
            await RequireRecordedRunAsync(identity, cancellationToken).ConfigureAwait(false);
            return;
        }

        var ingested = Deserialize<IngestPayload>(body)?.Ingested
            ?? throw Malformed("compteurs d'ingestion absents de la réponse");

        // ECHOS fait autorité sur l'identité : c'est le flux qui la porte. Le
        // Launcher n'a donc pas à la redéduire, mais il doit **constater** que
        // l'identité enregistrée est celle qu'il va demander à analyser. Sans ce
        // contrôle, un flux d'une autre campagne produirait un « run inconnu »
        // opaque sur l'analyse suivante, au lieu de nommer le décalage.
        if (!string.IsNullOrWhiteSpace(ingested.RunId)
            && !string.Equals(ingested.RunId, identity, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"ECHOS a enregistré le flux sous « {ingested.RunId} » alors que le run demandé est « {identity} »");
        }
    }

    /// <summary>Meilleur effort : demande à ECHOS de consommer le flux WebSocket
    /// live du run en cours (analyse temps réel). Un échec — ECHOS absent, refus,
    /// délai — n'est jamais fatal : le run reste ingérable en fin de campagne.</summary>
    public async Task<bool> TryStartLiveIngestAsync(int observePort, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await PostJsonAsync("ingest/live",
                new LiveIngestRequest { WsUrl = $"ws://127.0.0.1:{observePort}/" },
                timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Vérifie que le run est déjà présent dans la base analytique.</summary>
    private async Task RequireRecordedRunAsync(string identity, CancellationToken cancellationToken)
    {
        var baseUri = ResolveControlBase();
        var path = $"api/runs/{Uri.EscapeDataString(identity)}";
        HttpResponseMessage response;
        try
        {
            response = await _client.GetAsync(new Uri(baseUri, path), cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"ECHOS injoignable sur {baseUri} — {exception.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"ECHOS refuse l'ingestion du run « {identity} » (déjà enregistré) " +
                    $"mais ne le connaît pas ({(int)response.StatusCode}){DescribeDetail(body)}");
            }
        }
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyDictionary<string, byte[]> AggregateFiles, string? EmergenceReport)> AnalyzeExperimentAsync(
        string experimentId,
        IReadOnlyList<string> runIds,
        CancellationToken cancellationToken)
    {
        var experimentPath = ExperimentPath(experimentId);
        await WriteExperimentManifestAsync(experimentPath, experimentId, runIds, cancellationToken)
            .ConfigureAwait(false);
        var filesBody = await PostJsonAsync("analysis/experiment",
            new AnalysisRequest { ExperimentId = experimentId, ExperimentPath = experimentPath },
            cancellationToken).ConfigureAwait(false);
        var aggregateFiles = DecodeFiles(filesBody);

        var reportBody = await PostJsonAsync("analysis/report",
            new AnalysisRequest { ExperimentId = experimentId, ExperimentPath = experimentPath },
            cancellationToken).ConfigureAwait(false);
        var report = Deserialize<ReportPayload>(reportBody)?.Report
            ?? throw Malformed("rapport d'émergence absent de la réponse");

        return (aggregateFiles, report);
    }

    /// <summary>Libère le client HTTP.</summary>
    public void Dispose() => _client.Dispose();

    private string ExperimentPath(string experimentId) => Path.Combine(_packagesRoot, "work", experimentId);

    private string RunPath(string experimentId, string runId) => Path.Combine(ExperimentPath(experimentId), runId);

    private static async Task WriteExperimentManifestAsync(
        string experimentPath,
        string experimentId,
        IReadOnlyList<string> runIds,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(experimentPath);
        var manifestPath = Path.Combine(experimentPath, "experiment.json");
        var temporaryPath = Path.Combine(experimentPath, $"experiment.{Guid.NewGuid():N}.tmp");

        // Le manifeste liste les identités analytiques, pas les « RUN-nnnn » :
        // c'est sous ces clés que les runs ont été enregistrés à l'ingestion.
        var contents = JsonSerializer.Serialize(
            new { experimentId, runIds = runIds.Select(runId => RunIdentity.For(experimentId, runId)).ToArray() },
            JsonOptions);
        try
        {
            await File.WriteAllTextAsync(temporaryPath, contents, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>Base de contrôle d'ECHOS : instance en cours, sinon manifeste, sinon 5000.</summary>
    private Uri ResolveControlBase()
    {
        var instance = _orchestration.Registry.FindByComponent("echos");
        if (instance is not null && instance.Endpoints.TryGetValue("control", out var resolved))
        {
            return new Uri(resolved.Url, UriKind.Absolute);
        }

        var installation = _orchestration.Registry.GetActiveInstallation("echos");
        var declaredPort = installation?.ManifestValid == true
            ? installation.Manifest?.Endpoints?.GetValueOrDefault("control")?.Port
            : null;
        return new Uri($"http://127.0.0.1:{declaredPort ?? DefaultControlPort}/", UriKind.Absolute);
    }

    private async Task<string> PostJsonAsync(string path, object payload, CancellationToken cancellationToken)
    {
        var baseUri = ResolveControlBase();
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, path)) { Content = content };

        HttpResponseMessage response;
        try
        {
            response = await _client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"ECHOS injoignable sur {baseUri} — {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException($"ECHOS n'a pas répondu dans le délai accordé ({path})");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // Le détail FastAPI explique le refus (« run inconnu »,
                // « entité introuvable… ») : affiché tel quel dans le journal.
                throw new InvalidOperationException(
                    $"ECHOS a refusé la demande {path} ({(int)response.StatusCode}){DescribeDetail(body)}");
            }

            return body;
        }
    }

    /// <summary>Extrait le champ <c>detail</c> d'une réponse d'erreur FastAPI, vide sinon.</summary>
    private static string DescribeDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("detail", out var detail))
            {
                var text = detail.ValueKind == JsonValueKind.String
                    ? detail.GetString()
                    : detail.GetRawText();
                return string.IsNullOrWhiteSpace(text) ? string.Empty : $" — {text}";
            }
        }
        catch (JsonException)
        {
            // Corps non JSON : le code de statut reste seul, jamais d'exception.
        }

        return string.Empty;
    }

    private static IReadOnlyDictionary<string, byte[]> DecodeFiles(string body)
    {
        var payload = Deserialize<AnalysisPayload>(body)
            ?? throw Malformed("liste de fichiers absente de la réponse");

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in payload.Files ?? Array.Empty<AnalysisFile>())
        {
            // Le nom vient d'ECHOS : le contenu est accepté tel quel, le chemin non
            // (PACKAGE_FORMAT.md §8). Un nom refusé fait échouer l'analyse, jamais l'écriture
            // du paquet — la campagne consigne alors une analyse indisponible.
            if (!Launcher.Package.PackageEntryRules.IsSafe(file.Name))
            {
                throw Malformed($"nom d'entrée refusé « {file.Name} » (PACKAGE_FORMAT.md §8)");
            }

            byte[] content;
            try
            {
                content = Convert.FromBase64String(file.Content ?? string.Empty);
            }
            catch (FormatException exception)
            {
                throw Malformed($"contenu base64 illisible pour « {file.Name} » : {exception.Message}");
            }

            files[file.Name] = content;
        }

        return files;
    }

    private static T? Deserialize<T>(string body) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw Malformed($"réponse illisible : {exception.Message}");
        }
    }

    private static InvalidOperationException Malformed(string detail) =>
        new($"réponse malformée d'ECHOS — {detail}");

    /// <summary>Corps des demandes d'analyse (INTEGRATION_CONTRACT.md §10.1).</summary>
    private sealed class AnalysisRequest
    {
        [JsonPropertyName("experimentId")]
        public string? ExperimentId { get; set; }

        [JsonPropertyName("runId")]
        public string? RunId { get; set; }

        [JsonPropertyName("runPath")]
        public string? RunPath { get; set; }

        [JsonPropertyName("experimentPath")]
        public string? ExperimentPath { get; set; }
    }

    /// <summary>Corps de la demande d'ingestion live (flux WebSocket du run).</summary>
    private sealed class LiveIngestRequest
    {
        [JsonPropertyName("wsUrl")]
        public string WsUrl { get; set; } = string.Empty;
    }

    private sealed class AnalysisPayload
    {
        [JsonPropertyName("files")]
        public AnalysisFile[]? Files { get; set; }
    }

    private sealed class AnalysisFile
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class ReportPayload
    {
        [JsonPropertyName("report")]
        public string? Report { get; set; }
    }

    private sealed class IngestPayload
    {
        [JsonPropertyName("ingested")]
        public IngestedRun? Ingested { get; set; }
    }

    private sealed class IngestedRun
    {
        [JsonPropertyName("runId")]
        public string? RunId { get; set; }

        [JsonPropertyName("ticks")]
        public int Ticks { get; set; }
    }
}
