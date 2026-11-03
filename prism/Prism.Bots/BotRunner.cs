using System.Text.Json;
using Prism.Contracts;
using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Bots;

/// <summary>Where an agent enters the world.</summary>
public sealed record BotSpawn(ulong AgentId, BotCoordinate Position);

/// <summary>Per-agent accounting of a bot run.</summary>
public sealed record BotAgentReport(
    ulong AgentId,
    string BotKind,
    int CommandsSent,
    int CommandsAccepted,
    IReadOnlyDictionary<string, int> FailureReasons);

/// <summary>Outcome of a bot run: the pinned fingerprint plus why commands were refused.</summary>
public sealed record BotRunResult(
    long FirstTick,
    long LastTick,
    ulong Fingerprint,
    int CommandsSent,
    int CommandsAccepted,
    IReadOnlyList<BotAgentReport> Agents,
    IReadOnlyDictionary<string, int> FailureReasons)
{
    public string FingerprintHex => RunFingerprint.Format(Fingerprint);
}

/// <summary>
/// Drives an <see cref="IWorldHost"/> with bots, exactly like a client would over the contract:
/// incarnation, one tick request per tick, observations fed back to the bots. It never reads the
/// world directly, so nothing here can bypass the contract or the command vocabulary.
/// </summary>
public sealed class BotRunner
{
    private const int CommandIdIndex = 0;

    private readonly IWorldHost _host;
    private readonly IReadOnlyDictionary<ulong, IPrismBot> _bots;
    private readonly Dictionary<ulong, BotPerceptionMemory> _memories = [];
    private readonly Dictionary<string, string> _commandKinds = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, List<BotCommandFailure>> _failures = [];
    private readonly Dictionary<ulong, int> _commandsSent = [];
    private readonly Dictionary<ulong, int> _commandsAccepted = [];
    private readonly Dictionary<ulong, Dictionary<string, int>> _failureReasons = [];

    public BotRunner(IWorldHost host, IReadOnlyDictionary<ulong, IPrismBot> bots)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(bots);
        _host = host;
        _bots = bots;
        foreach (var agentId in bots.Keys)
        {
            _memories[agentId] = new BotPerceptionMemory();
            _failures[agentId] = [];
            _commandsSent[agentId] = 0;
            _commandsAccepted[agentId] = 0;
            _failureReasons[agentId] = new Dictionary<string, int>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Runs <paramref name="ticks"/> ticks from <paramref name="startTick"/> inclusive. The first
    /// request incarnates every spawn; the host must already be started.
    /// </summary>
    public BotRunResult Run(long startTick, IReadOnlyList<BotSpawn> spawns, long ticks)
    {
        ArgumentNullException.ThrowIfNull(spawns);
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        if (spawns.Count != _bots.Count)
        {
            throw new ArgumentException("Every bot needs exactly one spawn.", nameof(spawns));
        }

        foreach (var spawn in spawns)
        {
            if (!_bots.ContainsKey(spawn.AgentId))
            {
                throw new ArgumentException($"Spawn '{spawn.AgentId}' has no bot.", nameof(spawns));
            }
        }

        var hash = RunFingerprint.Empty;
        var commands = new List<CommandEnvelope>(spawns.Count);
        foreach (var spawn in spawns)
        {
            commands.Add(CreateCommand(spawn.AgentId, startTick, CommandIdIndex, BotCommand.Incarnate(spawn.Position)));
        }

        var lastTick = startTick - 1;
        for (var offset = 0L; offset < ticks; offset++)
        {
            var tick = checked(startTick + offset);
            var result = _host.Step(new TickRequest(ProtocolVersion.Current.ToString(), tick, commands));
            hash = HashTick(hash, result);
            ApplyResult(result);
            lastTick = tick;
            commands = DecideCommands(checked(tick + 1));
        }

        var agents = new List<BotAgentReport>(_bots.Count);
        foreach (var entry in _bots.OrderBy(static item => item.Key))
        {
            var agentId = entry.Key;
            agents.Add(new BotAgentReport(
                agentId,
                entry.Value.Kind,
                _commandsSent[agentId],
                _commandsAccepted[agentId],
                new Dictionary<string, int>(_failureReasons[agentId], StringComparer.Ordinal)));
        }

        var totals = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var report in agents)
        {
            foreach (var (reason, count) in report.FailureReasons)
            {
                totals[reason] = totals.GetValueOrDefault(reason) + count;
            }
        }

        return new BotRunResult(
            startTick,
            lastTick,
            hash,
            _commandsSent.Values.Sum(),
            _commandsAccepted.Values.Sum(),
            agents,
            new Dictionary<string, int>(totals, StringComparer.Ordinal));
    }

