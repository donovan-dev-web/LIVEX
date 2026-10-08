using System.Text;
using System.Text.Json;
using Launcher.Domain;

namespace Launcher.Infrastructure;

/// <summary>
/// Journal de session du Launcher (ARCHITECTURE.md §6) : JSON Lines, un fichier par jour,
/// rotation quotidienne, purge configurable. UTF-8, fins de ligne LF (PACKAGING.md §11).
/// </summary>
public sealed class SessionFileJournal : ISessionJournal, IDisposable
{
    private readonly string _directory;
    private readonly object _gate = new();
    private StreamWriter? _writer;
    private string _currentDate = string.Empty;

    /// <summary>Initialise le journal dans le répertoire de sessions donné.</summary>
    public SessionFileJournal(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    /// <inheritdoc />
    public void Log(SessionEvent entry)
    {
        lock (_gate)
        {
            EnsureWriter();
            _writer!.WriteLine(JsonSerializer.Serialize(entry, Launcher.Protocol.ContractJson.Compact));
            _writer.Flush();
        }
    }

    /// <inheritdoc />
    public void Info(string operation, string message, string? correlationId = null, string? instanceId = null) =>
        Log(Build("Info", operation, message, correlationId, instanceId));

    /// <inheritdoc />
    public void Warn(string operation, string message, string? correlationId = null, string? instanceId = null) =>
        Log(Build("Warn", operation, message, correlationId, instanceId));

    /// <inheritdoc />
    public void Fail(string operation, string message, string? correlationId = null, string? instanceId = null) =>
        Log(Build("Error", operation, message, correlationId, instanceId));

    private static SessionEvent Build(string level, string operation, string message, string? correlationId, string? instanceId) => new()
    {
        Ts = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
        Level = level,
        Component = "launcher",
        InstanceId = instanceId,
        Operation = operation,
        CorrelationId = correlationId,
        Message = message,
    };

    private void EnsureWriter()
    {
        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (_writer is not null && _currentDate == today)
        {
            return;
        }

        _writer?.Dispose();
        _writer = new StreamWriter(Path.Combine(_directory, $"launcher-session-{today}.ndjson"), append: true, Encoding.UTF8)
        {
            AutoFlush = false,
        };
        _currentDate = today;
    }

    /// <summary>
    /// Lecture partagée d'un journal de session : le fichier reste ouvert en écriture tant que
    /// le Launcher tourne, et sous Windows un lecteur doit explicitement autoriser l'écrivain
    /// (<see cref="FileShare.ReadWrite"/>), sinon la lecture échoue en violation de partage —
    /// là où Linux n'a aucun verrou obligatoire sur les fichiers.
    /// </summary>
    public static IEnumerable<string> ReadSharedLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    /// <summary>Variante asynchrone de la lecture partagée (export des journaux de session).</summary>
    public static async Task<string> ReadSharedTextAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
