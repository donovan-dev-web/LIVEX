// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : Non-confusion du .livexp scellé : il est structurellement distinct d'un fichier
//        SQLite (en-tête « SQLite format 3\0 ») et d'un snapshot SYNE (document JSON brut).

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Launcher.Domain.Model;
using Launcher.Package;
using Launcher.Protocol;
using Launcher.Protocol.Model;
using Xunit;

namespace Launcher.Tests.Unit.Package;

/// <summary>
/// Non-confusion du <c>.livexp</c> scellé. Un même contenu de campagne peut exister sous
/// trois formes distinctes — archive ZIP du paquet, base SQLite de persistance SYNE,
/// document JSON du snapshot SYNE — et aucune ne doit être lisible pour les raisons de
/// l'autre. Ces propriétés sont structurelles (signatures de format, extensions), pas
/// conventionnelles : elles sont donc vérifiées sur un paquet réellement scellé.
///
/// <para>ZIP : en-tête de fichier local <c>PK\x03\x04</c> (0x50 0x4B 0x03 0x04),
/// variantes <c>PK\x05\x06</c>/<c>PK\x07\x08</c> pour une archive vide ou un marqueur de
/// descripteur. SQLite : les 16 premiers octets valent <c>SQLite format 3\0</c>
/// (0x53 0x51 0x4C 0x69 0x74 ...). Un <c>.livexp</c> ne peut porter ni l'une ni l'autre.</para>
/// </summary>
public sealed class SealedPackageNonConfusionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"livexp-non-confusion-{Guid.NewGuid():N}");

    /// <summary>En-tête d'un fichier local ZIP : signature d'archive du paquet.</summary>
    private static readonly byte[] ZipLocalFileHeader = [(byte)'P', (byte)'K', 0x03, 0x04];

    /// <summary>En-tête d'une base SQLite : « SQLite format 3\0 », 16 octets.</summary>
    private static readonly byte[] SqliteFormat3Header = "SQLite format 3\0"u8.ToArray();

    /// <summary>
    /// Snapshot SYNE : <c>SimulationSnapshotCodec</c> sérialise l'état du moteur en document
    /// JSON déterministe. C'est cette forme-là, et non le paquet, qui s'ouvre par une accolade.
    /// </summary>
    private const string SyneSnapshotJson =
        "{\"schemaVersion\":2,\"runId\":\"EXP-TEST-001-RUN-0001\",\"tick\":1000,\"prngState\":42,\"world\":{}}";

    public SealedPackageNonConfusionTests()
    {
        Directory.CreateDirectory(_directory);
    }

    private ExperimentDefinition Definition() => new()
    {
        Id = "EXP-TEST-001",
        Title = "Campagne de test",
        Profile = WellKnownProfiles.Analyse,
        Simulation = "ecosystem_01",
        RunCount = 1,
        Ticks = 1000,
        SeedStrategy = SeedStrategy.Derived,
        BaseSeed = 1000,
        FailurePolicy = FailurePolicy.Continue,
    };

    /// <summary>Produit un paquet minimal scellé : campagne (experiment.json) + un run terminé.</summary>
    private string CreateSealedPackage(string name)
    {
        var path = Path.Combine(_directory, name);
        var definition = Definition();
        using (var writer = LivexPackageWriter.CreateNew(path, definition, "0.1.0", DateTimeOffset.UnixEpoch))
        {
            writer.CompleteRun(
                "RUN-0001",
                new RunIndexEntry { RunId = "RUN-0001", Status = RunStatuses.Termine, Seed = definition.SeedFor(0) },
                "{\"runId\":\"RUN-0001\",\"seed\":1000}",
                "{\"schema\":1,\"config\":\"resolved\"}",
                new Dictionary<string, RunDataFile> { ["data/result.json"] = Encoding.UTF8.GetBytes("{\"v\":1}") },
                new List<(string, byte[])> { ("syne.log", Encoding.UTF8.GetBytes("journal du run")) },
                JournalLine.Pack(DateTimeOffset.UnixEpoch, "run_completed", "run terminé", "RUN-0001"));

            writer.Seal(DateTimeOffset.UnixEpoch);
        }

        return path;
    }

    private static bool Contains(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle) =>
        haystack.IndexOf(needle) >= 0;

    // ------------------------------------------------------------------
    // G1.1. Signature : le paquet scellé est un ZIP, jamais une base SQLite.
    // ------------------------------------------------------------------
    [Fact]
    public void Paquet_scelle_commence_par_la_signature_ZIP_et_porte_l_extension_livexp()
    {
        var path = CreateSealedPackage("signature.livexp");

        // L'extension annoncée est celle du format réellement écrit : ni .db, ni .sqlite, ni .json.
        Assert.Equal(".livexp", Path.GetExtension(path));

        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length >= ZipLocalFileHeader.Length, "paquet scellé vide : rien à examiner");
        Assert.Equal(ZipLocalFileHeader, bytes[..ZipLocalFileHeader.Length]);
    }

    // ------------------------------------------------------------------
    // G1.2. Négation de signature : aucun octet SQLite, en tête ni ailleurs.
    // ------------------------------------------------------------------
    [Fact]
    public void Paquet_scelle_ne_porte_jamais_la_signature_SQLite()
    {
        var path = CreateSealedPackage("sans-sqlite.livexp");
        var bytes = File.ReadAllBytes(path);

        // La sonde se vérifie sur elle-même : une recherche qui ne trouve jamais rien
        // ne prouverait pas l'absence de la signature, seulement l'inertie du test.
        Assert.True(Contains(SqliteFormat3Header, SqliteFormat3Header), "la sonde de signature SQLite doit se détecter elle-même");
        Assert.True(Contains(ZipLocalFileHeader, ZipLocalFileHeader), "la sonde de signature ZIP doit se détecter elle-même");
        Assert.False(Contains(ZipLocalFileHeader, SqliteFormat3Header), "les deux signatures doivent être disjointes");

        // Ni en tête (le cas « fichier SQLite renommé en .livexp »)…
        Assert.False(
            Contains(bytes[..Math.Min(bytes.Length, SqliteFormat3Header.Length)], SqliteFormat3Header),
            "un .livexp scellé ne doit pas s'ouvrir par « SQLite format 3 »");

        // …ni nulle part ailleurs dans l'archive (ni empreinte de base embarquée).
        Assert.False(
            Contains(bytes, SqliteFormat3Header),
            "un .livexp scellé ne contient aucune signature « SQLite format 3 »");
    }

    // ------------------------------------------------------------------
    // G1.3. Lisibilité : c'est bien une archive ZIP qui porte manifest.json.
    // ------------------------------------------------------------------
    [Fact]
    public void Paquet_scelle_est_une_archive_ZIP_lisible_portant_manifest_json()
    {
        var path = CreateSealedPackage("archive.livexp");

        // L'ouverture ZIP échoue sur un fichier SQLite ou JSON brut : réussir ici
        // suffit à affirmer que le conteneur est une archive valide.
        using var archive = ZipFile.OpenRead(path);
        Assert.NotEmpty(archive.Entries);

        var manifest = archive.GetEntry(PackageConstants.ManifestEntry);
        Assert.NotNull(manifest);
        Assert.Equal("manifest.json", manifest.FullName);

        // Relisible par le lecteur du format : l'état scellé est porté par le manifeste,
        // preuve que le ZIP contient la campagne et non un simple emballage.
        using var reader = new LivexPackageReader(path);
        Assert.Equal(PackageStates.Sealed, reader.Manifest.State);
        Assert.Single(reader.RunIndex.Runs);
    }

    // ------------------------------------------------------------------
    // G1.4. Non-confusion avec le snapshot SYNE : JSON brut contre conteneur ZIP.
    // ------------------------------------------------------------------
    [Fact]
    public void Paquet_scelle_n_est_pas_un_document_JSON_comme_un_snapshot_SYNE()
    {
        var path = CreateSealedPackage("pas-json.livexp");
        var text = Encoding.UTF8.GetString(File.ReadAllBytes(path));

        // Le snapshot SYNE est un document JSON : accolade d'ouverture, parseable tel quel.
        using (var snapshot = JsonDocument.Parse(SyneSnapshotJson))
        {
            Assert.Equal(JsonValueKind.Object, snapshot.RootElement.ValueKind);
        }

        // Le paquet scellé s'ouvre sur l'en-tête ZIP : jamais une accolade, un crochet ou une guillemet.
        var firstCharacter = text[0];
        Assert.True(
            firstCharacter is not '{' and not '[' and not '"',
            $"un .livexp scellé ne doit pas s'ouvrir comme un document JSON (premier caractère « {firstCharacter} »)");

        // Et il ne se laisse pas interpréter comme du JSON du tout.
        // Assert.ThrowsAny (et non Assert.Throws) : xUnit exige le type EXACT, or
        // JsonDocument.Parse lève JsonReaderException, dérivé de JsonException.
        Assert.ThrowsAny<JsonException>(() => { _ = JsonDocument.Parse(text); });

        // Deux contenus, deux lectures : là où le snapshot s'ouvre, le paquet se rejette.
        Assert.NotEqual(SyneSnapshotJson[0..1], text[0..1]);
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
