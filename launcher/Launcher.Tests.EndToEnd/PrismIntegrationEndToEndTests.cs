using Launcher.Domain;
using Launcher.Domain.Model;
using Xunit;

namespace Launcher.Tests.EndToEnd;

/// <summary>
/// G7 — déverrouillage du mode Immersion (ROADMAP.md §6.8) : le verrou est la conséquence
/// d'un manifeste PRISM conforme au contrat §11.1, pas une constante. PRISM est détecté,
/// pilôté et arrêté comme n'importe quel autre composant de la pile.
/// </summary>
[Collection("Composition")]
public sealed class PrismUnlockEndToEndTests : IDisposable
{
    private readonly string _root;
    private readonly string _componentsParent;
    private readonly string _dataRoot;
    private readonly int _prismPort;

    public PrismUnlockEndToEndTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"livexp-g7-{Guid.NewGuid():N}");
        _componentsParent = Path.Combine(_root, "components");
        _dataRoot = Path.Combine(_root, "data");
        _prismPort = StubInstall.FreePort();

        StubInstall.Install(_componentsParent, "syne", "Stub.Syne", Manifests.Syne);
        StubInstall.Install(_componentsParent, "prism", "Stub.Prism", Manifests.PrismConformant(_prismPort));
    }

    [Fact]
    public async Task Manifeste_conforme_deverrouille_et_pilote_prism()
    {
        using var composition = new Launcher.App.Composition.LauncherComposition(
            Path.Combine(_root, "packages"), _componentsParent, _dataRoot);

        // Déverrouillage : conséquence du manifeste détecté, aucun code spécial (ADR-006).
        var immersion = composition.Orchestration.ResolveProfile(WellKnownProfiles.Immersion);
        Assert.False(immersion.ImmersionLocked, string.Join(" ; ", immersion.Problems));
        Assert.True(immersion.Satisfiable, string.Join(" ; ", immersion.Problems));
        Assert.Equal(["syne", "prism"], immersion.StartupOrder);

        // Cycle de vie : PRISM est démarré comme tout autre composant (§5), sur son port déclaré.
        var startError = await composition.Facade.ToggleComponentAsync("prism", true);
        Assert.Null(startError);

        var instance = composition.Orchestration.Registry.FindByComponent("prism");
        Assert.NotNull(instance);
        Assert.Equal(_prismPort, instance!.Endpoints["control"].Port);
        Assert.Equal("ws", new Uri(instance.Endpoints["ws"].Url).Scheme);
        await StubInstall.WaitForHealthyAsync(instance.Endpoints["control"].Url);

        // Arguments : communs du §3.1 seulement — aucun argument de lot SYNE (§3.3) sur PRISM.
        var argsFile = Path.Combine(_dataRoot, "workspace", "sessions", instance.InstanceId, "args.txt");
        var args = File.ReadAllLines(argsFile);
        Assert.Contains("--instance-id", args);
        Assert.Contains("--control-port", args);
        Assert.Contains("--data-port", args);
        Assert.Contains("--headless", args);
        Assert.DoesNotContain("--seed", args);
        Assert.DoesNotContain("--ticks", args);
        Assert.DoesNotContain("--simulation", args);
        Assert.DoesNotContain("--autostart", args);

        // Arrêt : commande standard §5.1, registre à jour, aucun survivant.
        var stopError = await composition.Facade.ToggleComponentAsync("prism", false);
        Assert.Null(stopError);
        Assert.Null(composition.Orchestration.Registry.FindByComponent("prism"));
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

/// <summary>
/// G7 — le verrou reste fermé, avec la cause exacte du §11.1, quand le manifeste détecté
/// ne satisfait pas les exigences publiées. Le verrou est évalué, jamais codé en dur.
/// </summary>
[Collection("Composition")]
public sealed class PrismLockEndToEndTests : IDisposable
{
    private readonly string _root;
    private readonly string _componentsParent;

    public PrismLockEndToEndTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"livexp-g7lock-{Guid.NewGuid():N}");
        _componentsParent = Path.Combine(_root, "components");

        StubInstall.Install(_componentsParent, "syne", "Stub.Syne", Manifests.Syne);
        StubInstall.Install(_componentsParent, "prism", "Stub.Prism", Manifests.PrismNonConformant);
    }

    [Fact]
    public void Manifeste_non_conforme_tient_le_verrou_avec_la_cause_exacte()
    {
        using var composition = new Launcher.App.Composition.LauncherComposition(
            Path.Combine(_root, "packages"), _componentsParent, Path.Combine(_root, "data"));

        var immersion = composition.Orchestration.ResolveProfile(WellKnownProfiles.Immersion);
        Assert.True(immersion.ImmersionLocked);
        Assert.False(immersion.Satisfiable);
        Assert.Contains(immersion.Problems, p => p.Contains("PRISM ne se déclare pas porteur du mode Immersion"));
        Assert.Contains(immersion.Problems, p => p.Contains("capacité « snapshotStream » absente du manifeste"));
        Assert.Contains(immersion.Problems, p => p.Contains("capacité « renderCadence » absente du manifeste"));
        Assert.Contains(immersion.Problems, p => p.Contains("G7"));
        Assert.DoesNotContain("prism", immersion.StartupOrder);
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

/// <summary>
/// Posture pilotée PRISM (ADR-017) : le moteur démarre sans `--autostart`, sur le profil
/// `prism`, avec la diffusion WebSocket exposée — le projet Unreal prépare puis démarre via
/// l'API de contrôle (prepare/ready/start, TRANSPORT_API.md §3). Aucun seed ni horizon n'est
/// imposé au moteur : ils viennent du plugin.
/// </summary>
[Collection("Composition")]
public sealed class PrismPilotedPostureEndToEndTests : IDisposable
{
    private readonly string _root;
    private readonly string _componentsParent;
    private readonly string _dataRoot;

    public PrismPilotedPostureEndToEndTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"livexp-prismpilot-{Guid.NewGuid():N}");
        _componentsParent = Path.Combine(_root, "components");
        _dataRoot = Path.Combine(_root, "data");
        StubInstall.Install(_componentsParent, "syne", "Stub.Syne", Manifests.Syne);
    }

    [Fact]
    public async Task Demarrage_pilote_passe_au_profil_prism_et_expose_l_observation()
    {
        using var composition = new Launcher.App.Composition.LauncherComposition(
            Path.Combine(_root, "packages"), _componentsParent, _dataRoot);
        composition.DetectComponents();

        var result = await composition.Facade.StartEnginePrismAsync();

        // Posture pilotée : moteur démarré, points d'accès que le projet Unreal rejoint.
        // Le port d'observation (ws://) n'est exposé que par cette posture, jamais en lot.
        Assert.Null(result.Error);
        Assert.Equal("syne", result.ComponentId);
        Assert.NotNull(result.ControlUrl);
        Assert.NotNull(result.ObserveUrl);
        Assert.StartsWith("ws://", result.ObserveUrl, StringComparison.Ordinal);

        // IdInstance relevé dans la foulée du démarrage (le stub, sans horizon imposé,
        // n'entretient que sa courte boucle de 10 ticks avant de sortir de lui-même).
        var instance = composition.Orchestration.Registry.FindByComponent("syne");
        Assert.NotNull(instance);

        // Le profil prism (et non reference) a été transmis au moteur : la posture pilote
        // le monde ADR-017. Le stub écrit launch.json à son démarrage ; on attend le fichier.
        var launchPath = Path.Combine(_dataRoot, "workspace", "sessions", instance!.InstanceId, "launch.json");
        string? simulation = null;
        for (var attempt = 0; attempt < 100 && simulation is null; attempt++)
        {
            if (File.Exists(launchPath))
            {
                using var launch = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(launchPath));
                simulation = launch.RootElement.GetProperty("simulation").GetString();
            }
            else
            {
                await Task.Delay(20);
            }
        }

        Assert.Equal(WellKnownSimulations.Prism, simulation);
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

