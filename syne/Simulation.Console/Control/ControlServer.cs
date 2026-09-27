using System.Net;
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
    private bool _running;

    public ControlServer(
        int port = DefaultPort,
        SimulationController? controller = null,
        ObservabilityServer? observability = null)
    {
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        Port = port;
        _observability = observability;
        _controller = controller ?? new SimulationController(observability);
    }

    public int Port { get; }

    public SimulationController Controller => _controller;

    public bool IsListening => _listener.IsListening;

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
        }
    }

    private async Task<(int Status, string Body)> RouteAsync(HttpListenerRequest request)
    {
        string path = request.Url?.AbsolutePath ?? string.Empty;
        string method = request.HttpMethod;

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
                _controller.Stop();
                return (200, ToJson(OkJson("stopped")));
            case "reset":
                return await ResetAsync(body);
            default:
                return (404, ToJson(ErrorJson("not_found", $"Action de contrôle inconnue : {action}.")));
        }
    }

    private async Task<(int Status, string Body)> PrepareAsync(JsonElement body)
    {
            ulong? seed = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("seed", out var s) ? s.GetUInt64() : null;
            SimulationOptions? config = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out var c) ? ParseConfig(c) : null;
            int? ticksPerSecond = null;
            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("ticksPerSecond", out var tps))
            {
                if (tps.ValueKind != JsonValueKind.Number || !tps.TryGetInt32(out int parsedTicksPerSecond) || parsedTicksPerSecond <= 0)
                    return (400, ToJson(ErrorJson("invalid_ticks_per_second", "ticksPerSecond doit être un entier strictement positif.")));
                ticksPerSecond = parsedTicksPerSecond;
            }
            var world = await _controller.PrepareAsync(seed, config ?? SimulationProfiles.Reference(), ticksPerSecond);
            return (200, ToJson(new { ok = true, action = "prepared", ticksPerSecond = world.TicksPerSecond, world }));
    }

    private async Task<(int Status, string Body)> StartAsync(JsonElement body)
    {
        ulong? seed = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("seed", out JsonElement seedElement)
            ? seedElement.GetUInt64()
            : null;
        SimulationOptions? config = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out JsonElement configElement)
            ? ParseConfig(configElement)
            : null;
        int? maxTicks = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("maxTicks", out JsonElement maxTicksElement)
            ? maxTicksElement.GetInt32()
            : null;

        // Une commande start sans options ne remplace pas un run actif.
        if (config is null && seed is null && maxTicks is null && _controller.HasRun)
        {
            return (409, ToJson(ErrorJson("run_active", "Un run est déjà en cours — utilisez /stop ou /reset avant de redémarrer.")));
        }

        // Manual/UI runs use the reviewed reference profile unless the caller
        // supplies an explicit configuration overlay.
        try
        {
            SimulationOptions? effectiveConfig = _controller.WorldPrepared && config is null
                ? null
                : config ?? SimulationProfiles.Reference();
            string runId = await _controller.StartAsync(seed, effectiveConfig, maxTicks);
            return (200, ToJson(OkJson("started", runId, _controller.Status())));
        }
        catch (PreparedWorldMismatchException exception)
        {
            return (409, ToJson(ErrorJson(exception.Code, exception.Message)));
        }
        catch (InvalidOperationException exception)
        {
            return (409, ToJson(ErrorJson("world_not_ready", exception.Message)));
        }
    }

    private async Task<(int Status, string Body)> ResetAsync(JsonElement body)
    {
        ulong? seed = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("seed", out JsonElement seedElement)
            ? seedElement.GetUInt64()
            : null;
        int? maxTicks = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("maxTicks", out JsonElement maxTicksElement)
            ? maxTicksElement.GetInt32()
            : null;

        string runId = await _controller.ResetAsync(seed, maxTicks);
        return (200, ToJson(OkJson("reset", runId, _controller.Status())));
    }

    private static SimulationOptions? ParseConfig(JsonElement configElement)
    {
        if (configElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(configElement.GetRawText(), typeof(SimulationOptions), JsonOptions) as SimulationOptions;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpListenerRequest request)
    {
        if (request.ContentLength64 <= 0)
        {
            return new JsonElement();
        }

        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
        string text = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new JsonElement();
        }

        return JsonDocument.Parse(text).RootElement;
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