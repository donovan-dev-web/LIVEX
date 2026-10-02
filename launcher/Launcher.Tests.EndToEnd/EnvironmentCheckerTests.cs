using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Launcher.App.Composition;
using Launcher.Domain.Model;
using Launcher.Package;
using Launcher.Protocol;
using Xunit;

namespace Launcher.Tests.EndToEnd;

/// <summary>
/// G6 — commande --check (PACKAGING.md §6) : les huit vérifications du tableau, dans l'ordre,
/// avec les causes attendues et une sortie textuelle stable citable en rapport d'incident.
/// Hermétique : racines de données et de composants explicites, aucune source standard.
/// </summary>
public sealed class EnvironmentCheckerTests : IDisposable
{
    private readonly string _root;
    private readonly string _componentsRoot;

    public EnvironmentCheckerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"livexp-check-{Guid.NewGuid():N}");
        _componentsRoot = Path.Combine(_root, "components");
        Directory.CreateDirectory(_componentsRoot);
    }

    [Fact]
    public void Les_huit_verifications_de_packaging_sont_dans_l_ordre_du_tableau()
    {
        var checks = EnvironmentChecker.Run(_root, Array.Empty<string>());

        Assert.Equal(
        [
                "exécution",
                "droits d'écriture",
                "espace disque",
                "version du format",
                "composants",
                "moteur",
                "ports",
                "navigateur",
            ],
            checks.Select(check => check.Name).ToArray());

        // Post vierge : format et ports conformes, composants et moteur absents avec leur cause.
        Assert.True(checks.Single(check => check.Name == "exécution").Passed);
        Assert.True(checks.Single(check => check.Name == "droits d'écriture").Passed);
        Assert.True(checks.Single(check => check.Name == "version du format").Passed);
        Assert.Contains("aucun paquet", checks.Single(check => check.Name == "version du format").Detail);
        Assert.False(checks.Single(check => check.Name == "composants").Passed);
        Assert.Contains("aucun composant détecté", checks.Single(check => check.Name == "composants").Detail);
        Assert.False(checks.Single(check => check.Name == "moteur").Passed);
        Assert.Contains("moteur absent", checks.Single(check => check.Name == "moteur").Detail);
        Assert.True(checks.Single(check => check.Name == "ports").Passed);

        // Le navigateur est une information : il ne bloque jamais (PACKAGING.md §6).
        Assert.True(checks.Single(check => check.Name == "navigateur").Passed);
        Assert.Contains("non bloquant", checks.Single(check => check.Name == "navigateur").Detail);

        // Un seul échec rend le diagnostic en échec (code de sortie 2).
        Assert.Contains(checks, check => !check.Passed);
    }

    [Fact]
    public void Sortie_textuelle_stable_marqueur_nom_detail()
    {
        Assert.Equal("[OK  ] exécution : probe", EnvironmentChecker.Format(new EnvironmentCheck("exécution", true, "probe")));
        Assert.Equal("[ÉCHEC] moteur : moteur absent", EnvironmentChecker.Format(new EnvironmentCheck("moteur", false, "moteur absent")));

        foreach (var check in EnvironmentChecker.Run(_root, Array.Empty<string>()))
        {
            Assert.Matches(@"^\[(OK  |ÉCHEC)\] [^:]+ : .+$", EnvironmentChecker.Format(check));
        }
    }

    [Fact]
    public void Moteur_present_et_conflit_de_port_signales()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        var busyPort = ((IPEndPoint)occupied.LocalEndpoint).Port;
        try
        {
            InstallFakeEngine(busyPort);
            var checks = EnvironmentChecker.Run(_root, [_componentsRoot]);

            var engine = checks.Single(check => check.Name == "moteur");
            Assert.True(engine.Passed, engine.Detail);
            Assert.Contains("présent et exécutable", engine.Detail);

            Assert.True(checks.Single(check => check.Name == "composants").Passed);

            // Le port déclaré au manifeste est occupé : conflit nommé (NETWORK.md §6.2).
            var ports = checks.Single(check => check.Name == "ports");
            Assert.False(ports.Passed);
            Assert.Contains("conflit de port", ports.Detail);
            Assert.Contains(busyPort.ToString(System.Globalization.CultureInfo.InvariantCulture), ports.Detail);
            Assert.Contains("syne/control", ports.Detail);
        }
        finally
        {
            occupied.Stop();
        }
    }

    [Fact]
    public void Version_de_paquet_inconnue_signalee()
    {
        var packagesRoot = Path.Combine(_root, "packages");
        Directory.CreateDirectory(packagesRoot);
        var packagePath = Path.Combine(packagesRoot, "ANCIEN.livexp");

        var definition = new ExperimentDefinition
        {
            Id = "EXP-CHECK-001",
            Title = "Paquet d'ancien schéma",
            Profile = WellKnownProfiles.Analyse,
            Simulation = "ecosystem_01",
            RunCount = 1,
            Ticks = 1,
            SeedStrategy = SeedStrategy.Derived,
            BaseSeed = 1,
            FailurePolicy = FailurePolicy.Continue,
        };
        using (var writer = LivexPackageWriter.CreateNew(packagePath, definition, "0.1.0-test", DateTimeOffset.UnixEpoch))
        {
            writer.Seal(DateTimeOffset.UnixEpoch);
        }

        // Forge un manifeste de schéma 99 dans le conteneur.
        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry(PackageConstants.ManifestEntry)!;
            entry.Delete();
            var forged = archive.CreateEntry(PackageConstants.ManifestEntry);
            using var target = forged.Open();
            target.Write(Encoding.UTF8.GetBytes("{\"schema\":99,\"packageId\":\"X\",\"state\":\"live\"}"));
        }

        var checks = EnvironmentChecker.Run(_root, Array.Empty<string>());
        var format = checks.Single(check => check.Name == "version du format");
        Assert.False(format.Passed);
        Assert.Contains("version de paquet non prise en charge", format.Detail);
        Assert.Contains("99", format.Detail);
    }

    /// <summary>Fabrique une installation « syne » détectable : manifeste + binaire présent, sans l'exécuter.</summary>
    private void InstallFakeEngine(int controlPort)
    {
        var location = Path.Combine(_componentsRoot, "syne");
        Directory.CreateDirectory(location);
        File.WriteAllText(Path.Combine(location, "component.json"),
            $$"""
            {
              "schema": 1,
              "id": "syne",
              "name": "SYNE",
              "type": "engine",
              "version": "0.1.0-fake",
              "executable": { "windows": "Stub.Syne.exe", "linux": "Stub.Syne", "path": "Stub.Syne" },
              "capabilities": ["headless", "seed", "tickLimit"],
              "endpoints": { "control": { "transport": "http", "port": {{controlPort}} } }
            }
            """);
        File.WriteAllText(Path.Combine(location, "Stub.Syne"), "binaire factice");
        File.WriteAllText(Path.Combine(location, "Stub.Syne.exe"), "binaire factice");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
