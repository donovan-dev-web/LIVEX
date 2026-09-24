using Simulation.Core.Configuration;
using Simulation.Core.Loop;

namespace Simulation.Console.Control;

/// <summary>Machine à états du run piloté par HTTP :5181 (API_CONTRACTS.md §3).</summary>
public enum SimulationControlState
{
    /// <summary>Aucun run démarré (état initial).</summary>
    Idle,

    /// <summary>Run en cours d'exécution, les ticks avancent.</summary>
    Running,

    /// <summary>Run suspendu : la boucle n'avance plus tant que <c>resume</c> n'a pas été appelé.</summary>
    Paused,

    /// <summary>Run terminé (le nombre de ticks cible est atteint).</summary>
    Finished,
}

/// <summary>État courant du run piloté (API_CONTRACTS.md §3 — GET /api/control/status).</summary>
public sealed record SimulationStatusSnapshot(
    SimulationControlState State,
    string RunId,
    ulong Tick,
    int AliveCount,
    ulong Seed,
    int MaxTicks);

/// <summary>
/// Contrôleur du moteur SYNE exposé via HTTP :5181 (SYNE-113, API_CONTRACTS.md §3).
/// Il administre une <see cref="SimulationLoop"/> en tâche d'arrière-plan et expose
/// les commandes <c>start</c>, <c>pause</c>, <c>resume</c>, <c>reset</c> ainsi qu'un
/// état interrogable (<c>status</c>). Le contrôle est **non intrusif** (SYNE-081,
/// DETERMINISM.md §3) : aucune commande ne retire de tirage au PRNG ni ne change la
/// trajectoire — elles gèlent/reprises simplement l'avancement des ticks.
/// </summary>
public sealed class SimulationController : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly ManualResetEventSlim _runSignal = new(initialState: false);
    private SimulationLoop? _loop;
    private SimulationControlState _state = SimulationControlState.Idle;
    private ulong _seed;
    private string _runId = string.Empty;
    private int _maxTicks;
    private CancellationTokenSource? _runCts;

    public SimulationControlState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public string RunId
    {
        get
        {
            lock (_gate)
            {
                return _runId;
            }
        }
    }

    public bool HasRun
    {
        get
        {
            lock (_gate)
            {
                return _loop is not null;
            }
        }
    }

    /// <summary>
    /// Démarre un run (SYNE-113) pour <paramref name="seed"/> (défaut : la
    /// configuration) et la surcouche de configuration <paramref name="config"/>
    /// (JSON partiel optionnel, fusionné sur les défauts). Construit le monde,
    /// lance la boucle d'arrière-plan et renvoie l'identifiant du run.
    /// </summary>
    public async Task<string> StartAsync(
        ulong? seed,
        SimulationOptions? config,
        int? maxTicks,
        CancellationToken cancellationToken = default)
    {
        await CancelCurrentRunSafeAsync();

        int target = maxTicks ?? config?.Simulation.MaxTicks ?? 1_000_000;
        (SimulationOptions options, ulong effectiveSeed) = SimulationFactory.ResolveOptions(
            ConfigLoader.LoadDefaults(), config, seed);
        var (_, loop) = SimulationFactory.Build(options, effectiveSeed);

        var runCts = new CancellationTokenSource();

        lock (_gate)
        {
            _loop = loop;
            _seed = effectiveSeed;
            _maxTicks = target;
            _runId = Guid.NewGuid().ToString("N")[..12];
            _state = SimulationControlState.Running;
            _runCts = runCts;
        }

        _ = Task.Run(() => RunLoopAsync(loop, target, runCts), CancellationToken.None);

        return _runId;
    }

    /// <summary>Suspend l'avancement : la boucle gèle au plus vite (au plus un tick après l'appel).</summary>
    public void Pause()
    {
        lock (_gate)
        {
            if (_state == SimulationControlState.Running)
            {
                _state = SimulationControlState.Paused;
            }
        }
    }

    /// <summary>Reprend l'avancement après une pause.</summary>
    public void Resume()
    {
        bool waken = false;
        lock (_gate)
        {
            if (_state == SimulationControlState.Paused)
            {
                _state = SimulationControlState.Running;
                waken = true;
            }
        }

        if (waken)
        {
            _runSignal.Set();
        }
    }

    /// <summary>
    /// Réinitialise le run : arrête la boucle courante puis démarre un nouveau run
    /// pour <paramref name="seed"/> (ou la configuration de départ).
    /// </summary>
    public async Task<string> ResetAsync(
        ulong? seed,
        int? maxTicks,
        CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? oldCts;
        lock (_gate)
        {
            oldCts = _runCts;
            _runCts = null;
            _state = SimulationControlState.Idle;
        }

        StopRun(oldCts);
        _runSignal.Reset();

        lock (_gate)
        {
            _loop = null;
        }

        return await StartAsync(seed, config: null, maxTicks, cancellationToken);
    }

    /// <summary>État courant (API_CONTRACTS.md §3, GET /api/control/status).</summary>
    public SimulationStatusSnapshot Status()
    {
        lock (_gate)
        {
            ulong tick = _loop is null ? 0 : _loop.CurrentTick;
            int alive = _loop is null ? 0 : _loop.World.Entities.Count;
            return new SimulationStatusSnapshot(_state, _runId, tick, alive, _seed, _maxTicks);
        }
    }

    private async Task RunLoopAsync(SimulationLoop loop, int targetTicks, CancellationTokenSource runCts)
    {
        using (runCts)
        {
            CancellationToken token = runCts.Token;
            while (!token.IsCancellationRequested)
            {
                SimulationControlState state;
                lock (_gate)
                {
                    state = _state;
                }

                if (state == SimulationControlState.Paused)
                {
                    try
                    {
                        _runSignal.Wait(token);
                        _runSignal.Reset();
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    continue;
                }

                if (state != SimulationControlState.Running)
                {
                    await Task.Delay(10, token);
                    continue;
                }

                if (loop.CurrentTick >= (ulong)targetTicks)
                {
                    lock (_gate)
                    {
                        _state = SimulationControlState.Finished;
                    }

                    return;
                }

                loop.AdvanceOneTick();
            }
        }
    }

    private async Task CancelCurrentRunSafeAsync()
    {
        CancellationTokenSource? oldCts;
        lock (_gate)
        {
            oldCts = _runCts;
            _runCts = null;
        }

        if (oldCts is null)
        {
            return;
        }

        StopRun(oldCts);
    }

    private static void StopRun(CancellationTokenSource? runCts)
    {
        if (runCts is null)
        {
            return;
        }

        try
        {
            runCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // déjà annulé
        }
    }

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? oldCts;
        lock (_gate)
        {
            oldCts = _runCts;
            _runCts = null;
            _state = SimulationControlState.Idle;
        }

        StopRun(oldCts);
        try
        {
            await Task.Delay(20);
        }
        catch (OperationCanceledException)
        {
            // arrêt attendu
        }

        _runSignal.Dispose();
    }
}