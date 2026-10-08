using System.Text;

namespace Launcher.Application;

/// <summary>
/// Collecte des journaux techniques d'un run (EXPERIMENTS.md §6). Le moteur écrit encore ses
/// journaux par une pompe détachée (<c>ProcessManager.PumpAsync</c>) au moment où le Launcher
/// les archive : sous Windows un lecteur doit explicitement autoriser l'écrivain
/// (<c>FileShare.ReadWrite</c>), sinon la lecture échoue en violation de partage —
/// <c>File.ReadAllBytes</c> ouvre avec <c>FileShare.Read</c> et refuse l'écrivain en cours,
/// là où Linux, sans verrou obligatoire sur les fichiers, ne le montre jamais. C'est la même
/// règle que <c>SessionFileJournal.ReadShared*</c> (correctif du Lot W, étape 5 de
/// ROADMAP-V01.md) : elle a coûté la campagne J3 en CI, l'archivage d'un run pourtant réussi
/// échouant sur son propre journal.
/// Un journal illisible n'échoue jamais la collecte : il est consigné dans l'archive, pour
/// que le diagnostic d'un échec ne soit jamais remplacé par l'échec du diagnostic.
/// </summary>
public static class RunLogArchive
{
    /// <summary>Entrée d'archive qui consigne les journaux non lisibles de la collecte.</summary>
    public const string UnreadableLogName = "logs-unreadable.txt";

    /// <summary>
    /// Lit tous les journaux d'un dossier en lecture partagée. Le fichier réservé
    /// <see cref="UnreadableLogName"/> est régénéré à chaque collecte : il décrit cette
    /// collecte, pas une précédente.
    /// </summary>
    public static IReadOnlyList<(string Name, byte[] Content)> Collect(string logsDirectory)
    {
        var logs = new List<(string, byte[])>();
        if (!Directory.Exists(logsDirectory))
        {
            return logs;
        }

        var unreadable = new List<string>();
        foreach (var file in Directory.EnumerateFiles(logsDirectory))
        {
            if (Path.GetFileName(file).Equals(UnreadableLogName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                logs.Add((Path.GetFileName(file), ReadSharedBytes(file)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // L'archive d'un run ne doit jamais déterminer l'état du run : ce qui n'a pas
                // pu être lu est écrit dans l'archive, avec sa cause.
                unreadable.Add($"{Path.GetFileName(file)} : {exception.Message}");
            }
        }

        if (unreadable.Count > 0)
        {
            logs.Add((UnreadableLogName, Encoding.UTF8.GetBytes(string.Join("\n", unreadable) + "\n")));
        }

        return logs;
    }

    /// <summary>Lecture partagée : l'écrivain en cours est explicitement autorisé.</summary>
    private static byte[] ReadSharedBytes(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, FileOptions.SequentialScan);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
