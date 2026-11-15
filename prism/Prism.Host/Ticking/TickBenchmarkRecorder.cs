// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : relevé machine-lisible du coût du tick (PRISM-106) — une ligne JSON par
//         tick, consommée par le harnais de benchmark couplé. Le profilage
//         textuel (`PRISM_TICK_PROFILE=1`) sert à lire un run à l'œil ; celui-ci
//         sert à le rejouer en CI sans grep de log.

using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Prism.Core.Navigation;

namespace Prism.Host.Ticking;

/// <summary>
/// Relevé JSONL opt-in du tick, activé par <c>PRISM_BENCHMARK_JSON=&lt;chemin&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Une ligne par tick, en JSON invariant : durées par phase du monde, détail des
/// écritures différées, octets alloués et pause GC du tick, taille de la réponse.
/// Le harnais <c>scripts/prism-coupled-benchmark.py</c> agrège ces lignes en
/// médianes et p95 sur plusieurs exécutions.
/// </para>
/// <para>
/// **Le relevé ne décide rien.** Activé, il n'ajoute que le coût de la mesure
/// (horodatages, une écriture de quelques centaines d'octets) ; le monde, l'ordre
/// des phases et les empreintes sont inchangés. Désactivé, il vaut
/// <see langword="null"/> et les chemins d'appel retombent sur le code actuel.
/// </para>
/// </remarks>
public sealed class TickBenchmarkRecorder : IDisposable
{
    /// <summary>Variable d'environnement qui active le relevé (chemin du fichier JSONL).</summary>
    public const string EnvironmentVariable = "PRISM_BENCHMARK_JSON";

