using System.Globalization;
using System.Text.Json;
using Prism.Contracts;
using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Host.Ticking;

/// <summary>
/// Wear/expiry slot of the tick: applies container spoilage (slowed inside a storage structure) in
/// addition to the construction-system state, which is checkpointed under the same phase name.
/// </summary>
public sealed class StructureConstructionTickPhase(
    StructureConstructionSystem? construction,
    WorldContainerStore? containers = null,
    MatterLedger? ledger = null,
    StructureEffectConsumer? structureEffects = null,
    ContainerDecayRules? containerDecayRules = null) : IOptionalCheckpointableWorldTickPhaseHandler
{
    private readonly ContainerDecayRules _decayRules = containerDecayRules ?? new ContainerDecayRules();

    public WorldTickPhase Phase => WorldTickPhase.WearExpiryAndConstruction;

    public void Execute(WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (containers is null || ledger is null || _decayRules.BasePermillePerTick == 0)
        {
            return;
        }

        foreach (var placed in containers.Enumerate())
        {
            var insideStorage =
                _decayRules.StorageSlowdownPermille > 0 &&
                structureEffects?.QueryVolume(
                    placed.Position,
                    StructureEffectConsumer.StorageEffectType) is not null;
            var rate = _decayRules.EffectivePermillePerTick(insideStorage);
            foreach (var spoilage in placed.Container.ApplyDecay(context.Tick, ledger, rate))
            {
                context.AddEvent(new EventEnvelope(
                    ProtocolVersion.Current.ToString(),
                    context.Tick,
                    $"spoilage:{context.Tick}:container:{placed.Position.X}:{placed.Position.Y}:{placed.Position.Z}:{spoilage.ItemTypeId}",
                    "item_spoiled",
                    null,
                    JsonSerializer.SerializeToElement(
                        new { itemTypeId = spoilage.ItemTypeId, quantity = spoilage.Quantity },
                        ProtocolJson.Options)));
            }
        }

        // Optional structure wear (disabled unless configured via content).
        construction?.ApplyWear(context.Tick);
    }

    public JsonElement CaptureState() =>
        JsonSerializer.SerializeToElement(construction?.CaptureState(), ProtocolJson.Options);

    public void RestoreEmptyState()
    {
        if (construction is not null && construction.Sites.Count != 0)
        {
            throw new JsonException("Construction state can only be initialized as empty in a new system.");
        }
    }

    public void RestoreState(JsonElement state)
    {
        var checkpoint = state.Deserialize<byte[]?>(ProtocolJson.Options);
        if (construction is null)
        {
            if (checkpoint is not null)
            {
                throw new JsonException("Construction state cannot be restored without a generated world.");
            }

            return;
        }

        if (checkpoint is null)
        {
            throw new JsonException("The construction checkpoint is empty.");
        }

        construction.RestoreState(checkpoint);
    }
}

public sealed class StructureConstructionCommandHandler : IWorldCommandHandler
{
    private readonly StructureConstructionSystem _construction;
    private readonly AgentLifecycleSystem? _lifecycle;
    private readonly MasterySystem? _mastery;

    public StructureConstructionCommandHandler(
        StructureConstructionSystem construction,
        string kind,
        AgentLifecycleSystem? lifecycle = null,
        MasterySystem? mastery = null)
    {
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (kind is not ("DemarrerChantier" or "Contribuer" or "DefinirAcces"))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Unsupported structure command kind.");
        }

