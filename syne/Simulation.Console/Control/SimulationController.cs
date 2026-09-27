using Simulation.Core.Configuration;
using Simulation.Core.Loop;
using Simulation.Console.Observability;

namespace Simulation.Console.Control;

/// <summary>Machine à états du run piloté par HTTP :5181 (API_CONTRACTS.md §3).</summary>
public enum SimulationControlState
{
    /// <summary>Aucun run démarré (état initial).</summary>
    Idle,
    /// <summary>Le monde est en cours de construction.</summary>
    WorldPreparing,
    /// <summary>Le monde est construit et accusé par Unreal, mais le run n'avance pas.</summary>
    Ready,

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
    int? MaxTicks,
    bool WorldPrepared = false,
    string? WorldVersion = null,
    bool WorldReadyAcknowledged = false);

public sealed class PreparedWorldMismatchException : InvalidOperationException
{
    public PreparedWorldMismatchException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>
/// Contrôleur du moteur SYNE exposé via HTTP :5181 (SYNE-113, API_CONTRACTS.md §3).
/// Il administre une <see cref="SimulationLoop"/> en tâche d'arrière-plan et expose
/// les commandes <c>start</c>, <c>pause</c>, <c>resume</c>, <c>stop</c>, <c>reset</c> ainsi qu'un
/// état interrogable (<c>status</c>). Le contrôle est **non intrusif** (SYNE-081,
/// DETERMINISM.md §3) : aucune commande ne retire de tirage au PRNG ni ne change la
/// trajectoire — elles gèlent/reprises simplement l'avancement des ticks.
/// </summary>
public sealed class SimulationController : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly ManualResetEventSlim _runSignal = new(initialState: false);
    private readonly IObservabilitySink? _observabilitySink;
    private SimulationLoop? _loop;
    private SimulationControlState _state = SimulationControlState.Idle;
    private ulong _seed;
    private string _runId = string.Empty;
    private int? _maxTicks;
    private CancellationTokenSource? _runCts;
    private Simulation.Core.World.WorldDescription? _worldDescription;
    private SimulationOptions? _preparedOptions;
    private bool _worldReadyAcknowledged;
    private bool _explicitPreparation;

    public SimulationController(IObservabilitySink? observabilitySink = null)
    {
        _observabilitySink = observabilitySink;
    }

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

        public bool WorldPrepared
            {
                get { lock (_gate) return _worldDescription is not null && _loop is not null; }
            }

        public bool WorldReadyAcknowledged
        {
            get { lock (_gate) return _worldReadyAcknowledged; }
        }

        public Simulation.Core.World.WorldDescription? WorldDescription
            {
                get { lock (_gate) return _worldDescription; }
            }

            /// <summary>Construit le monde déterministe et le laisse en état Ready.</summary>
        public async Task<Simulation.Core.World.WorldDescription> PrepareAsync(
                ulong? seed, SimulationOptions? config, int? ticksPerSecond = null,
                CancellationToken cancellationToken = default)
            => await PrepareCoreAsync(seed, config, ticksPerSecond, autoReady: false, cancellationToken);

