using System.Net;
using System.Net.Sockets;

namespace Launcher.Infrastructure;

/// <summary>
/// Emplacements de l'installation et des données (PACKAGING.md §2.2, §7).
/// L'installation est remplaçable sans perte de données : les deux hiérarchies sont séparées.
/// </summary>
public static class WorkspaceLayout
{
    /// <summary>Racine des données utilisateur. Variable LIVEX_DATA, sinon profil utilisateur.</summary>
    public static string UserDataRoot()
    {
        var configured = Environment.GetEnvironmentVariable("LIVEX_DATA");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(profile, ".livex-data");
    }

    /// <summary>Racine de l'espace de travail Launcher (DATA_FLOW.md §2).</summary>
    public static string WorkspaceRoot() => Path.Combine(UserDataRoot(), "workspace");
}

/// <summary>Sonde de disponibilité d'un port : pré-vol avant tout démarrage (NETWORK.md §6.2).</summary>
public static class TcpPortProbe
{
    /// <summary>Vrai si le port de la boucle locale est libre.</summary>
    public static bool IsFree(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
