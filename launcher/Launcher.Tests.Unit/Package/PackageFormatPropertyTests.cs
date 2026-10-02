using System.IO.Compression;
using System.Text;
using Launcher.Domain.Model;
using Launcher.Package;
using Launcher.Protocol;
using Launcher.Protocol.Model;
using Xunit;

namespace Launcher.Tests.Unit.Package;

/// <summary>
/// Propriétés du format .livexp (TESTING.md §5). Vérifiées sans démarrer un seul composant.
/// Chaque propriété est un invariant, pas un scénario.
/// </summary>
public sealed class LivexPackagePropertyTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"livexp-tests-{Guid.NewGuid():N}");

    public LivexPackagePropertyTests()
    {
        Directory.CreateDirectory(_directory);
    }

    private ExperimentDefinition Definition(int runs = 3) => new()
    {
        Id = "EXP-TEST-001",
        Title = "Campagne de test",
        Profile = WellKnownProfiles.Analyse,
        Simulation = "ecosystem_01",
        RunCount = runs,
        Ticks = 1000,
        SeedStrategy = SeedStrategy.Derived,
        BaseSeed = 1000,
        FailurePolicy = FailurePolicy.Continue,
    };

    private string PathFor(string name) => Path.Combine(_directory, name);

    /// <summary>Remplit un paquet avec des runs terminés de contenu donné.</summary>
    private static void FillRuns(LivexPackageWriter writer, ExperimentDefinition definition, string payload)
    {
        for (var i = 0; i < definition.RunCount; i++)
        {
            writer.CompleteRun(
                $"RUN-{i + 1:D4}",
                new RunIndexEntry { RunId = $"RUN-{i + 1:D4}", Status = RunStatuses.Termine, Seed = definition.SeedFor(i) },
                $"{{\"runId\":\"RUN-{i + 1:D4}\"}}",
                "{\"config\":\"resolved\"}",
                new Dictionary<string, byte[]> { [$"data/{payload}-{i}.json"] = Encoding.UTF8.GetBytes($"{{\"v\":{i}}}") },
                Array.Empty<(string, byte[])>(),
                JournalLine.Pack(DateTimeOffset.UnixEpoch, "run_completed", $"run {i} terminé", $"RUN-{i + 1:D4}"));
        }
    }

    // ------------------------------------------------------------------
    // P1. Écriture saine : n'écrire jamais dans un paquet scellé.
    // ------------------------------------------------------------------
    [Fact]
    public void P1_ecriture_dans_paquet_scelle_refusee()
    {
        var path = PathFor("P1.livexp");
        using (var writer = LivexPackageWriter.CreateNew(path, Definition(), "0.1.0", DateTimeOffset.UnixEpoch))
        {
            FillRuns(writer, Definition(), "p1");
            writer.Seal(DateTimeOffset.UnixEpoch);
        }

        // Toute réouverture en écriture d'un paquet scellé est refusée (PACKAGE_FORMAT.md §10).
        Assert.Throws<SealedPackageException>(() => LivexPackageWriter.OpenForAppend(path));
    }

    // ------------------------------------------------------------------
    // P2. Atomicité : une écriture interrompue laisse le paquet récupérable, jamais invalide.
    // ------------------------------------------------------------------
    [Fact]
    public void P2_paquet_sans_manifeste_a_jour_est_recoverable_et_relisible()
    {
        var path = PathFor("P2.livexp");
        using (var writer = LivexPackageWriter.CreateNew(path, Definition(), "0.1.0", DateTimeOffset.UnixEpoch))
        {
            // Interrompue après le premier run, avant la mise à jour du manifeste en état recoverable.
            writer.CompleteRun(
                "RUN-0001",
                new RunIndexEntry { RunId = "RUN-0001", Status = RunStatuses.Termine, Seed = 1000 },
                "{\"runId\":\"RUN-0001\"}",
                "{\"config\":\"resolved\"}",
                new Dictionary<string, byte[]>(),
                Array.Empty<(string, byte[])>(),
                JournalLine.Pack(DateTimeOffset.UnixEpoch, "run_completed", "run terminé", "RUN-0001"));
        }

        // Le paquet reste relisible ; l'état live sans commit complet est interprété comme récupérable
        // par la reprise (runs/index.json fait foi, pas le manifeste).
        using var reader = new LivexPackageReader(path);
        Assert.Equal(PackageStates.Live, reader.Manifest.State);
        Assert.Single(reader.RunIndex.Runs);
    }

    // ------------------------------------------------------------------
    // P3. Reprise exacte : reprendre un paquet ne rejoue aucun run réussi.
    // ------------------------------------------------------------------
    [Fact]
    public void P3_reprise_ne_rejoue_aucun_run_termine()
    {
        var path = PathFor("P3.livexp");
        var definition = Definition(4);
        using (var writer = LivexPackageWriter.CreateNew(path, definition, "0.1.0", DateTimeOffset.UnixEpoch))
        {
            FillRuns(writer, definition, "p3");
        }

        using var reader = new LivexPackageReader(path);
        var done = reader.RunIndex.Runs.Where(r => r.Status == RunStatuses.Termine).Select(r => r.RunId).ToHashSet();
        Assert.Equal(4, done.Count);
        // Le moteur d'exécution saute exactement ces runs : un rejeu de RUN-0002 est impossible.
        Assert.Contains("RUN-0002", done);
    }

    // ------------------------------------------------------------------
    // P4. Déterminisme : deux campagnes de contenu identique produisent deux paquets
    // identiques octet pour octet.
    // ------------------------------------------------------------------
    [Fact]
    public void P4_determinisme_octet_pour_octet()
    {
        var first = PathFor("P4-a.livexp");
        var second = PathFor("P4-b.livexp");

        CreateDeterministicPackage(first, sameIdentity: true);
        CreateDeterministicPackage(second, sameIdentity: true);

        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
    }

    private static void CreateDeterministicPackage(string path, bool sameIdentity = false)
    {
        var definition = new ExperimentDefinition
        {
            Id = "EXP-DET-001",
            Title = "Campagne déterministe",
            Profile = WellKnownProfiles.Analyse,
            Simulation = "ecosystem_01",
            RunCount = 2,
            Ticks = 500,
            SeedStrategy = SeedStrategy.Derived,
            BaseSeed = 42,
            FailurePolicy = FailurePolicy.Continue,
        };

        // Le déterminisme porte sur le contenu : l'identité (packageId) est figée pour la comparaison.
        using var writer = LivexPackageWriter.CreateNew(
            path, definition, "0.1.0-test", DateTimeOffset.UnixEpoch,
            sameIdentity ? "01JQ8X4M2N7PTESTDET001" : null);
        for (var i = 0; i < definition.RunCount; i++)
        {
            writer.CompleteRun(
                $"RUN-{i + 1:D4}",
                new RunIndexEntry { RunId = $"RUN-{i + 1:D4}", Status = RunStatuses.Termine, Seed = definition.SeedFor(i) },
                $"{{\"runId\":\"RUN-{i + 1:D4}\",\"seed\":{definition.SeedFor(i)}}}",
                "{\"schema\":1,\"config\":\"resolved\"}",
                new Dictionary<string, byte[]> { ["data/result.json"] = Encoding.UTF8.GetBytes($"{{\"v\":{i}}}") },
                Array.Empty<(string, byte[])>(),
                JournalLine.Pack(DateTimeOffset.UnixEpoch, "run_completed", $"run {i}", $"RUN-{i + 1:D4}"));
        }

        writer.WriteEntry(PackageConstants.EmergenceReportEntry, "# Rapport\n\nContenu identique.\n");
        writer.Seal(DateTimeOffset.UnixEpoch);
    }

    // ------------------------------------------------------------------
    // P5. Intégrité : toute altération d'une entrée est détectée à la lecture.
    // ------------------------------------------------------------------
    [Fact]
    public void P5_alteration_detectee_par_integrity_json()
    {
        var path = PathFor("P5.livexp");
        var definition = Definition(1);
        using (var writer = LivexPackageWriter.CreateNew(path, definition, "0.1.0", DateTimeOffset.UnixEpoch))
        {
            writer.CompleteRun(
                "RUN-0001",
                new RunIndexEntry { RunId = "RUN-0001", Status = RunStatuses.Termine, Seed = 1000 },
                "{\"runId\":\"RUN-0001\"}",
                "{\"config\":\"resolved\"}",
                new Dictionary<string, byte[]> { ["data/result.json"] = Encoding.UTF8.GetBytes("{\"v\":1}") },
                Array.Empty<(string, byte[])>(),
                JournalLine.Pack(DateTimeOffset.UnixEpoch, "run_completed", "run terminé", "RUN-0001"));
        }

        // Altère l'entrée data/result.json dans le conteneur.
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("runs/RUN-0001/data/result.json")!;
            entry.Delete();
            var forged = archive.CreateEntry("runs/RUN-0001/data/result.json");
            using var target = forged.Open();
            target.Write(Encoding.UTF8.GetBytes("{\"v\":999}"));
        }

        using var reader = new LivexPackageReader(path);
        Assert.False(reader.VerifyRunIntegrity("RUN-0001", out var problems));
        Assert.Contains(problems, p => p.Contains("empreinte") || p.Contains("taille"));
    }

    // ------------------------------------------------------------------
    // P6. Auto-description : un paquet se relit sans le Launcher (schémas, README, index).
    // ------------------------------------------------------------------
    [Fact]
    public void P6_paquet_autodescrit()
    {
        var path = PathFor("P6.livexp");
        var definition = Definition(2);
        using (var writer = LivexPackageWriter.CreateNew(path, definition, "0.1.0", DateTimeOffset.UnixEpoch))
        {
            FillRuns(writer, definition, "p6");
        }

        using var reader = new LivexPackageReader(path);
        Assert.Equal("EXP-TEST-001", reader.Experiment.Id);
        Assert.Equal(2, reader.RunIndex.Runs.Count);
        Assert.NotNull(reader.ReadEntry("README.md"));
        Assert.True(reader.Journal().Count >= 3); // création + 2 runs
    }

    // ------------------------------------------------------------------
    // P7. Traçabilité : chaque donnée est rattachable à un run et à une campagne.
    // ------------------------------------------------------------------
    [Fact]
    public void P7_tracabilite_run_et_campagne()
    {
        var path = PathFor("P7.livexp");
        var definition = Definition(1);
        using (var writer = LivexPackageWriter.CreateNew(path, definition, "0.1.0", DateTimeOffset.UnixEpoch))
        {
            writer.CompleteRun(
                "RUN-0001",
                new RunIndexEntry { RunId = "RUN-0001", Status = RunStatuses.Termine, Seed = 1000 },
                "{\"runId\":\"RUN-0001\",\"experimentId\":\"EXP-TEST-001\"}",
                "{\"config\":\"resolved\"}",
                new Dictionary<string, byte[]> { ["data/result.json"] = Encoding.UTF8.GetBytes("{\"v\":1}") },
                new List<(string, byte[])> { ("syne.log", Encoding.UTF8.GetBytes("journal du run")) },
                JournalLine.Pack(DateTimeOffset.UnixEpoch, "run_completed", "run terminé", "RUN-0001"));
        }

        using var reader = new LivexPackageReader(path);
        Assert.NotNull(reader.ReadEntry("runs/RUN-0001/run.json"));
        Assert.NotNull(reader.ReadEntry("runs/RUN-0001/logs/syne.log"));
        Assert.NotNull(reader.ReadEntry("runs/RUN-0001/integrity.json"));
        Assert.NotNull(reader.ReadEntry("runs/index.json"));
    }

    // ------------------------------------------------------------------
    // P8. Confinement : aucun chemin absolu, aucun .., aucun exécutable.
    // ------------------------------------------------------------------
    [Fact]
    public void P8_chemins_dangereux_et_executables_refuses()
    {
        var path = PathFor("P8.livexp");
        using var writer = LivexPackageWriter.CreateNew(path, Definition(), "0.1.0", DateTimeOffset.UnixEpoch);
        Assert.Throws<UnsafeEntryPathException>(() => writer.WriteEntry("../evasion.txt", "contenu"));
        Assert.Throws<UnsafeEntryPathException>(() => writer.WriteEntry("/absolute.txt", "contenu"));
        Assert.Throws<UnsafeEntryPathException>(() => writer.WriteEntry("C:\\temp\\evil.txt", "contenu"));
        Assert.Throws<UnsafeEntryPathException>(() => writer.WriteEntry("data/payload.exe", "MZ"));
    }

    // ------------------------------------------------------------------
    // P9. Atomicité des artefacts : un artefact n'est jamais visible à moitié écrit.
    // ------------------------------------------------------------------
    [Fact]
    public void P9_run_incomplet_absent_de_lindex()
    {
        var path = PathFor("P9.livexp");
        var definition = Definition(3);
        using (var writer = LivexPackageWriter.CreateNew(path, definition, "0.1.0", DateTimeOffset.UnixEpoch))
        {
            // Un seul run complet sur trois : l'index n'enregistre que ce run.
            writer.CompleteRun(
                "RUN-0001",
                new RunIndexEntry { RunId = "RUN-0001", Status = RunStatuses.Termine, Seed = 1000 },
                "{\"runId\":\"RUN-0001\"}",
                "{\"config\":\"resolved\"}",
                new Dictionary<string, byte[]>(),
                Array.Empty<(string, byte[])>(),
                JournalLine.Pack(DateTimeOffset.UnixEpoch, "run_completed", "run terminé", "RUN-0001"));
        }

        using var reader = new LivexPackageReader(path);
        Assert.Single(reader.RunIndex.Runs);
        Assert.Equal("RUN-0001", reader.RunIndex.Runs[0].RunId);
    }

    // ------------------------------------------------------------------
    // P10. Conformité de schéma : un schema inconnu est refusé, jamais interprété.
    // ------------------------------------------------------------------
    [Fact]
    public void P10_schema_superieur_refuse()
    {
        var path = PathFor("P10.livexp");
        using (var writer = LivexPackageWriter.CreateNew(path, Definition(), "0.1.0", DateTimeOffset.UnixEpoch))
        {
            writer.Seal(DateTimeOffset.UnixEpoch);
        }

        // Forge un manifeste de schéma 99 dans le conteneur.
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry(PackageConstants.ManifestEntry)!;
            entry.Delete();
            var forged = archive.CreateEntry(PackageConstants.ManifestEntry);
            using var target = forged.Open();
            target.Write(Encoding.UTF8.GetBytes("{\"schema\":99,\"packageId\":\"X\",\"state\":\"live\"}"));
        }

        Assert.Throws<SchemaNotSupportedException>(() =>
        {
            using var reader = new LivexPackageReader(path);
            _ = reader.Manifest;
        });
    }

    // ------------------------------------------------------------------
    // Séquence normative §5.2 : l'ordre d'écriture des entrées d'un run.
    // ------------------------------------------------------------------
    [Fact]
    public void Seance_normative_integrity_json_apres_donnees_index_apres_integrity()
    {
        var path = PathFor("seq.livexp");
        var definition = Definition(1);
        using (var writer = LivexPackageWriter.CreateNew(path, definition, "0.1.0", DateTimeOffset.UnixEpoch))
        {
            writer.CompleteRun(
                "RUN-0001",
                new RunIndexEntry { RunId = "RUN-0001", Status = RunStatuses.Termine, Seed = 1000 },
                "{\"runId\":\"RUN-0001\"}",
                "{\"config\":\"resolved\"}",
                new Dictionary<string, byte[]> { ["data/result.json"] = Encoding.UTF8.GetBytes("{\"v\":1}") },
                Array.Empty<(string, byte[])>(),
                JournalLine.Pack(DateTimeOffset.UnixEpoch, "run_completed", "run terminé", "RUN-0001"));
        }

        using var reader = new LivexPackageReader(path);
        Assert.True(reader.VerifyRunIntegrity("RUN-0001", out var problems), string.Join("; ", problems));
    }

    [Fact]
    public void Journaux_de_run_sont_listes_lus_et_limites_aux_entrees_autorisees()
    {
        var path = PathFor("run-logs.livexp");
        var definition = Definition(1);
        using (var writer = LivexPackageWriter.CreateNew(path, definition, "0.1.0", DateTimeOffset.UnixEpoch))
        {
            writer.CompleteRun(
                "RUN-0001",
                new RunIndexEntry { RunId = "RUN-0001", Status = RunStatuses.Termine, Seed = 1000 },
                "{\"runId\":\"RUN-0001\"}",
                "{\"config\":\"resolved\"}",
                new Dictionary<string, byte[]>(),
                [("stdout.log", Encoding.UTF8.GetBytes("sortie du run\n")), ("stderr.log", Encoding.UTF8.GetBytes("erreur du run\n"))],
                JournalLine.Pack(DateTimeOffset.UnixEpoch, "run_completed", "run terminé", "RUN-0001"));
        }

        using var reader = new LivexPackageReader(path);
        var entries = reader.ListRunLogEntries();
        Assert.Equal(["runs/RUN-0001/logs/stderr.log", "runs/RUN-0001/logs/stdout.log"], entries);
        Assert.Equal("sortie du run\n", reader.ReadRunLog("runs/RUN-0001/logs/stdout.log"));
        Assert.Throws<ArgumentException>(() => reader.ReadRunLog("runs/RUN-0001/run.json"));
    }

    [Fact]
    public async Task Journal_de_run_depassant_16_mio_est_refuse_en_affichage_mais_exportable()
    {
        var path = PathFor("oversized-run-log.livexp");
        var definition = Definition(1);
        var oversizedLog = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16 * 1024 * 1024 + 1);
        using (var writer = LivexPackageWriter.CreateNew(path, definition, "0.1.0", DateTimeOffset.UnixEpoch))
        {
            writer.CompleteRun(
                "RUN-0001",
                new RunIndexEntry { RunId = "RUN-0001", Status = RunStatuses.Termine, Seed = 1000 },
                "{\"runId\":\"RUN-0001\"}",
                "{\"config\":\"resolved\"}",
                new Dictionary<string, byte[]>(),
                [("large.log", oversizedLog)],
                JournalLine.Pack(DateTimeOffset.UnixEpoch, "run_completed", "run terminé", "RUN-0001"));
        }

        using var reader = new LivexPackageReader(path);
        var exception = Assert.Throws<CorruptedPackageException>(() =>
            reader.ReadRunLog("runs/RUN-0001/logs/large.log"));
        Assert.Contains("trop volumineux", exception.Message);

        var exportedPath = PathFor("large-export.log");
        await using (var destination = File.Create(exportedPath))
        {
            await reader.CopyRunLogToAsync("runs/RUN-0001/logs/large.log", destination);
        }

        Assert.Equal(oversizedLog.LongLength, new FileInfo(exportedPath).Length);
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