        private async Task<Simulation.Core.World.WorldDescription> PrepareCoreAsync(
                ulong? seed, SimulationOptions? config, int? ticksPerSecond, bool autoReady,
                CancellationToken cancellationToken)
            {
                await CancelCurrentRunSafeAsync();
                cancellationToken.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    _state = SimulationControlState.WorldPreparing;
                    _worldDescription = null;
                    _preparedOptions = null;
                    _worldReadyAcknowledged = false;
                    _explicitPreparation = !autoReady;
                }
                (SimulationOptions options, ulong effectiveSeed) = SimulationFactory.ResolveOptions(
                    ConfigLoader.LoadDefaults(), config, seed);
                if (ticksPerSecond is not null)
                {
                    if (ticksPerSecond <= 0)
                        throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), "ticksPerSecond doit être strictement positif.");
                    options.Simulation.TicksPerSecond = ticksPerSecond.Value;
                }
                var (_, loop) = SimulationFactory.Build(options, effectiveSeed);
                var description = Simulation.Core.World.WorldDescriptionBuilder.Build(
                    loop.World, effectiveSeed, options.Simulation.WorldCellSize, options.Simulation.TicksPerSecond);
                lock (_gate)
                {
                    _loop = loop;
                    _preparedOptions = options;
                    _seed = effectiveSeed;
                    _runId = $"run-{effectiveSeed}";
                    _maxTicks = null;
                    _worldDescription = description;
                    _state = SimulationControlState.Ready;
                    _worldReadyAcknowledged = autoReady;
                }
                if (_observabilitySink is not null)
                {
                    await _observabilitySink.BroadcastAsync(System.Text.Json.JsonSerializer.Serialize(
                        new { type = "world_initialized", version = description.Version, seed = effectiveSeed, world = description },
                        new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
                }
                return description;
            }

            /// <summary>Accusé de réception Unreal. L'accusé est idempotent.</summary>
        public bool AcknowledgeReady(string? version)
            {
                lock (_gate)
                {
                    if (_state != SimulationControlState.Ready
                        || (!string.IsNullOrWhiteSpace(version) && version != _worldDescription?.Version))
                        return false;
                    _worldReadyAcknowledged = true;
                    return true;
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
        if (!WorldPrepared)
            await PrepareCoreAsync(seed, config, ticksPerSecond: null, autoReady: true, cancellationToken: cancellationToken);
        else if (!_explicitPreparation && (seed is not null || config is not null))
            await PrepareAsync(seed, config, cancellationToken: cancellationToken);
        else if (_explicitPreparation && config is not null)
            throw new PreparedWorldMismatchException("prepared_config_mismatch", "La configuration du monde préparé ne peut pas être remplacée au démarrage ; appelez Prepare pour générer un nouveau monde.");
        else if (_explicitPreparation && seed is not null && seed != _seed)
            throw new PreparedWorldMismatchException("prepared_seed_mismatch", "Le seed de Start doit correspondre au seed du monde préparé ; appelez Prepare pour en générer un nouveau.");

        lock (_gate)
        {
            if (_explicitPreparation && !_worldReadyAcknowledged)
                throw new InvalidOperationException("Le monde préparé explicitement doit être accusé via /api/control/ready avant start.");
        }

        SimulationOptions options;
        ulong effectiveSeed;
        SimulationLoop loop;
        lock (_gate)
        {
            options = _preparedOptions ?? ConfigLoader.LoadDefaults();
            effectiveSeed = _seed;
            loop = _loop!;
        }
        var runCts = new CancellationTokenSource();
        string runId = Guid.NewGuid().ToString("N")[..12];
        TimeSpan tickInterval = TimeSpan.FromSeconds(1d / options.Simulation.TicksPerSecond);
        var emitter = _observabilitySink is null
            ? null
            : new ObservabilityTickEmitter(loop, effectiveSeed, _observabilitySink, runId);

        lock (_gate)
        {
            _loop = loop;
            _seed = effectiveSeed;
            _maxTicks = maxTicks;
            _runId = runId;
            _state = SimulationControlState.Running;
            _runCts = runCts;
        }

        _ = Task.Run(() => RunLoopAsync(loop, maxTicks, emitter, runCts, tickInterval), CancellationToken.None);

        return runId;
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

    /// <summary>Arrête le run courant et remet le serveur à l'état initial.</summary>
    public void Stop()
    {
        CancellationTokenSource? runCts;
        lock (_gate)
        {
            runCts = _runCts;
            _runCts = null;
            _loop = null;
            _runId = string.Empty;
            _state = SimulationControlState.Idle;
            _worldDescription = null;
            _worldReadyAcknowledged = false;
            _explicitPreparation = false;
            _maxTicks = null;
        }

        StopRun(runCts);
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
            _worldReadyAcknowledged = false;
            _explicitPreparation = false;
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
            return new SimulationStatusSnapshot(_state, _runId, tick, alive, _seed, _maxTicks,
                _worldDescription is not null, _worldDescription?.Version, _worldReadyAcknowledged);
        }
    }

    private async Task RunLoopAsync(
        SimulationLoop loop,
        int? targetTicks,
        ObservabilityTickEmitter? emitter,
        CancellationTokenSource runCts,
        TimeSpan tickInterval)
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

                if (targetTicks is not null && loop.CurrentTick >= (ulong)targetTicks.Value)
                {
                    lock (_gate)
                    {
                        _state = SimulationControlState.Finished;
                    }

                    return;
                }

                loop.AdvanceOneTick();
                if (emitter is not null)
                {
                    await emitter.EmitCurrentTickAsync();
                }

                await Task.Delay(tickInterval, token);
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