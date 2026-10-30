// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Setup.Core.Distribution;
using Setup.Core.Execution;
using Setup.Core.Lifecycle;
using Setup.Core.Planning;
using Setup.Core.Platform;
using Setup.Core.Preferences;
using Setup.Core.Simulation;
using Setup.Tests.Support;
using Xunit;

namespace Setup.Tests.Integration;

/// <summary>
/// Ces tests écrivent réellement sur le disque, dans un répertoire temporaire
/// propre à chaque test. L'intégration système est simulée et l'enregistrement
/// vit en mémoire : ni registre, ni raccourci, ni entrée « Applications » ne
/// sont touchés sur la machine qui exécute les tests.
/// </summary>
public class InstallFlowTests : IDisposable
{
    private readonly string _root;
    private readonly FakePathPolicy _policy;
    private readonly SimulatedIntegration _integration = new();
    private readonly InMemoryInstallationStore _store = new();
    private readonly InstallerEngine _engine;

    public InstallFlowTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "livex-it-" + Guid.NewGuid().ToString("N"));
        _policy = new FakePathPolicy
        {
            InstallDir = Path.Combine(_root, "app"),
            DataDir = Path.Combine(_root, "data"),
            ShortcutDir = Path.Combine(_root, "startmenu"),
            DesktopDir = Path.Combine(_root, "desktop")
        };
        _engine = new InstallerEngine(
            _policy, new FakeProcessProbe(), new FakePrerequisiteProbe(), new NoopFileSync(),
            _integration, _store);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        catch { /* nettoyage au mieux */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Installation_Reelle_Ecrit_Fichiers_Identite_Inventaire_Et_Preferences()
    {
        var (manifest, payload) = CreatePayload();
        var request = RealRequest(manifest, payload);

        var preflight = await _engine.PreflightAsync(request);
        Assert.True(preflight.Success, string.Join("; ", preflight.Errors));

        var plan = await _engine.PlanAsync(request);
        var result = await _engine.ExecuteAsync(plan, request);

        Assert.True(result.Success, string.Join("; ", result.Residuals));
        Assert.False(result.Simulated);

        Assert.True(File.Exists(Path.Combine(_policy.InstallDir, "livex-launcher.exe")));
        Assert.True(File.Exists(Path.Combine(_policy.InstallDir, "components", "syne", "syne.dll")));
        Assert.True(File.Exists(Path.Combine(_policy.InstallDir, ".livex-install.json")));
        Assert.True(File.Exists(Path.Combine(_policy.InstallDir, ".livex-inventory.json")));
        Assert.True(File.Exists(Path.Combine(_policy.InstallDir, ".livex-distribution.json")));
        Assert.True(Directory.Exists(request.WorkspaceDir));

        var identity = JsonSerializer.Deserialize<InstallationRecord>(
            File.ReadAllText(Path.Combine(_policy.InstallDir, ".livex-install.json")));
        Assert.NotNull(identity);
        Assert.Equal(manifest.Version, identity.Version);
        Assert.Equal(request.WorkspaceDir, identity.WorkspacePath);

        var inventory = JsonSerializer.Deserialize<InstallationInventory>(
            File.ReadAllText(Path.Combine(_policy.InstallDir, ".livex-inventory.json")));
        Assert.NotNull(inventory);
        Assert.Equal(manifest.Files.Count, inventory.Files.Count);

        var settings = WorkspaceSettings.Read(Path.Combine(_policy.DataDir, "settings.json"));
        Assert.Equal(request.WorkspaceDir, settings.WorkspacePath);

        Assert.Contains("association:.livexp", _integration.Calls);
        Assert.Contains("menu:LIVEX", _integration.Calls);
        Assert.DoesNotContain("bureau:LIVEX", _integration.Calls);
        Assert.NotNull(_store.Read());
    }

    [Fact]
    public async Task Desinstallation_Portee_Logiciel_Supprime_Le_Logiciel_Et_Conserve_Les_Donnees()
    {
        var (manifest, payload) = CreatePayload();
        var request = RealRequest(manifest, payload);
        var plan = await _engine.PlanAsync(request);
        Assert.True((await _engine.ExecuteAsync(plan, request)).Success);

        var record = _store.Read();
        Assert.NotNull(record);

        var inventory = JsonSerializer.Deserialize<InstallationInventory>(
            File.ReadAllText(Path.Combine(_policy.InstallDir, ".livex-inventory.json")))!;

        var uninstall = UninstallPlanner.Plan(new UninstallRequest(
            record, UninstallScope.Software, inventory.Files.Select(f => new FileEntry(f.Path, f.Size, f.Sha256)).ToArray(),
            DryRun: false));

        var result = await _engine.ExecuteUninstallAsync(uninstall, new UninstallRequest(
            record, UninstallScope.Software, Array.Empty<FileEntry>(), DryRun: false));

        Assert.True(result.Success, string.Join("; ", result.Residuals));
        Assert.False(File.Exists(Path.Combine(_policy.InstallDir, "livex-launcher.exe")));
        Assert.False(File.Exists(Path.Combine(_policy.InstallDir, ".livex-install.json")));
        Assert.True(File.Exists(Path.Combine(_policy.DataDir, "settings.json")));
        Assert.True(Directory.Exists(request.WorkspaceDir));
    }

    [Fact]
    public async Task Un_Fichier_Etranger_Dans_Le_Repertoire_Logiciel_Est_Conserve_Et_Signale()
    {
        var (manifest, payload) = CreatePayload();
        var request = RealRequest(manifest, payload);
        Assert.True((await _engine.ExecuteAsync(await _engine.PlanAsync(request), request)).Success);

        var foreign = Path.Combine(_policy.InstallDir, "notes-utilisateur.txt");
        await File.WriteAllTextAsync(foreign, "à conserver");

        var record = _store.Read()!;
        var uninstall = UninstallPlanner.Plan(new UninstallRequest(
            record, UninstallScope.Software, ReadInventory(), DryRun: false));

        var result = await _engine.ExecuteUninstallAsync(uninstall, new UninstallRequest(
            record, UninstallScope.Software, Array.Empty<FileEntry>(), DryRun: false));

        Assert.True(result.Success, string.Join("; ", result.Residuals));
        Assert.True(File.Exists(foreign));
        Assert.Contains(result.Warnings, warning => warning.Contains("conservé", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Une_Empreinte_Invalide_Echoue_Et_Nettoie_Les_Fichiers_Crees()
    {
        var (manifest, payload) = CreatePayload();
        // Corrompt un fichier de la charge utile sans toucher au manifeste.
        await File.WriteAllTextAsync(Path.Combine(payload, "components", "syne", "syne.dll"), "corrompu");

        var request = RealRequest(manifest, payload);
        var plan = await _engine.PlanAsync(request);
        var result = await _engine.ExecuteAsync(plan, request);

        Assert.False(result.Success);
        Assert.Contains(result.Residuals, residual => residual.Contains("Empreinte invalide", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(_policy.InstallDir, "components", "syne", "syne.dll")));
    }

    [Fact]
    public async Task L_Ajout_D_un_Composant_Fusionne_L_Inventaire()
    {
        var (manifest, payload) = CreatePayload();
        var request = RealRequest(manifest, payload);
        Assert.True((await _engine.ExecuteAsync(await _engine.PlanAsync(request), request)).Success);

        var record = _store.Read()!;
        var extra = CreateExtraComponent();
        Directory.CreateDirectory(Path.Combine(payload, "components", "extra"));
        await File.WriteAllTextAsync(Path.Combine(payload, "components", "extra", "extra.dll"), "extra-payload");
        var maintenance = MaintenancePlanner.Plan(new MaintenanceRequest(
            record, MaintenanceOperation.ManageComponents,
            Manifest: manifest with { Components = manifest.Components.Append(extra).ToArray() },
            AddComponents: new[] { extra.Id },
            PayloadRoot: payload));

        Assert.True((await _engine.ExecuteAsync(maintenance, request)).Success);

        var inventory = JsonSerializer.Deserialize<InstallationInventory>(
            File.ReadAllText(Path.Combine(_policy.InstallDir, ".livex-inventory.json")))!;

        Assert.Contains(inventory.Files, entry => entry.Path == "components/extra/extra.dll");
        Assert.Contains(inventory.Files, entry => entry.Path == "livex-launcher.exe");
        Assert.True(File.Exists(Path.Combine(_policy.InstallDir, "components", "extra", "extra.dll")));
    }

    [Fact]
    public async Task Une_Configuration_Invalide_Est_Remplacee_Sans_Perte_Des_Cles_Inconnues()
    {
        Directory.CreateDirectory(_policy.DataDir);
        var settingsPath = Path.Combine(_policy.DataDir, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{\"workspace\":\"ancien\",\"theme\":\"sombre\"}");

        var (manifest, payload) = CreatePayload();
        var request = RealRequest(manifest, payload);
        Assert.True((await _engine.ExecuteAsync(await _engine.PlanAsync(request), request)).Success);

        var settings = WorkspaceSettings.Read(settingsPath);
        Assert.Equal(request.WorkspaceDir, settings.WorkspacePath);
        Assert.True(settings.Extra.ContainsKey("theme"));
    }

    // ------------------------------------------------------------- utilitaires

    private InstallRequest RealRequest(DistributionManifest manifest, string payload) =>
        new(
            manifest,
            _policy.InstallDir,
            _policy.DataDir,
            Path.Combine(_root, "workspace"),
            new[] { "core" },
            AcceptLicense: true,
            CreateDesktopShortcut: false,
            LaunchAfterInstall: true,
            DryRun: false,
            OfflineAssetsDir: null,
            NoNetwork: true,
            PayloadRoot: payload,
            Scenario: DemoScenario.FreshInstall,
            SimulationDelayMs: 0);

    private IReadOnlyList<FileEntry> ReadInventory() =>
        JsonSerializer.Deserialize<InstallationInventory>(
            File.ReadAllText(Path.Combine(_policy.InstallDir, ".livex-inventory.json")))!
            .Files.Select(f => new FileEntry(f.Path, f.Size, f.Sha256)).ToArray();

    private static ComponentDescriptor CreateExtraComponent()
    {
        var bytes = Encoding.UTF8.GetBytes("extra-payload");
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new ComponentDescriptor(
            "extra", "1.0.0", false, "Extra", "Extra", "Composant de test", "Test component",
            "GPL-3.0-or-later", Array.Empty<string>(),
            new[] { new FileEntry("components/extra/extra.dll", bytes.Length, sha) },
            new AcquisitionDescriptor("payload", null, null, null, null, null, null, null));
    }

    private (DistributionManifest Manifest, string PayloadRoot) CreatePayload()
    {
        var payload = Path.Combine(_root, "payload");
        Directory.CreateDirectory(payload);

        var sources = new (string Path, string Content)[]
        {
            ("livex-launcher.exe", "launcher-binaire"),
            ("components/syne/syne.dll", "syne-binaire"),
            ("assets/livex.ico", "icone")
        };

        var files = new List<FileEntry>();
        foreach (var source in sources)
        {
            var bytes = Encoding.UTF8.GetBytes(source.Content);
            var target = Path.Combine(payload, source.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, bytes);
            files.Add(new FileEntry(
                source.Path,
                bytes.Length,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
        }

        var component = new ComponentDescriptor(
            "core", "1.0.0", true, "Socle", "Core", "Composants obligatoires", "Mandatory components",
            "GPL-3.0-or-later", Array.Empty<string>(), files,
            new AcquisitionDescriptor("payload", null, null, null, null, null, null, null));

        var manifest = new DistributionManifest(
            1, "LIVEX", "1.0.0", "test", "core",
            OperatingSystem.IsWindows() ? "win-x64" : "linux-x64",
            DateTimeOffset.UtcNow,
            new[] { component },
            files,
            null);

        return (manifest, payload);
    }
}
