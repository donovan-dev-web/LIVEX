// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : Emplacements de l'installation et des données.

using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Launcher.Infrastructure;

/// <summary>
/// Emplacements de l'installation et des données.
/// L'installation est remplaçable sans perte de données : les deux hiérarchies sont séparées.
/// Les chemins choisis dans l'installateur (<c>.livex-install.json</c>) font foi ;
/// à défaut, les valeurs par défaut de la plateforme s'appliquent.
/// </summary>
public static class WorkspaceLayout
{
    private const string InstallIdentityFileName = ".livex-install.json";

    /// <summary>
    /// Racine des données utilisateur. Priorité : variable <c>LIVEX_DATA</c>,
    /// puis chemin enregistré par l'installateur, puis défaut de la plateforme.
    /// </summary>
    public static string UserDataRoot()
    {
        var configured = Environment.GetEnvironmentVariable("LIVEX_DATA");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var record = ReadInstallationRecord();
        if (!string.IsNullOrWhiteSpace(record?.DataRoot))
        {
            return record.DataRoot!;
        }

        return DefaultDataRoot();
    }

    /// <summary>
    /// Racine de l'espace de travail Launcher : chemin choisi dans
    /// l'installateur, sinon <c>&lt;racine de données&gt;/workspace</c>.
    /// </summary>
    public static string WorkspaceRoot()
    {
        var record = ReadInstallationRecord();
        if (!string.IsNullOrWhiteSpace(record?.WorkspacePath))
        {
            return record.WorkspacePath!;
        }

        return Path.Combine(UserDataRoot(), "workspace");
    }

    /// <summary>Racine de l'installation quand le Launcher vit dans <c>core/launcher</c>.</summary>
    public static string? InstallationRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        if (directory.Name.Equals("launcher", StringComparison.OrdinalIgnoreCase)
            && directory.Parent?.Name.Equals("core", StringComparison.OrdinalIgnoreCase) == true)
        {
            return directory.Parent?.Parent?.FullName;
        }

        return null;
    }

    /// <summary>Racine de données par défaut de la plateforme.</summary>
    private static string DefaultDataRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "LIVEX", "data");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", "livex", "data");
    }

    /// <summary>Lit l'identité d'installation écrite par l'installateur, si elle existe.</summary>
    private static InstallationIdentity? ReadInstallationRecord()
    {
        var root = InstallationRoot();
        if (root is null)
        {
            return null;
        }

        var path = Path.Combine(root, InstallIdentityFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<InstallationIdentity>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Vue minimale du fichier d'identité de l'installateur.</summary>
    private sealed record InstallationIdentity(
        string? Product,
        string? Version,
        string? InstallDir,
        string? DataRoot,
        string? WorkspacePath);
}

/// <summary>Sonde de disponibilité d'un port : pré-vol avant tout démarrage.</summary>
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
