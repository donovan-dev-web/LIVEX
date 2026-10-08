using System.Net;
using System.Net.Sockets;
using Launcher.Tests.Integration.Infrastructure;
using Xunit;

namespace Launcher.Tests.EndToEnd;

/// <summary>
/// Installe un stub comme composant détectable (binaire + component.json) sous une racine
/// donnée, ouvre un port libre éphémère pour déclarer un point d'accès déterministe.
/// Même recette que l'installation manuelle de CampaignEndToEndTests, mutualisée.
/// </summary>
public static class StubInstall
{
    /// <summary>Copie les fichiers du stub compilé et écrit son manifeste. Renvoie l'emplacement.</summary>
    public static string Install(string parentDirectory, string componentId, string stubName, string manifestJson)
    {
        var location = Path.Combine(parentDirectory, componentId);
        Directory.CreateDirectory(location);

        var stubSource = StubLocator.StubPath(stubName);
        var stubSourceDirectory = Path.GetDirectoryName(stubSource)!;
        foreach (var file in Directory.GetFiles(stubSourceDirectory, $"{stubName}*"))
        {
            var target = Path.Combine(location, Path.GetFileName(file));
            File.Copy(file, target, overwrite: true);
            if (!OperatingSystem.IsWindows() && Path.GetExtension(file) is "." or "")
            {
                File.SetUnixFileMode(target, ExecutableMode);
            }
        }

        // L'apphost Linux n'a aucune extension : ses droits d'exécution sont posés nommément,
        // indépendamment de la façon dont la copie a préservé le mode source.
        var appHost = Path.Combine(location, OperatingSystem.IsWindows() ? $"{stubName}.exe" : stubName);
        if (!OperatingSystem.IsWindows() && File.Exists(appHost))
        {
            File.SetUnixFileMode(appHost, ExecutableMode);
        }

        File.WriteAllText(Path.Combine(location, "component.json"), manifestJson);
        return location;
    }

    /// <summary>Port libre éphémère de la boucle locale (jamais de collision entre bancs).</summary>
    public static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>Attend que /health/ready du point de contrôle réponde (INTEGRATION_CONTRACT.md §7).</summary>
    public static async Task WaitForHealthyAsync(string controlUrl)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync(new Uri(new Uri(controlUrl), "/health/ready"));
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Démarrage en cours : on retente.
            }
            catch (TaskCanceledException)
            {
                // Sous Windows, une connexion vers un port encore sans écoute ne rend pas
                // « refus » immédiatement : la pile TCP retransmet le SYN ~2 s avant
                // ECONNREFUSED (« slow TCP connect on Windows », daniel.haxx.se, 14/08/2024),
                // soit exactement le délai d'attente du client — le délai perçu est donc un
                // expiration de requête, pas une annulation. C'est aussi le démarrage en cours :
                // on retente, jusqu'au délai global de 20 s ci-dessous.
            }

            await Task.Delay(150);
        }

        Assert.Fail($"point de contrôle injoignable : {controlUrl}");
    }

    private const UnixFileMode ExecutableMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
}
