using System.Text;
using Launcher.Application;
using Xunit;

namespace Launcher.Tests.Integration.Infrastructure;

/// <summary>
/// Archivage des journaux d'un run : la pompe détachée du moteur
/// (<c>ProcessManager.PumpAsync</c>) peut encore tenir <c>stdout.log</c> quand le Launcher
/// l'archive. Sous Windows un lecteur doit explicitement autoriser l'écrivain
/// (<c>FileShare.ReadWrite</c>), sinon l'archivage échoue en violation de partage — c'est ce
/// qui a fait échouer la reprise de J3 en CI, un run réussi devenant Échoué sur son propre
/// journal, là où Linux, sans verrou obligatoire, ne le montrait jamais (règle du Lot W,
/// étape 5 de ROADMAP-V01.md).
/// </summary>
public sealed class RunLogArchiveTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"livexp-runlog-{Guid.NewGuid():N}");

    public RunLogArchiveTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>Le journal est archivé pendant que l'écrivain le tient encore ouvert.</summary>
    [Fact]
    public void Collect_archive_le_journal_pendant_que_lecrivain_le_tient_ouvert()
    {
        var path = Path.Combine(_directory, "stdout.log");
        using (var writer = new StreamWriter(path, append: false, Encoding.UTF8))
        {
            writer.AutoFlush = true;
            writer.WriteLine("sortie en cours");

            var open = Assert.Single(RunLogArchive.Collect(_directory));
            Assert.Equal("stdout.log", open.Name);
            Assert.Contains("sortie en cours", Encoding.UTF8.GetString(open.Content), StringComparison.Ordinal);

            // L'écrivain n'a pas été dépossédé : la pompe continue d'écrire.
            writer.WriteLine("seconde ligne");
        }

        var closed = Assert.Single(RunLogArchive.Collect(_directory));
        Assert.Contains("seconde ligne", Encoding.UTF8.GetString(closed.Content), StringComparison.Ordinal);
    }

    /// <summary>La règle de plateforme qui motive la lecture partagée (constatée sur Windows).</summary>
    [WindowsOnlyFact]
    public void Sous_Windows_une_lecture_naive_echoue_pendant_que_Collect_reussit()
    {
        var path = Path.Combine(_directory, "stdout.log");
        using (var writer = new StreamWriter(path, append: false, Encoding.UTF8))
        {
            writer.AutoFlush = true;
            writer.WriteLine("sortie");

            // File.ReadAllBytes ouvre avec FileShare.Read : l'écrivain en cours est refusé.
            // C'est exactement l'échec qu'élimine la lecture partagée de Collect.
            Assert.Throws<IOException>(() => File.ReadAllBytes(path));
            Assert.Single(RunLogArchive.Collect(_directory));
        }
    }

    /// <summary>Un journal réellement inaccessible est consigné, jamais fatal à la collecte.</summary>
    [WindowsOnlyFact]
    public void Sous_Windows_un_journal_inaccessible_est_consigne_et_ne_fait_pas_echouer_la_collecte()
    {
        var path = Path.Combine(_directory, "stdout.log");
        using (var exclusive = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            var note = RunLogArchive.Collect(_directory)
                .Single(entry => entry.Name == RunLogArchive.UnreadableLogName);
            Assert.Contains("stdout.log", Encoding.UTF8.GetString(note.Content), StringComparison.Ordinal);
        }

        // Verrou levé : le journal redevient archivable, sans entrée résiduelle de diagnostic.
        Assert.Single(RunLogArchive.Collect(_directory));
    }
}
