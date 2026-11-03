using System.Text.Json;
using Prism.Contracts;
using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Bots.Tests;

/// <summary>
/// Minimal deterministic host: it incarnates agents, publishes one perception frame per agent and
/// can refuse a given command kind on demand. It exists so the bot logic is tested in
/// milliseconds, without paying world generation.
/// </summary>
internal sealed class FakeWorldHost : IWorldHost
{
    private readonly Dictionary<ulong, AgentPosition> _agents = [];
    private readonly List<(BotCoordinate Position, ushort MaterialId)> _blocks;


    public FakeWorldHost(BotCoordinate? block = null, ushort blockMaterialId = 4)
    {
        _blocks = block is null ? [] : [(block.Value, blockMaterialId)];
    }

    public string? FailCommandKind { get; set; }

    public string FailReason { get; set; } = "refused_by_fake";

    public List<TickResult> Results { get; } = [];

    public List<(ulong AgentId, string Kind)> SentCommands { get; } = [];

    public IReadOnlyDictionary<ulong, AgentPosition> Agents => _agents;

    public AgentPosition PositionOf(ulong agentId) => _agents[agentId];

    public TickResult Step(TickRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var events = new List<EventEnvelope>();
        foreach (var command in request.Commands)
        {

            SentCommands.Add((command.AgentId, command.Kind));
            switch (command.Kind)
            {
                case "Incarner":
                    _agents[command.AgentId] = AgentPosition.AtCell(
                        command.Payload.GetProperty("position").GetProperty("x").GetInt32(),
                        command.Payload.GetProperty("position").GetProperty("y").GetInt32(),
                        command.Payload.GetProperty("position").GetProperty("z").GetInt32());
                    events.Add(CreateEvent(request.Tick, command, "action_completed", null));
                    break;
                case "Aller":
                    _agents[command.AgentId] = AgentPosition.AtCell(
                        command.Payload.GetProperty("target").GetProperty("x").GetInt32(),
                        command.Payload.GetProperty("target").GetProperty("y").GetInt32(),
                        command.Payload.GetProperty("target").GetProperty("z").GetInt32());
                    events.Add(CreateEvent(request.Tick, command, "action_completed", null));
                    break;
                default:
                    var failed = StringComparer.Ordinal.Equals(command.Kind, FailCommandKind);
                    events.Add(CreateEvent(
                        request.Tick,
                        command,
                        failed ? "action_failed" : "action_completed",
                        failed ? FailReason : null));
                    break;
            }
        }

        var observations = new List<ObservationEnvelope>();
        foreach (var agent in _agents.OrderBy(static entry => entry.Key))
        {
            var changes = new List<PerceptionChange>();
            foreach (var block in _blocks)
            {
                changes.Add(new PerceptionChange(
                    PerceptionChangeKind.Appeared,
                    new PerceptionEntry(
                        PerceptionEntityKind.Block,
                        block.Position.X,
                        block.Position.Y,
                        block.Position.Z,
                        0,
                        block.MaterialId,
                        0,
                        0,
                        0,
                        0)));
            }

            var frame = new { changes };
            observations.Add(new ObservationEnvelope(
                ProtocolVersion.Current.ToString(),
                request.Tick,
                agent.Key,
                JsonSerializer.SerializeToElement(frame, ProtocolJson.Options),
                JsonSerializer.SerializeToElement(
                    new SelfObservation(agent.Value, Array.Empty<AgentItemStack>()),
                    ProtocolJson.Options)));
        }

        var result = new TickResult(ProtocolVersion.Current.ToString(), request.Tick, observations, events);
        Results.Add(result);
        return result;
    }

    private static EventEnvelope CreateEvent(long tick, CommandEnvelope command, string kind, string? reason) =>
        new(
            ProtocolVersion.Current.ToString(),
            tick,
            $"{command.CommandId}:{tick}",
            kind,
            command.AgentId,
            JsonSerializer.SerializeToElement(
                new { commandId = command.CommandId, reason },
                ProtocolJson.Options));
}
