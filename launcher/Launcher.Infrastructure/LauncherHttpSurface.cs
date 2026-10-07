using System.Net;
using System.Net.Sockets;
using System.Text;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Protocol;

namespace Launcher.Infrastructure;

/// <summary>
/// Surface HTTP du Launcher (NETWORK.md §4.1, OBSERVABILITY.md §4) :
/// - /health/live, /health/ready : le Launcher lui-même est supervisable ;
/// - /info : identité et version ;
/// - /metrics : métriques d'orchestration, texte Prometheus ;
/// - /registry : le registre de services, en lecture seule, pour les autres composants.
/// Lecture seule pour l'observabilité : aucune de ces routes ne modifie l'état.
/// </summary>
public sealed class LauncherHttpSurface : IDisposable
{
    private readonly MiniHttpServer _server;
    private readonly OrchestrationService _orchestration;
    private readonly Func<string> _registryJson;
    private readonly string? _packagesPath;

    /// <summary>Port d'écoute du Launcher, alloué dans la plage interne.</summary>
    public int Port { get; }

    /// <summary>Initialise la surface HTTP avec le registre à exposer.</summary>
    public LauncherHttpSurface(OrchestrationService orchestration, Func<string> registryJson, int port, string? packagesPath = null)
    {
        _orchestration = orchestration;
        _registryJson = registryJson;
        _packagesPath = packagesPath;
        Port = port;
        _server = new MiniHttpServer(port);

        _server.Get("/health/live", _ => "{\"status\":\"Healthy\"}");
        _server.Get("/health/ready", _ => "{\"status\":\"Healthy\"}");
        _server.Get("/health/details", _ => "{\"status\":\"Healthy\",\"checks\":[{\"name\":\"registry\",\"status\":\"Healthy\"}]}");
        _server.Get("/info", _ => "{\"id\":\"launcher\",\"version\":\"0.1.0\",\"protocolVersion\":1}");
        _server.Get("/metrics", "text/plain; version=0.0.4", _ => BuildMetrics());
        _server.Get("/registry", _ => _registryJson());
        _server.Start();
    }

    /// <summary>Métriques d'orchestration (OBSERVABILITY.md §5.4), sans métrique scientifique.</summary>
    private string BuildMetrics()
    {
        var builder = new StringBuilder();
        foreach (var instance in _orchestration.Registry.All())
        {
            var state = StateRules.Name(instance.Health.State);
            builder.Append("livex_launcher_component_state{component=\"").Append(instance.ComponentId)
                .Append("\",instance=\"").Append(instance.InstanceId).Append("\"} 1\n");
            builder.Append("# instance ").Append(instance.InstanceId).Append(" state ").Append(state).Append('\n');
        }

        var packagesRoot = _packagesPath ?? WorkspaceLayout.UserDataRoot();
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(packagesRoot))!);
            builder.Append("livex_launcher_disk_free_bytes{path=\"packages\"} ").Append(drive.AvailableFreeSpace).Append('\n');
        }
        catch (Exception exception) when (exception is ArgumentException
            or DriveNotFoundException or IOException or UnauthorizedAccessException)
        {
        }

        return builder.ToString();
    }

    /// <summary>Construit le document du registre au format NETWORK.md §4.1.</summary>
    public static string BuildRegistryJson(IReadOnlyList<ComponentInstance> instances)
    {
        var entries = instances
            .Select(instance => new LauncherRegistryEntry(
                instance.InstanceId,
                instance.ComponentId,
                "local",
                StateRules.Name(instance.Health.State),
                instance.Endpoints.ToDictionary(
                    endpoint => endpoint.Key,
                    endpoint => endpoint.Value.Url,
                    StringComparer.Ordinal),
                instance.ProcessId,
                instance.StartedAt))
            .ToList();
        return ContractJson.Serialize(new LauncherRegistryDocument(entries));
    }

    /// <inheritdoc />
    public void Dispose() => _server.Dispose();
}

/// <summary>Document du registre de services exposé en lecture (NETWORK.md §4.1).</summary>
public sealed record LauncherRegistryDocument(IReadOnlyList<LauncherRegistryEntry> Instances);

/// <summary>Une instance vivante du registre, vue par les autres composants.</summary>
public sealed record LauncherRegistryEntry(
    string InstanceId,
    string Component,
    string Node,
    string State,
    IReadOnlyDictionary<string, string> Endpoints,
    int? ProcessId,
    DateTimeOffset? StartedAt);