/// <summary>Manifestes des bancs G7 : le conforme satisfait intégralement §11.1, le non conforme non.</summary>
internal static class Manifests
{
    /// <summary>SYNE moteur, identique au banc de campagne.</summary>
    public const string Syne =
        """
        {
          "schema": 1,
          "id": "syne",
          "name": "SYNE",
          "type": "engine",
          "version": "0.1.0-stub",
          "executable": { "windows": "Stub.Syne.exe", "linux": "Stub.Syne", "path": "Stub.Syne" },
          "capabilities": ["headless", "seed", "tickLimit", "export", "pause"],
          "endpoints": { "control": { "transport": "http" } },
          "health": { "probe": "http", "path": "/health/ready", "intervalMs": 1000 },
          "timeouts": { "startupMs": 30000, "shutdownMs": 15000 },
          "contributesTo": ["analyse", "immersion"]
        }
        """;

    /// <summary>PRISM conforme au contrat §11.1 : mode déclaré, flux, cadence, contrôle.</summary>
    public static string PrismConformant(int port) =>
        $$"""
        {
          "schema": 1,
          "id": "prism",
          "name": "PRISM",
          "type": "immersion",
          "version": "0.1.0-stub",
          "executable": { "windows": "Stub.Prism.exe", "linux": "Stub.Prism", "path": "Stub.Prism" },
          "capabilities": ["headless", "snapshotStream", "renderCadence"],
          "endpoints": {
            "control": { "transport": "http", "port": {{port}} },
            "ws": { "transport": "websocket", "launchArgument": "--data-port" }
          },
          "health": { "probe": "http", "path": "/health/ready", "intervalMs": 1000 },
          "timeouts": { "startupMs": 30000, "shutdownMs": 15000 },
          "contributesTo": ["immersion"]
        }
        """;

    /// <summary>PRISM non conforme : type muet sur le mode, capacités de rendu absentes.</summary>
    public const string PrismNonConformant =
        """
        {
          "schema": 1,
          "id": "prism",
          "name": "PRISM",
          "type": "service",
          "version": "0.1.0-stub",
          "executable": { "windows": "Stub.Prism.exe", "linux": "Stub.Prism", "path": "Stub.Prism" },
          "capabilities": ["headless"],
          "endpoints": { "control": { "transport": "http" } },
          "health": { "probe": "http", "path": "/health/ready", "intervalMs": 1000 },
          "timeouts": { "startupMs": 30000, "shutdownMs": 15000 },
          "contributesTo": ["analyse"]
        }
        """;
}
