using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Application;
using Launcher.Domain;

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
        var body = await PostJsonAsync("analysis/run",
            new AnalysisRequest { ExperimentId = experimentId, RunId = run.RunId, RunPath = runPath },
            cancellationToken).ConfigureAwait(false);
        return DecodeFiles(body);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyDictionary<string, byte[]> AggregateFiles, string? EmergenceReport)> AnalyzeExperimentAsync(string experimentId, CancellationToken cancellationToken)
    {
        var experimentPath = ExperimentPath(experimentId);
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

    private async Task<string> PostJsonAsync(string path, AnalysisRequest request, CancellationToken cancellationToken)
    {
        var baseUri = ResolveControlBase();
        var json = JsonSerializer.Serialize(request, JsonOptions);

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
                throw new InvalidOperationException($"ECHOS a refusé la demande {path} ({(int)response.StatusCode})");
            }

            return body;
        }
    }

    private static IReadOnlyDictionary<string, byte[]> DecodeFiles(string body)
    {
        var payload = Deserialize<AnalysisPayload>(body)
            ?? throw Malformed("liste de fichiers absente de la réponse");

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in payload.Files ?? Array.Empty<AnalysisFile>())
        {
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
}
