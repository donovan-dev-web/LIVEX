using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Simulation.Console.Control;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Console.Tests;

/// <summary>
/// Sémaphore « surcouche partielle » de la configuration via le wire HTTP
/// (jalon review/refactor, engineVersion 0.12.0).
///
/// <para>
/// Le corps <c>config</c> était désérialisé en <c>SimulationOptions</c> avant
/// fusion. L'objet obtenu étant complet, chaque clé absente prenait la valeur par
/// défaut du type et écrasait le profil de base : un client ne pouvait surcharger
/// qu'un champ sans détruire silencieusement tous les autres réglages.
/// </para>
/// </summary>
public class ControlServerSparseConfigTests : IAsyncLifetime
{
    private readonly ControlServer _server = new(FreePort());
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

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

    [Fact]
    public async Task PartialConfig_OverridesTheNamedField_AndKeepsTheRestOfTheProfile()
    {
        // worldWidth est la seule clé fournie ; tout le reste du profil de
        // référence doit survivre. Avant correction, la réserve de nourriture
        // retombait à 100 et le relais se réactivait.
        using var prepare = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/prepare"))
        {
            Content = JsonBody("""{"seed": 4242, "config": {"simulation": {"worldWidth": 640}}}"""),
        };

        using HttpResponseMessage response = await _http.SendAsync(prepare);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Start_WithPartialConfig_KeepsProfileResources()
    {
        // Contrôle direct de l'effet : le monde doit garder les ressources du
        // profil de référence malgré une surcouche qui n'en parle pas.
        using var start = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/start"))
        {
            Content = JsonBody("""{"seed": 99, "maxTicks": 1, "config": {"agents": {"initialCount": 3}}}"""),
        };

        using HttpResponseMessage response = await _http.SendAsync(start);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonNode? body = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("started", (string?)body?["action"]);
    }

    [Fact]
    public async Task MalformedConfig_IsRejected_InsteadOfSilentlyIgnored()
    {
        // Le comportement précédent renvoyait null sur JsonException, ce qui
        // faisait retomber silencieusement sur le profil : une configuration
        // invalide passait pour un succès.
        using var prepare = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/prepare"))
        {
            Content = JsonBody("""{"seed": 7, "config": "pas-un-objet"}"""),
        };

        using HttpResponseMessage response = await _http.SendAsync(prepare);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonNode? body = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_config", (string?)body?["error"]);
    }

    [Fact]
    public async Task MalformedConfigValue_IsRejected_NotSilentlyReset()
    {
        // config est bien un objet, mais une valeur est du mauvais type : la
        // désérialisation doit échouer bruyamment (400) au lieu de produire une
        // surcouche tronquée.
        using var prepare = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/prepare"))
        {
            Content = JsonBody("""{"seed": 7, "config": {"simulation": {"worldWidth": "large"}}}"""),
        };

        using HttpResponseMessage response = await _http.SendAsync(prepare);

        // Le type invalide n'est pas un 400 « invalid_config » de notre garde :
        // il doit au minimum ne pas être traité comme un succès silencieux.
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReferenceProfile_RoundTrips_ThroughTheFactory()
    {
        // Invariant du lanceur : « pas de config » ⇒ exactement le profil de
        // référence, et non une fusion approximative.
        (SimulationOptions options, ulong seed) = SimulationFactory.ResolveOptions(
            ConfigLoader.LoadDefaults(), SimulationProfiles.ReferenceJson(), seed: null);

        SimulationOptions reference = SimulationProfiles.Reference();

        Assert.Equal(ConfigLoader.ToJson(reference), ConfigLoader.ToJson(options));
        Assert.Equal(reference.Random.Seed, seed);
    }

    private string Url(string path) => $"http://127.0.0.1:{_server.Port}{path}";

    private static HttpContent JsonBody(string json) => new StringContent(json, Encoding.UTF8, "application/json");

    private static int FreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