        _construction = construction;
        _lifecycle = lifecycle;
        _mastery = mastery;
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
            "DemarrerChantier" => StartSite(command),
            "Contribuer" => Contribute(command),
            "DefinirAcces" => SetAccessList(command),
            _ => throw new InvalidOperationException("The registered structure command kind is invalid."),
        };
    }

    private CommandExecutionResult StartSite(CommandEnvelope command)
    {
        if (!TryReadString(command.Payload, "siteId", out var siteId) ||
            !TryReadString(command.Payload, "templateId", out var templateId) ||
            !TryReadCoordinate(command.Payload, out var origin))
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        if (!_construction.Templates.TryGetValue(templateId, out var template))
        {
            return CommandExecutionResult.Failure("unknown_template");
        }

        var owner = AccessPrincipal.ForAgent(command.AgentId);
        return ToCommandResult(_construction.CreateSite(siteId, template, origin, owner));
    }

    private CommandExecutionResult Contribute(CommandEnvelope command)
    {
        if (!TryReadString(command.Payload, "siteId", out var siteId))
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        var workerId = command.AgentId.ToString(CultureInfo.InvariantCulture);
        var principal = AccessPrincipal.ForAgent(command.AgentId);
        AgentInventory? inventory = null;
        if (_lifecycle is not null &&
            _lifecycle.TryGetAgent(command.AgentId, out var agent))
        {
            inventory = agent!.Inventory;
        }

        var result = _construction.ContributeWork(
            siteId,
            workerId,
            command.Tick,
            principal,
            inventory);
        if (result.Succeeded)
        {
            _mastery?.Award(command.AgentId, MasteryCategories.Building);
        }

        return ToCommandResult(result);
    }

    private CommandExecutionResult SetAccessList(CommandEnvelope command)
    {
        if (!TryReadString(command.Payload, "siteId", out var siteId) ||
            !TryReadAccessRules(command.Payload, out var rules))
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        var result = _construction.SetAccessList(
            siteId,
            AccessPrincipal.ForAgent(command.AgentId),
            rules);
        if (!result.Allowed)
        {
            return CommandExecutionResult.Failure(result.ErrorCode!);
        }

        return CommandExecutionResult.Success(
            JsonSerializer.SerializeToElement(new { siteId, accessUpdated = true }, ProtocolJson.Options));
    }

    private static CommandExecutionResult ToCommandResult(StructureConstructionResult result)
    {
        if (!result.Succeeded)
        {
            return CommandExecutionResult.Failure(result.ErrorCode!);
        }

        return CommandExecutionResult.Success(
            JsonSerializer.SerializeToElement(new { site = result.Site }, ProtocolJson.Options));
    }

    private static bool TryReadString(JsonElement payload, string propertyName, out string value)
    {
        value = string.Empty;
        return payload.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value = property.GetString()!);
    }

    private static bool TryReadCoordinate(JsonElement payload, out BlockCoordinate coordinate)
    {
        coordinate = default;
        if ((!payload.TryGetProperty("origin", out var position) &&
             !payload.TryGetProperty("position", out position)) ||
            position.ValueKind != JsonValueKind.Object ||
            !position.TryGetProperty("x", out var x) ||
            !position.TryGetProperty("y", out var y) ||
            !position.TryGetProperty("z", out var z) ||
            !x.TryGetInt32(out var xValue) ||
            !y.TryGetInt32(out var yValue) ||
            !z.TryGetInt32(out var zValue))
        {
            return false;
        }

        coordinate = new BlockCoordinate(xValue, yValue, zValue);
        return true;
    }

    private static bool TryReadAccessRules(
        JsonElement payload,
        out IReadOnlyList<ContainerAccessRule> rules)
    {
        rules = [];
        if (!payload.TryGetProperty("rules", out var rulesElement) ||
            rulesElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsedRules = new List<ContainerAccessRule>();
        var principals = new HashSet<AccessPrincipal>();
        foreach (var ruleElement in rulesElement.EnumerateArray())
        {
            if (ruleElement.ValueKind != JsonValueKind.Object ||
                !ruleElement.TryGetProperty("kind", out var kindElement) ||
                kindElement.ValueKind != JsonValueKind.String ||
                !ruleElement.TryGetProperty("id", out var idElement) ||
                !idElement.TryGetUInt64(out var id) ||
                !ruleElement.TryGetProperty("rights", out var rightsElement) ||
                rightsElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var principalKind = kindElement.GetString()?.ToLowerInvariant() switch
            {
                "agent" => AccessPrincipalKind.Agent,
                "group" => AccessPrincipalKind.Group,
                _ => (AccessPrincipalKind?)null,
            };
            if (principalKind is null)
            {
                return false;
            }

            var rights = ContainerAccessRights.None;
            foreach (var rightElement in rightsElement.EnumerateArray())
            {
                if (rightElement.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                var right = rightElement.GetString()?.ToLowerInvariant() switch
                {
                    "use" => ContainerAccessRights.Use,
                    "take" => ContainerAccessRights.Take,
                    "put" => ContainerAccessRights.Put,
                    "build" => ContainerAccessRights.Build,
                    _ => ContainerAccessRights.None,
                };
                if (right == ContainerAccessRights.None || (rights & right) != 0)
                {
                    return false;
                }

                rights |= right;
            }

            var principal = new AccessPrincipal(principalKind.Value, id);
            if (rights == ContainerAccessRights.None || !principals.Add(principal))
            {
                return false;
            }

            parsedRules.Add(new ContainerAccessRule(principal, rights));
        }

        rules = parsedRules;
        return true;
    }
}
