using System.Text;
using Simulation.Console.Observability;

namespace Simulation.Console.Batch;

/// <summary>
/// Cible d'observabilité écrivant le flux SYNE dans un fichier JSON Lines
/// (API_CONTRACTS.md §2). Le contenu est exactement celui diffusé sur
/// WebSocket : un <c>snapshot</c> par tick puis ses événements, dans l'ordre
/// d'émission. Rien n'est filtré, réordonné ni reformulé, de sorte qu'un
/// consommateur ECHOS puisse relire le fichier avec le même contrat que le flux
/// live (<c>stream.aligned_ticks</c>) sans adaptation.
///
/// <para>
/// Invariants :
/// <list type="bullet">
/// <item>Aucun tirage PRNG, aucune mutation du monde : le déterminisme bit-à-bit
/// est indépendant de l'activation de l'export (DETERMINISM.md §3).</item>
/// <item>UTF-8 sans BOM, séparateur <c>\n</c> : l'empreinte du fichier est
/// reproductible entre Linux et Windows pour un même contenu.</item>
/// <item>Une demande d'export explicite qui ne peut pas être satisfaite
/// (disque plein, droits insuffisants) lève une <see cref="IOException"/> —
/// l'export n'est jamais best-effort, un fichier tronqué passerait pour un
/// flux complet côté ECHOS.</item>
/// </list>
/// </para>
/// </summary>
public sealed class BatchStreamFile : IObservabilitySink, IObservabilityDemand, IAsyncDisposable
{
    /// <summary>Nom de fichier produit dans le répertoire d'export du run.</summary>
    public const string DefaultFileName = "stream.jsonl";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly FileStream _stream;
    private readonly StreamWriter _writer;
    private bool _disposed;

    /// <summary>Ouvre (ou tronque) <paramref name="path"/> en flux d'observabilité batch.</summary>
    public BatchStreamFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
        string? directory = System.IO.Path.GetDirectoryName(Path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        _stream = new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 1 << 16);
        _writer = new StreamWriter(_stream, Utf8NoBom) { NewLine = "\n" };
    }

    /// <summary>Chemin du flux écrit.</summary>
    public string Path { get; }

    /// <summary>Nombre de trames écrites.</summary>
    public long FramesWritten { get; private set; }

    /// <summary>
    /// L'export demandé a toujours un consommateur : l'émetteur peut donc
    /// construire le snapshot à chaque tick, sans client WebSocket connecté.
    /// </summary>
    public bool HasSubscribers => true;

    /// <inheritdoc />
    public async Task BroadcastAsync(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            await _writer.WriteLineAsync(text).ConfigureAwait(false);
            FramesWritten++;
        }
        catch (IOException exception)
        {
            throw new IOException($"Écriture du flux d'observabilité impossible ({Path}) : {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Vide les tampons et ferme le fichier. Les trames non écrites sont
    /// perdues sans trace si le processus est tué : le fichier produit est alors
    /// un préfixe, que l'ingestion ECHOS refuse (tick final absent).
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            await _writer.FlushAsync().ConfigureAwait(false);
            await _writer.DisposeAsync().ConfigureAwait(false);
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new IOException($"Fermeture du flux d'observabilité impossible ({Path}) : {exception.Message}", exception);
        }
    }
}