using Launcher.Domain;
using Launcher.Infrastructure;
using Launcher.Tests.Integration.Infrastructure;
using Xunit;

namespace Launcher.Tests.EndToEnd;

public sealed class ManifestContractTests
{
    [Fact]
    public void Exemple_v1_partage_est_accepte()
    {
        var detected = InspectFixture("component-manifest-v1.valid.json");

        Assert.NotNull(detected);
        Assert.True(detected.ManifestValid, detected.DetectionCause);
        Assert.Equal("example-worker", detected.ComponentId);
    }

    [Fact]
    public void Exemple_v1_avec_traversee_de_chemin_est_refuse()
    {
        var detected = InspectFixture("component-manifest-v1.invalid-path.json");

        Assert.NotNull(detected);
        Assert.False(detected.ManifestValid);
        Assert.Contains("relatifs", detected.DetectionCause, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Manifeste_v1_accepte_un_port_de_controle_dynamique()
    {
        using var installation = new TemporaryInstallation();
        var detected = new ManifestDetector(new InMemoryJournalAdapter()).Inspect(installation.Path);

        Assert.NotNull(detected);
        Assert.True(detected.ManifestValid, detected.DetectionCause);
        Assert.Equal("test-service", detected.ComponentId);
    }

    [Theory]
    [InlineData("../outside", null, "chemins")]
    [InlineData("bin/service", 70000, "port invalide")]
    [InlineData("bin/service", 5181, "timeouts")]
    [InlineData("bin/service", 5181, "version de manifeste non prise en charge", 2)]
    public void Manifeste_v1_invalide_est_refuse_avec_une_cause(
        string executable,
        int? controlPort,
        string expectedCause,
        int schemaVersion = 1)
    {
        using var installation = new TemporaryInstallation(executable, controlPort,
            includeTimeouts: expectedCause != "timeouts", schemaVersion: schemaVersion);
        var detected = new ManifestDetector(new InMemoryJournalAdapter()).Inspect(installation.Path);

        Assert.NotNull(detected);
        Assert.False(detected.ManifestValid);
        Assert.Contains(expectedCause, detected.DetectionCause, StringComparison.OrdinalIgnoreCase);
    }

    private static Launcher.Domain.Model.ComponentInstallation? InspectFixture(string fixtureName)
    {
        using var installation = new TemporaryInstallation();
        var fixturesDirectory = FindFixturesDirectory();
        File.Copy(Path.Combine(fixturesDirectory, fixtureName),
            System.IO.Path.Combine(installation.Path, "component.json"), overwrite: true);
        Directory.CreateDirectory(System.IO.Path.Combine(installation.Path, "bin"));
        File.WriteAllText(System.IO.Path.Combine(installation.Path, "bin", "worker"), "stub");
        return new ManifestDetector(new InMemoryJournalAdapter()).Inspect(installation.Path);
    }

    private static string FindFixturesDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var fixtures = System.IO.Path.Combine(directory.FullName, "contracts", "examples");
            if (Directory.Exists(fixtures))
            {
                return fixtures;
            }
        }

        throw new DirectoryNotFoundException("fixtures de contrat Launcher introuvables");
    }

    private sealed class TemporaryInstallation : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"livex-manifest-{Guid.NewGuid():N}");

        public TemporaryInstallation(
            string executable = "bin/service",
            int? controlPort = null,
            bool includeTimeouts = true,
            int schemaVersion = 1)
        {
            Directory.CreateDirectory(Path);
            var binaryPath = System.IO.Path.Combine(Path, "bin", "service");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(binaryPath)!);
            File.WriteAllText(binaryPath, "stub");

            var port = controlPort is null ? string.Empty : $",\"port\":{controlPort.Value}";
            var timeouts = includeTimeouts
                ? "\"timeouts\":{\"startupMs\":30000,\"shutdownMs\":5000},"
                : "\"timeouts\":{\"startupMs\":30000},";
            File.WriteAllText(System.IO.Path.Combine(Path, "component.json"),
                $$"""
                {
                  "schema": {{schemaVersion}},
                  "id": "test-service",
                  "name": "Test service",
                  "type": "utility",
                  "version": "1.0.0",
                  "executable": { "path": {{System.Text.Json.JsonSerializer.Serialize(executable)}} },
                  "endpoints": { "control": { "transport": "http"{{port}} } },
                  "capabilities": ["headless"],
                  "health": { "probe": "http", "path": "/health/ready", "intervalMs": 1000 },
                  {{timeouts}}
                  "contributesTo": []
                }
                """);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
