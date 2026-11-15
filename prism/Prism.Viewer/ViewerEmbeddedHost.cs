using Prism.Content;
using Prism.Contracts;
using Prism.Host.Hosting;

namespace Prism.Viewer;

/// <summary>
/// Mode embarqué (RENDERING_SPEC.md §7) : le viewer héberge un host PRISM sur des tâches de
/// fond et consomme son propre flux de lecture. Le rendu reste un client : il ne décide de
/// rien et peut être fermé sans conséquence pour le run.
/// </summary>
public sealed class ViewerEmbeddedHost : IAsyncDisposable
{
    private readonly PrismHostRuntime _runtime = new(checkpointOnStop: false);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ViewerLiveConnection _sink;
    private readonly ulong _seed;
    private readonly string _contentDirectory;
    private readonly double _ticksPerSecond;
    private Task? _clock;
    private Task? _pump;
    private ReadStreamSubscription? _subscription;

    public ViewerEmbeddedHost(
        ViewerLiveConnection sink,
        ulong seed,
        string contentDirectory,
        double ticksPerSecond)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentDirectory);
        ArgumentOutOfRangeException.ThrowIfNegative(ticksPerSecond);
        _sink = sink;
        _seed = seed;
        _contentDirectory = Path.GetFullPath(contentDirectory);
        _ticksPerSecond = ticksPerSecond;
    }

    public PrismHostRuntime Runtime => _runtime;

    public string? Error { get; private set; }

    public GeneratedWorldSnapshot? World => _runtime.WorldSnapshot;

    public string? ContentHash { get; private set; }

    public void Start()
    {
        var compilation = new ContentPackCompiler().CompileDirectory(_contentDirectory);
        if (!compilation.Succeeded)
        {
            Error = string.Join(
                "; ",
                compilation.Errors.Select(error => $"{error.Code}: {error.Path}"));
            return;
        }

        ContentHash = compilation.Pack!.ContentHash;
        _runtime.Prepare(new PrepareWorldRequest(_seed, ContentHash, _contentDirectory));
        _runtime.Start(new StartWorldRequest());
        _subscription = _runtime.SubscribeReadStream();
        _pump = Task.Run(() => PumpAsync(_cancellation.Token), CancellationToken.None);
        _clock = Task.Run(() => ClockAsync(_cancellation.Token), CancellationToken.None);
    }

    /// <summary>Une intervention du viewer est attribuée à un agent et appliquée au prochain tick.</summary>
    public long QueueIntervention(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _runtime.QueueIntervention(command);
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var envelope in _subscription!.ReadAllAsync(cancellationToken))
            {
                _sink.Enqueue(new ViewerStreamEnvelope(
                    envelope.ProtocolVersion,
                    envelope.Sequence,
                    envelope.Tick,
                    envelope.Kind,
                    envelope.Payload));
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

    private async Task ClockAsync(CancellationToken cancellationToken)
    {
        if (_ticksPerSecond <= 0d)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(1d / _ticksPerSecond);
        // La cadence embarquée suit le temps réel du viewer ; elle n'affecte aucune empreinte
        // du run (le nombre de ticks joués, lui, se lit dans le statut du host).
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    _runtime.Step(new TickRequest(
                        ProtocolVersion.Current.ToString(),
                        _runtime.Status.LastCompletedTick + 1,
                        []));
                }
                catch (Exception exception)
                {
                    Error = exception.Message;
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Fermeture normale du viewer : l'horloge embarquée s'arrête avec lui.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync();
        if (_subscription is not null)
        {
            await _subscription.DisposeAsync();
        }

        foreach (var task in new[] { _pump, _clock })
        {
            if (task is null)
            {
                continue;
            }

            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cancellation.Dispose();
    }
}
