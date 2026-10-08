using System.Diagnostics;
using Xunit;

namespace Launcher.Tests.EndToEnd;

/// <summary>
/// L'apphost natif (`livex-launcher` / `livex-launcher.exe`) doit réellement démarrer sous
/// l'OS courant. Défaut réel du Lot W : `app.manifest` déclarait
/// <c>&lt;assembly manifestType="dualIdentity"&gt;</c> sans <c>manifestVersion</c> — le
/// chargeur Windows refusait le manifeste (SideBySide 11 puis 64, « configuration
/// côté-à-côté incorrecte ») et l'exécutable ne produisait aucune sortie, quel que soit
/// l'argument. Aucune autre suite ne couvre ce chemin : `dotnet test` invoque la DLL.
/// </summary>
public sealed class AppHostLaunchTests
{
    /// <summary>L'apphost s'exécute nativement et déroule les huit contrôles de --check.</summary>
    [Fact]
    public void L_apphost_native_demarre_et_execute_les_huit_verifications()
    {
        var appHost = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "livex-launcher.exe" : "livex-launcher");
        Assert.True(File.Exists(appHost), $"apphost introuvable : {appHost}");

        var isolatedHome = Path.Combine(Path.GetTempPath(), $"livexp-apphost-{Guid.NewGuid():N}");
        var startInfo = new ProcessStartInfo
        {
            FileName = appHost,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        // Hermétique : racine de composants vide, jamais la source standard de la machine.
        startInfo.Environment["LIVEX_HOME"] = isolatedHome;
        startInfo.ArgumentList.Add("--check");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"démarrage impossible : {appHost}");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(60_000), "l'apphost ne s'est pas arrêté dans les 60 s");

        // Un manifeste refusé par le système s'arrête avant toute sortie : ni le premier ni le
        // dernier contrôle ne sont imprimés. La sortie complète prouve que les huit se sont
        // déroulés (PACKAGING.md §6), et le code de sortie est celui d'un --check exécuté —
        // 0 si un composant est détecté sur la machine, 2 sur un poste vierge.
        Assert.Contains("exécution : livex-launcher", output, StringComparison.Ordinal);
        Assert.Contains("navigateur", output, StringComparison.Ordinal);
        Assert.True(
            process.ExitCode is 0 or 2,
            $"code de sortie inattendu : {process.ExitCode} — sortie : {output}");

        if (Directory.Exists(isolatedHome))
        {
            Directory.Delete(isolatedHome, recursive: true);
        }
    }
}
