// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Contrôleur du moteur SYNE exposé via HTTP :5181 (SYNE-113).

using Simulation.Core.Configuration;
using Simulation.Core.Loop;
using Simulation.Console.Observability;

namespace Simulation.Console.Control;

/// <summary>Machine à états du run piloté par HTTP :5181.</summary>
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

/// <summary>État courant du run piloté (GET /api/control/status).</summary>
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
/// Contrôleur du moteur SYNE exposé via HTTP :5181 (SYNE-113).
/// Il administre une <see cref="SimulationLoop"/> en tâche d'arrière-plan et expose
/// les commandes <c>start</c>, <c>pause</c>, <c>resume</c>, <c>stop</c>, <c>reset</c> ainsi qu'un
/// état interrogable (<c>status</c>). Le contrôle est **non intrusif** (SYNE-081,
/// aucune commande ne retire de tirage au PRNG ni ne change la
/// trajectoire — elles gèlent/reprises simplement l'avancement des ticks.
/// </summary>
public sealed partial class SimulationController : IAsyncDisposable
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
            /// même profil (doctrine).
            /// </summary>
        public SimulationOptions? PreparedOptions
            {
                get { lock (_gate) return _preparedOptions; }
            }
}
