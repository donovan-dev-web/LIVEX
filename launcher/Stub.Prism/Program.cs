using System.Net.WebSockets;
using System.Text;

namespace Stub.Prism;

/// <summary>
/// Stub.Prism (INTEGRATION_CONTRACT.md §13) : « processus vivant et client WebSocket factice ».
/// Expose les endpoints standard du §6 sur son port de contrôle et, quand --ws-url est fourni,
/// tente en boucle une connexion WebSocket vers le moteur : l'échec n'est jamais fatal,
/// l'indisponibilité du flux est un état, pas une panne (INTEGRATION_CONTRACT.md §8).
/// Fonctionne sans Launcher.
/// </summary>
internal static class Program
{
    private static volatile bool _shutdownRequested;
    private static volatile bool _wsConnected;

    private static int Main(string[] args)
    {
        var port = 5299;
        var workDirectory = string.Empty;
        string? wsUrl = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--control-port" when i + 1 < args.Length:
                    port = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--work-dir" when i + 1 < args.Length:
                    workDirectory = args[++i];
                    break;
                case "--ws-url" when i + 1 < args.Length:
                    wsUrl = args[++i];
                    break;
            }
        }

        if (!string.IsNullOrEmpty(workDirectory))
        {
            Directory.CreateDirectory(workDirectory);

            // Preuve de télémétrie : arguments réellement reçus, pour vérifier que le
            // Launcher ne passe d'arguments SYNE qu'au moteur (INTEGRATION_CONTRACT.md §3.3).
            File.WriteAllLines(Path.Combine(workDirectory, "args.txt"), args);
        }

        var server = new MiniHttpServer(port);
        server.Get("/health/live", _ => "{\"status\":\"Healthy\"}");
        server.Get("/health/ready", _ => _shutdownRequested ? null : "{\"status\":\"Healthy\"}");
        server.Get("/health/details", _ =>
            $"{{\"status\":\"Healthy\",\"checks\":[{{\"name\":\"render_scene\",\"status\":\"Healthy\"}},{{\"name\":\"snapshot_stream\",\"status\":\"{(_wsConnected ? "Healthy" : "Degraded")}\"}}]}}");
        server.Get("/info", _ => "{\"id\":\"prism\",\"version\":\"0.1.0-stub\",\"protocolVersion\":1}");
        server.Get("/metrics", _ =>
            $"livex_prism_fps 0\nlivex_prism_ws_connected{{url=\"{wsUrl ?? ""}\"}} {(_wsConnected ? 1 : 0)}\n");
        server.Post("/control/shutdown", (_, _) =>
        {
            _shutdownRequested = true;
            return "{\"status\":\"shutting_down\"}";
        });
        try
        {
            server.Start();
        }
        catch (System.Net.Sockets.SocketException exception)
        {
            // Port indisponible, échec d'écoute → code 3 (INTEGRATION_CONTRACT.md §4), jamais un crash.
            Console.Error.WriteLine($"[Stub.Prism] port {port} indisponible : {exception.Message}");
            return 3;
        }

        if (!string.IsNullOrWhiteSpace(wsUrl))
        {
            _ = Task.Run(() => WebSocketLoopAsync(wsUrl));
        }

        Console.WriteLine($"[Stub.Prism] écoute sur :{port}, flux : {wsUrl ?? "(aucun — fournir --ws-url)"}");

        // Arrêt propre (INTEGRATION_CONTRACT.md §5.1) : exit sur /control/shutdown.
        while (!_shutdownRequested)
        {
            Thread.Sleep(100);
        }

        return 0;
    }

    /// <summary>Client WebSocket factice : connexion en boucle, jamais fatal, état exposé par /metrics.</summary>
    private static async Task WebSocketLoopAsync(string wsUrl)
    {
        var retry = TimeSpan.FromSeconds(2);
        while (!_shutdownRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await socket.ConnectAsync(new Uri(wsUrl), cancellation.Token).ConfigureAwait(false);
                _wsConnected = true;

                // Flux consommé et jeté : le stub prouve qu'il reçoit, sans le rejouer.
                var buffer = new byte[4096];
                while (socket.State is WebSocketState.Open && !_shutdownRequested)
                {
                    using var read = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var result = await socket.ReceiveAsync(buffer, read.Token).ConfigureAwait(false);
                    if (result.MessageType is WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
            }
            catch (Exception exception) when (exception is WebSocketException
                or OperationCanceledException
                or IOException
                or UriFormatException)
            {
            }
            finally
            {
                _wsConnected = false;
            }

            await Task.Delay(retry).ConfigureAwait(false);
        }
    }
}
