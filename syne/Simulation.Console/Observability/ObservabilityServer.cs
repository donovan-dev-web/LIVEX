using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace Simulation.Console.Observability;

/// <summary>
/// Cible de diffusion des trames d'observabilité (API_CONTRACTS.md §2).
/// Contrat unique pour le serveur WebSocket réel et les tests (in-memory) :
/// l'infusion des trames ne doit jamais muter l'état de la simulation
/// (anti-triche SYNE-081 — DETERMINISM.md §3).
/// </summary>
public interface IObservabilitySink
{
    /// <summary>Diffuse une trame texte JSON (sans effet sur le monde simulé).</summary>
    Task BroadcastAsync(string text);
}

/// <summary>
/// Serveur WebSocket minimal (BCL uniquement, ADR-002/ECHOS-010) exposant les
/// messages d'observabilité SYNE sur ws://127.0.0.1:[port]/. Il n'archive rien :
/// purement de diffusion. Chaque message = une trame texte JSON
/// (API_CONTRACTS.md §2), diffusion fiable-en-fonction-du-mieux (V0.1).
/// </summary>
public sealed class ObservabilityServer : IObservabilitySink, IAsyncDisposable
{
    public const int DefaultPort = 5180;

    private readonly HttpListener _listener;
    private readonly ConcurrentDictionary<WebSocket, byte> _clients = new();
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public ObservabilityServer(int port = DefaultPort)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        Port = port;
    }

    public int Port { get; }

    public int ClientCount => _clients.Count;

    public bool IsListening => _listener.IsListening;

    public void Start()
    {
        _listener.Start();
        _cts = new CancellationTokenSource();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    /// <summary>Diffuse une trame texte à tous les clients connectés (meilleur effort V0.1).</summary>
    public async Task BroadcastAsync(string text)
    {
        byte[] payload = Encoding.UTF8.GetBytes(text);
        foreach (WebSocket client in _clients.Keys)
        {
            if (client.State != WebSocketState.Open)
            {
                _clients.TryRemove(client, out _);
                continue;
            }

            try
            {
                await client.SendAsync(
                    new ArraySegment<byte>(payload),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    CancellationToken.None);
            }
            catch (WebSocketException)
            {
                _clients.TryRemove(client, out _);
            }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                HttpListenerContext context = await _listener.GetContextAsync();
                WebSocket socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
                _clients.TryAdd(socket, 0);
                _ = Task.Run(() => DrainAsync(socket, token));
            }
            catch (Exception) when (token.IsCancellationRequested || !_listener.IsListening)
            {
                break;
            }
        }
    }

    private static async Task DrainAsync(WebSocket socket, CancellationToken token)
    {
        var buffer = new byte[1024];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                await socket.ReceiveAsync(buffer, token);
            }
        }
        catch (Exception) when (token.IsCancellationRequested || socket.State != WebSocketState.Open)
        {
            // client enlevé de façon paresseuse
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        foreach (WebSocket client in _clients.Keys)
        {
            try
            {
                _ = client.CloseAsync(WebSocketCloseStatus.NormalClosure, "arrêt", CancellationToken.None);
            }
            catch (Exception)
            {
                // ignoré : fermeture best effort
            }
        }

        _listener.Stop();
        _listener.Close();
        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop;
            }
            catch (Exception)
            {
                // boucle d'accept déjà terminée
            }
        }

        _cts?.Dispose();
    }
}