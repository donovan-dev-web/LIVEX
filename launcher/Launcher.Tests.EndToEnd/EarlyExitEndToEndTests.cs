using Xunit;
using Xunit.Sdk;

namespace Launcher.Tests.EndToEnd;

/// <summary>
/// Arrêt immédiat au démarrage : un composant qui meurt dans la foulée de son lancement
/// (port repris entre le pré-vol et le bind, exécutable défaillant) publie sa fin de façon
/// asynchrone, souvent avant même que son instance soit enregistrée. Cette notification est
/// unique : si elle est perdue, l'interface affiche une carte « Démarrage » qui ne finit
/// jamais, le port reste réservé et le composant ne peut plus être relancé.
/// </summary>
[Collection("Composition")]
public sealed class EarlyExitEndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"livex-early-exit-{Guid.NewGuid():N}");

    [Fact]
    public async Task Un_composant_qui_s_arrete_immediatement_ne_laisse_aucune_instance()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw SkipException.ForSkip("Le composant de ce banc est un script shell.");
        }

        var componentsParent = Path.Combine(_root, "components");
        var location = Path.Combine(componentsParent, "syne");
        Directory.CreateDirectory(location);

        var executable = Path.Combine(location, "exit-now");
        await File.WriteAllTextAsync(executable, "#!/bin/sh\nexit 3\n");
        File.SetUnixFileMode(executable,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        await File.WriteAllTextAsync(Path.Combine(location, "component.json"), Manifest);

        using var composition = new Launcher.App.Composition.LauncherComposition(
            Path.Combine(_root, "packages"),
            componentsParent,
            Path.Combine(_root, "data"));

        _ = await composition.Facade.ToggleComponentAsync("syne", true);

        // L'instance ne doit survivre ni dans l'un ni dans l'autre ordre d'arrivée :
        // démarrage refusé sur place, ou sortie supervisée puis nettoyée.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && composition.Orchestration.Registry.FindByComponent("syne") is not null)
        {
            await Task.Delay(50);
        }

        Assert.Null(composition.Orchestration.Registry.FindByComponent("syne"));

        // Le composant reste relançable : aucune entrée résiduelle ne le verrouille en
        // « déjà démarré » et aucun port n'est resté réservé.
        var secondAttempt = await composition.Facade.ToggleComponentAsync("syne", true);
        Assert.DoesNotContain("déjà démarré", secondAttempt ?? string.Empty);
    }

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

    private const string Manifest =
        """
        {
          "schema": 1,
          "id": "syne",
          "name": "SYNE",
          "type": "engine",
          "version": "0.0.0-exit",
          "protocolVersion": 1,
          "executable": { "path": "exit-now" },
          "capabilities": ["headless", "seed", "tickLimit"],
          "endpoints": { "control": { "transport": "http" } },
          "health": { "probe": "http", "path": "/health/ready", "intervalMs": 1000 },
          "timeouts": { "startupMs": 5000, "shutdownMs": 2000 }
        }
        """;
}
