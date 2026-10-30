// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text;
using Setup.Core.Distribution;
using Setup.Core.Lifecycle;

namespace Setup.Core.Simulation;

/// <summary>
/// Monde fictif : manifeste, inventaire et installation existante de
/// démonstration. Aucune URL réelle, aucun fichier lu, aucun accès réseau.
/// Il sert à dérouler l'assistant de bout en bout (installation, maintenance,
/// désinstallation) pour vérifier les écrans et les flux.
/// </summary>
public static class DemoWorld
{
    public const string Product = "LIVEX";
    public const string Version = "0.2.0-demo";
    public const string InstalledVersion = "0.1.0";

    /// <summary>Commande d'association des fichiers de paquet de campagne.</summary>
    public const string PackageExtension = ".livexp";

    public static DistributionManifest CreateManifest(DemoScenario scenario = DemoScenario.FreshInstall)
    {
        var components = CreateComponents(scenario);
        var files = components
            .Where(c => c.Acquisition.Kind == "payload")
            .SelectMany(c => c.Files)
            .ToArray();

        return new DistributionManifest(
            Schema: 1,
            Product: Product,
            Version: Version,
            Channel: "demo",
            Profile: "core",
            Platform: OperatingSystem.IsWindows() ? "win-x64" : "linux-x64",
            ReleasedAt: new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero),
            Components: components,
            Files: files,
            Signature: new SignatureDescriptor("Ed25519", "demo-key", "demo-signature"));
    }

    /// <summary>
    /// Installation existante utilisée par le scénario <see cref="DemoScenario.ExistingInstall"/>
    /// et par l'écran de désinstallation.
    /// </summary>
    public static InstallationRecord CreateExistingInstallation(string installDir, string dataRoot, string workspaceDir) =>
        new(Product, InstalledVersion, installDir, dataRoot, workspaceDir,
            new DateTimeOffset(2026, 9, 17, 10, 30, 0, TimeSpan.Zero));

    /// <summary>
    /// Inventaire fictif d'une installation de démonstration. Les chemins sont
    /// relatifs au répertoire logiciel, comme dans le fichier réel.
    /// </summary>
    public static IReadOnlyList<FileEntry> CreateInventory()
    {
        var manifest = CreateManifest();
        var files = manifest.Files.ToList();
        files.Add(File("assets/livex.ico", 428_614));
        files.Add(File("assets/livex.svg", 11_875));
        files.Add(File("assets/livexp.ico", 262_144));
        files.Add(File("assets/livexp.svg", 12_554));
        files.Add(File("LICENSE", 35_149));
        files.Add(File("NOTICE", 1_024));
        files.Add(File("VERSION", 32));
        return files.DistinctBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Composants proposés à la sélection, dans l'ordre d'affichage.</summary>
    public static IReadOnlyList<ComponentDescriptor> CreateComponents(DemoScenario scenario = DemoScenario.FreshInstall)
    {
        var prismAvailable = scenario != DemoScenario.PrismAbsent;

        return new List<ComponentDescriptor>
        {
            new("launcher", Version, true,
                "Lanceur LIVEX", "LIVEX launcher",
                "Point d'entrée principal : détecte les composants installés et lance une expérience.",
                "Main entry point: detects installed components and starts an experiment.",
                "GPL-3.0-or-later", Array.Empty<string>(),
                new[]
                {
                    File("livex-launcher.exe", 12_845_056),
                    File("Launcher.Core.dll", 3_145_728),
                    File("Launcher.Infrastructure.dll", 896_512)
                },
                Payload()),

            new("syne", Version, true,
                "SYNE", "SYNE",
                "Moteur de simulation déterministe, exécutable en mode headless.",
                "Deterministic simulation engine, runs headless.",
                "GPL-3.0-or-later", Array.Empty<string>(),
                new[]
                {
                    File("components/syne/syne.exe", 18_350_080),
                    File("components/syne/syne.core.dll", 5_242_880),
                    File("components/syne/component.json", 1_284)
                },
                Payload()),

            new("echos", Version, true,
                "ECHOS", "ECHOS",
                "Observation, analyse et pilotage : consomme les événements de SYNE.",
                "Observation, analysis and control: consumes SYNE events.",
                "GPL-3.0-or-later", Array.Empty<string>(),
                new[]
                {
                    File("components/echos/echos-launcher.cmd", 2_048),
                    File("components/echos/component.json", 1_536),
                    File("components/echos/runtime/python.exe", 8_912_896)
                },
                Payload()),

            new("contracts", Version, true,
                "Contrats de communication", "Communication contracts",
                "Schémas JSON partagés par les composants.",
                "JSON schemas shared by the components.",
                "GPL-3.0-or-later", Array.Empty<string>(),
                new[] { File("contracts/component-manifest-v1.schema.json", 6_144) },
                Payload()),

            new("resources", Version, true,
                "Ressources", "Resources",
                "Icônes, licences et mentions légales requises par la distribution.",
                "Icons, licences and legal notices required by the distribution.",
                "GPL-3.0-or-later", Array.Empty<string>(),
                new[]
                {
                    File("assets/livex.ico", 428_614),
                    File("assets/livex.svg", 11_875),
                    File("assets/livexp.ico", 262_144),
                    File("assets/livexp.svg", 12_554),
                    File("LICENSE", 35_149),
                    File("NOTICE", 1_024)
                },
                Payload()),

            new("prism", Version, false,
                prismAvailable ? "PRISM (représentation 3D)" : "PRISM (indisponible)",
                prismAvailable ? "PRISM (3D representation)" : "PRISM (unavailable)",
                prismAvailable
                    ? "Couche de représentation 3D Godot. Sans PRISM, l'analyse reste disponible mais aucune scène 3D ne peut être ouverte."
                    : "Ce build de démonstration ne contient pas PRISM : les scènes 3D ne seront pas disponibles.",
                prismAvailable
                    ? "Godot 3D representation layer. Without PRISM, analysis still works but no 3D scene can be opened."
                    : "This demonstration build does not include PRISM: 3D scenes will not be available.",
                "GPL-3.0-or-later", Array.Empty<string>(),
                prismAvailable
                    ? new[]
                    {
                        File("components/prism/Prism.exe", 24_117_248),
                        File("components/prism/prism.pck", 41_943_040)
                    }
                    : Array.Empty<FileEntry>(),
                prismAvailable ? Payload() : Unavailable("Composant absent de ce build de démonstration.")),

            new("prism-ldk", Version, false,
                "PrismLDK (plugin Unreal)", "PrismLDK (Unreal plugin)",
                "Plugin de développement pour Unreal Engine, livré sous forme d'archive ZIP. Indépendant de PRISM.",
                "Development plugin for Unreal Engine, shipped as a ZIP archive. Independent from PRISM.",
                "GPL-3.0-or-later", Array.Empty<string>(), Array.Empty<FileEntry>(),
                ReleaseAsset("PrismLdk-0.2.0.zip", 14_680_064)),

            new("docs", Version, false,
                "Documentation hors ligne", "Offline documentation",
                "Documentation complète consultable sans connexion Internet.",
                "Full documentation, readable without an Internet connection.",
                "GPL-3.0-or-later", Array.Empty<string>(), Array.Empty<FileEntry>(),
                ReleaseAsset("livex-docs-0.2.0.zip", 9_437_184)),

            new("syne-mock", Version, false,
                "SYNE-Mock", "SYNE-Mock",
                "Émulateur de SYNE pour le développement et les tests, avec runtime Node portable.",
                "SYNE emulator for development and testing, with a portable Node runtime.",
                "GPL-3.0-or-later", Array.Empty<string>(), Array.Empty<FileEntry>(),
                ReleaseAsset("syne-mock-0.2.0.zip", 31_457_280))
        };
    }

    /// <summary>Identifiants sélectionnés par défaut : socle + PRISM, comme le produit.</summary>
    public static IReadOnlyList<string> DefaultSelection(DemoScenario scenario = DemoScenario.FreshInstall)
    {
        var ids = CreateComponents(scenario)
            .Where(c => c.Mandatory || (c.Id == "prism" && c.Acquisition.Kind != "unavailable"))
            .Select(c => c.Id)
            .ToList();
        return ids;
    }

    private static AcquisitionDescriptor Payload() => new("payload", null, null, null, null, null, null, null);

    private static AcquisitionDescriptor ReleaseAsset(string asset, long size) =>
        new("release-asset", asset, $"https://demo.invalid/{asset}", size, Hash(asset), "zip", 0, null);

    private static AcquisitionDescriptor Unavailable(string reason) =>
        new("unavailable", null, null, null, null, null, null, reason);

    private static FileEntry File(string path, long size) => new(path, size, Hash(path));

    /// <summary>Empreinte déterministe et fictive, stable entre deux exécutions.</summary>
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("LIVEX-DEMO:" + value))).ToLowerInvariant();
}
