using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Stub.Syne;

/// <summary>
/// Serveur HTTP/1.1 minimal sur TcpListener : évite les réservations d'URL de HttpListener
/// sous Windows et reste identique sous Linux. Une requête par connexion, réponse JSON.
/// </summary>
public sealed class MiniHttpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Dictionary<string, Func<Dictionary<string, string>, string?>> _routes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<Dictionary<string, string>, string?, string?>> _postRoutes = new(StringComparer.Ordinal);
    private CancellationTokenSource? _cancellation;

    /// <summary>Initialise le serveur sur la boucle locale, port donné.</summary>
    public MiniHttpServer(int port)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    /// <summary>Enregistre une route GET.</summary>
    public void Get(string path, Func<Dictionary<string, string>, string?> handler) => _routes[path] = handler;

    /// <summary>Enregistre une route POST.</summary>
    public void Post(string path, Func<Dictionary<string, string>, string?, string?> handler) => _postRoutes[path] = handler;

    /// <summary>Démarre l'écoute en tâche de fond.</summary>
    public void Start()
    {
        _cancellation = new CancellationTokenSource();
        _listener.Start();
        _ = Task.Run(() => AcceptLoopAsync(_cancellation.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            _ = Task.Run(() => ServeAsync(client, cancellationToken), cancellationToken);
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (requestLine is null)
        {
            return;
        }

        var parts = requestLine.Split(' ');
        var method = parts.ElementAtOrDefault(0) ?? "GET";
        var rawPath = parts.ElementAtOrDefault(1) ?? "/";
        var path = rawPath.Split('?')[0];

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } headerLine && headerLine.Length > 0)
        {
            var separator = headerLine.IndexOf(':');
            if (separator > 0)
            {
                headers[headerLine[..separator].Trim()] = headerLine[(separator + 1)..].Trim();
            }
        }

        string? body = null;
        if (headers.TryGetValue("Content-Length", out var lengthValue) && int.TryParse(lengthValue, out var length) && length > 0)
        {
            var buffer = new char[length];
            var read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            body = new string(buffer, 0, read);
        }

        var query = rawPath.Contains('?') ? rawPath[(rawPath.IndexOf('?') + 1)..] : string.Empty;
        var queryParameters = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]), StringComparer.Ordinal);

        string? payload = null;
        var status = 200;
        if (method == "GET" && _routes.TryGetValue(path, out var handler))
        {
            payload = handler(queryParameters);
            status = payload is null ? 404 : 200;
        }
        else if (method == "POST" && _postRoutes.TryGetValue(path, out var postHandler))
        {
            payload = postHandler(queryParameters, body);
            status = payload is null ? 404 : 200;
        }
        else
        {
            status = 404;
            payload = "{\"error\":\"not_found\"}";
        }

        if (payload is null)
        {
            status = 503;
            payload = "{\"error\":\"unavailable\"}";
        }

        var bodyBytes = Encoding.UTF8.GetBytes(payload);
        var response = new StringBuilder();
        response.Append("HTTP/1.1 ").Append(status).Append(' ').Append(status == 200 ? "OK" : "ERROR").Append("\r\n");
        response.Append("Content-Type: application/json\r\n");
        response.Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n");
        response.Append("Connection: close\r\n\r\n");
        var head = Encoding.UTF8.GetBytes(response.ToString());
        await stream.WriteAsync(head, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bodyBytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cancellation?.Cancel();
        try
        {
            _listener.Stop();
        }
        catch (SocketException)
        {
        }
    }
}
