using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Launcher.Domain.Model;
using Launcher.Protocol;
using Launcher.Protocol.Model;

namespace Launcher.Package;

/// <summary>
/// Écriture d'un paquet .livexp (PACKAGE_FORMAT.md). Vivant puis scellé :
/// - écriture en incrément dans un conteneur ZIP64, index central maintenu en mémoire ;
/// - ordre d'écriture normatif §5.2 : répertoire du run, integrity.json, runs/index.json,
///   journal.ndjson, manifest.json en dernier (point de commit) ;
/// - scellement immuable, horodatages d'entrées normalisés, déterminisme octet pour octet ;
/// - contre-mesures de sécurité §8 : chemins relatifs, refus d'exécutables et de symlinks,
///   écriture exclusive.
/// </summary>
public sealed class LivexPackageWriter : IDisposable
{
    private FileStream _fileStream;
    private ZipArchive _archive;
    private readonly object _gate = new();
    private readonly HashSet<string> _writtenEntries = new(StringComparer.Ordinal);
    private readonly List<JournalLine> _journal = new();
    private RunIndex _runIndex = new();
    private PackageManifest _manifest;
    private readonly string _path;
    private bool _sealed;

    private LivexPackageWriter(string path, FileStream stream, PackageManifest manifest, RunIndex runIndex, List<JournalLine> journal)
    {
        _path = path;
        _fileStream = stream;
        _manifest = manifest;
        _runIndex = runIndex;
        _journal = journal;
        _archive = new ZipArchive(stream, ZipArchiveMode.Update);
        foreach (var entry in _archive.Entries)
        {
            _writtenEntries.Add(entry.FullName);
        }
    }

    /// <summary>Crée un paquet neuf pour une campagne : squelette complet, déjà valide et relisible (§5.1).
    /// « packageId » est généré par défaut ; le passer explicitement permet de vérifier la propriété de déterminisme.</summary>
    public static LivexPackageWriter CreateNew(string path, ExperimentDefinition experiment, string launcherVersion, DateTimeOffset createdAt, string? packageId = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var stream = new FileStream(
            path,
            new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.Read });
        var manifest = new PackageManifest
        {
            PackageId = packageId ?? Ulid.NewUlid().ToString(),
            State = PackageStates.Live,
            CreatedAt = FormatUtc(createdAt),
            Generator = new JsonGenerator { Version = launcherVersion },
            Experiment = new JsonExperimentRef
            {
                Id = experiment.Id,
                Title = experiment.Title,
                Profile = experiment.Profile,
            },
            Counts = new JsonCounts { Runs = experiment.RunCount },
        };
        var journal = new List<JournalLine>
        {
            JournalLine.Pack(createdAt, "package_created", $"paquet créé pour la campagne {experiment.Id}"),
        };

