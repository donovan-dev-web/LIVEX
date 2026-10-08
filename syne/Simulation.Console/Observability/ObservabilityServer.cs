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
///
/// <para>
/// Invariant central : <see cref="BroadcastAsync"/> ne doit jamais bloquer ni
/// faire échouer la boucle de simulation. Elle est donc bornée dans le temps
/// par client, et ne propage jamais d'exception. Un <see cref="WebSocket"/>
/// n'acceptant qu'un seul <c>SendAsync</c> à la fois, chaque client porte son
/// propre sémaphore d'écriture.
/// </para>
/// </summary>
public sealed class ObservabilityServer : IObservabilitySink, IObservabilityDemand, IAsyncDisposable
{
    public const int DefaultPort = 5180;

    /// <summary>Envoi borné : au-delà, le client est considéré mort et retiré.</summary>
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Fermeture bornée pour ne pas retenir <see cref="DisposeAsync"/>.</summary>
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Taille du tampon de réception des trames client (trafic entrant réduit).</summary>
    private static readonly int ReceiveBufferSize = 1024;

    private readonly HttpListener _listener;
    private readonly ConcurrentDictionary<WebSocket, ClientSession> _clients = new();
    private readonly ConcurrentDictionary<ClientSession, byte> _sessions = new();
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private int _started;
    private int _disposed;

    /// <summary>
    /// Dernière trame <c>world_initialized</c> diffusée. La description de monde
    /// n'est émise qu'une fois par préparation : un consommateur qui se connecte
    /// après (analyse temps réel depuis le Launcher) ne pourrait jamais la
    /// reconstituer — le serveur la rejoue donc à chaque nouvelle connexion.
    /// </summary>
    private volatile string? _worldFrame;

