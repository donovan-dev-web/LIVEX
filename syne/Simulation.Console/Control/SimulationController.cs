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
    private Task? _runTask;
    private ulong _runGeneration;
    private int _disposed;
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

    public SimulationLoop? CurrentLoop
    {
        get
        {
            lock (_gate)
            {
                return _loop;
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

            /// <summary>
            /// Options effectives du monde préparé (null tant qu'aucun monde n'est
            /// préparé). Exposées pour que les tests puissent vérifier que deux
            /// chemins de préparation (prepare, reset) construisent exactement le
            /// même profil — cf. défaut 1 du RAPPORT-ELEMENTS-OUVERTS §5.1.
            /// </summary>
        public SimulationOptions? PreparedOptions
            {
                get { lock (_gate) return _preparedOptions; }
            }

            /// <summary>Construit le monde déterministe et le laisse en état Ready.</summary>
        public async Task<Simulation.Core.World.WorldDescription> PrepareAsync(
                ulong? seed, string? configJson, int? ticksPerSecond = null,
                CancellationToken cancellationToken = default)
            => await PrepareCoreAsync(seed, configJson, ticksPerSecond, autoReady: false, cancellationToken);

        private async Task<Simulation.Core.World.WorldDescription> PrepareCoreAsync(
                ulong? seed, string? configJson, int? ticksPerSecond, bool autoReady,
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
                // La surcouche reste en JSON brut jusqu'ici : la désérialiser en
                // SimulationOptions la rendrait complète et écraserait le profil de
                // référence avec les valeurs par défaut du type (cf. MergeJson).
                // Sans surcouche, le profil de référence est appliqué explicitement :
                // le chemin reset (configJson null) doit produire exactement le même
                // monde que prepare, sans jamais dépendre des défauts intégrés
                // (défaut 1, RAPPORT-ELEMENTS-OUVERTS §5.1 ; doctrine ADR-016).
                string effectiveConfig = string.IsNullOrWhiteSpace(configJson)
                    ? SimulationProfiles.ReferenceJson()
                    : configJson;
                (SimulationOptions options, ulong effectiveSeed) = SimulationFactory.ResolveOptions(
                    ConfigLoader.LoadDefaults(), effectiveConfig, seed);
                if (ticksPerSecond is not null)
                {
                    if (ticksPerSecond <= 0)
                        throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), "ticksPerSecond doit être strictement positif.");
                    options.Simulation.TicksPerSecond = ticksPerSecond.Value;
                }
                var (_, loop) = SimulationFactory.Build(options, effectiveSeed);
                var description = Simulation.Core.World.WorldDescriptionBuilder.Build(
                    loop.World, effectiveSeed, options.Simulation.WorldCellSize, options.Simulation.TicksPerSecond,
                    options.Simulation.SimulatedSecondsPerTick, options.World.MetersPerUnit);
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
    /// Format canonique du run_id piloté : <c>run-&lt;seed&gt;-&lt;12hex&gt;</c>.
    /// Le seed reste lisible dans l'identifiant (contrat ECHOS, repli ``_seed_of``
    /// sur les flux V0.2.0) et le suffixe aléatoire conserve l'unicité entre deux
    /// runs de même seed — l'ancien format <c>run-&lt;seed&gt;</c> du monde préparé
    /// collisionnait les runs successifs d'un même seed côté stockage ECHOS.
    /// </summary>
    internal static string RunIdFor(ulong seed)
    {
        string hex = Guid.NewGuid().ToString("N")[..12];
        return $"run-{seed}-{hex}";
    }

    /// <summary>
    /// Identité du run à archiver dans le flux d'observabilité. Un identifiant
    /// demandé par l'appelant est retenu tel quel : il est alors déterministe,
    /// ce qu'exige l'export batch (deux runs de même graine doivent produire
    /// le même fichier <c>stream.jsonl</c>). Sans demande, on retombe sur le
    /// format généré, qui garantit l'unicité des runs observés en direct.
    /// </summary>
    private static string ResolveRunId(ulong seed, string? requestedRunId) =>
        string.IsNullOrWhiteSpace(requestedRunId) ? RunIdFor(seed) : requestedRunId;

    /// <summary>
    /// Démarre un run (SYNE-113) pour <paramref name="seed"/> et la surcouche de
    /// configuration <paramref name="configJson"/> (JSON partiel optionnel, en
    /// <b>texte brut</b>, fusionné sur les défauts — cf. <see cref="SimulationFactory.ResolveOptions"/>).
    /// Construit le monde, lance la boucle d'arrière-plan et renvoie l'identifiant
    /// du run.
    /// </summary>
    public async Task<string> StartAsync(
        ulong? seed,
        string? configJson,
        int? maxTicks,
        CancellationToken cancellationToken = default,
        string? requestedRunId = null)
    {
        bool hasConfig = !string.IsNullOrWhiteSpace(configJson);
        if (!WorldPrepared)
            await PrepareCoreAsync(seed, configJson, ticksPerSecond: null, autoReady: true, cancellationToken: cancellationToken);
        else if (!_explicitPreparation && (seed is not null || hasConfig))
            await PrepareAsync(seed, configJson, cancellationToken: cancellationToken);
        else if (_explicitPreparation && hasConfig)
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
        string runId = ResolveRunId(effectiveSeed, requestedRunId);
        TimeSpan tickInterval = TimeSpan.FromSeconds(1d / options.Simulation.TicksPerSecond);
        var emitter = _observabilitySink is null
            ? null
            : new ObservabilityTickEmitter(loop, effectiveSeed, _observabilitySink, runId);

        Task task;
        ulong generation;
        lock (_gate)
        {
            // Un second Start sur un run déjé actif est refusé : deux boucles
            // de tick concurrentes mutileraient le même monde, et l'ancienne
            // tâche — non référencée par _runCts — continuerait de tourner
            // après un Stop (qui n'annule que la plus récente).
            if (IsRunActiveLocked())
            {
                runCts.Dispose();
                throw new InvalidOperationException(
                    "Un run est déjà actif — utilisez /stop ou /reset avant de redémarrer.");
            }

            generation = ++_runGeneration;
            _loop = loop;
            _seed = effectiveSeed;
            _maxTicks = maxTicks;
            _runId = runId;
            _state = SimulationControlState.Running;
            _runCts = runCts;
        }

        // La tâche est conservée : Stop/Reset/Dispose doivent pouvoir l'attendre
        // réellement au lieu de supposer qu'elle s'est terminée.
        task = Task.Run(() => RunLoopAsync(loop, maxTicks, emitter, runCts, tickInterval, generation), CancellationToken.None);
        lock (_gate)
        {
            if (ReferenceEquals(_runCts, runCts))
            {
                _runTask = task;
            }
        }

        return runId;
    }

    /// <summary>
    /// Vrai si une boucle de tick est encore vivante. Doit être appelé sous
    /// <c>_gate</c> : se fier à l'état <c>Running</c> seul laisserait passer
    /// un Start juste après la fin naturelle d'un run (la tâche n'a pas encore
    /// effacé son marqueur).
    /// </summary>
    private bool IsRunActiveLocked() => _runCts is not null && _runTask is { IsCompleted: false };

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
    /// Arrête le run courant et remet le serveur à l'état initial. N'écrit pas
    /// l'état partagé depuis la boucle de tick : la génération est incrémentée
    /// pour que la boucle en cours perde immédiatement le droit d'y toucher.
    /// L'<see cref="StopAsync"/> décharge l'attente complète de la tâche.
    /// </summary>
    public void Stop()
    {
        CancellationTokenSource? runCts;
        lock (_gate)
        {
            runCts = _runCts;
            _runCts = null;
            _runTask = null;
            _runGeneration++;
            _loop = null;
            _runId = string.Empty;
            _state = SimulationControlState.Idle;
            _worldDescription = null;
            _worldReadyAcknowledged = false;
            _explicitPreparation = false;
            _maxTicks = null;
        }

        StopRun(runCts);
        _runSignal.Reset();
    }

    /// <summary>Variante asynchrone de <see cref="Stop"/> : attend la fin de la boucle.</summary>
    public async Task StopAsync()
    {
        CancellationTokenSource? runCts;
        Task? runTask;
        lock (_gate)
        {
            runCts = _runCts;
            runTask = _runTask;
            _runCts = null;
            _runTask = null;
            _runGeneration++;
            _loop = null;
            _runId = string.Empty;
            _state = SimulationControlState.Idle;
            _worldDescription = null;
            _worldReadyAcknowledged = false;
            _explicitPreparation = false;
            _maxTicks = null;
        }

        StopRun(runCts);
        _runSignal.Reset();
        await AwaitRunAsync(runTask).ConfigureAwait(false);
    }

    /// <summary>
    /// Réinitialise le run : arrête la boucle courante puis démarre un nouveau run
    /// pour <paramref name="seed"/> (ou la configuration de départ).
    /// Sans surcouche, le nouveau monde est construit sur le profil de référence
    /// (cf. <see cref="PrepareCoreAsync"/>), au même titre que <c>prepare</c>.
    /// </summary>
    public async Task<string> ResetAsync(
        ulong? seed,
        int? maxTicks,
        string? configJson = null,
        CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? oldCts;
        Task? runTask;
        lock (_gate)
        {
            oldCts = _runCts;
            runTask = _runTask;
            _runCts = null;
            _runTask = null;
            _runGeneration++;
            _state = SimulationControlState.Idle;
            _worldReadyAcknowledged = false;
            _explicitPreparation = false;
            _loop = null;
        }

        StopRun(oldCts);
        _runSignal.Reset();
        await AwaitRunAsync(runTask).ConfigureAwait(false);

        return await StartAsync(seed, configJson, maxTicks, cancellationToken);
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
        TimeSpan tickInterval,
        ulong generation)
    {
        try
        {
            CancellationToken token = runCts.Token;
            while (!token.IsCancellationRequested)
            {
                SimulationControlState state;
                lock (_gate)
                {
                    // Dès le début d'un nouveau run, l'ancienne boucle perd le
                    // droit de piloter l'état : sans ce contrôle de génération, elle
                    // pouvait repasser _state à Finished après que le nouveau run
                    // démarrait (race sur _state partagé).
                    if (generation != _runGeneration)
                    {
                        return;
                    }

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
                        if (generation == _runGeneration)
                        {
                            _state = SimulationControlState.Finished;
                        }
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
        catch (OperationCanceledException)
        {
            // Arrêt attendu (Stop/Reset/Dispose).
        }
        finally
        {
            lock (_gate)
            {
                if (generation == _runGeneration)
                {
                    _runTask = null;
                    _runCts = null;
                }
            }
        }
    }

    /// <summary>
    /// Annule le run courant et <b>attend</b> sa fin. Sans cette attente,
    /// <c>Stop</c>/<c>Reset</c>/<c>Dispose</c> libéraient le monde pendant que
    /// l'ancienne boucle pouvait encore avancer d'un tick.
    /// </summary>
    private async Task StopAndAwaitRunAsync()
    {
        CancellationTokenSource? runCts;
        Task? runTask;
        lock (_gate)
        {
            runCts = _runCts;
            runTask = _runTask;
            _runCts = null;
            _runTask = null;
            _runGeneration++;
        }

        StopRun(runCts);
        await AwaitRunAsync(runTask).ConfigureAwait(false);
    }

    /// <summary>
    /// Attend la fin d'une boucle de tick, bornée dans le temps. Ne lèche
    /// jamais remonter une erreur : un arrêt ne doit pas faire échouer un
    /// <c>stop</c>/<c>reset</c>/<c>dispose</c> qui a réussi son objectif.
    /// </summary>
    private static async Task AwaitRunAsync(Task? runTask)
    {
        if (runTask is null)
        {
            return;
        }

        try
        {
            await runTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // La boucle est restée bloquée (envoi WebSocket lent) : on ne la
            // laisse pas retenir l'appelant, son jeton est déjà annulé.
        }
        catch (OperationCanceledException)
        {
            // arrêt attendu
        }
    }

    private async Task CancelCurrentRunSafeAsync() => await StopAndAwaitRunAsync().ConfigureAwait(false);

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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        CancellationTokenSource? oldCts;
        Task? runTask;
        lock (_gate)
        {
            oldCts = _runCts;
            runTask = _runTask;
            _runCts = null;
            _runTask = null;
            _runGeneration++;
            _state = SimulationControlState.Idle;
        }

        StopRun(oldCts);
        await AwaitRunAsync(runTask).ConfigureAwait(false);

        _runSignal.Dispose();
    }
}