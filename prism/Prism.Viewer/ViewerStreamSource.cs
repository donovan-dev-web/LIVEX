using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Prism.Contracts;

namespace Prism.Viewer;

/// <summary>
/// Source d'enveloppes du flux de lecture (PRISM-115) : connexion WebSocket live, rejeu d'un
/// fichier enregistré, ou flux embarqué. Le rendu ne bloque jamais le flux : les enveloppes
/// attendent dans une file bornée, et une file saturée provoque une resynchronisation propre
/// (le viewer saute les deltas intermédiaires, RENDERING_SPEC.md §8).
/// </summary>
public sealed class ViewerLiveConnection : IAsyncDisposable
{
    public const int MaximumPending = 512;

    private ViewerReadStreamClient _client = new();
    private readonly ConcurrentQueue<ViewerStreamEnvelope> _queue = new();
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _pump;
    private ViewerStreamRecorder? _recorder;

    public string? Error { get; private set; }

    public bool IsRunning => _pump is { IsCompleted: false };

    public int PendingCount => _queue.Count;

    public long Received { get; private set; }

    public long Dropped { get; private set; }

    public async Task StartAsync(
        Uri uri,
        ViewerStreamRecorder? recorder = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        _recorder = recorder;

        // L'hôte prépare son monde avant d'ouvrir son HTTP : lancé juste après lui
        // par le Launcher, le viewer peut se présenter trop tôt. On réessaie avec
        // un client neuf, un ClientWebSocket en échec n'étant pas réutilisable.
        Exception? last = null;
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                await _client.ConnectAsync(uri, cancellationToken);
                _pump = Task.Run(() => PumpAsync(_cancellation.Token), CancellationToken.None);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                last = exception;
                if (attempt == 20)
                {
                    break;
                }

                await _client.DisposeAsync();
                _client = new ViewerReadStreamClient();
                await Task.Delay(500, cancellationToken);
            }
        }

        throw new IOException(
            $"Connexion au flux PRISM impossible après 20 tentatives : {last?.Message}",
            last);
    }

    public bool TryDequeue(out ViewerStreamEnvelope envelope)
    {
        if (_queue.TryDequeue(out var dequeued))
        {
            envelope = dequeued;
            return true;
        }

        envelope = null!;
        return false;
    }

    public void Enqueue(ViewerStreamEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        Received++;
        _recorder?.Append(envelope);
        _queue.Enqueue(envelope);
        while (_queue.Count > MaximumPending && _queue.TryDequeue(out _))
        {
            Dropped++;
        }
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var envelope in _client.ReadAllAsync(cancellationToken))
            {
                Enqueue(envelope);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Error = exception.Message;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync();
        if (_pump is not null)
        {
            try
            {
                await _pump.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
            }
        }

        _recorder?.Dispose();
        await _client.DisposeAsync();
        _cancellation.Dispose();
    }
}

/// <summary>Enregistrement d'un run lu (deltas compris) : une enveloppe JSON par ligne.</summary>
public sealed class ViewerStreamRecorder : IDisposable
{
    private readonly StreamWriter _writer;

    public ViewerStreamRecorder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = false,
        };
        FilePath = path;
    }

    public string FilePath { get; }

    public int Count { get; private set; }

    public void Append(ViewerStreamEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        _writer.WriteLine(JsonSerializer.Serialize(envelope, ProtocolJson.Options));
        Count++;
    }

    public void Flush() => _writer.Flush();

    public void Dispose()
    {
        _writer.Dispose();
    }
}

public static class ViewerReplayFile
{
    public static void Write(string path, IEnumerable<ViewerStreamEnvelope> envelopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(envelopes);
        using var recorder = new ViewerStreamRecorder(path);
        foreach (var envelope in envelopes)
        {
            recorder.Append(envelope);
        }

        recorder.Flush();
    }

    public static IReadOnlyList<ViewerStreamEnvelope> Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var envelopes = new List<ViewerStreamEnvelope>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            envelopes.Add(ViewerStreamEnvelope.Parse(line));
        }

        return envelopes;
    }
}

/// <summary>
/// Avance un rejeu au rythme demandé (ticks par seconde) sans jamais dépendre de l'horloge
/// du run : le même fichier rejoué deux fois produit les mêmes états.
/// </summary>
public sealed class ViewerReplayCursor(IReadOnlyList<ViewerStreamEnvelope> envelopes, double ticksPerSecond)
{
    private double _clock;
    private long _lastTick = -1;

    public IReadOnlyList<ViewerStreamEnvelope> Envelopes { get; } = envelopes
        ?? throw new ArgumentNullException(nameof(envelopes));

    public double TicksPerSecond { get; } = ticksPerSecond >= 0d
        ? ticksPerSecond
        : throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));

    public int Position { get; private set; }

    public bool Complete => Position >= Envelopes.Count;

    public long LastTick => _lastTick;

    public IReadOnlyList<ViewerStreamEnvelope> Next(double deltaSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deltaSeconds);
        if (Complete)
        {
            return [];
        }

        _clock += deltaSeconds * TicksPerSecond;
        var limit = (long)Math.Floor(_clock);
        var batch = new List<ViewerStreamEnvelope>();
        while (Position < Envelopes.Count)
        {
            var envelope = Envelopes[Position];
            var unlocked = TicksPerSecond == 0d ||
                           envelope.Tick < 0 ||
                           envelope.Tick <= limit ||
                           batch.Count == 0;
            if (!unlocked)
            {
                break;
            }

            batch.Add(envelope);
            _lastTick = Math.Max(_lastTick, envelope.Tick);
            Position++;
        }

        return batch;
    }

    /// <summary>Vide le rejeu sans tenir compte du rythme (fin de fichier ou avance rapide).</summary>
    public IReadOnlyList<ViewerStreamEnvelope> Drain()
    {
        var remaining = Envelopes.Skip(Position).ToArray();
        Position = Envelopes.Count;
        return remaining;
    }
}
