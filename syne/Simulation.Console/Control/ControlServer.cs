using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Simulation.Core.Configuration;
using Simulation.Console.Observability;

namespace Simulation.Console.Control;

/// <summary>
/// Serveur de contrôle HTTP :5181 (SYNE-113, API_CONTRACTS.md §3, ADR-003).
/// Expose un sous-ensemble REST local (binding <c>127.0.0.1</c>) de pilotage du
/// moteur : <c>POST /api/control/start|pause|resume|stop|reset</c> et
/// <c>GET /api/control/status</c>. Implémentation BCL uniquement (HttpListener),
/// comme l'observabilité — aucune dépendance externe (ADR-002).
/// </summary>
public sealed class ControlServer : IAsyncDisposable
{
    public const int DefaultPort = 5181;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpListener _listener = new();
    private readonly SimulationController _controller;
    private readonly ObservabilityServer? _observability;
    private readonly string? _sessionToken;
    private readonly string _instanceId;
    private readonly string _version;
    private readonly TaskCompletionSource _shutdownRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _running;
    private volatile bool _ready;

    public ControlServer(
        int port = DefaultPort,
        SimulationController? controller = null,
        ObservabilityServer? observability = null,
        string? sessionToken = null,
        string instanceId = "syne",
        string version = "0.15.0",
        bool initiallyReady = true)
    {
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        Port = port;
        _observability = observability;
        _controller = controller ?? new SimulationController(observability);
        _sessionToken = sessionToken;
        _instanceId = instanceId;
        _version = version;
        _ready = initiallyReady;
    }

    public int Port { get; }

    public SimulationController Controller => _controller;

    public bool IsListening => _listener.IsListening;

    public Task ShutdownRequested => _shutdownRequested.Task;

    public void MarkReady() => _ready = true;

    public void Start()
    {
        _listener.Start();
        _observability?.Start();
        _running = true;
        _ = Task.Run(AcceptLoopAsync);
    }

    public async ValueTask DisposeAsync()
    {
        _running = false;
        _listener.Stop();
        _listener.Close();
        await _controller.DisposeAsync();
        if (_observability is not null)
        {
            await _observability.DisposeAsync();
        }
    }

    private async Task AcceptLoopAsync()
    {
        while (_running && _listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (!_running || !_listener.IsListening)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            (int status, string body) = await RouteAsync(context.Request);
            byte[] payload = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = payload.Length;
            await context.Response.OutputStream.WriteAsync(payload);
        }
        catch (RequestException exception)
        {
            try
            {
                await WriteErrorAsync(context, exception.Status, exception.Code, exception.Detail);
            }
            catch (Exception)
            {
                // best effort : aucune réponse possible
            }
        }
        catch (Exception)
        {
            try
            {
                await WriteErrorAsync(context, 500, "internal_error", "Erreur interne du serveur de contrôle.");
            }
            catch (Exception)
            {
                // best effort : aucune réponse possible
            }
        }
        finally
        {
            context.Response.Close();
            if (context.Request.Url?.AbsolutePath == "/control/shutdown"
                && context.Request.HttpMethod == "POST"
                && context.Response.StatusCode == 200)
            {
                _shutdownRequested.TrySetResult();
            }
        }
    }

