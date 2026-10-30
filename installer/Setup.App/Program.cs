// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using Avalonia;
using Setup.App.CommandLine;
using Setup.App.Localization;
using Setup.App.Diagnostics;
using Setup.App.Services;
using Avalonia.Logging;
using Setup.App.ViewModels;
using Setup.Core.CommandLine;
using Setup.Core.Execution;
using Setup.Core.Lifecycle;
using Setup.Core.Planning;

namespace Setup.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch
        {
            // Console redirigée : l'encodage d'affichage importe peu.
        }

        var options = AppOptions.Parse(args);
        App.Options = options;

        Strings.Instance.SetLanguage(options.Language);
        try
        {
            CultureInfo.CurrentUICulture = Strings.Instance.Culture;
        }
        catch
        {
            // Culture inconnue : la langue de repli reste le français.
        }

        if (options.Help)
        {
            PrintUsage();
            return 0;
        }

        if (options.Unknown.Count > 0)
        {
            foreach (var unknown in options.Unknown)
            {
                Console.Error.WriteLine($"[Error] Option inconnue : {unknown}");
            }

            Console.Error.WriteLine("Utilisez --help pour la liste des options.");
            return ExitCodes.UsageError;
        }

        if (options.Verbose)
        {
            Logger.Sink = new ConsoleLogSink();
        }

        var session = InstallerSession.Create(options);
        App.Session = session;

        if (options.Version)
        {
            PrintVersion(session);
            return 0;
        }

        if (options.Check)
        {
            return RunCheck(session, options);
        }

        if (options.Core.Uninstall)
        {
            return RunUninstall(session, options).GetAwaiter().GetResult();
        }

        if (options.Core.Repair)
        {
            return RunRepair(session, options).GetAwaiter().GetResult();
        }

        if (options.Core.Unattended)
        {
            return RunUnattended(session, options).GetAwaiter().GetResult();
        }

        App.InitialStep = options.Core.Maintenance ? WizardStep.Maintenance : WizardStep.Welcome;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static int RunCheck(InstallerSession session, AppOptions options)
    {
        var request = new InstallRequest(
            session.Manifest,
            session.DefaultInstallDir,
            session.DefaultDataRoot,
            session.DefaultWorkspaceDir,
            session.Manifest.Components.Where(c => c.Mandatory || c.Id == "prism").Select(c => c.Id).ToArray(),
            AcceptLicense: true,
            CreateDesktopShortcut: false,
            LaunchAfterInstall: false,
            DryRun: true,
            Simulated: session.IsDemo,
            OfflineAssetsDir: options.Core.OfflineAssetsDir,
            NoNetwork: options.NoNetwork,
            PayloadRoot: PayloadRoot());

        var result = session.PreflightAsync(request).GetAwaiter().GetResult();
        foreach (var check in result.Checks)
        {
            Console.WriteLine($"[{check.Status}] {check.Name}{(string.IsNullOrEmpty(check.Message) ? string.Empty : ": " + check.Message)}");
        }

        if (session.IsDemo)
        {
            Console.WriteLine("(monde fictif : aucun contrôle réel n'a été effectué)");
        }

        Console.WriteLine(result.Success ? "PREFLIGHT_OK" : "PREFLIGHT_FAIL");
        return result.Success ? 0 : 2;
    }

    private static async Task<int> RunUninstall(InstallerSession session, AppOptions options)
    {
        // Une désinstallation réelle ne nécessite pas la distribution d'origine,
        // mais elle ne doit jamais être simulée en silence : --demo est refusé.
        if (options.Demo)
        {
            Console.Error.WriteLine(
                "[Error] Le monde fictif est interdit en mode non interactif : aucune désinstallation simulée ne peut rapporter un succès.");
            return ExitCodes.PreflightFailed;
        }

        var engine = new InstallerEngine();
        var detection = await engine.DetectAsync();
        if (!detection.IsInstalled || detection.Record is null)
        {
            Console.WriteLine("[Error] Aucune installation LIVEX détectée : rien à désinstaller.");
            return ExitCodes.PreflightFailed;
        }

        var record = detection.Record;
        var scope = options.Core.Scope.ToLowerInvariant() switch
        {
            "b" => UninstallScope.SoftwareAndConfig,
            "c" => UninstallScope.SoftwareConfigAndWorkspace,
            _ => UninstallScope.Software,
        };

        var request = new UninstallRequest(
            record,
            scope,
            InventoryStore.ReadEntries(record.InstallDir),
            WorkspaceDeletionConfirmed: scope != UninstallScope.SoftwareConfigAndWorkspace
                                       || options.ConfirmWorkspace,
            PurgeResearch: options.Core.PurgeResearch,
            DryRun: false,
            SimulationDelayMs: 0);

        var outcome = await HeadlessRunner.UninstallAsync(engine, request, Console.Out);
        Console.WriteLine(outcome.Message);
        return outcome.ExitCode;
    }

    private static async Task<int> RunRepair(InstallerSession session, AppOptions options)
    {
        if (options.Demo || session.IsDemo)
        {
            Console.Error.WriteLine(
                "[Error] Le monde fictif est interdit en mode non interactif, et la distribution d'origine est introuvable. " +
                "Placez distribution.json à côté de livex-setup.");
            return ExitCodes.PreflightFailed;
        }

        var engine = new InstallerEngine();
        var detection = await session.DetectAsync();
        if (!detection.IsInstalled || detection.Record is null)
        {
            Console.WriteLine("[Error] Aucune installation LIVEX détectée : rien à réparer.");
            return ExitCodes.PreflightFailed;
        }

        var record = detection.Record;
        var maintenance = new MaintenanceRequest(
            record,
            MaintenanceOperation.Repair,
            session.Manifest,
            Inventory: session.Inventory,
            PayloadRoot: PayloadRoot(),
            DryRun: session.IsDemo);

        var outcome = await HeadlessRunner.RepairAsync(engine, maintenance, Console.Out);
        Console.WriteLine(outcome.Message);
        return outcome.ExitCode;
    }

    private static async Task<int> RunUnattended(InstallerSession session, AppOptions options)
    {
        if (options.Demo || session.IsDemo)
        {
            Console.Error.WriteLine(
                "[Error] Le monde fictif est interdit en mode non interactif : une installation simulée ne peut pas rapporter un succès. " +
                "Placez distribution.json à côté de livex-setup, ou utilisez --check --demo pour un diagnostic simulé.");
            return ExitCodes.PreflightFailed;
        }

        if (options.Core.ConfigPath is null)
        {
            Console.WriteLine("[Error] --unattended exige --config <fichier.json>.");
            return ExitCodes.PreflightFailed;
        }

        var engine = new InstallerEngine();
        var manifest = session.Manifest;

        if (!UnattendedResponsesFile.TryLoad(
                options.Core.ConfigPath, manifest, session.DefaultDataRoot, options.NoNetwork,
                out var responses, out var errors))
        {
            foreach (var error in errors)
            {
                Console.WriteLine("[Error] " + error);
            }

            return ExitCodes.PreflightFailed;
        }

        var request = responses!.ToRequest(manifest, PayloadRoot()) with
        {
            DryRun = session.IsDemo,
            Scenario = session.Scenario,
            SimulationDelayMs = 0,
        };

        var outcome = await HeadlessRunner.InstallAsync(engine, request, Console.Out);
        Console.WriteLine(outcome.Message);
        return outcome.ExitCode;
    }

    private static string? PayloadRoot()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "payload");
        return Directory.Exists(root) ? root : null;
    }

    private static string Version() => typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    private static void PrintVersion(InstallerSession session)
    {
        var manifest = session.Manifest;
        Console.WriteLine($"livex-setup {Version()}");
        Console.WriteLine(
            $"distribution: {manifest.Product} {manifest.Version} (profil {manifest.Profile}, canal {manifest.Channel})" +
            (session.IsDemo ? " — monde fictif" : string.Empty));
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            Utilisation : livex-setup [options]

              --demo                     Parcours complet avec le monde fictif (aucune écriture)
              --demo-scenario <nom>      fresh (défaut), existing, prism ou failure
              --check                    Contrôles préalables sans interface, sans écriture
              --lang <fr|en>             Langue de l'assistant
              --unattended --config <f>  Installation sans assistance
              --uninstall [--scope a|b|c] [--purge-research] [--confirm-workspace]
              --repair                   Vérification et restauration
              --maintenance              Parcours de maintenance
              --offline-assets <dir>     Actifs facultatifs lus depuis un répertoire local
              --no-network               Aucune requête réseau autorisée
              --verbose                  Trace de diagnostic sur la sortie d'erreur
              --version                  Version de l'installateur et de la distribution
              --help                     Cette aide

            Codes de sortie : 0 succès, 1 erreur d'usage, 2 préflight en échec ou
            opération annulée, 3 échec d'installation, 4 désinstallation partielle,
            5 installation déjà présente, 6 actif facultatif indisponible.
            """);
    }
}
