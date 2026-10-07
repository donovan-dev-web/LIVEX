using System.Text;
using System.Text.Json;

namespace Stub.Echos;

/// <summary>
/// Stub.Echos (INTEGRATION_CONTRACT.md §13) : analyste simulé. Expose /health et /info,
/// et répond aux demandes d'analyse du §10.1 — AnalyzeRun, AnalyzeExperiment,
/// GenerateReport — avec un contenu déterministe dérivé de la demande et du dossier
/// d'entrée, jamais de l'horloge. Fonctionne sans Launcher.
/// </summary>
internal static class Program
{
    private static bool _shutdownRequested;
    private static string _workDirectory = string.Empty;

    private static int Main(string[] args)
    {
        var port = 5298;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--control-port" when i + 1 < args.Length:
                    port = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--work-dir" when i + 1 < args.Length:
                    _workDirectory = args[++i];
                    break;
            }
        }

        if (!string.IsNullOrEmpty(_workDirectory))
        {
            Directory.CreateDirectory(_workDirectory);
            Directory.CreateDirectory(Path.Combine(_workDirectory, "analysis"));

            // Preuve de télémétrie : arguments réellement reçus (INTEGRATION_CONTRACT.md §3.1).
            File.WriteAllLines(Path.Combine(_workDirectory, "args.txt"), args);
        }

        var server = new MiniHttpServer(port);
        server.Get("/health/live", _ => "{\"status\":\"Healthy\"}");
        server.Get("/health/ready", _ => _shutdownRequested ? null : "{\"status\":\"Healthy\"}");
        server.Get("/health/details", _ => "{\"status\":\"Healthy\",\"checks\":[{\"name\":\"analysis_api\",\"status\":\"Healthy\"}]}");
        server.Get("/info", _ => "{\"id\":\"echos\",\"version\":\"0.1.0-stub\",\"protocolVersion\":1}");
        server.Get("/metrics", _ => "livex_echos_analysis_jobs{state=\"idle\"} 0\n");
        server.Post("/control/shutdown", (_, _) =>
        {
            _shutdownRequested = true;
            return "{\"status\":\"shutting_down\"}";
        });

        // Demandes d'analyse (INTEGRATION_CONTRACT.md §10.1) : corps JSON, réponse JSON.
        server.Post("/analysis/run", (_, body) => Analyze("run", body));
        server.Post("/analysis/experiment", (_, body) => Analyze("experiment", body));
        server.Post("/analysis/report", (_, body) => Report(body));

        // Ingestion d'un run archivé (J2B) : le stub vérifie la présence du flux
        // exporté par SYNE et refuse (404) un dossier de run sans artefact —
        // c'est la seule chose qu'un stub puisse honnêtement trancher.
        // Il ne sait pas, contrairement au vrai ECHOS, renvoyer 409 sur une
        // seconde ingestion d'un même run : le MiniHttpServer ne connaît que
        // 200 et 404.
        server.Post("/ingest/run", (_, body) => Ingest(body));
        try
        {
            server.Start();
        }
        catch (System.Net.Sockets.SocketException exception)
        {
            // Port indisponible, échec d'écoute → code 3 (INTEGRATION_CONTRACT.md §4), jamais un crash.
            Console.Error.WriteLine($"[Stub.Echos] port {port} indisponible : {exception.Message}");
            return 3;
        }

        Console.WriteLine($"[Stub.Echos] écoute sur :{port}, travail dans {_workDirectory}");

        // Arrêt propre (INTEGRATION_CONTRACT.md §5.1) : la commande /control/shutdown
        // termine le processus, qui n'a pas à rester resident après l'arrêt demandé.
        while (!_shutdownRequested)
        {
            Thread.Sleep(100);
        }

        return 0;
    }

    /// <summary>Analyse individuelle ou agrégée : un fichier .md déterministe, encodé en base64.</summary>
    private static string? Analyze(string kind, string? body)
    {
        var experimentId = Field(body, "experimentId");
        var folder = kind == "run" ? Field(body, "runPath") : Field(body, "experimentPath");
        if (experimentId is null || folder is null)
        {
            return null;
        }

        var title = kind == "run" ? "# Analyse de run (stub)" : "# Analyse agrégée (stub)";
        var builder = new StringBuilder();
        builder.Append(title).Append("\n\n");
        builder.Append("- expérience : ").Append(experimentId).Append('\n');
        if (kind == "run")
        {
            builder.Append("- run : ").Append(Field(body, "runId") ?? "?").Append('\n');
        }

        builder.Append("- dossier : ").Append(folder).Append('\n');
        builder.Append("- fichiers :\n").Append(DescribeFolder(folder)).Append('\n');

        var fileName = kind == "run" ? $"{Field(body, "runId") ?? "run"}.md" : "experiment.md";
        var content = Convert.ToBase64String(Encoding.UTF8.GetBytes(builder.ToString()));
        LogRequest($"{kind} {experimentId} {folder}");
        return $"{{\"files\":[{{\"name\":\"{fileName}\",\"content\":\"{content}\"}}]}}";
    }

    /// <summary>
    /// Enregistre le flux archivé d'un run. Renvoie les compteurs que le vrai
    /// ECHOS renvoie, déduits des artefacts du run : l'identité et les ticks
    /// viennent du **flux**, pas de la demande.
    ///
    /// Refléter ce choix est ce qui rend le stub un double honnête du contrat
    /// (§10.2) : ECHOS fait autorité sur l'identité, donc un stub qui recopie le
    /// ``runId`` demandé ne détecterait jamais un dossier de run mal apparié — il
    /// renverrait justement l'identité attendue, masquant le défaut.
    /// </summary>
    private static string? Ingest(string? body)
    {
        var requested = Field(body, "runId");
        var runPath = Field(body, "runPath");
        if (requested is null || runPath is null)
        {
            return null;
        }

        var streamPath = Path.Combine(runPath, "data", "stream.jsonl");
        if (!File.Exists(streamPath))
        {
            return null;
        }

        var declared = RunIdFromStream(streamPath) ?? requested;
        var ticks = TicksFromResult(Path.Combine(runPath, "data", "result.json"));
        LogRequest($"ingest {declared} {runPath} (demandé {requested})");
        return $"{{\"ingested\":{{\"runId\":{JsonSerializer.Serialize(declared)},\"ticks\":{ticks}}}}}";
    }

    /// <summary>
    /// Identité déclarée par le premier snapshot du flux, ou ``null`` si le flux
    /// n'en porte pas. Le vrai ECHOS lit la même source et refuse le dossier si
    /// elle diverge de l'identité demandée ; le stub s'arrête au premier snapshot
    /// et renvoie l'identité qu'il a lue.
    /// </summary>
    private static string? RunIdFromStream(string streamPath)
    {
        try
        {
            using var reader = new StreamReader(streamPath);
            for (var line = reader.ReadLine(); line is not null; line = reader.ReadLine())
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(line);
                if (document.RootElement.GetProperty("type").GetString() != "snapshot")
                {
                    continue;
                }
                return document.RootElement.TryGetProperty("runId", out var runId)
                    ? runId.GetString()
                    : null;
            }
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }

        return null;
    }

    /// <summary>Ticks annoncés par le résumé de run ; 0 si le résumé est absent.</summary>
    private static int TicksFromResult(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return 0;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("ticks", out var value)
                && value.TryGetInt32(out var ticks) ? ticks : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>Rapport d'émergence : Markdown déterministe pour un dossier d'entrée donné.</summary>
    private static string? Report(string? body)
    {
        var experimentId = Field(body, "experimentId");
        var experimentPath = Field(body, "experimentPath");
        if (experimentId is null || experimentPath is null)
        {
            return null;
        }

        var runs = Directory.Exists(experimentPath)
            ? string.Join(", ", Directory.EnumerateDirectories(experimentPath)
                .Select(Path.GetFileName)
                .Where(name => name is not null)
                .OrderBy(name => name, StringComparer.Ordinal))
            : string.Empty;

        var report = new StringBuilder()
            .Append("# Rapport d'émergence (stub)\n\n")
            .Append("- expérience : ").Append(experimentId).Append('\n')
            .Append("- dossier : ").Append(experimentPath).Append('\n')
            .Append("- runs : ").Append(runs.Length > 0 ? runs : "(dossier absent)").Append('\n')
            .ToString();

        LogRequest($"report {experimentId} {experimentPath}");
        return $"{{\"report\":{JsonSerializer.Serialize(report)}}}";
    }

    /// <summary>Listing trié et déterministe du dossier d'entrée.</summary>
    private static string DescribeFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return "  (dossier absent)";
        }

        var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Select(file => $"  - {Path.GetRelativePath(path, file).Replace('\\', '/')} ({new FileInfo(file).Length} o)")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();
        return files.Count > 0 ? string.Join("\n", files) : "  (dossier vide)";
    }

    private static string? Field(string? body, string name)
    {
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Marqueur de preuve : chaque demande d'analyse reçue est consignée dans le dossier de travail.</summary>
    private static void LogRequest(string line)
    {
        if (string.IsNullOrEmpty(_workDirectory))
        {
            return;
        }

        try
        {
            File.AppendAllText(Path.Combine(_workDirectory, "analysis", "requests.log"), line + "\n", Encoding.UTF8);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
