using Launcher.Domain.Model;
using Launcher.Package;
using Launcher.Protocol;
using Launcher.Protocol.Model;
using Xunit;

namespace Launcher.Tests.Unit.Package;

/// <summary>
/// Régression du plafond « byte[] &lt; 2 Gio » : une donnée de campagne
/// (<c>stream.jsonl</c>) dépasse <c>int.MaxValue</c> (mesuré en conditions réelles :
/// 2 337 158 145 o pour 2500 ticks × 50 agents) et l'archivage échouait avec
/// « The file is too long. This operation is currently limited to supporting files
/// less than 2 gigabytes in size. » — <c>File.ReadAllBytes</c>, l'intégrité hachée
/// depuis un <c>byte[]</c> et le scellement qui replaçait chaque entrée dans un
/// <c>MemoryStream</c> partageaient tous la même borne.
///
/// <para>Le test promeut la chaîne complète avec une source fichier réelle au-dessus
/// de la borne : écriture en flux, hachage en flux, scellement en flux, vérification
/// d'intégrité après relecture.</para>
/// </summary>
public sealed class LargeDataEntryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"livexp-large-{Guid.NewGuid():N}");

    public LargeDataEntryTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Entree_de_donnees_au_dela_de_2_Gio_est_archivee_scellee_et_verifiablee()
    {
        // Une unité au-dessus de la borne : c'est exactement cette taille qui plantait.
        const long Size = (long)int.MaxValue + 1;
        Assert.True(Size > int.MaxValue);

        var dataPath = Path.Combine(_directory, "stream.jsonl");
        WriteIncompressible(dataPath, Size);

        var definition = new ExperimentDefinition
        {
            Id = "EXP-LARGE-001",
            Title = "Campagne hors borne byte[]",
            Profile = WellKnownProfiles.Analyse,
            Simulation = "ecosystem_01",
            RunCount = 1,
            Ticks = 2500,
            SeedStrategy = SeedStrategy.Derived,
            BaseSeed = 1000,
            FailurePolicy = FailurePolicy.Continue,
        };

        var packagePath = Path.Combine(_directory, "EXP-LARGE-001.livexp");
        using (var writer = LivexPackageWriter.CreateNew(packagePath, definition, "0.1.0", DateTimeOffset.UnixEpoch))
        {
            writer.CompleteRun(
                "RUN-0001",
                new RunIndexEntry { RunId = "RUN-0001", Status = RunStatuses.Termine, Seed = 1000 },
                "{\"runId\":\"RUN-0001\"}",
                "{\"config\":\"resolved\"}",
                new Dictionary<string, RunDataFile> { ["data/stream.jsonl"] = RunDataFile.FromFile(dataPath) },
                Array.Empty<(string, byte[])>(),
                JournalLine.Pack(DateTimeOffset.UnixEpoch, "run_completed", "run terminé", "RUN-0001"));

            // Le scellement replaçait chaque entrée dans un MemoryStream : il devait
            // échouer au même endroit que la collecte, il doit aujourd'hui réussir.
            writer.Seal(DateTimeOffset.UnixEpoch);
        }

        using var reader = new LivexPackageReader(packagePath);
        Assert.Equal(PackageStates.Sealed, reader.Manifest.State);
        Assert.True(
            reader.VerifyRunIntegrity("RUN-0001", out var problems),
            string.Join("; ", problems));
    }

    /// <summary>
    /// Écrit <paramref name="size"/> octets pseudo-aléatoires (graine fixe) sans tenir le
    /// contenu en mémoire. Données quasi incompressibles : le ratio de décompression reste
    /// très en deçà du plafond zip-bomb 100:1, comme un vrai <c>stream.jsonl</c> — seuls
    /// les octets « zéros » déclencheraient à tort cette garde légitime.
    /// </summary>
    private static void WriteIncompressible(string path, long size)
    {
        var random = new Random(12345);
        var chunk = new byte[1 << 20];
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        long written = 0;
        while (written < size)
        {
            random.NextBytes(chunk);
            var count = (int)Math.Min(chunk.Length, size - written);
            stream.Write(chunk, 0, count);
            written += count;
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Meilleur effort : le nettoyage d'un fichier de 2 Gio ne doit pas masquer l'issue du test.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
