using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Launcher.Domain.Model;
using Launcher.Protocol;
using Launcher.Protocol.Model;

namespace Launcher.Package;

/// <summary>Ligne du journal d'exécution du paquet (journal.ndjson, ajout seul).</summary>
public sealed class JournalLine
{
    /// <summary>Horodatage UTC ISO 8601.</summary>
    public string Ts { get; init; } = string.Empty;

    /// <summary>Type d'événement stable.</summary>
    public string Event { get; init; } = string.Empty;

    /// <summary>Message lisible.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Identifiant de run si applicable.</summary>
    public string? RunId { get; init; }

    /// <summary>Construit une ligne d'horodatage donnée.</summary>
    public static JournalLine Pack(DateTimeOffset at, string @event, string message, string? runId = null) => new()
    {
        Ts = at.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
        Event = @event,
        Message = message,
        RunId = runId,
    };

    /// <summary>Sérialise en une ligne JSON compacte.</summary>
    public string ToJsonLine() => Launcher.Protocol.ContractJson.SerializeLine(new Dictionary<string, string?>
    {
        ["ts"] = Ts,
        ["event"] = Event,
        ["message"] = Message,
        ["runId"] = RunId,
    });

    internal static JournalLine Parse(string line)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(line);
            var root = document.RootElement;
            return new JournalLine
            {
                Ts = root.TryGetProperty("ts", out var ts) ? ts.GetString() ?? string.Empty : string.Empty,
                Event = root.TryGetProperty("event", out var @event) ? @event.GetString() ?? string.Empty : string.Empty,
                Message = root.TryGetProperty("message", out var message) ? message.GetString() ?? string.Empty : string.Empty,
                RunId = root.TryGetProperty("runId", out var runId) && runId.ValueKind == System.Text.Json.JsonValueKind.String
                    ? runId.GetString()
                    : null,
            };
        }
        catch (System.Text.Json.JsonException)
        {
            return new JournalLine { Event = "unparsed", Message = line };
        }
    }
}

/// <summary>
/// Lecture d'un paquet .livexp (PACKAGE_FORMAT.md §10) : ouverture en lecture seule,
/// vérification d'intégrité, contre-mesures zip-bomb et chemins dangereux à la lecture.
/// </summary>
public sealed class LivexPackageReader : IDisposable
{
    private const long MaxUncompressedBytes = 4L * 1024 * 1024 * 1024;
    private const long MaxCompressionRatio = 100;
    private const long MaxTextLogBytes = 16L * 1024 * 1024;
    private const long MaxRunLogExportBytes = 256L * 1024 * 1024;

    private readonly ZipArchive _archive;

    /// <summary>Initialise la lecture et valide immédiatement la sécurité des noms d'entrées.</summary>
    public LivexPackageReader(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        _archive = new ZipArchive(stream, ZipArchiveMode.Read);

        try
        {
            foreach (var entry in _archive.Entries)
            {
                EnsureSafeEntryName(entry.FullName);

                // Plafond zip-bomb : taille décompressée déclarée et ratio plafonnés (PACKAGE_FORMAT.md §9).
                if (entry.Length > MaxUncompressedBytes
                    || (entry.CompressedLength > 0 && entry.Length / entry.CompressedLength > MaxCompressionRatio))
                {
                    throw new CorruptedPackageException(entry.FullName, "entrée au-delà des plafonds de décompression");
                }
            }
        }
        catch
        {
            _archive.Dispose();
            throw;
        }
    }

    /// <summary>Manifeste du paquet.</summary>
    public PackageManifest Manifest => ReadJson<PackageManifest>(PackageConstants.ManifestEntry);

    /// <summary>Index des runs (source de reprise).</summary>
    public RunIndex RunIndex => ReadJson<RunIndex>(PackageConstants.RunsIndexEntry);

    /// <summary>Définition de campagne.</summary>
    public ExperimentDefinition Experiment => ReadJson<ExperimentDefinition>(PackageConstants.ExperimentEntry);