    private async Task<(int Status, string Body)> RouteAsync(HttpListenerRequest request)
    {
        string path = request.Url?.AbsolutePath ?? string.Empty;
        string method = request.HttpMethod;

        if (method == "GET" && path is "/health/live" or "/health/ready" or "/health/details")
        {
            if (path == "/health/ready" && !_ready)
            {
                return (503, ToJson(ErrorJson("not_ready", "Le service n'a pas terminé sa préparation.")));
            }

            if (path == "/health/details" && !_ready)
            {
                return (503, ToJson(new { status = "NotReady", readiness = false }));
            }

            return (200, ToJson(new { status = "Healthy" }));
        }

        if (method == "GET" && path == "/info")
        {
            return (200, ToJson(new { id = "syne", version = _version, protocolVersion = 1, instanceId = _instanceId }));
        }

        if (path == "/control/shutdown" && method == "POST")
        {
            if (!IsAuthorized(request))
            {
                return (401, ToJson(ErrorJson("unauthorized", "Un jeton de session valide est requis.")));
            }

            return (200, ToJson(OkJson("shutdown")));
        }

        if (path == "/api/control/status" && method == "GET")
        {
            return (200, ToJson(StatusJson(_controller.Status(), _controller.WorldDescription?.TicksPerSecond)));
        }

        if (path == "/api/world" && method == "GET")
        {
            return _controller.WorldDescription is { } world
                ? (200, ToJson(world))
                : (409, ToJson(ErrorJson("world_not_prepared", "Préparez le monde avant de le consulter.")));
        }

        if (!path.StartsWith("/api/control/", StringComparison.Ordinal) || method != "POST")
        {
            return (404, ToJson(ErrorJson("not_found", "Endpoint inconnu.")));
        }

        if (_sessionToken is not null && !IsAuthorized(request))
        {
            return (401, ToJson(ErrorJson("unauthorized", "Un jeton de session valide est requis.")));
        }

        string action = path["/api/control/".Length..];
        JsonElement body = await ReadBodyAsync(request);

        switch (action)
        {
            case "prepare":
                return await PrepareAsync(body);
            case "ready":
                string? version = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("worldVersion", out var v)
                    ? v.GetString() : null;
                return _controller.AcknowledgeReady(version)
                    ? (200, ToJson(OkJson("ready")))
                    : (409, ToJson(ErrorJson("world_not_ready", "Le monde n'est pas préparé ou sa version est incorrecte.")));
            case "start":
                return await StartAsync(body);
            case "pause":
                _controller.Pause();
                return (200, ToJson(OkJson("paused")));
            case "resume":
                _controller.Resume();
                return (200, ToJson(OkJson("resumed")));
            case "stop":
                // On attend la fin réelle de la boucle : répondre 200 avant
                // l'arrêt effectif laisserait au client l'impression que le run
                // est terminé alors qu'il peut encore avancer d'un tick.
                await _controller.StopAsync();
                return (200, ToJson(OkJson("stopped")));
            case "reset":
                return await ResetAsync(body);
            default:
                return (404, ToJson(ErrorJson("not_found", $"Action de contrôle inconnue : {action}.")));
        }

    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        if (string.IsNullOrEmpty(_sessionToken))
        {
            return false;
        }

        string? header = request.Headers["Authorization"];
        string[] authorization = header?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (authorization.Length != 2 || !authorization[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[] supplied = Encoding.UTF8.GetBytes(authorization[1]);
        byte[] expected = Encoding.UTF8.GetBytes(_sessionToken);
        return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    private async Task<(int Status, string Body)> PrepareAsync(JsonElement body)
    {
            ulong? seed = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("seed", out var s) ? s.GetUInt64() : null;
            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out var c) && c.ValueKind != JsonValueKind.Object)
                return (400, ToJson(ErrorJson("invalid_config", "config doit etre un objet JSON.")));
            string? config = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out var c2)
                ? c2.GetRawText()
                : null;
            int? ticksPerSecond = null;
            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("ticksPerSecond", out var tps))
            {
                if (tps.ValueKind != JsonValueKind.Number || !tps.TryGetInt32(out int parsedTicksPerSecond) || parsedTicksPerSecond <= 0)
                    return (400, ToJson(ErrorJson("invalid_ticks_per_second", "ticksPerSecond doit être un entier strictement positif.")));
                ticksPerSecond = parsedTicksPerSecond;
            }
            try
            {
                var world = await _controller.PrepareAsync(seed, config ?? SimulationProfiles.ReferenceJson(), ticksPerSecond);
                return (200, ToJson(new { ok = true, action = "prepared", ticksPerSecond = world.TicksPerSecond, world }));
            }
            catch (JsonException exception)
            {
                // JSON bien formé mais valeurs incompatibles avec le modèle
                // (mauvais types, nombres hors domaine) : c'est une requête
                // invalide, pas une erreur interne du serveur.
                return (400, ToJson(ErrorJson("invalid_config", DescribeJsonError(exception))));
            }
    }

    private async Task<(int Status, string Body)> StartAsync(JsonElement body)
    {
        ulong? seed = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("seed", out JsonElement seedElement)
            ? seedElement.GetUInt64()
            : null;
        if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out JsonElement configRaw) && configRaw.ValueKind != JsonValueKind.Object)
            return (400, ToJson(ErrorJson("invalid_config", "config doit être un objet JSON.")));
        // La surcouche est conservée en JSON brut : désérialiser la surcouche
        // en SimulationOptions la rendrait complète et la fusion
        // réinitialiserait le profil (cf. ConfigLoader.MergeJson).
        string? config = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out JsonElement configElement)
            ? configElement.GetRawText()
            : null;
        int? maxTicks = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("maxTicks", out JsonElement maxTicksElement)
            ? maxTicksElement.GetInt32()
            : null;

        // Une commande start sans options ne remplace pas un run actif.
        if (config is null && seed is null && maxTicks is null && _controller.HasRun)        {
            return (409, ToJson(ErrorJson("run_active", "Un run est déjà en cours — utilisez /stop ou /reset avant de redémarrer.")));
        }

        // Un run « finished » a atteint son nombre de ticks cible : le monde est
        // prêt mais le run n'avancera plus. Le 409 world_not_ready générique
        // orientait le client vers /prepare alors que le remède est /reset.
        if (_controller.State == SimulationControlState.Finished)
        {
            return (409, ToJson(ErrorJson("run_finished", "Run terminé — appelez /api/control/reset avant de redémarrer.")));
        }

        // Manual/UI runs use the reviewed reference profile unless the caller
        // supplies an explicit configuration overlay.
        try
        {
            string? effectiveConfig = _controller.WorldPrepared && config is null
                ? null
                : config ?? SimulationProfiles.ReferenceJson();
            string runId = await _controller.StartAsync(seed, effectiveConfig, maxTicks);
            return (200, ToJson(OkJson("started", runId, _controller.Status())));
        }
        catch (PreparedWorldMismatchException exception)
        {
            return (409, ToJson(ErrorJson(exception.Code, exception.Message)));
        }
        catch (JsonException exception)
        {
            return (400, ToJson(ErrorJson("invalid_config", DescribeJsonError(exception))));
        }
        catch (InvalidOperationException exception)
        {
            return (409, ToJson(ErrorJson("world_not_ready", exception.Message)));
        }
    }

    /// <summary>
    /// Traduit une erreur de déserialisation en message utile sans exposer la
    /// pile d'appels ni un numéro de ligne interne.
    /// </summary>
    private static string DescribeJsonError(JsonException exception)
    {
        string message = exception.Message;
        int marker = message.IndexOf(" Path: ", StringComparison.Ordinal);
        if (marker > 0)
        {
            message = message[..marker].Trim();
        }

        return string.IsNullOrWhiteSpace(message)
            ? "La configuration contient des valeurs ou des types invalides."
            : message;
    }    private async Task<(int Status, string Body)> ResetAsync(JsonElement body)
    {
        ulong? seed = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("seed", out JsonElement seedElement) ? seedElement.GetUInt64() : null;
        // Même règle que start/prepare : la surcouche reste en JSON brut et un
        // type non objet est un 400 explicite, jamais un repli silencieux.
        if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out JsonElement resetConfig) && resetConfig.ValueKind != JsonValueKind.Object)
            return (400, ToJson(ErrorJson("invalid_config", "config doit etre un objet JSON.")));
        string? config = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out JsonElement resetConfigRaw)
            ? resetConfigRaw.GetRawText()
            : null;
        int? maxTicks = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("maxTicks", out JsonElement maxTicksElement)
            ? maxTicksElement.GetInt32()
            : null;

        try
        {
            string runId = await _controller.ResetAsync(seed, maxTicks, config);
            return (200, ToJson(OkJson("reset", runId, _controller.Status())));
        }
        catch (JsonException exception)
        {
            return (400, ToJson(ErrorJson("invalid_config", DescribeJsonError(exception))));
        }
        catch (InvalidOperationException exception)
        {
            return (409, ToJson(ErrorJson("run_active", exception.Message)));
        }
    }

    /// <summary>
    /// Limite de taille du corps de requête. Sans elle, un
    /// <c>Content-Length</c> (ou un transfert par blocs) arbitrairement grand
    /// était lu entièrement en mémoire avant tout rejet.
    /// </summary>
    private const int MaxBodyBytes = 1 << 20;

    /// <summary>
    /// Plafond de vidage (16 Mio) avant de répondre 413 sur un
    /// <c>Content-Length</c> déclaré trop grand : fermer la socket alors que des
    /// octets du corps sont encore non lus émet un RST et le client reçoit
    /// « broken pipe » au lieu du 413 (course observée en parallélisme).
    /// </summary>
    private const int DrainCapBytes = 16 * MaxBodyBytes;

    private static async Task<JsonElement> ReadBodyAsync(HttpListenerRequest request)
    {
        if (request.ContentLength64 == 0)
        {
            return new JsonElement();
        }

        if (request.ContentLength64 > MaxBodyBytes)
        {
            // Vidage best effort, borné en taille, dans un tampon jetable de
            // 8 Kio (le corps n'est jamais accumulé en mémoire — la garantie
            // « not buffered » tient) : le client termine son envoi et peut
            // lire la réponse 413 au lieu d'être coupé en pleine écriture.
            await DiscardAsync(request.InputStream,
                Math.Min(request.ContentLength64, DrainCapBytes)).ConfigureAwait(false);
            throw new RequestException(413, "payload_too_large",
                $"Le corps de la requête dépasse la limite de {MaxBodyBytes} octets.");
        }

        // ContentLength64 est -1 pour un transfert par blocs : la lecture reste
        // donc bornée par ReadBoundedAsync, qui compte les octets reçus.
        byte[] raw = await ReadBoundedAsync(request.InputStream, MaxBodyBytes).ConfigureAwait(false);
        if (raw.Length == 0)
        {
            return new JsonElement();
        }

        string text;
        try
        {
            text = Encoding.UTF8.GetString(raw);
        }
        catch (ArgumentException)
        {
            throw new RequestException(400, "invalid_json", "Le corps de la requête n'est pas de l'UTF-8 valide.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new JsonElement();
        }

        try
        {
            // Le RootElement dépend du JsonDocument : sans Clone, il devient
            // invalide à la dépose et libère la mémoire native gérée
            // alors que les propriétés sont encore lues par le routeur.
            using JsonDocument document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new RequestException(400, "invalid_json", "Le corps de la requête n'est pas un JSON valide.");
        }
    }

    /// <summary>
    /// Vide jusqu'à <paramref name="maxBytes"/> octets sans les conserver.
    /// Best effort : toute erreur (client déconnecté) est ignorée, la réponse
    /// 413 part de toute façon.
    /// </summary>
    private static async Task DiscardAsync(Stream stream, long maxBytes)
    {
        byte[] scratch = new byte[8192];
        try
        {
            long discarded = 0;
            while (discarded < maxBytes)
            {
                int read = await stream.ReadAsync(scratch).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                discarded += read;
            }
        }
        catch (Exception)
        {
            // best effort : aucune requête à servir au-delà du rejet
        }
    }

    /// <summary>
    /// Lit au plus <paramref name="limit"/> octets et détrègue le dépassement,
    /// même lorsque la longueur déclarée est inconnue (transfert par blocs).
    /// </summary>
    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit)
    {
        using MemoryStream buffer = new(capacity: Math.Min(limit, 8192));
        byte[] chunk = new byte[8192];
        while (true)
        {
            int read = await stream.ReadAsync(chunk).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > limit)
            {
                throw new RequestException(413, "payload_too_large",
                    $"Le corps de la requête dépasse la limite de {limit} octets.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>Erreur de requète associée à un code HTTP (4xx) plutôt qu'à une 500.</summary>
    private sealed class RequestException(int status, string code, string detail) : Exception(detail)
    {
        public int Status { get; } = status;

        public string Code { get; } = code;

        public string Detail { get; } = detail;
    }

    private static async Task WriteErrorAsync(HttpListenerContext context, int status, string code, string detail)
    {
        byte[] payload = Encoding.UTF8.GetBytes(ToJson(ErrorJson(code, detail)));
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = payload.Length;
        await context.Response.OutputStream.WriteAsync(payload);
    }

    private static string ToJson<T>(T value) where T : notnull
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static Dictionary<string, object?> OkJson(string action) => new()
    {
        ["ok"] = true,
        ["action"] = action,
    };

    private static Dictionary<string, object?> OkJson(string action, string runId, SimulationStatusSnapshot status) => new()
    {
        ["ok"] = true,
        ["action"] = action,
        ["runId"] = runId,
        ["state"] = status.State.ToString().ToLowerInvariant(),
        ["tick"] = status.Tick,
        ["aliveCount"] = status.AliveCount,
        ["seed"] = status.Seed,
    };

    private static Dictionary<string, object?> ErrorJson(string code, string detail) => new()
    {
        ["ok"] = false,
        ["error"] = code,
        ["detail"] = detail,
    };

    private static Dictionary<string, object?> StatusJson(SimulationStatusSnapshot status, int? ticksPerSecond) => new()
    {
        ["state"] = status.State.ToString().ToLowerInvariant(),
        ["runId"] = status.RunId,
        ["tick"] = status.Tick,
        ["aliveCount"] = status.AliveCount,
        ["seed"] = status.Seed,
        ["worldPrepared"] = status.WorldPrepared,
        ["worldVersion"] = status.WorldVersion,
        ["worldReadyAcknowledged"] = status.WorldReadyAcknowledged,
        ["maxTicks"] = status.MaxTicks,
        ["ticksPerSecond"] = ticksPerSecond,
    };
}