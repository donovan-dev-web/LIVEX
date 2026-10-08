using Launcher.Infrastructure;
using Xunit;
using Xunit.Sdk;

namespace Launcher.Tests.Integration.Infrastructure;

/// <summary>
/// Journal de session : le fichier reste ouvert en écriture tant que le Launcher tourne.
/// Sous Windows un lecteur doit explicitement autoriser l'écrivain (<c>FileShare.ReadWrite</c>),
/// sinon il échoue en violation de partage — c'était le cas de l'export des journaux et du
/// panneau « journaux récents », jamais rafraîchi alors que Linux, sans verrou obligatoire,
/// ne le montrait pas (correctif du Lot W, étape 5 de ROADMAP-V01.md).
/// </summary>
public sealed class SessionFileJournalTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"livexp-journal-{Guid.NewGuid():N}");

    /// <summary>La lecture partagée voit les entrées écrites sans interrompre l'écrivain.</summary>
    [Fact]
    public async Task Lecture_partagée_pendant_l_ecriture_renvoie_les_entrees_et_laisse_l_ecrivain_vivant()
    {
        using (var journal = new SessionFileJournal(_directory))
        {
            journal.Info("Export", "entrée écrite sous verrou");
            var path = Directory.EnumerateFiles(_directory, "launcher-session-*.ndjson").Single();

            // La lecture partagée est le chemin de l'export (OrchestrationFacade) et des journaux
            // récents : elle doit voir l'entrée sans fermer ni voler le fichier à l'écrivain.
            var lines = SessionFileJournal.ReadSharedLines(path).ToArray();
            Assert.Contains(lines, line => line.Contains("entrée écrite sous verrou", StringComparison.Ordinal));

            var content = await SessionFileJournal.ReadSharedTextAsync(path);
            Assert.Contains("entrée écrite sous verrou", content, StringComparison.Ordinal);

            // L'écrivain n'a pas été dépossédé : la suite de la session s'écrit toujours.
            journal.Warn("Export", "seconde entrée après lecture");
            lines = SessionFileJournal.ReadSharedLines(path).ToArray();
            Assert.Contains(lines, line => line.Contains("seconde entrée après lecture", StringComparison.Ordinal));
        }

        // Journal fermé : tout lecteur standard relit la totalité des deux entrées.
        var closed = Directory.EnumerateFiles(_directory, "launcher-session-*.ndjson")
            .SelectMany(SessionFileJournal.ReadSharedLines);
        Assert.Equal(2, closed.Count());
    }

    /// <summary>Règle de plateforme qui motive la lecture partagée (constatée sur Windows).</summary>
    [WindowsOnlyFact]
    public void Sous_Windows_un_lecteur_naif_echoue_pendant_lecriture()
    {
        using (var journal = new SessionFileJournal(_directory))
        {
            journal.Info("Export", "entrée sous verrou");
            var path = Directory.EnumerateFiles(_directory, "launcher-session-*.ndjson").Single();

            // File.ReadAllText / File.ReadLines ouvrent avec FileShare.Read : l'écrivain en
            // cours est refusé. C'est exactement l'échec qu'élimine la lecture partagée.
            Assert.Throws<IOException>(() => File.ReadAllText(path));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}

/// <summary>
/// <c>[Fact]</c> exécuté seulement sous Windows. Le saut est posé <b>à la découverte</b>
/// (propriété <c>Skip</c> de l'attribut) et non levé en cours de test : le saut dynamique
/// (<c>SkipException.ForSkip</c>) n'est pas converti en « ignoré » par la pile
/// xunit 2.9.3 + vstest, il ferait échouer le job Linux de la CI.
/// </summary>
internal sealed class WindowsOnlyFactAttribute : FactAttribute
{
    /// <summary>Initialise l'attribut, sauté hors Windows.</summary>
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Linux n'a pas de verrou obligatoire sur les fichiers : le constat est Windows.
            Skip = "le verrou de partage est une règle Windows";
        }
    }
}