    /// <summary>Lignes du journal d'exécution.</summary>
    public IReadOnlyList<JournalLine> Journal()
    {
        var entry = _archive.GetEntry(PackageConstants.JournalEntry);
        if (entry is null)
        {
            return Array.Empty<JournalLine>();
        }

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var lines = new List<JournalLine>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                lines.Add(JournalLine.Parse(line));
            }
        }

        return lines;
    }

    /// <summary>Lit le rapport d'émergence, ou null s'il n'est pas encore produit.</summary>
    public string? ReadEmergenceReport()
    {
        var entry = _archive.GetEntry(PackageConstants.EmergenceReportEntry);
        if (entry is null)
        {
            return null;
        }

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Lit une entrée du paquet par son nom.</summary>
    public byte[]? ReadEntry(string entryName)
    {
        var entry = _archive.GetEntry(entryName);
        if (entry is null)
        {
            return null;
        }

        using var source = entry.Open();
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>Liste les fichiers de journal des runs, sans extraire le paquet.</summary>
    public IReadOnlyList<string> ListRunLogEntries() =>
        _archive.Entries
            .Where(entry => entry.FullName.StartsWith("runs/", StringComparison.Ordinal)
                && entry.FullName.Contains("/logs/", StringComparison.Ordinal)
                && !entry.FullName.EndsWith("/", StringComparison.Ordinal))
            .Select(entry => entry.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Lit un journal de run avec un plafond de taille pour l'affichage dans l'interface.</summary>
    public string ReadRunLog(string entryName)
    {
        var entry = GetRunLogEntry(entryName);
        if (entry.Length > MaxTextLogBytes)
        {
            throw new CorruptedPackageException(entryName, "journal trop volumineux pour l'affichage (limite 16 Mio)");
        }

        using var source = entry.Open();
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>Copie un journal vers une destination avec un plafond supérieur au plafond d'affichage.</summary>
    public async Task CopyRunLogToAsync(string entryName, Stream destination, CancellationToken cancellationToken = default)
    {
        var entry = GetRunLogEntry(entryName);
        if (entry.Length > MaxRunLogExportBytes)
        {
            throw new CorruptedPackageException(entryName, "journal trop volumineux pour l'export (limite 256 Mio)");
        }

        await using var source = entry.Open();
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private ZipArchiveEntry GetRunLogEntry(string entryName)
    {
        if (!ListRunLogEntries().Contains(entryName, StringComparer.Ordinal))
        {
            throw new ArgumentException("l'entrée n'est pas un journal de run du paquet", nameof(entryName));
        }

        return _archive.GetEntry(entryName)
            ?? throw new CorruptedPackageException(entryName, "entrée de journal absente");
    }

    /// <summary>Vérifie l'intégrité d'un run : recalcul des empreintes contre integrity.json.</summary>
    public bool VerifyRunIntegrity(string runId, out IReadOnlyList<string> problems)
    {
        var problemsList = new List<string>();
        var integrityEntry = ReadJson<RunIntegrity>($"runs/{runId}/{PackageConstants.RunIntegrityEntry}");
        foreach (var expected in integrityEntry.Files)
        {
            var actual = ReadEntry(expected.Path);
            if (actual is null)
            {
                problemsList.Add($"entrée manquante : {expected.Path}");
                continue;
            }

            if (actual.Length != expected.SizeBytes)
            {
                problemsList.Add($"taille incohérente : {expected.Path}");
                continue;
            }

            var digest = Convert.ToHexString(SHA256.HashData(actual)).ToLowerInvariant();
            if (!string.Equals(digest, expected.Sha256, StringComparison.Ordinal))
            {
                problemsList.Add($"empreinte incohérente : {expected.Path}");
            }
        }

        problems = problemsList;
        return problemsList.Count == 0;
    }

    private T ReadJson<T>(string entryName)
        where T : ISchemaVersioned
    {
        var entry = _archive.GetEntry(entryName)
            ?? throw new CorruptedPackageException(entryName, "entrée manquante");
        using var source = entry.Open();
        using var reader = new StreamReader(source, Encoding.UTF8);
        var json = reader.ReadToEnd();
        return ContractJson.DeserializeWithSchema<T>(json, PackageConstants.SchemaVersion, entryName);
    }

    private static void EnsureSafeEntryName(string entryName)
    {
        if (Path.IsPathRooted(entryName)
            || entryName.Contains("..", StringComparison.Ordinal)
            || entryName.Contains(':')
            || entryName.StartsWith("/", StringComparison.Ordinal)
            || entryName.StartsWith("\\", StringComparison.Ordinal))
        {
            throw new UnsafeEntryPathException(entryName);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _archive.Dispose();
}
