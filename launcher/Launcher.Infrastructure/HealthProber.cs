using System.Text.Json;
using Launcher.Domain;

namespace Launcher.Infrastructure;

/// <summary>
/// Sonde de santé HTTP (INTEGRATION_CONTRACT.md §6, OBSERVABILITY.md §4).
/// Pull : le Launcher fixe la cadence, il n'est jamais sollicité par les composants.
/// Lecture seule : la supervision ne modifie jamais l'état du composant.
/// </summary>
public sealed class HealthProber : IHealthProbe
{
    private readonly HttpClient _httpClient;

    /// <summary>Initialise la sonde avec un délai d'attente court, adapté au sondage.</summary>
    public HealthProber(TimeSpan? timeout = null)
    {
        _httpClient = new HttpClient(new SocketsHttpHandler { UseProxy = false })
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(2),
        };
    }

    /// <inheritdoc />
    public async Task<(bool Ready, string? Cause)> ProbeReadyAsync(Uri controlEndpoint, CancellationToken cancellationToken, string readyPath = "/health/ready")
    {
        // Chemin déclaré au manifeste (COMPONENTS.md §3) : le §6 fixe /health/ready pour
        // les composants conformes, un composant réel peut déclarer /health.
        var path = string.IsNullOrWhiteSpace(readyPath) ? "/health/ready" : readyPath;
        try
        {
            using var response = await _httpClient.GetAsync(new Uri(controlEndpoint, path), cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            return (false, $"sonde {path} : HTTP {(int)response.StatusCode}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return (false, $"sonde injoignable : {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<RunTickProgress?> FetchRunProgressAsync(Uri controlEndpoint, CancellationToken cancellationToken)
    {
        try
        {
            var json = await _httpClient.GetStringAsync(
                new Uri(controlEndpoint, "/api/control/status"), cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("tick", out var tickElement) || tickElement.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            long? maxTicks = root.TryGetProperty("maxTicks", out var maxElement)
                             && maxElement.ValueKind == JsonValueKind.Number
                ? maxElement.GetInt64()
                : null;
            int alive = root.TryGetProperty("aliveCount", out var aliveElement)
                        && aliveElement.ValueKind == JsonValueKind.Number
                ? aliveElement.GetInt32()
                : 0;
            string state = root.TryGetProperty("state", out var stateElement)
                ? stateElement.GetString() ?? string.Empty
                : string.Empty;
            return new RunTickProgress(tickElement.GetInt64(), maxTicks, alive, state);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Un moteur qui n'expose pas cet état ne doit pas faire échouer le run :
            // l'avancement est un confort d'affichage, pas une condition d'exécution.
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ComponentInfo?> FetchInfoAsync(Uri controlEndpoint, CancellationToken cancellationToken)
    {
        try
        {
            var json = await _httpClient.GetStringAsync(new Uri(controlEndpoint, "/info"), cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var version = root.TryGetProperty("version", out var versionElement) ? versionElement.GetString() : null;
            int? protocol = root.TryGetProperty("protocolVersion", out var protocolElement) && protocolElement.ValueKind == JsonValueKind.Number
                ? protocolElement.GetInt32()
                : null;
            return new ComponentInfo(id ?? string.Empty, version ?? string.Empty, protocol);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }
}
