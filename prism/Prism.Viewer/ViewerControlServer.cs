// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Surface de contrôle HTTP du viewer : santé, arrêt et pilotage du run.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Prism.Viewer;

/// <summary>
/// Surface de contrôle du viewer : <c>/health/live|ready|details</c>, <c>/info</c>,
/// <c>POST /control/shutdown|quit</c> et le pilotage du run (<c>play|pause|stop</c>),
/// relayé à l'hôte PRISM. Le viewer ne touche pas au monde lui-même : il déclenche
/// les commandes de contrôle de l'hôte, qui reste l'autorité.
/// </summary>
internal sealed class ViewerControlServer : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpListener _listener = new();
    private readonly string? _sessionToken;
    private readonly string _instanceId;
    private readonly Uri? _hostControl;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly TaskCompletionSource _shutdownRequested =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _ready;
    private volatile bool _running;

    public ViewerControlServer(int port, string? sessionToken, string instanceId, Uri? hostControl)
    {
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        Port = port;
        _sessionToken = sessionToken;
        _instanceId = instanceId;
        _hostControl = hostControl;
    }

    public int Port { get; }

    /// <summary>Complété quand le Launcher demande l'arrêt ou la fermeture du viewer.</summary>
    public Task ShutdownRequested => _shutdownRequested.Task;

    public void MarkReady() => _ready = true;

    public void Start()
    {
        _listener.Start();
        _running = true;
        _ = Task.Run(AcceptLoopAsync);
    }

    public void Dispose()
    {
        _running = false;
        try
        {
            _listener.Stop();
        }
        catch (ObjectDisposedException)
        {
        }

        _listener.Close();
        _http.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (_running)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var path = request.Url?.AbsolutePath ?? string.Empty;
            var method = request.HttpMethod;

            if (method == "OPTIONS")
            {
                context.Response.StatusCode = 204;
                WriteCors(context.Response);
                return;
            }

            if (method == "GET" && path is "/health/live" or "/health/ready" or "/health/details")
            {
                if (path == "/health/ready" && !_ready)
                {
                    await WriteJsonAsync(context.Response, 503, new { status = "NotReady", readiness = false });
                    return;
                }

                await WriteJsonAsync(context.Response, 200, new { status = "Healthy", readiness = _ready });
                return;
            }

            if (method == "GET" && path == "/info")
            {
                await WriteJsonAsync(context.Response, 200, new
                {
                    id = "prism-viewer",
                    version = "2.0.0-preview.1",
                    protocolVersion = 1,
                    instanceId = _instanceId,
                });
                return;
            }

            if (method == "POST" && path is "/control/shutdown" or "/control/quit")
            {
                if (!IsAuthorized(request))
                {
                    await WriteJsonAsync(context.Response, 401, new
                    {
                        error = "unauthorized",
                        detail = "Un jeton de session valide est requis.",
                    });
                    return;
                }

                await WriteJsonAsync(context.Response, 202, new { ok = true, action = "shutdown" });
                _shutdownRequested.TrySetResult();
                return;
            }

            if (method == "POST" && path is "/control/play" or "/control/pause" or "/control/stop")
            {
                if (!IsAuthorized(request))
                {
                    await WriteJsonAsync(context.Response, 401, new
                    {
                        error = "unauthorized",
                        detail = "Un jeton de session valide est requis.",
                    });
                    return;
                }

                var (status, body) = await ForwardAsync(path);
                await WriteJsonAsync(context.Response, status, JsonSerializer.Deserialize<JsonElement>(body));
                return;
            }

            await WriteJsonAsync(context.Response, 404, new { error = "not_found" });
        }
        catch (Exception)
        {
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch (Exception)
            {
                // best effort : aucune réponse possible.
            }
        }
    }

    /// <summary>
    /// Relaie au host : play démarre ou reprend selon son état, pause et stop sont
    /// directs. Sans hôte configuré, la commande est refusée explicitement.
    /// </summary>
    private async Task<(int Status, string Body)> ForwardAsync(string path)
    {
        if (_hostControl is null)
        {
            return (409, "{\"error\":\"host_unavailable\",\"detail\":\"Aucun hôte PRISM à piloter.\"}");
        }

        try
        {
            if (path == "/control/play")
            {
                var state = await HostStateAsync();
                if (string.Equals(state, "running", StringComparison.OrdinalIgnoreCase))
                {
                    return (200, "{\"ok\":true,\"state\":\"running\"}");
                }

                return string.Equals(state, "paused", StringComparison.OrdinalIgnoreCase)
                    ? await PostHostAsync("/control/resume", "{}")
                    : await PostHostAsync("/control/start", "{\"maxTicks\":null}");
            }

            return await PostHostAsync(path, "{}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return (502, JsonSerializer.Serialize(new { error = "host_unreachable", detail = exception.Message }));
        }
    }

    private async Task<string?> HostStateAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_hostControl!, "/control/status"));
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("state", out var state) ? state.GetString() : null;
    }

    private async Task<(int Status, string Body)> PostHostAsync(string path, string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_hostControl!, path))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrEmpty(_sessionToken))
        {
            request.Headers.TryAddWithoutValidation("X-LIVEX-Session-Token", _sessionToken);
        }

        using var response = await _http.SendAsync(request);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        if (string.IsNullOrEmpty(_sessionToken))
        {
            return true;
        }

        var header = request.Headers["Authorization"];
        if (header is null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var provided = Encoding.UTF8.GetBytes(header["Bearer ".Length..].Trim());
        var expected = Encoding.UTF8.GetBytes(_sessionToken);
        return provided.Length == expected.Length &&
            CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, int status, object payload)
    {
        response.StatusCode = status;
        response.ContentType = "application/json; charset=utf-8";
        WriteCors(response);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private static void WriteCors(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Headers"] = "Authorization, Content-Type";
        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
    }
}