    public ObservabilityServer(int port = DefaultPort)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        Port = port;
    }

    public int Port { get; }

    public int ClientCount => _clients.Count;

    /// <summary>
    /// Permet à l'émetteur de ne pas construire de snapshot quand personne
    /// n'écoute (capture O(entités) par tick).
    /// </summary>
    public bool HasSubscribers => _clients.Count > 0;

    public bool IsListening => _listener.IsListening;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        _listener.Start();
        _cts = new CancellationTokenSource();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Diffuse une trame texte à tous les clients connectés (meilleur effort V0.1).
    /// Ne lève jamais : un client mort ou lent ne doit pas interrompre le run.
    /// </summary>
    public async Task BroadcastAsync(string text)
    {
        if (text.Contains("\"world_initialized\"", StringComparison.Ordinal))
        {
            _worldFrame = text;
        }

        if (Volatile.Read(ref _disposed) != 0 || _clients.IsEmpty)
        {
            return;
        }

        foreach ((WebSocket _client, ClientSession session) in _clients.ToArray())
        {
            await SendTextAsync(session, text).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Envoie une trame à un client sous son sémaphore d'écriture. Renvoie faux si
    /// le client est mort ou trop lent (il est alors retiré). Ne lève jamais.
    /// </summary>
    private async Task<bool> SendTextAsync(ClientSession session, string text)
    {
        WebSocket client = session.Socket;
        if (client.State != WebSocketState.Open)
        {
            RemoveClient(client, session);
            return false;
        }

        // Un seul SendAsync à la fois par socket : deux diffusions concurrentes
        // sans ce verrou lèvent InvalidOperationException (« There is already
        // one outstanding 'SendAsync' call »).
        if (!await session.SendGate.WaitAsync(SendTimeout).ConfigureAwait(false))
        {
            RemoveClient(client, session);
            return false;
        }

        try
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);
            await client
                .SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None)
                .WaitAsync(SendTimeout)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException or InvalidOperationException or TimeoutException)
        {
            // Client mort, trop lent, ou déjà coupé : on le retire, on ne
            // remonte jamais l'erreur au simulateur. TimeoutException est
            // celle de WaitAsync(SendTimeout) (V3, 08/10/2026) : sans elle dans
            // le filtre, un client figé n'était jamais retiré, chaque trame
            // rebloquait 2 s et la cadence moteur se couplait au consommateur —
            // le run devait continuer, pas le client.
            RemoveClient(client, session);
            return false;
        }
        finally
        {
            session.SendGate.Release();
        }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            WebSocket socket;
            try
            {
                HttpListenerContext context = await _listener.GetContextAsync().ConfigureAwait(false);
                socket = (await context.AcceptWebSocketAsync(null).ConfigureAwait(false)).WebSocket;
            }
            catch (Exception) when (token.IsCancellationRequested || !_listener.IsListening)
            {
                break;
            }
            catch (Exception)
            {
                // Requête non-WebSocket (ou upgrade refusé) : on l'ignore et on
                // continue d'accepter au lieu de tuer la boucle.
                continue;
            }

            var session = new ClientSession(socket);

            // Rejeu de la description de monde AVANT l'inscription du client :
            // ainsi aucune trame de snapshot ne peut lui parvenir avant, et un
            // consommateur tardif reçoit le monde même après la préparation.
            string? replay = _worldFrame;
            if (replay is not null && !await SendTextAsync(session, replay).ConfigureAwait(false))
            {
                await SafeCloseAsync(socket).ConfigureAwait(false);
                continue;
            }

            if (!_clients.TryAdd(socket, session))
            {
                await SafeCloseAsync(socket).ConfigureAwait(false);
                continue;
            }

            _sessions.TryAdd(session, 0);

            // Fenêtre minime : la diffusion a pu produire la trame pendant l'envoi
            // initial — un second envoi est inoffensif (le monde est idempotent).
            string? late = _worldFrame;
            if (late is not null && !ReferenceEquals(late, replay))
            {
                _ = SendTextAsync(session, late).ConfigureAwait(false);
            }

            session.Drain = Task.Run(() => DrainAsync(session, token), CancellationToken.None);
        }
    }

    /// <summary>
    /// Consomme les trames entrantes. Le retrait du client se fait dans le
    /// <c>finally</c> : auparavant un client déconnecté restait au dictionnaire
    /// jusqu'à la diffusion suivante, et son socket n'était jamais fermé.
    /// </summary>
    private async Task DrainAsync(ClientSession session, CancellationToken token)
    {
        WebSocket socket = session.Socket;
        byte[] buffer = new byte[ReceiveBufferSize];
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, session.Stop.Token);
        try
        {
            while (socket.State == WebSocketState.Open && !linked.IsCancellationRequested)
            {
                await socket.ReceiveAsync(buffer, linked.Token).ConfigureAwait(false);
            }
        }
        catch (Exception) when (linked.IsCancellationRequested || socket.State != WebSocketState.Open)
        {
            // fermeture attendue ou client parti
        }
        catch (WebSocketException)
        {
            // reset brutal : même conséquence
        }
        finally
        {
            RemoveClient(socket, session);
            // La réception est terminée : c'est le seul endroit sûr pour fermer.
            // Sur Windows, un CloseAsync lancé pendant qu'une ReceiveAsync est en
            // vol se mort avec elle (WebSocketBase.TakeLocks / Monitor.Enter) et
            // fige l'appelant — d'où cet ordre strict, jamais l'inverse.
            await SafeCloseAsync(socket).ConfigureAwait(false);
        }
    }

    private void RemoveClient(WebSocket socket, ClientSession session)
    {
        _clients.TryRemove(socket, out _);
        if (_sessions.TryRemove(session, out _))
        {
            // On termine d'abord la réception en vol ; la fermeture viendra du
            // `finally` de DrainAsync (réception finie) ou de DisposeAsync.
            // Fermer ici, tant que la réception tourne, reproduirait le verrou
            // mortel Windows décrit dans DrainAsync.
            try
            {
                session.Stop.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Session déjà démontée
            }
        }
    }

    private static async Task SafeCloseAsync(WebSocket socket)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            using CancellationTokenSource cts = new(CloseTimeout);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "arrêt", cts.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            try
            {
                socket.Abort();
            }
            catch (Exception)
            {
                // socket déjà détruit
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        CancellationTokenSource? cts = _cts;
        cts?.Cancel();

        // Ordre strict : on laisse chaque réception en vol se terminer AVANT de
        // fermer son socket (voir DrainAsync). Fermer d'abord figerait l'appelant
        // sur Windows, où CloseAsync et ReceiveAsync concurrents se disputent le
        // même verrou interne au WebSocket.
        foreach (ClientSession session in _sessions.Keys)
        {
            try
            {
                session.Stop.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Session déjà démontée
            }

            Task? drain = session.Drain;
            if (drain is not null)
            {
                try
                {
                    await drain.WaitAsync(CloseTimeout).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Réception toujours en vol au-delà du délai : on ne ferme PAS
                    // (fermer avec une réception en vol est justement ce qui fige
                    // Windows). Le socket est abandonné à la fin du processus.
                    continue;
                }
            }

            await SafeCloseAsync(session.Socket).ConfigureAwait(false);
        }

        _sessions.Clear();
        _clients.Clear();

        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (Exception)
        {
        }

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop.WaitAsync(CloseTimeout).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // boucle d'accept déjà terminée ou arrêtée
            }
        }

        cts?.Dispose();
        _cts = null;
        _acceptLoop = null;
    }

    /// <summary>Client connecté : sémaphore d'écriture sérialisant les diffusions.</summary>
    private sealed class ClientSession(WebSocket socket)
    {
        public WebSocket Socket { get; } = socket;

        public SemaphoreSlim SendGate { get; } = new(initialCount: 1);

        /// <summary>Arrêt propre de la réception de ce client (retrait ou démontage).</summary>
        public CancellationTokenSource Stop { get; } = new();

        /// <summary>Boucle de réception du client : à terminer avant toute fermeture.</summary>
        public Task? Drain { get; set; }
    }
}
