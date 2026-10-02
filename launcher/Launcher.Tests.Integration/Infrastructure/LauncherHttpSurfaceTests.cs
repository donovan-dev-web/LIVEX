using System.Net.Http;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Infrastructure;
using Xunit;

namespace Launcher.Tests.Integration.Infrastructure;

/// <summary>
/// Surface HTTP du Launcher (NETWORK.md §4.1, OBSERVABILITY.md §4) : endpoints de santé,
/// /info, /metrics en texte Prometheus et /registry en lecture seule, tous sur la boucle locale.
/// </summary>
public sealed class LauncherHttpSurfaceTests
{
    [Fact]
    public async Task Expose_sante_info_metriques_et_registre()
    {
        var registry = new ServiceRegistry();
        var ports = new PortAllocator(5200, 5399, TcpPortProbe.IsFree);
        var orchestration = new OrchestrationService(registry, ports, new SystemClock(), new SilentJournal());
        registry.Add(new ComponentInstance
        {
            InstanceId = "syne-0001",
            ComponentId = "syne",
            Installation = new ComponentInstallation { ComponentId = "syne", Location = "/tmp/syne" },
        });

        var port = FreePort();
        using var surface = new LauncherHttpSurface(
            orchestration,
            () => LauncherHttpSurface.BuildRegistryJson(orchestration.Registry.All()),
            port);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/") };

        var health = await client.GetAsync("/health/live");
        Assert.Equal(System.Net.HttpStatusCode.OK, health.StatusCode);
        Assert.Contains("Healthy", await health.Content.ReadAsStringAsync());

        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(System.Net.HttpStatusCode.OK, ready.StatusCode);

        var info = await client.GetAsync("/info");
        Assert.Contains("launcher", await info.Content.ReadAsStringAsync());

        var metrics = await client.GetAsync("/metrics");
        var metricsBody = await metrics.Content.ReadAsStringAsync();
        Assert.Contains("livex_launcher_component_state", metricsBody);
        Assert.Contains("syne-0001", metricsBody);
        Assert.Contains("livex_launcher_disk_free_bytes", metricsBody);
        Assert.Equal("text/plain", metrics.Content.Headers.ContentType?.MediaType);

        var registryResponse = await client.GetAsync("/registry");
        var registryBody = await registryResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"instances\"", registryBody);
        Assert.Contains("syne-0001", registryBody);
        Assert.Contains("\"component\":\"syne\"", registryBody.Replace(" ", string.Empty));
        Assert.Contains("\"state\":\"Inactif\"", registryBody.Replace(" ", string.Empty));

        var unknown = await client.GetAsync("/unknown");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public void Registre_json_au_format_network_4_1()
    {
        var instance = new ComponentInstance
        {
            InstanceId = "echos-0001",
            ComponentId = "echos",
            Installation = new ComponentInstallation { ComponentId = "echos", Location = "/tmp/echos" },
            StartedAt = DateTimeOffset.Parse("2026-09-30T10:12:00Z"),
        };
        instance.Endpoints["control"] = new ResolvedEndpoint("control", "http://127.0.0.1:5201/", 5201);

        var json = LauncherHttpSurface.BuildRegistryJson([instance]);

        Assert.Contains("\"instanceId\":\"echos-0001\"", Compact(json));
        Assert.Contains("\"node\":\"local\"", Compact(json));
        Assert.Contains("\"control\":\"http://127.0.0.1:5201/\"", Compact(json));
        Assert.Contains("\"startedAt\"", Compact(json));
    }

    private static string Compact(string json) => json.Replace(" ", string.Empty).Replace("\n", string.Empty);

    private static int FreePort()
    {
        for (var port = 5300; port <= 5399; port++)
        {
            if (TcpPortProbe.IsFree(port))
            {
                return port;
            }
        }

        throw new InvalidOperationException("aucun port libre dans la zone haute de la plage interne");
    }

    /// <summary>Journal sans effet : la surface HTTP n'en dépend que pour journaliser son démarrage.</summary>
    private sealed class SilentJournal : ISessionJournal
    {
        public void Log(SessionEvent entry)
        {
        }

        public void Info(string operation, string message, string? correlationId = null, string? instanceId = null)
        {
        }

        public void Warn(string operation, string message, string? correlationId = null, string? instanceId = null)
        {
        }

        public void Fail(string operation, string message, string? correlationId = null, string? instanceId = null)
        {
        }
    }
}