    private List<CommandEnvelope> DecideCommands(long tick)
    {
        var commands = new List<CommandEnvelope>();
        foreach (var entry in _bots.OrderBy(static item => item.Key))
        {
            var agentId = entry.Key;
            var memory = _memories[agentId];
            var senses = new BotSenses(
                tick,
                _lastPosition.GetValueOrDefault(agentId, AgentPosition.AtCell(0, 0, 0)),
                _lastInventory.GetValueOrDefault(agentId, Array.Empty<AgentItemStack>()),
                memory.Entries,
                _failures[agentId]);
            _failures[agentId] = [];

            var decisions = entry.Value.Decide(senses);
            var index = 0;
            foreach (var decision in decisions)
            {
                commands.Add(CreateCommand(agentId, tick, index++, decision));
            }
        }

        return commands;
    }

    private readonly Dictionary<ulong, AgentPosition> _lastPosition = [];
    private readonly Dictionary<ulong, IReadOnlyList<AgentItemStack>> _lastInventory = [];

    private CommandEnvelope CreateCommand(ulong agentId, long tick, int index, BotCommand command)
    {
        var commandId = $"bot-{agentId}-{tick}-{index}";
        _commandKinds[commandId] = command.Kind;
        _commandsSent[agentId] = _commandsSent.GetValueOrDefault(agentId) + 1;
        return new CommandEnvelope(
            ProtocolVersion.Current.ToString(),
            tick,
            commandId,
            agentId,
            command.Kind,
            command.Payload);
    }

    private static ulong HashTick(ulong hash, TickResult result)
    {
        hash = RunFingerprint.Append(hash, result.Tick);
        foreach (var eventEnvelope in result.Events)
        {
            hash = RunFingerprint.Append(hash, eventEnvelope.Kind);
            hash = RunFingerprint.Append(hash, eventEnvelope.AgentId?.ToString() ?? "-");
            hash = RunFingerprint.Append(hash, eventEnvelope.Payload.GetRawText());
        }

        foreach (var observation in result.Observations)
        {
            hash = RunFingerprint.Append(hash, "obs");
            hash = RunFingerprint.Append(hash, observation.AgentId);
            hash = RunFingerprint.Append(hash, observation.Changes.GetRawText());
            hash = RunFingerprint.Append(hash, observation.Self.GetRawText());
        }

        return hash;
    }

    private void ApplyResult(TickResult result)
    {
        foreach (var eventEnvelope in result.Events)
        {
            if (!StringComparer.Ordinal.Equals(eventEnvelope.Kind, "action_failed") ||
                eventEnvelope.AgentId is not { } failedAgentId ||
                !_failures.TryGetValue(failedAgentId, out var failures))
            {
                continue;
            }

            var reason = ReadReason(eventEnvelope.Payload);
            var commandKind = ReadCommandKind(eventEnvelope.Payload);
            failures.Add(new BotCommandFailure(commandKind, reason));
            var reasons = _failureReasons[failedAgentId];
            reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
        }

        foreach (var eventEnvelope in result.Events)
        {
            if (eventEnvelope.AgentId is { } agentId &&
                _commandsAccepted.ContainsKey(agentId) &&
                eventEnvelope.Kind is "action_completed" or "action_started")
            {
                _commandsAccepted[agentId] = _commandsAccepted.GetValueOrDefault(agentId) + 1;
            }
        }

        foreach (var observation in result.Observations)
        {
            if (!_memories.TryGetValue(observation.AgentId, out var memory))
            {
                continue;
            }

            var frame = JsonSerializer.Deserialize<PerceptionChangesFrame>(
                            observation.Changes,
                            ProtocolJson.Options)
                        ?? throw new JsonException("Observation changes could not be read.");
            memory.Merge(observation.Tick, frame.Changes ?? []);
            var self = JsonSerializer.Deserialize<SelfObservation>(observation.Self, ProtocolJson.Options)
                       ?? throw new JsonException("Observation self could not be read.");
            _lastPosition[observation.AgentId] = self.Position;
            _lastInventory[observation.AgentId] = self.Inventory;
        }
    }

    private string ReadCommandKind(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("commandId", out var commandIdElement) &&
            commandIdElement.ValueKind == JsonValueKind.String &&
            commandIdElement.GetString() is { } commandId &&
            _commandKinds.TryGetValue(commandId, out var kind))
        {
            return kind;
        }

        return "unknown";
    }

    private static string ReadReason(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty("reason", out var reasonElement) &&
        reasonElement.ValueKind == JsonValueKind.String
            ? reasonElement.GetString() ?? "unspecified"
            : "unspecified";

    private sealed record PerceptionChangesFrame(PerceptionChange[]? Changes);
}
