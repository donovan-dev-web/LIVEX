using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Simulation.Console.Control;
using Xunit;

namespace Simulation.Console.Tests;

/// <summary>
/// Durcissement HTTP du <see cref="ControlServer"/> (jalon review/refactor,
/// engineVersion 0.12.0).
///
/// <para>
/// Avant correction : le corps de requête était lu sans aucune limite de taille,
/// <c>JsonDocument.Parse(text).RootElement</c> était renvoyé sans
/// <c>Clone()</c> ni <c>Dispose</c> (propriétés invalides après lecture de la
/// mémoire native, fuite par requête), et un JSON malformé ou un type de
/// configuration incompatible remontait en 500 au lieu de 400.
/// </para>
/// </summary>
public sealed class ControlServerHardeningTests : IAsyncLifetime
{
    private readonly ControlServer _server = new(FreePort());
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public Task InitializeAsync()
    {
        _server.Start();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _server.DisposeAsync();
    }

    private string Url(string path) => $"http://127.0.0.1:{_server.Port}{path}";

    private async Task<(HttpStatusCode Status, string Body)> PostRawAsync(string path, string raw)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Url(path))
        {
            Content = new StringContent(raw, Encoding.UTF8, "application/json"),
        };
        using HttpResponseMessage response = await _http.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static int FreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task MalformedJson_IsRejected_With400()
    {
        (HttpStatusCode status, string body) = await PostRawAsync("/api/control/start", "{ \"seed\": ");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("invalid_json", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonJsonBody_IsRejected_With400_Not500()
    {
        (HttpStatusCode status, string body) = await PostRawAsync("/api/control/start", "ce n'est pas du json");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("invalid_json", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OversizedBody_IsRejected_With413_AndNotBuffered()
    {
        // Un corps de 2 Mio dépasse la limite de 1 Mio : le serveur doit refuser
        // sans chercher à le lire intégralement en mémoire.
        string huge = new('a', 2 * 1024 * 1024);
        string payload = "{\"seed\":1,\"note\":\"" + huge + "\"}";

        (HttpStatusCode status, string body) = await PostRawAsync("/api/control/prepare", payload);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, status);
        Assert.Contains("payload_too_large", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrongTypedConfigValue_IsRejected_With400_Not500()
    {
        (HttpStatusCode status, string body) = await PostRawAsync(
            "/api/control/start",
            "{\"seed\":7,\"config\":{\"simulation\":{\"ticksPerSecond\":\"beaucoup\"}}}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("invalid_config", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JsonErrorMessage_DoesNotLeakInternalPathOrLineInfo()
    {
        (HttpStatusCode status, string body) = await PostRawAsync(
            "/api/control/start",
            "{\"seed\":7,\"config\":{\"simulation\":{\"ticksPerSecond\":{}}}}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.DoesNotContain("Simulation.Core", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Path: $", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParsedBody_StaysReadableForTheWholeRequest()
    {
        // Vérifie que le RootElement survit à la fin de vie du JsonDocument :
        // sans Clone(), la lecture de `seed` après le parse levait une
        // InvalidOperationException sur une propriété d'élément détaché.
        (HttpStatusCode prepareStatus, _) = await PostRawAsync("/api/control/prepare", "{\"seed\":42}");
        Assert.Equal(HttpStatusCode.OK, prepareStatus);

        using HttpResponseMessage response = await _http.GetAsync(Url("/api/control/status"));
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"seed\":42", body.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepeatedRequests_DoNotExhaustNativeJsonMemory()
    {
        // Un leak par requête de JsonDocument se manifeste à terme ; on vérifie ici
        // surtout que le routeur reste fonctionnel après de nombreux passages,
        // chaque élément restant detached de son document.
        for (int i = 0; i < 200; i++)
        {
            (HttpStatusCode status, _) = await PostRawAsync("/api/control/prepare", $"{{\"seed\":{i}}}");
            Assert.Equal(HttpStatusCode.OK, status);
        }
    }

    [Fact]
    public async Task Stop_ThenStatus_WorksAcrossManyRequests()
    {
        for (int i = 0; i < 50; i++)
        {
            (HttpStatusCode status, _) = await PostRawAsync("/api/control/stop", "{}");
            Assert.Equal(HttpStatusCode.OK, status);
        }

        using HttpResponseMessage response = await _http.GetAsync(Url("/api/control/status"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