/// <summary>Serveur HTTP minimal partagé par la surface du Launcher et les stubs.</summary>
public sealed class MiniHttpServer : IDisposable
{
    /// <summary>Plafond d'un corps de requête : au-delà, 413 sans jamais allouer le tampon.</summary>
    private const int MaxRequestBodyBytes = 1024 * 1024;

    private readonly TcpListener _listener;
    private readonly Dictionary<string, Func<Dictionary<string, string>, string?>> _routes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<Dictionary<string, string>, string?, string?>> _postRoutes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _contentTypes = new(StringComparer.Ordinal);
    private CancellationTokenSource? _cancellation;

    /// <summary>Initialise le serveur sur la boucle locale, port donné.</summary>
    public MiniHttpServer(int port)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    /// <summary>Enregistre une route GET servie en application/json.</summary>
    public void Get(string path, Func<Dictionary<string, string>, string?> handler)
    {
        _routes[path] = handler;
        _contentTypes[path] = "application/json";
    }

    /// <summary>Enregistre une route GET avec un Content-Type explicite (ex. texte Prometheus).</summary>
    public void Get(string path, string contentType, Func<Dictionary<string, string>, string?> handler)
    {
        _routes[path] = handler;
        _contentTypes[path] = contentType;
    }

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
            if (length > MaxRequestBodyBytes)
            {
                await WriteResponseAsync(stream, 413, "Payload Too Large", "{\"error\":\"payload_too_large\"}", "application/json").ConfigureAwait(false);
                return;
            }

            // Lecture complète : un seul ReadAsync ne garantit pas de recevoir tout le corps
            // (il arrive par paquets), et un corps tronqué se lit comme une requête malformée.
            var buffer = new char[length];
            var read = 0;
            while (read < length)
            {
                var chunk = await reader.ReadAsync(buffer.AsMemory(read, length - read), cancellationToken).ConfigureAwait(false);
                if (chunk == 0)
                {
                    break; // Flux fermé avant la fin annoncée : corps partiel, jamais une attente infinie.
                }

                read += chunk;
            }

            body = new string(buffer, 0, read);
        }

        var query = rawPath.Contains('?') ? rawPath[(rawPath.IndexOf('?') + 1)..] : string.Empty;
        // Dictionnaire tolérant : « ?a=1&a=2 » est répété, pas invalide — ne pas lever ici,
        // sinon la requête meurt sans réponse et le client attend jusqu'à son délai.
        var queryParameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var segments = pair.Split('=', 2);
            if (segments.Length == 2)
            {
                queryParameters[Uri.UnescapeDataString(segments[0])] = Uri.UnescapeDataString(segments[1]);
            }
        }

        string? payload;
        var status = 200;
        try
        {
            if (method == "GET" && _routes.TryGetValue(path, out var handler))
            {
                payload = handler(queryParameters);
            }
            else if (method == "POST" && _postRoutes.TryGetValue(path, out var postHandler))
            {
                payload = postHandler(queryParameters, body);
            }
            else
            {
                payload = null;
            }
        }
        catch (Exception)
        {
            // Un gestionnaire en échec doit répondre, pas laisser la connexion suspendue.
            status = 500;
            payload = "{\"error\":\"internal\"}";
            await WriteResponseAsync(stream, status, "Internal Server Error", payload, "application/json").ConfigureAwait(false);
            return;
        }

        if (payload is null)
        {
            status = 404;
            payload = "{\"error\":\"not_found\"}";
        }

        // Le type de contenu est celui de la route, jamais muté par la réponse : écrire dans
        // un dictionnaire partagé ici corromprait des réponses concurrentes et ferait croître
        // le tableau à chaque chemin inconnu servi en 404.
        var contentType = status == 200
            ? _contentTypes.GetValueOrDefault(path, "application/json")
            : "application/json";
        await WriteResponseAsync(stream, status, status switch
        {
            200 => "OK",
            404 => "Not Found",
            413 => "Payload Too Large",
            _ => "Error",
        }, payload, contentType).ConfigureAwait(false);
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int status, string reason, string payload, string contentType)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(payload);
        var response = new StringBuilder();
        response.Append("HTTP/1.1 ").Append(status).Append(' ').Append(reason).Append("\r\n");
        response.Append("Content-Type: ").Append(contentType).Append("\r\n");
        response.Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n");
        response.Append("Connection: close\r\n\r\n");
        var head = Encoding.UTF8.GetBytes(response.ToString());
        await stream.WriteAsync(head).ConfigureAwait(false);
        await stream.WriteAsync(bodyBytes).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
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
