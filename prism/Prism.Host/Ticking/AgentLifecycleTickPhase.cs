using System.Text.Json;
using Prism.Contracts;
using Prism.Core.World;

namespace Prism.Host.Ticking;

public sealed class AgentLifecycleTickPhase(
    AgentLifecycleSystem? lifecycle,
    Prism.Core.Accounting.MatterLedger? ledger = null,
    int freshnessDecayPermillePerTick = 1_000) : IOptionalCheckpointableWorldTickPhaseHandler
{
    public WorldTickPhase Phase => WorldTickPhase.Lifecycle;

    public void Execute(WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (lifecycle is null)
        {
            return;
        }

        foreach (var status in lifecycle.AdvanceMoveOrders())
        {
            context.AddEvent(new EventEnvelope(
                ProtocolVersion.Current.ToString(),
                context.Tick,
                $"{status.CommandId}:move:{context.Tick}",
                status.Completed ? "action_completed" : "action_failed",
                status.AgentId,
                JsonSerializer.SerializeToElement(
                    new
                    {
                        commandId = status.CommandId,
                        reason = status.ErrorCode,
                        result = new
                        {
                            completed = status.Completed,
                            stepsRemaining = status.StepsRemaining,
                            target = status.Target,
                        },
                    },
                    ProtocolJson.Options)));
        }

        if (lifecycle is null || ledger is null)
        {
            return;
        }

        foreach (var entry in lifecycle.AdvanceFreshness(
                     context.Tick,
                     freshnessDecayPermillePerTick,
                     ledger))
        {
            context.AddEvent(new EventEnvelope(
                ProtocolVersion.Current.ToString(),
                context.Tick,
                $"spoilage:{context.Tick}:{entry.AgentId}:{entry.Spoilage.ItemTypeId}",
                "item_spoiled",
                entry.AgentId,
                JsonSerializer.SerializeToElement(
                    new
                    {
                        itemTypeId = entry.Spoilage.ItemTypeId,
                        quantity = entry.Spoilage.Quantity,
                    },
                    ProtocolJson.Options)));
        }
    }

    public JsonElement CaptureState() =>
        JsonSerializer.SerializeToElement(lifecycle?.CaptureState(), ProtocolJson.Options);

    public void RestoreEmptyState()
    {
        if (lifecycle is not null &&
            (lifecycle.AgentCount != 0 ||
             lifecycle.GroundItemCount != 0 ||
             lifecycle.MoveOrderCount != 0))
        {
            throw new JsonException("Agent lifecycle state can only be initialized as empty in a new system.");
        }
    }

    public void RestoreState(JsonElement state)
    {
        var checkpoint = state.Deserialize<byte[]?>(ProtocolJson.Options);
        if (lifecycle is null)
        {
            if (checkpoint is not null)
            {
                throw new JsonException("Agent lifecycle state cannot be restored without a generated world.");
            }

            return;
        }

        if (checkpoint is null)
        {
            throw new JsonException("The agent lifecycle checkpoint is empty.");
        }

        lifecycle.RestoreState(checkpoint);
    }
}

public sealed class AgentLifecycleCommandHandler : IWorldCommandHandler
{
    private readonly AgentLifecycleSystem _lifecycle;

    public AgentLifecycleCommandHandler(AgentLifecycleSystem lifecycle, string kind)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (kind is not ("Incarner" or "Retirer" or "Aller"))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Unsupported agent lifecycle command kind.");
        }

        _lifecycle = lifecycle;
        Kind = kind;
    }

    public string Kind { get; }

    public CommandExecutionResult Execute(CommandEnvelope command, WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        if (command.Payload.ValueKind != JsonValueKind.Object)
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        return Kind switch
        {
            "Incarner" => Incarnate(command),
            "Retirer" => Retire(command),
            "Aller" => StartMove(command),
            _ => CommandExecutionResult.Failure("unknown_command"),
        };
    }

    private CommandExecutionResult StartMove(CommandEnvelope command)
    {
        if (!TryReadTarget(command.Payload, out var target))
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        if (!_lifecycle.StartMoveOrder(
                command.AgentId,
                command.CommandId,
                target,
                out var errorCode,
                out var stepCount))
        {
            return CommandExecutionResult.Failure(errorCode ?? "path_not_found");
        }

        return CommandExecutionResult.Success(JsonSerializer.SerializeToElement(
            new { agentId = command.AgentId, target, stepCount, accepted = true },
            ProtocolJson.Options));
    }

    private static bool TryReadTarget(JsonElement payload, out BlockCoordinate target)
    {
        target = default;
        if ((!payload.TryGetProperty("target", out var value) &&
             !payload.TryGetProperty("position", out value)) ||
            value.ValueKind != JsonValueKind.Object ||
            !TryReadInt(value, "x", out var x) ||
            !TryReadInt(value, "y", out var y) ||
            !TryReadInt(value, "z", out var z))
        {
            return false;
        }

        target = new BlockCoordinate(x, y, z);
        return true;
    }

    private CommandExecutionResult Incarnate(CommandEnvelope command)
    {
        if (!TryReadPosition(command.Payload, out var position) ||
            !TryReadProfile(command.Payload, out var profile))
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        try
        {
            var agent = _lifecycle.Incarnate(command.AgentId, position, profile);
            return CommandExecutionResult.Success(JsonSerializer.SerializeToElement(
                new
                {
                    agentId = agent.AgentId,
                    position = agent.Position,
                    profile = agent.Profile,
                },
                ProtocolJson.Options));
        }
        catch (AgentLifecycleException exception)
        {
            return CommandExecutionResult.Failure(exception.Code);
        }
    }

    private CommandExecutionResult Retire(CommandEnvelope command)
    {
        if (!_lifecycle.Retire(command.AgentId))
        {
            return CommandExecutionResult.Failure("agent_not_found");
        }

        return CommandExecutionResult.Success(JsonSerializer.SerializeToElement(
            new { agentId = command.AgentId, retired = true },
            ProtocolJson.Options));
    }

    private static bool TryReadPosition(JsonElement payload, out AgentPosition position)
    {
        position = default;
        if (!payload.TryGetProperty("position", out var value) ||
            value.ValueKind != JsonValueKind.Object ||
            !TryReadInt(value, "x", out var x) ||
            !TryReadInt(value, "y", out var y) ||
            !TryReadInt(value, "z", out var z))
        {
            return false;
        }

        try
        {
            position = AgentPosition.AtCell(x, y, z);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool TryReadProfile(JsonElement payload, out AgentBodyProfile profile)
    {
        profile = new AgentBodyProfile();
        if (!payload.TryGetProperty("profile", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var speed = profile.SpeedPermille;
        var carrySlots = profile.CarrySlots;
        if ((value.TryGetProperty("speedPermille", out var speedValue) &&
             !speedValue.TryGetInt32(out speed)) ||
            (value.TryGetProperty("carrySlots", out var carryValue) &&
             !carryValue.TryGetInt32(out carrySlots)))
        {
            return false;
        }

        profile = new AgentBodyProfile(speed, carrySlots);
        try
        {
            profile.Validate();
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool TryReadInt(JsonElement value, string name, out int result)
    {
        result = 0;
        return value.TryGetProperty(name, out var property) && property.TryGetInt32(out result);
    }
}