    private static readonly Lazy<TickBenchmarkRecorder?> Shared =
        new(FromEnvironment, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly StreamWriter _writer;
    private readonly object _gate = new();
    private readonly ArrayBufferWriter<byte> _buffer = new(1024);
    private readonly Dictionary<string, double> _phasesMs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _phasesAllocated = new(StringComparer.Ordinal);

    private long _tick = -1;
    private long _allocatedAtStart;
    private TimeSpan _pauseAtStart;
    private long _requestAllocated;
    private double _requestMs;
    private int _writes;
    private double _writesMs;
    private double _writesMaxMs;
    private double _writesPauseMs;
    private long _writesAllocated;
    private double _outputMs;
    private long _outputAllocated;
    private int _payloadBytes;
    private int _observations;
    private int _events;
    private PathfindingMetricsSnapshot _pathfinding;

    private TickBenchmarkRecorder(string path)
    {
        _writer = new StreamWriter(
            new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = false,
        };
    }

    /// <summary>Relevé actif, ou <see langword="null"/> si l'environnement ne le demande pas.</summary>
    public static TickBenchmarkRecorder? Current => Shared.Value;

    /// <summary>Ouvre le relevé si <c>PRISM_BENCHMARK_JSON</c> désigne un fichier.</summary>
    /// <remarks>
    /// Un chemin invalide ne doit pas empêcher l'hôte de tourner : l'avertissement
    /// part sur stderr et le relevé reste désactivé.
    /// </remarks>
    public static TickBenchmarkRecorder? FromEnvironment()
    {
        string? path = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return new TickBenchmarkRecorder(Path.GetFullPath(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine(
                $"[benchmark] relevé JSON désactivé ({EnvironmentVariable}={path}) : {exception.Message}");
            return null;
        }
    }

    /// <summary>Ouvre le relevé au premier tick mesuré et remet les compteurs à zéro.</summary>
    public void BeginTick(long tick)
    {
        _tick = tick;
        _phasesMs.Clear();
        _phasesAllocated.Clear();
        _allocatedAtStart = GC.GetTotalAllocatedBytes(precise: false);
        _pauseAtStart = GC.GetTotalPauseDuration();
        _requestMs = 0;
        _requestAllocated = 0;
        _writes = 0;
        _writesMs = 0;
        _writesMaxMs = 0;
        _writesPauseMs = 0;
        _writesAllocated = 0;
        _outputMs = 0;
        _outputAllocated = 0;
        _payloadBytes = 0;
        _observations = 0;
        _events = 0;
        _pathfinding = default;
    }

    /// <summary>Durée et octets alloués d'une phase du monde.</summary>
    public void RecordPhase(WorldTickPhase phase, double milliseconds, long allocatedBytes = 0)
    {
        var name = phase.ToString();
        _phasesMs[name] = milliseconds;
        _phasesAllocated[name] = allocatedBytes;
    }

    /// <summary>
    /// Coût de la copie de la requête du tick (aller-retour JSON), mesuré avant
    /// les phases. Sert à nommer une allocation de protocole, pas de monde.
    /// </summary>
    public void RecordRequest(double milliseconds, long allocatedBytes)
    {
        _requestMs = milliseconds;
        _requestAllocated = allocatedBytes;
    }

    /// <summary>Détail des écritures différées appliquées pendant le tick.</summary>
    public void RecordWrites(long tick, int count, double totalMs, double maxMs, double pauseMs, long allocatedBytes)
    {
        if (tick != _tick)
        {
            return;
        }

        _writes = count;
        _writesMs = totalMs;
        _writesMaxMs = maxMs;
        _writesPauseMs = pauseMs;
        _writesAllocated = allocatedBytes;
    }

    /// <summary>
    /// Compteurs de recherche de chemin du tick (PRISM-104) : recherches exécutées, servies par
    /// le cache, entrées périmées, nœuds développés, échecs et budgets épuisés. C'est ce qui
    /// distingue « le tick est lent parce qu'il calcule » de « le tick est lent parce qu'il
    /// cherche des chemins qui n'existent pas ».
    /// </summary>
    public void RecordPathfinding(PathfindingMetricsSnapshot metrics) => _pathfinding = metrics;

    /// <summary>Coût de la création et de la sérialisation du résultat du tick.</summary>
    public void RecordOutput(
        double milliseconds,
        int payloadBytes,
        int observations,
        int events,
        long allocatedBytes)
    {
        _outputMs = milliseconds;
        _payloadBytes = payloadBytes;
        _observations = observations;
        _events = events;
        _outputAllocated = allocatedBytes;
    }

    /// <summary>Clôt le tick et écrit sa ligne JSON.</summary>
    public void CompleteTick(long tick, double totalMs)
    {
        if (tick != _tick)
        {
            return;
        }

        // Les deltas sont lus AVANT de sérialiser : la mesure ne se compte pas
        // elle-même dans les octets alloués du tick.
        long allocated = Math.Max(0, GC.GetTotalAllocatedBytes(precise: false) - _allocatedAtStart);
        double pauseMs = Math.Max(0, (GC.GetTotalPauseDuration() - _pauseAtStart).TotalMilliseconds);

        _buffer.Clear();
        using (var writer = new Utf8JsonWriter(_buffer, new JsonWriterOptions { SkipValidation = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("tick", tick);
            writer.WriteNumber("totalMs", Math.Round(totalMs, 3));

            writer.WriteStartObject("phasesMs");
            foreach ((string phase, double milliseconds) in _phasesMs)
            {
                writer.WriteNumber(phase, Math.Round(milliseconds, 3));
            }

            writer.WriteEndObject();

            writer.WriteStartObject("phasesAllocatedBytes");
            foreach ((string phase, long allocatedBytes) in _phasesAllocated)
            {
                writer.WriteNumber(phase, allocatedBytes);
            }

            writer.WriteEndObject();

            writer.WriteStartObject("request");
            writer.WriteNumber("ms", Math.Round(_requestMs, 3));
            writer.WriteNumber("allocatedBytes", _requestAllocated);
            writer.WriteEndObject();

            writer.WriteStartObject("writes");
            writer.WriteNumber("count", _writes);
            writer.WriteNumber("totalMs", Math.Round(_writesMs, 3));
            writer.WriteNumber("maxMs", Math.Round(_writesMaxMs, 3));
            writer.WriteNumber("pauseGcMs", Math.Round(_writesPauseMs, 3));
            writer.WriteNumber("allocatedBytes", _writesAllocated);
            writer.WriteEndObject();

            writer.WriteNumber("outputMs", Math.Round(_outputMs, 3));
            writer.WriteNumber("outputAllocatedBytes", _outputAllocated);
            writer.WriteNumber("payloadBytes", _payloadBytes);
            writer.WriteNumber("observations", _observations);
            writer.WriteNumber("events", _events);

            writer.WriteStartObject("pathfinding");
            writer.WriteNumber("searches", _pathfinding.Searches);
            writer.WriteNumber("cacheHits", _pathfinding.CacheHits);
            writer.WriteNumber("cacheInvalidations", _pathfinding.CacheInvalidations);
            writer.WriteNumber("expandedNodes", _pathfinding.ExpandedNodes);
            writer.WriteNumber("failed", _pathfinding.FailedSearches);
            writer.WriteNumber("budgetExhausted", _pathfinding.BudgetExhaustedSearches);
            writer.WriteNumber("failureRatePermille", _pathfinding.FailureRatePermille);
            writer.WriteEndObject();

            writer.WriteNumber("allocatedBytes", allocated);
            writer.WriteNumber("gcPauseMs", Math.Round(pauseMs, 3));
            writer.WriteEndObject();
        }

        string line = Encoding.UTF8.GetString(_buffer.WrittenSpan);
        lock (_gate)
        {
            _writer.WriteLine(line);
            _writer.Flush();
        }

        _tick = -1;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer.Dispose();
        }
    }
}
