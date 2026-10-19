// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Serveur de contrôle HTTP :5181 (SYNE-113).

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Simulation.Core.Configuration;
using Simulation.Console.Observability;

namespace Simulation.Console.Control;

/// <summary>
/// Serveur de contrôle HTTP :5181 (SYNE-113).
/// Expose un sous-ensemble REST local (binding <c>127.0.0.1</c>) de pilotage du
/// moteur : <c>POST /api/control/start|pause|resume|stop|reset</c> et
/// <c>GET /api/control/status</c>. Implémentation BCL uniquement (HttpListener),
/// comme l'observabilité — aucune dépendance externe.
/// </summary>
public sealed partial class ControlServer : IAsyncDisposable
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
        string version = "0.17.0",
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
}
