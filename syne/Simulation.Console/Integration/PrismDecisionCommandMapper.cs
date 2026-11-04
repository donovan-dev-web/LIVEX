using System.Globalization;
using System.Text.Json;
using Prism.Contracts;
using Simulation.Core.Cognition;
using Simulation.Core.World;

namespace Simulation.Console.Integration;

public readonly record struct PrismBlockPosition(int X, int Y, int Z);

public static class PrismDecisionCommandMapper
{
    public static IReadOnlyList<CommandEnvelope> Map(
        long tick,
        IEnumerable<DecisionRecord> decisions,
        Func<Position, PrismBlockPosition> mapPosition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(mapPosition);

        var commands = new List<CommandEnvelope>();
        var agents = new HashSet<ulong>();
        foreach (var decision in decisions.OrderBy(static decision => decision.EntityId))
        {
            if (decision.Tick != (ulong)tick)
            {
                throw new ArgumentException("Every BDI decision must match the requested PRISM tick.", nameof(decisions));
            }

            if (!agents.Add(decision.EntityId))
            {
                throw new ArgumentException("At most one BDI decision per agent may be mapped per tick.", nameof(decisions));
            }

            var command = MapOne(tick, decision, mapPosition);
            if (command is not null)
            {
                commands.Add(command);
            }
        }

        return commands;
    }

    private static CommandEnvelope? MapOne(
        long tick,
        DecisionRecord decision,
        Func<Position, PrismBlockPosition> mapPosition)
    {
        if (decision.Missing || decision.State == DecisionState.None)
        {
            return null;
        }

        if (decision.Chosen is DesireKind.Idle or DesireKind.Rest)
        {
            return null;
        }

        var kind = decision.Chosen switch
        {
            DesireKind.SeekFood or DesireKind.SeekWater or DesireKind.Explore or
                DesireKind.Socialize or DesireKind.Flee => "Aller",
            DesireKind.Eat or DesireKind.Drink => "Consommer",
            _ => throw new NotSupportedException(
                $"SYNE action '{decision.Chosen}' has no PRISM command mapping yet."),
        };
        if (decision.Target is not { } target)
        {
            throw new InvalidOperationException(
                $"SYNE action '{decision.Chosen}' requires a known target before it can be sent to PRISM.");
        }

        var position = mapPosition(target.Position);
        var targetPayload = new
        {
            type = target.TargetType,
            id = target.TargetId,
            position = new { x = position.X, y = position.Y, z = position.Z },
        };
        var payload = JsonSerializer.SerializeToElement(
            new { target = targetPayload },
            ProtocolJson.Options);
        var commandId = string.Concat(
            "syne:",
            tick.ToString(CultureInfo.InvariantCulture),
            ":",
            decision.EntityId.ToString(CultureInfo.InvariantCulture),
            ":",
            kind);
        return new CommandEnvelope(
            ProtocolVersion.Current.ToString(),
            tick,
            commandId,
            decision.EntityId,
            kind,
            payload);
    }
}
