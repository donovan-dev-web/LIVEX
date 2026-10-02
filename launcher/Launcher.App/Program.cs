using System.Threading;
using Avalonia;
using Launcher.App.Composition;

namespace Launcher.App;

/// <summary>
/// Point d'entrée du Launcher (README docs-launcher) :
/// livex-launcher                 fenêtre native, ouverture de l'espace de travail
/// livex-launcher --check         vérification de l'environnement, sans interface
/// livex-launcher --package X     ouverture d'une campagne
/// </summary>
internal static class Program
{
    /// <summary>Code de sortie : environnement conforme.</summary>
    private const int ExitOk = 0;
    /// <summary>Code de sortie : diagnostic en échec.</summary>
    private const int ExitCheckFailed = 2;

    [STAThread]
    public static int Main(string[] args)
    {
        // Instance unique : le second Launcher le dit explicitement (INTEGRATION_CONTRACT.md §5.3).
        if (!SingleInstanceGuard.TryAcquire(out var guard))
        {
            Console.Error.WriteLine("livex-launcher : une instance est déjà en cours d'exécution sur cet espace de travail.");
            return ExitCheckFailed;
        }

        using (guard)
        {
            if (args.Contains("--check"))
            {
                return RunEnvironmentCheck();
            }

            var packageArgument = ExtractPackage(args);
            BuildAvaloniaApp(packageArgument).StartWithClassicDesktopLifetime(args);
            return ExitOk;
        }
    }

    /// <summary>Construit l'application Avalonia avec la composition du Launcher.</summary>
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(null);

    /// <summary>Construit l'application en ouvrant directement un paquet (--package).</summary>
    public static AppBuilder BuildAvaloniaApp(string? packageToOpen)
    {
        var composition = new LauncherComposition();
        return AppBuilder.Configure(() => new App(composition, packageToOpen))
            .UsePlatformDetect()
            .LogToTrace();
    }

    private static string? ExtractPackage(string[] args)
    {
        var index = Array.IndexOf(args, "--package");
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>
    /// Diagnostic sans interface (PACKAGING.md §6) : sortie textuelle et stable,
    /// citable dans un rapport d'incident. N'écrit aucun journal de paquet.
    /// </summary>
    private static int RunEnvironmentCheck()
    {
        var checks = EnvironmentChecker.Run();
        foreach (var check in checks)
        {
            Console.WriteLine(EnvironmentChecker.Format(check));
        }

        return checks.All(c => c.Passed) ? ExitOk : ExitCheckFailed;
    }
}
