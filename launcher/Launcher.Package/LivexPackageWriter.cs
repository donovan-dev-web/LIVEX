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
    private readonly FileStream _fileStream;
    private readonly ZipArchive _archive;
    private readonly object _gate = new();
    private readonly SortedDictionary<string, byte[]?> _pendingEntries = new(StringComparer.Ordinal);
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
    public void WriteEntry(string entryName, string content) => WriteEntry(entryName, Encoding.UTF8.GetBytes(content));

    /// <summary>Écrit ou remplace une entrée binaire.</summary>
    public void WriteEntry(string entryName, byte[] content)
    {
        ThrowIfSealed();
        PackageEntryRules.EnsureSafe(entryName);
        lock (_gate)
        {
            _pendingEntries[entryName] = content;
            FlushPendingLocked();
        }
    }

    /// <summary>
    /// Clôture un run selon la séquence normative §5.2 : métadonnées et données du run,
    /// integrity.json, runs/index.json, journal.ndjson, manifest.json en dernier.
    /// « dataFiles » porte les fichiers de données collectés (chemin d'entrée → contenu).
    /// « analysisFiles » porte l'analyse individuelle d'ECHOS pour ce run (PACKAGE_FORMAT.md §3),
    /// écrite sous analysis/individual/ ; elle peut être vide si ECHOS est indisponible.
    /// </summary>
    public void CompleteRun(
        string runId,
        RunIndexEntry indexEntry,
        string runJson,
        string configResolvedJson,
        IReadOnlyDictionary<string, byte[]> dataFiles,
        IReadOnlyList<(string Name, byte[] Content)> logFiles,
        JournalLine completionEvent,
        IReadOnlyDictionary<string, byte[]>? analysisFiles = null)
    {
        ThrowIfSealed();
        lock (_gate)
        {
            var prefix = $"runs/{runId}/";

            // 1a. Métadonnées du run.
            WriteEntryLocked($"{prefix}run.json", Encoding.UTF8.GetBytes(runJson));
            WriteEntryLocked($"{prefix}config.resolved.json", Encoding.UTF8.GetBytes(configResolvedJson));

            // 1b. Données du run, écrites par les composants, collectées par le Launcher.
            foreach (var (entryName, content) in dataFiles)
            {
                WriteEntryLocked($"{prefix}{entryName}", content);
            }

            foreach (var (name, content) in logFiles)
            {
                WriteEntryLocked($"{prefix}logs/{name}", content);
            }

            // 1c. Analyse individuelle d'ECHOS (canal 3 — restitution), rangée à sa place de référence.
            foreach (var (entryName, content) in analysisFiles ?? new Dictionary<string, byte[]>())
            {
                WriteEntryLocked($"analysis/individual/{entryName}", content);
            }

            // 2. integrity.json : empreintes SHA-256 des entrées du run.
            var integrity = new RunIntegrity();
            foreach (var (entryName, content) in EnumerateRunEntries(prefix))
            {
                integrity.Files.Add(new IntegrityEntry
                {
                    Path = entryName,
                    SizeBytes = content.Length,
                    Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                });
            }

            WriteEntryLocked($"{prefix}{PackageConstants.RunIntegrityEntry}", ContractJson.Serialize(integrity));

            // 3. runs/index.json : mise à jour de l'état du run.
            UpsertRunIndexLocked(indexEntry);
            WriteEntryLocked(PackageConstants.RunsIndexEntry, ContractJson.Serialize(_runIndex));

            // 4. journal.ndjson : une ligne d'événement.
            _journal.Add(completionEvent);
            WriteEntryLocked(PackageConstants.JournalEntry, Encoding.UTF8.GetBytes(string.Join("\n", _journal.Select(j => j.ToJsonLine())) + "\n"));

            // 5. manifest.json : recomptage et nouvelle empreinte. Point de commit.
            RecomputeCountsLocked();
            WriteEntryLocked(PackageConstants.ManifestEntry, ContractJson.Serialize(_manifest));
        }
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
            var allEntries = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var entry in _archive.Entries.ToList())
            {
                using var source = entry.Open();
                using var buffer = new MemoryStream();
                source.CopyTo(buffer);
                allEntries[entry.FullName] = buffer.ToArray();
            }

            _archive.Dispose();
            _fileStream.Dispose();

            var temporaryPath = $"{_path}.{Guid.NewGuid():N}.sealing.tmp";
            try
            {
                using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var fresh = new ZipArchive(output, ZipArchiveMode.Create))
                {
                    foreach (var (name, content) in allEntries)
                    {
                        var entry = fresh.CreateEntry(name, CompressionLevel.SmallestSize);
                        entry.LastWriteTime = DateTimeOffset.FromUnixTimeSeconds(PackageConstants.ZipEpochTimestamp).UtcDateTime;
                        using var target = entry.Open();
                        target.Write(content);
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

    private void WriteEntryLocked(string entryName, byte[] content)
    {
        // Toute écriture passe par la règle §8, y compris les noms venant d'un composant
        // (données d'un run, analyse ECHOS) : le producteur ne choisit pas le chemin écrit.
        PackageEntryRules.EnsureSafe(entryName);
        _pendingEntries[entryName] = content;
        FlushPendingLocked();
    }

    private void WriteEntryLocked(string entryName, string content) => WriteEntryLocked(entryName, Encoding.UTF8.GetBytes(content));

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

    private void FlushPendingLocked()
    {
        foreach (var (name, content) in _pendingEntries)
        {
            var existing = _archive.GetEntry(name);
            existing?.Delete();
            var entry = _archive.CreateEntry(name, CompressionLevel.SmallestSize);
            entry.LastWriteTime = DateTimeOffset.FromUnixTimeSeconds(PackageConstants.ZipEpochTimestamp).UtcDateTime;
            using var target = entry.Open();
            target.Write(content);
            _writtenEntries.Add(name);
        }

        _pendingEntries.Clear();
        _fileStream.Flush();
    }

    private IEnumerable<(string Name, byte[] Content)> EnumerateRunEntries(string prefix)
    {
        foreach (var name in _writtenEntries.Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal))
        {
            var entry = _archive.GetEntry(name);
            if (entry is null)
            {
                continue;
            }

            using var source = entry.Open();
            using var buffer = new MemoryStream();
            source.CopyTo(buffer);
            yield return (name, buffer.ToArray());
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