        var resolvedPackageId = packageId ?? manifest.PackageId;
        var writer = new LivexPackageWriter(path, stream, manifest, new RunIndex(), journal);
        writer.WriteEntry(PackageConstants.ExperimentEntry, ContractJson.Serialize(experiment));
        writer.WriteEntry(PackageConstants.RunsIndexEntry, ContractJson.Serialize(writer._runIndex));
        writer.WriteEntry(PackageConstants.ProvenanceEntry, ContractJson.Serialize(BuildProvenance(experiment, launcherVersion, createdAt, resolvedPackageId)));
        writer.WriteEntry("README.md", BuildReadme(experiment));
        writer.WriteJournalAndManifest();
        return writer;
    }

    /// <summary>Ouvre un paquet existant pour écriture (reprise). Refuse un paquet scellé.</summary>
    public static LivexPackageWriter OpenForAppend(string path)
    {
        var stream = new FileStream(
            path,
            new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.ReadWrite, Share = FileShare.Read });
        // leaveOpen : le flux reste utilisable pour la réouverture en mode Update après la lecture.
        var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        PackageManifest manifest;
        RunIndex runIndex;
        var journal = new List<JournalLine>();
        try
        {
            manifest = ReadJson<PackageManifest>(archive, PackageConstants.ManifestEntry);
            runIndex = ReadJson<RunIndex>(archive, PackageConstants.RunsIndexEntry);
            var journalEntry = archive.GetEntry(PackageConstants.JournalEntry);
            if (journalEntry is not null)
            {
                using var reader = new StreamReader(journalEntry.Open(), Encoding.UTF8);
                while (reader.ReadLine() is { } line)
                {
                    journal.Add(JournalLine.Parse(line));
                }
            }
        }
        finally
        {
            archive.Dispose();
        }

        if (manifest.State == PackageStates.Sealed)
        {
            stream.Dispose();
            throw new SealedPackageException();
        }

        return new LivexPackageWriter(path, stream, manifest, runIndex, journal);
    }

    /// <summary>Ouvre un paquet en lecture seule, quel que soit son état (PACKAGE_FORMAT.md §10).</summary>
    public static LivexPackageReader OpenRead(string path) => new(path);

    /// <summary>État courant du manifeste.</summary>
    public string State => Volatile.Read(ref _sealed) ? PackageStates.Sealed : _manifest.State;

    /// <summary>Manifeste courant.</summary>
    public PackageManifest Manifest => _manifest;

    /// <summary>Index des runs courant.</summary>
    public RunIndex RunIndex => _runIndex;

    /// <summary>Écrit ou remplace une entrée de métadonnées (JSON).</summary>
    public void WriteEntry(string entryName, string content) => WriteEntry(entryName, Encoding.UTF8.GetBytes(content));    /// <summary>Écrit ou remplace une entrée binaire.</summary>
    public void WriteEntry(string entryName, byte[] content)
    {
        ThrowIfSealed();

        PackageEntryRules.EnsureSafe(entryName);
        lock (_gate)
        {
            WriteEntryLocked(entryName, RunDataFile.FromBytes(content));
        }
    }

    /// <summary>
    /// Clôture un run selon la séquence normative §5.2 : métadonnées et données du run,
    /// integrity.json, runs/index.json, journal.ndjson, manifest.json en dernier.
    /// « dataFiles » porte les fichiers de données collectés (chemin d'entrée → source),
    /// copiés en flux depuis le disque sans jamais être chargés en mémoire — une donnée
    /// de campagne dépasse les 2 Gio d'un byte[].
    /// « analysisFiles » porte l'analyse individuelle d'ECHOS pour ce run (PACKAGE_FORMAT.md §3),
    /// écrite sous analysis/individual/ ; elle peut être vide si ECHOS est indisponible.
    ///
    /// <para><b>Pourquoi une réécriture complète</b> : le mode Update de
    /// <see cref="ZipArchive"/> bufferise toute entrée écrite dans un
    /// <c>MemoryStream</c> (borné à <c>int.MaxValue</c> o) — un run réel de campagne
    /// (« The file is too long ») échouait à cet endroit. Chaque clôture réécrit donc
    /// l'archive en mode <c>Create</c>, qui compresse directement dans le fichier :
    /// aucun plafond, aucun contenu en mémoire. L'ordre normatif §5.2, l'horodatage
    /// d'époque et le niveau de compression sont identiques à l'ancien chemin.</para>
    /// </summary>
    public void CompleteRun(
        string runId,
        RunIndexEntry indexEntry,
        string runJson,
        string configResolvedJson,
        IReadOnlyDictionary<string, RunDataFile> dataFiles,
        IReadOnlyList<(string Name, byte[] Content)> logFiles,
        JournalLine completionEvent,
        IReadOnlyDictionary<string, byte[]>? analysisFiles = null)
    {
        ThrowIfSealed();
        lock (_gate)
        {
            var prefix = $"runs/{runId}/";

            // 0. Mutations mémoire (identiques à l'ancien chemin) : index, journal et
            //    manifeste sont sérialisés en fin de séquence, le manifeste en dernier.
            UpsertRunIndexLocked(indexEntry);
            _journal.Add(completionEvent);
            RecomputeCountsLocked();

            // Noms réécrits : ceux du run (toutes tentives antérieures comprises), les
            // métadonnées systématiquement remplacées, et l'analyse individuelle écrasée.
            var rewritten = new HashSet<string>(StringComparer.Ordinal)
            {
                $"{prefix}run.json",
                $"{prefix}config.resolved.json",
                $"{prefix}{PackageConstants.RunIntegrityEntry}",
                PackageConstants.RunsIndexEntry,
                PackageConstants.JournalEntry,
                PackageConstants.ManifestEntry,
            };
            foreach (var entryName in dataFiles.Keys)
            {
                rewritten.Add($"{prefix}{entryName}");
            }

            foreach (var (name, _) in logFiles)
            {
                rewritten.Add($"{prefix}logs/{name}");
            }

            foreach (var entryName in analysisFiles?.Keys ?? Array.Empty<string>())
            {
                rewritten.Add($"analysis/individual/{entryName}");
            }

            var temporaryPath = $"{_path}.{Guid.NewGuid():N}.complete.tmp";
            _archive.Dispose();
            _fileStream.Dispose();
            try
            {
                WriteCompleteRunEntries(temporaryPath, prefix, rewritten, runJson, configResolvedJson,
                    dataFiles, logFiles, analysisFiles);

                // Échange atomique : le paquet existant n'est écrasé qu'après l'écriture
                // complète du temporaire (même garantie que le scellement §5.3).
                File.Move(temporaryPath, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                // Réouverture dans tous les cas : sur échec, le paquet d'origine est intact.
                ReopenForUpdateLocked();
            }
        }
    }

    /// <summary>
    /// Écrit le répertoire du run et les métadonnées dans une archive temporaire ouverte
    /// en mode <c>Create</c> (compression directe dans le fichier, aucun tampon en
    /// mémoire). À la retour, l'archive temporaire est complète et peut être échangée
    /// avec le paquet. L'ordre normatif §5.2 est respecté : données, integrity, index,
    /// journal, manifeste en dernier (point de commit).
    /// </summary>
    private void WriteCompleteRunEntries(
        string temporaryPath,
        string prefix,
        HashSet<string> rewritten,
        string runJson,
        string configResolvedJson,
        IReadOnlyDictionary<string, RunDataFile> dataFiles,
        IReadOnlyList<(string Name, byte[] Content)> logFiles,
        IReadOnlyDictionary<string, byte[]>? analysisFiles)
    {
        using var source = new ZipArchive(
            new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read),
            ZipArchiveMode.Read);
        using var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var fresh = new ZipArchive(output, ZipArchiveMode.Create);

        // 1. Entrées existantes conservées, dans l'ordre du répertoire central
        //    (l'ancien chemin Update supprimait puis réajoutait, ce qui laissait
        //    exactement les mêmes entrées à leur même place).
        foreach (var entry in source.Entries)
        {
            if (rewritten.Contains(entry.FullName)
                || entry.FullName.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            WriteStreamEntry(fresh, entry.FullName, entry.Open());
        }

        var integrity = new RunIntegrity();

        // 2a. Métadonnées du run.
        AddBytesEntry(fresh, integrity, $"{prefix}run.json", Encoding.UTF8.GetBytes(runJson));
        AddBytesEntry(fresh, integrity, $"{prefix}config.resolved.json", Encoding.UTF8.GetBytes(configResolvedJson));

        // 2b. Données du run, écrites par les composants, collectées par le Launcher :
        //     copie et empreinte en une seule passe sur le flux.
        foreach (var (entryName, dataFile) in dataFiles)
        {
            var fullName = $"{prefix}{entryName}";
            PackageEntryRules.EnsureSafe(fullName);
            using var content = dataFile.OpenRead();
            var entry = fresh.CreateEntry(fullName, CompressionLevel.SmallestSize);
            entry.LastWriteTime = DateTimeOffset.FromUnixTimeSeconds(PackageConstants.ZipEpochTimestamp).UtcDateTime;
            using var target = entry.Open();
            var (sizeBytes, sha256) = CopyWithHash(content, target);
            integrity.Files.Add(new IntegrityEntry { Path = fullName, SizeBytes = sizeBytes, Sha256 = sha256 });
        }

        foreach (var (name, content) in logFiles)
        {
            AddBytesEntry(fresh, integrity, $"{prefix}logs/{name}", content);
        }

        // 2c. Analyse individuelle d'ECHOS (canal 3 — restitution), hors préfixe du run.
        foreach (var (entryName, content) in analysisFiles ?? new Dictionary<string, byte[]>())
        {
            WriteBytesEntry(fresh, $"analysis/individual/{entryName}", content);
        }

        // 3. integrity.json (tri ordinal, comme l'ancien balayage des entrées).
        integrity.Files.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        WriteBytesEntry(fresh, $"{prefix}{PackageConstants.RunIntegrityEntry}", ContractJson.Serialize(integrity));

        // 4. runs/index.json, journal.ndjson, manifest.json — dans cet ordre,
        //    le manifeste en dernier : point de commit §5.2.
        WriteBytesEntry(fresh, PackageConstants.RunsIndexEntry, ContractJson.Serialize(_runIndex));
        WriteBytesEntry(fresh, PackageConstants.JournalEntry,
            Encoding.UTF8.GetBytes(string.Join("\n", _journal.Select(j => j.ToJsonLine())) + "\n"));
        WriteBytesEntry(fresh, PackageConstants.ManifestEntry, ContractJson.Serialize(_manifest));
    }

    /// <summary>Scelle le paquet : immuable, empreinte pleine, horodatages normalisés (§5.3).</summary>
    public string Seal(DateTimeOffset sealedAt)
    {
        ThrowIfSealed();
        lock (_gate)
        {
            _journal.Add(JournalLine.Pack(sealedAt, "package_sealed", "campagne terminée, paquet scellé"));
            WriteEntryLocked(PackageConstants.JournalEntry, Encoding.UTF8.GetBytes(string.Join("\n", _journal.Select(j => j.ToJsonLine())) + "\n"));
            _manifest.State = PackageStates.Sealed;
            _manifest.SealedAt = FormatUtc(sealedAt);
            RecomputeCountsLocked();
            WriteEntryLocked(PackageConstants.ManifestEntry, ContractJson.Serialize(_manifest));
        }

        // Recompactage déterministe : relit toutes les entrées dans l'ordre trié,
        // puis réécrit un paquet neuf : compression deflate à niveau fixé,
        // horodatage d'époque du format, attributs normalisés (PACKAGE_FORMAT.md §6).
        //
        // Écriture atomique : le paquet existant n'est écrasé qu'après la réussite complète
        // de la réécriture dans un fichier temporaire. Un plantage en cours de scellement
        // laissait un .livexp tronqué — la perte d'une campagne déjà exécutée n'est pas un
        // prix acceptable pour un scellement ; dans le pire des cas, le paquet reste vivant
        // et reprise.
        lock (_gate)
        {
            // Recompactage en flux, entrée par entrée (même ordre trié, même niveau de
            // compression, même horodatage) : aucune entrée n'est mise en mémoire —
            // une donnée de run dépasse les 2 Gio d'un byte[] et d'un MemoryStream.
            var names = _archive.Entries
                .Select(entry => entry.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            // Fermeture puis relecture en mode Read : une lecture Update bufferiserait
            // chaque entrée dans un MemoryStream — impossible au-delà de 2 Gio.
            _archive.Dispose();
            _fileStream.Dispose();

            var temporaryPath = $"{_path}.{Guid.NewGuid():N}.sealing.tmp";
            try
            {
                using (var source = new ZipArchive(
                           new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read),
                           ZipArchiveMode.Read))
                using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var fresh = new ZipArchive(output, ZipArchiveMode.Create))
                {
                    foreach (var name in names)
                    {
                        var existing = source.GetEntry(name);
                        if (existing is null)
                        {
                            continue;
                        }

                        var entry = fresh.CreateEntry(name, CompressionLevel.SmallestSize);
                        entry.LastWriteTime = DateTimeOffset.FromUnixTimeSeconds(PackageConstants.ZipEpochTimestamp).UtcDateTime;
                        using var sourceStream = existing.Open();
                        using var target = entry.Open();
                        sourceStream.CopyTo(target);
                    }
                }

                File.Move(temporaryPath, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            _sealed = true;
        }

        return _path;
    }

    /// <summary>Marque le paquet comme récupérable : écriture interrompue, reprise possible (§4.1).</summary>
    public void MarkRecoverable()
    {
        ThrowIfSealed();
        lock (_gate)
        {
            _manifest.State = PackageStates.Recoverable;
            WriteEntryLocked(PackageConstants.ManifestEntry, ContractJson.Serialize(_manifest));
        }
    }

    /// <summary>
    /// Enregistre les versions des composants engagées dans le manifeste (EXPERIMENTS.md §12 :
    /// « les versions des composants sont écrites — manifest.json »). Appelé par le Launcher
    /// au fil des runs, à partir du manifeste du composant réellement lancé.
    /// </summary>
    public void RegisterComponents(IReadOnlyList<JsonComponentRef> components)
    {
        ThrowIfSealed();
        lock (_gate)
        {
            foreach (var component in components)
            {
                _manifest.Components.RemoveAll(c => c.Id == component.Id);
                _manifest.Components.Add(component);
            }

            WriteEntryLocked(PackageConstants.ManifestEntry, ContractJson.Serialize(_manifest));
        }
    }

    /// <summary>Restaure l'état « live » après une écriture de restitution en paquet vivant.</summary>
    public void RestoreLiveState()
    {
        ThrowIfSealed();
        lock (_gate)
        {
            _manifest.State = PackageStates.Live;
            WriteEntryLocked(PackageConstants.ManifestEntry, ContractJson.Serialize(_manifest));
        }
    }

    private void WriteEntryLocked(string entryName, byte[] content) => WriteEntryLocked(entryName, RunDataFile.FromBytes(content));

    private void WriteEntryLocked(string entryName, string content) => WriteEntryLocked(entryName, Encoding.UTF8.GetBytes(content));

    /// <summary>
    /// Écrit une entrée en recopiant sa source en flux : un fichier de données de run
    /// (jusqu'à plusieurs Gio) transite du disque vers l'archive sans jamais être
    /// stocké en mémoire — la borne « byte[] < 2 Gio » ne s'applique donc jamais ici.
    /// </summary>
    private void WriteEntryLocked(string entryName, RunDataFile source)
    {
        // Toute écriture passe par la règle §8, y compris les noms venant d'un composant
        // (données d'un run, analyse ECHOS) : le producteur ne choisit pas le chemin écrit.
        PackageEntryRules.EnsureSafe(entryName);
        var existing = _archive.GetEntry(entryName);
        existing?.Delete();
        var entry = _archive.CreateEntry(entryName, CompressionLevel.SmallestSize);
        entry.LastWriteTime = DateTimeOffset.FromUnixTimeSeconds(PackageConstants.ZipEpochTimestamp).UtcDateTime;
        using (var content = source.OpenRead())
        using (var target = entry.Open())
        {
            content.CopyTo(target);
        }

        _writtenEntries.Add(entryName);
        _fileStream.Flush();
    }

    /// <summary>Copie un flux vers une entrée d'archive et le hache en une seule passe.</summary>
    private static (long SizeBytes, string Sha256) CopyWithHash(Stream source, Stream target)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long size = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            target.Write(buffer, 0, read);
            hash.AppendData(buffer, 0, read);
            size += read;
        }

        return (size, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    /// <summary>Écrit un contenu textuel UTF-8 dans une entrée (mode Create).</summary>
    private static void WriteBytesEntry(ZipArchive archive, string entryName, string content) =>
        WriteBytesEntry(archive, entryName, Encoding.UTF8.GetBytes(content));

    /// <summary>Écrit un contenu en mémoire dans une entrée (mode Create, flux fermé ici).</summary>
    private static void WriteBytesEntry(ZipArchive archive, string entryName, byte[] content)
    {
        PackageEntryRules.EnsureSafe(entryName);
        var entry = archive.CreateEntry(entryName, CompressionLevel.SmallestSize);
        entry.LastWriteTime = DateTimeOffset.FromUnixTimeSeconds(PackageConstants.ZipEpochTimestamp).UtcDateTime;
        using var target = entry.Open();
        target.Write(content);
    }

    /// <summary>Écrit un contenu en mémoire et enregistre son empreinte (entrée sous le préfixe du run).</summary>
    private static void AddBytesEntry(ZipArchive archive, RunIntegrity integrity, string entryName, byte[] content)
    {
        WriteBytesEntry(archive, entryName, content);
        integrity.Files.Add(new IntegrityEntry
        {
            Path = entryName,
            SizeBytes = content.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
        });
    }

    /// <summary>Copie un contenu existant (flux fourni, fermé ici) dans une entrée.</summary>
    private static void WriteStreamEntry(ZipArchive archive, string entryName, Stream content)
    {
        PackageEntryRules.EnsureSafe(entryName);
        var entry = archive.CreateEntry(entryName, CompressionLevel.SmallestSize);
        entry.LastWriteTime = DateTimeOffset.FromUnixTimeSeconds(PackageConstants.ZipEpochTimestamp).UtcDateTime;
        using (content)
        using (var target = entry.Open())
        {
            content.CopyTo(target);
        }
    }

    /// <summary>Réouvre le paquet en mode Update après une réécriture de fichier complet.</summary>
    private void ReopenForUpdateLocked()
    {
        _fileStream = new FileStream(
            _path,
            new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.ReadWrite, Share = FileShare.Read });
        _archive = new ZipArchive(_fileStream, ZipArchiveMode.Update);
        _writtenEntries.Clear();
        foreach (var entry in _archive.Entries)
        {
            _writtenEntries.Add(entry.FullName);
        }
    }

    /// <summary>Écrit le journal et le manifeste : séquence de création (PACKAGE_FORMAT.md §5.1).</summary>
    private void WriteJournalAndManifest()
    {
        lock (_gate)
        {
            WriteEntryLocked(PackageConstants.JournalEntry, string.Join("\n", _journal.Select(j => j.ToJsonLine())) + "\n");
            RecomputeCountsLocked();
            WriteEntryLocked(PackageConstants.ManifestEntry, ContractJson.Serialize(_manifest));
        }
    }

    private void UpsertRunIndexLocked(RunIndexEntry entry)
    {
        _runIndex.Runs.RemoveAll(r => r.RunId == entry.RunId);
        _runIndex.Runs.Add(entry);
        _runIndex.Runs.Sort((a, b) => string.CompareOrdinal(a.RunId, b.RunId));
    }

    private void RecomputeCountsLocked()
    {
        _manifest.Counts.RunsDone = _runIndex.Runs.Count(r => r.Status == RunStatuses.Termine);
        _manifest.Counts.RunsFailed = _runIndex.Runs.Count(r => r.Status == RunStatuses.Echoue);
        _manifest.Counts.Runs = Math.Max(_manifest.Counts.Runs, _runIndex.Runs.Count);
        _manifest.Integrity = new JsonIntegrity
        {
            Value = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join("\n", _runIndex.Runs.Select(r => $"{r.RunId}:{r.Status}:{r.Seed}"))))).ToLowerInvariant(),
        };
    }

    private void ThrowIfSealed()
    {
        if (Volatile.Read(ref _sealed) || _manifest.State == PackageStates.Sealed)
        {
            throw new SealedPackageException();
        }
    }


    private static T ReadJson<T>(ZipArchive archive, string entryName)
        where T : ISchemaVersioned
    {
        var entry = archive.GetEntry(entryName)
            ?? throw new CorruptedPackageException(entryName, "entrée manquante");
        using var source = entry.Open();
        using var reader = new StreamReader(source, Encoding.UTF8);
        var json = reader.ReadToEnd();
        return ContractJson.DeserializeWithSchema<T>(json, PackageConstants.SchemaVersion, entryName);
    }

    private static PackageProvenance BuildProvenance(ExperimentDefinition experiment, string launcherVersion, DateTimeOffset createdAt, string packageId) => new()
    {
        Generator = new JsonGenerator { Name = "livex-launcher", Version = launcherVersion },
        CreatedAt = FormatUtc(createdAt),
        Components =
        [
            new JsonComponentRef { Id = experiment.Simulation, Version = launcherVersion },
        ],
        Platform = new JsonPlatform
        {
            Os = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux",
            Arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            DotNet = System.Environment.Version.ToString(),
        },
    };

    private static string FormatUtc(DateTimeOffset moment) => moment.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static string BuildReadme(ExperimentDefinition experiment) => $"""
        # Paquet LIVEX — {experiment.Title}

        Campagne `{experiment.Id}`, profil « {experiment.Profile} ».

        Ce paquet .livexp est autoporteur : il porte la définition de la campagne
        (`experiment.json`), un run par répertoire `runs/RUN-nnnn/` (métadonnées,
        configuration résolue, données, journaux, empreintes), l'index de reprise
        (`runs/index.json`), le journal d'exécution (`journal.ndjson`) et le rapport
        d'émergence produit par ECHOS (`analysis/emergence_report.md`).

        Un paquet scellé est immuable. La reprise d'une campagne interrompue se base
        sur `runs/index.json`, jamais sur l'examen des répertoires.
        """;

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (!_sealed)
            {
                // Fermeture sans scellement : le paquet reste tel quel, relisible en l'état.
                // Aucune réécriture du manifeste ici : le dernier point de commit doit rester
                // exactement celui qui a été écrit (live ou recoverable), jamais un état reconstruit.
                _archive.Dispose();
                _fileStream.Dispose();
            }
        }
    }
}
