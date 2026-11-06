using System.Text.Json;
using Prism.Content;
using Prism.Contracts;
using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.Content;
using Prism.Core.Time;
using Prism.Core.World;

namespace Prism.Host.Ticking;

public sealed class WorldActionCommandHandler : IWorldCommandHandler
{
    private readonly AgentLifecycleSystem _lifecycle;
    private readonly CompiledContentPack _content;
    private readonly WorldGrid _world;
    private readonly BlockActionRules _blockRules;
    private readonly CraftingRules _craftingRules;
    private readonly CropActionRules _cropRules;
    private readonly ConsumptionRules _consumptionRules;
    private readonly CraftingStationRegistry _stations;
    private readonly WorldContainerStore _containers;
    private readonly MatterLedger _ledger;
    private readonly EventWheel _eventWheel;
    private readonly WorldCalendar _calendar;
    private readonly MasterySystem _mastery;
    private readonly ulong _runSeed;
    private readonly ActionDurationTracker? _durations;
    private readonly StructureEffectConsumer? _structureEffects;
    private readonly AblationMechanisms _ablation;
    private readonly ushort? _waterMaterialId;
    private readonly int _containerDecayPermillePerTick;

    public WorldActionCommandHandler(
        AgentLifecycleSystem lifecycle,
        CompiledContentPack content,
        WorldGrid world,
        BlockActionRules blockRules,
        CraftingRules craftingRules,
        CropActionRules cropRules,
        ConsumptionRules consumptionRules,
        CraftingStationRegistry stations,
        WorldContainerStore containers,
        MatterLedger ledger,
        EventWheel eventWheel,
        WorldCalendar calendar,
        MasterySystem mastery,
        ulong runSeed,
        string kind,
        ActionDurationTracker? durations = null,
        StructureEffectConsumer? structureEffects = null,
        ContainerDecayRules? containerDecayRules = null,
        AblationMechanisms? ablation = null)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(blockRules);
        ArgumentNullException.ThrowIfNull(craftingRules);
        ArgumentNullException.ThrowIfNull(cropRules);
        ArgumentNullException.ThrowIfNull(consumptionRules);
        ArgumentNullException.ThrowIfNull(stations);
        ArgumentNullException.ThrowIfNull(containers);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(eventWheel);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(mastery);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (!SupportedKinds.Contains(kind, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Unsupported world action command kind.");
        }

        _lifecycle = lifecycle;
        _content = content;
        _world = world;
        _blockRules = blockRules;
        _craftingRules = craftingRules;
        _cropRules = cropRules;
        _consumptionRules = consumptionRules;
        _stations = stations;
        _containers = containers;
        _ledger = ledger;
        _eventWheel = eventWheel;
        _calendar = calendar;
        _mastery = mastery;
        _runSeed = runSeed;
        _durations = durations;
        _structureEffects = structureEffects;
        _containerDecayPermillePerTick = (containerDecayRules ?? new ContainerDecayRules()).BasePermillePerTick;
        _ablation = ablation ?? AblationMechanisms.AllEnabled;
        _waterMaterialId = content.Registries.Materials.TryGetId("water", out var waterMaterialId)
            ? waterMaterialId
            : null;
        Kind = kind;
    }

    public string Kind { get; }

    public bool ValidatePayload(JsonElement payload, out string? detail) =>
        CommandPayloadValidator.TryValidate(Kind, payload, out detail);

    public static IReadOnlyList<string> SupportedKinds { get; } =
    [
        "Miner",
        "Poser",
        "Fabriquer",
        "Labourer",
        "Planter",
        "Recolter",
        "Consommer",
        "Transferer",
        "Deposer",
        "Prendre",
        "DefinirAccesConteneur",
    ];

    private static readonly string[] DurationGatedKinds =
    [
        "Miner",
        "Fabriquer",
        "Labourer",
        "Planter",
        "Recolter",
    ];

    public CommandExecutionResult Execute(CommandEnvelope command, WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        if (command.Payload.ValueKind != JsonValueKind.Object)
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        if (!_lifecycle.TryGetAgent(command.AgentId, out var agent))
        {
            return CommandExecutionResult.Failure("agent_not_found");
        }

        var gated = _durations is not null &&
                    DurationGatedKinds.Contains(command.Kind, StringComparer.Ordinal);
        var result = Dispatch(command, context, agent!);
        if (gated && result.Succeeded && !result.Deferred)
        {
            _durations!.TryBegin(
                command.AgentId,
                command.Kind,
                command.CommandId,
                command.Tick,
                ReadDurationTicks(result.Payload));
        }

        return result;
    }

    private CommandExecutionResult Dispatch(
        CommandEnvelope command,
        WorldTickContext context,
        AgentBody agent) => command.Kind switch
        {
            "Miner" => Mine(command, allowDefer: true),
            "Poser" => Place(command),
            "Fabriquer" => Craft(command),
            "Labourer" => Till(command, allowDefer: true),
            "Planter" => Plant(command, allowDefer: true),
            "Recolter" => Harvest(command, allowDefer: true),
            "Consommer" => Consume(command, context, agent),
            "Transferer" => Transfer(command),
            "Deposer" => TransferToContainer(command, agent, deposit: true),
            "Prendre" => TransferToContainer(command, agent, deposit: false),
            "DefinirAccesConteneur" => SetContainerAccess(command, agent),
            _ => CommandExecutionResult.Failure("unknown_command"),
        };

    private CommandExecutionResult? BusyFailure(CommandEnvelope command) =>
        _durations is not null && _durations.IsBusy(command.AgentId, command.Tick)
            ? CommandExecutionResult.Failure("agent_busy")
            : null;

    private static int ReadDurationTicks(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty("durationTicks", out var duration) &&
        duration.ValueKind == JsonValueKind.Number &&
        duration.TryGetInt32(out var ticks) && ticks >= 0
            ? ticks
            : 0;

    private CommandExecutionResult Mine(CommandEnvelope command, bool allowDefer)
    {
        if (!TryReadCoordinate(command.Payload, out var target))
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        if (!_lifecycle.HasPerceivedBlock(command.AgentId, target))
        {
            return CommandExecutionResult.Failure("not_perceived");
        }

        if (_containers.TryGet(target, out var container) && container!.UsedSlots > 0)
        {
            return CommandExecutionResult.Failure("container_not_empty");
        }

        if (BusyFailure(command) is { } busy)
        {
            return busy;
        }

        if (allowDefer && _durations is not null)
        {
            var duration = BlockActionSystem.GetMiningDurationTicks(
                _world,
                _blockRules,
                target,
                _ablation.Mastery ? _mastery.GetPermille(command.AgentId, MasteryCategories.Mining) : 0);
            if (duration > 0)
            {
                return Schedule(command, "Miner", duration);
            }
        }

        var result = _lifecycle.MineBlock(
            command.AgentId,
            _blockRules,
            _ledger,
            _runSeed,
            command.Tick,
            target,
            _mastery.GetPermille(command.AgentId, MasteryCategories.Mining));
        if (result.Succeeded)
        {
            _containers.Remove(target);
            if (_ablation.Mastery)
            {
                _mastery.Award(command.AgentId, MasteryCategories.Mining);
            }
        }

        return ToResult(result.Succeeded, result.ErrorCode, new
        {
            result.DurationTicks,
            result.Produced,
            result.BrokenToolItemTypeId,
        });
    }

    private CommandExecutionResult Place(CommandEnvelope command)
    {
        if (!TryReadCoordinate(command.Payload, out var target) ||
            !TryReadId(command.Payload, ["item", "itemId", "itemTypeId"], _content.Registries.Items, out var itemId) ||
            !TryReadId(command.Payload, ["material", "materialId"], _content.Registries.Materials, out var materialId))
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        var result = _lifecycle.PlaceBlock(
            command.AgentId,
            _blockRules,
            _ledger,
            target,
            itemId,
            materialId);
        if (result.Succeeded && IsContainerItem(itemId))
        {
            var container = new ContainerInventory(
                capacitySlots: 24,
                decayPermillePerTick: _containerDecayPermillePerTick,
                target,
                AccessPrincipal.ForAgent(command.AgentId));
            if (!_containers.TryAdd(container))
            {
                throw new InvalidOperationException("A placed container already exists at the target position.");
            }
        }

        return ToResult(result.Succeeded, result.ErrorCode, new
        {
            result.DurationTicks,
            result.Produced,
            result.BrokenToolItemTypeId,
        });
    }

    public CommandExecutionResult ResolveDeferred(CommandEnvelope command, WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        return command.Kind switch
        {
            "Fabriquer" => CraftCore(command, allowDefer: false),
            "Miner" => Mine(command, allowDefer: false),
            "Labourer" => Till(command, allowDefer: false),
            "Planter" => Plant(command, allowDefer: false),
            "Recolter" => Harvest(command, allowDefer: false),
            _ => Execute(command, context),
        };
    }

    private CommandExecutionResult Schedule(CommandEnvelope command, string kind, int durationTicks)
    {
        if (!_durations!.TryBegin(command.AgentId, kind, command.CommandId, command.Tick, durationTicks))
        {
            return CommandExecutionResult.Failure("agent_busy");
        }

        return CommandExecutionResult.Scheduled(JsonSerializer.SerializeToElement(
            new
            {
                accepted = true,
                action = kind,
                durationTicks,
                busyUntilTick = checked(command.Tick + durationTicks),
            },
            ProtocolJson.Options));
    }

    private CommandExecutionResult Craft(CommandEnvelope command) => CraftCore(command, allowDefer: true);

    private CommandExecutionResult CraftCore(CommandEnvelope command, bool allowDefer)
    {
        if (!TryReadId(command.Payload, ["recipe", "recipeId"], _content.Registries.Recipes, out var recipeId) ||
            !TryReadOptionalCoordinate(command.Payload, ["station", "stationPosition"], out var stationPosition))
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        if (!TryGetRecipeMasteryCategory(recipeId, out var masteryCategory))
        {
            return CommandExecutionResult.Failure("unknown_recipe");
        }

        if (BusyFailure(command) is { } busy)
        {
            return busy;
        }

        var baseDurationTicks = GetRecipeDurationTicks(recipeId);
        // A production workshop (structure effect of type 'production') speeds up crafting.
        if (_ablation.Production && _structureEffects is not null && baseDurationTicks > 0)
        {
            var agent = GetAgent(command.AgentId);
            var usage = _structureEffects.QueryProduction(
                command.AgentId,
                new BlockCoordinate(agent.Position.CellX, agent.Position.CellY, agent.Position.FeetZ));
            if (usage is not null)
            {
                baseDurationTicks = Math.Max(1, baseDurationTicks * 4 / 5);
            }
        }
        var stationPerceived = stationPosition is null ||
                               _lifecycle.HasPerceivedBlock(command.AgentId, stationPosition.Value);
        if (allowDefer && !stationPerceived)
        {
            return CommandExecutionResult.Failure("not_perceived");
        }

        if (allowDefer && _durations is not null && baseDurationTicks > 0)
        {
            if (!_durations.TryBegin(
                    command.AgentId,
                    "Fabriquer",
                    command.CommandId,
                    command.Tick,
                    baseDurationTicks))
            {
                return CommandExecutionResult.Failure("agent_busy");
            }

            return CommandExecutionResult.Scheduled(JsonSerializer.SerializeToElement(
                new
                {
                    accepted = true,
                    durationTicks = baseDurationTicks,
                    busyUntilTick = checked(command.Tick + baseDurationTicks),
                    recipe = recipeId,
                },
                ProtocolJson.Options));
        }

        var result = CraftingSystem.Craft(
            _world,
            GetAgent(command.AgentId),
            _stations,
            _craftingRules,
            _ledger,
            recipeId,
            stationPosition,
            command.Tick,
            _ablation.Mastery ? _mastery.GetPermille(command.AgentId, masteryCategory) : 0,
            stationPerceived);
        if (result.Succeeded)
        {
            _lifecycle.NotifyAgentMutation();
            if (_ablation.Mastery)
            {
                _mastery.Award(command.AgentId, masteryCategory);
            }
        }

        return ToResult(result.Succeeded, result.ErrorCode, new
        {
            result.DurationTicks,
            result.Produced,
            result.BrokenToolItemTypeId,
        });
    }

    private CommandExecutionResult Till(CommandEnvelope command, bool allowDefer)
    {
        if (!TryReadCoordinate(command.Payload, out var target))
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        if (BusyFailure(command) is { } busy)
        {
            return busy;
        }

        if (allowDefer && _durations is not null)
        {
            return _lifecycle.HasPerceivedBlock(command.AgentId, target)
                ? Schedule(command, "Labourer", 1)
                : CommandExecutionResult.Failure("not_perceived");
        }

        var result = CropActionSystem.Till(
            _world,
            GetAgent(command.AgentId),
            _cropRules,
            _ledger,
            target,
            _lifecycle.HasPerceivedBlock(command.AgentId, target));
        if (result.Succeeded)
        {
            _lifecycle.NotifyAgentMutation();
            if (_ablation.Mastery)
            {
                _mastery.Award(command.AgentId, MasteryCategories.Farming);
            }
        }

        return ToResult(result.Succeeded, result.ErrorCode, new
        {
            result.DurationTicks,
            result.Produced,
            result.BrokenToolItemTypeId,
        });
    }

    private CommandExecutionResult Plant(CommandEnvelope command, bool allowDefer)
    {
        if (!TryReadCoordinate(command.Payload, out var target) ||
            !TryReadId(command.Payload, ["seed", "seedItemId", "item", "itemId"], _content.Registries.Items, out var seedItemId))
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        if (BusyFailure(command) is { } busy)
        {
            return busy;
        }

        if (allowDefer && _durations is not null)
        {
            return _lifecycle.HasPerceivedBlock(command.AgentId, target)
                ? Schedule(command, "Planter", 1)
                : CommandExecutionResult.Failure("not_perceived");
        }

        var result = CropActionSystem.Plant(
            _world,
            GetAgent(command.AgentId),
            _eventWheel,
            _cropRules,
            _ledger,
            _runSeed,
            command.Tick,
            target,
            seedItemId,
            _calendar.GetSnapshot(command.Tick).Season,
            _lifecycle.HasPerceivedBlock(command.AgentId, target));
        if (result.Succeeded)
        {
            _lifecycle.NotifyAgentMutation();
            if (_ablation.Mastery)
            {
                _mastery.Award(command.AgentId, MasteryCategories.Farming);
            }
        }

        return ToResult(result.Succeeded, result.ErrorCode, new
        {
            result.DurationTicks,
            result.Produced,
            result.BrokenToolItemTypeId,
        });
    }

    private CommandExecutionResult Harvest(CommandEnvelope command, bool allowDefer)
    {
        if (!TryReadCoordinate(command.Payload, out var target))
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        if (BusyFailure(command) is { } busy)
        {
            return busy;
        }

        if (allowDefer && _durations is not null)
        {
            return _lifecycle.HasPerceivedBlock(command.AgentId, target)
                ? Schedule(command, "Recolter", 1)
                : CommandExecutionResult.Failure("not_perceived");
        }

        var result = CropActionSystem.Harvest(
            _world,
            GetAgent(command.AgentId),
            _cropRules,
            _ledger,
            _runSeed,
            command.Tick,
            target,
            _lifecycle.HasPerceivedBlock(command.AgentId, target));
        if (result.Succeeded)
        {
            _lifecycle.NotifyAgentMutation();
            if (_ablation.Mastery)
            {
                _mastery.Award(command.AgentId, MasteryCategories.Farming);
            }
        }

        return ToResult(result.Succeeded, result.ErrorCode, new
        {
            result.DurationTicks,
            result.Produced,
            result.BrokenToolItemTypeId,
        });
    }

    private CommandExecutionResult Consume(
        CommandEnvelope command,
        WorldTickContext context,
        AgentBody agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (TryReadId(command.Payload, ["item", "itemId", "itemTypeId"], _content.Registries.Items, out var itemId))
        {
            var result = ConsumptionSystem.ConsumeItem(agent, _consumptionRules, _ledger, itemId);
            if (!result.Succeeded)
            {
                return CommandExecutionResult.Failure(result.ErrorCode!);
            }

            _lifecycle.NotifyAgentMutation();
            AddConsumptionEvent(context, command, result.Event!);
            return CommandExecutionResult.Success(JsonSerializer.SerializeToElement(
                new { result.DurationTicks, result.Event },
                ProtocolJson.Options));
        }

        if (!TryReadCoordinate(command.Payload, out var source))
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        var drinkResult = ConsumptionSystem.DrinkFromSource(
            _world,
            agent,
            _consumptionRules,
            source,
            _lifecycle.HasPerceivedBlock(command.AgentId, source));
        if (!drinkResult.Succeeded &&
            (drinkResult.ErrorCode == "not_drinkable" || drinkResult.ErrorCode == "not_perceived"))
        {
            drinkResult = TryDrinkFromWaterAccess(agent) ?? drinkResult;
        }

        if (!drinkResult.Succeeded)
        {
            return CommandExecutionResult.Failure(drinkResult.ErrorCode!);
        }

        AddConsumptionEvent(context, command, drinkResult.Event!);
        return CommandExecutionResult.Success(JsonSerializer.SerializeToElement(
            new { drinkResult.DurationTicks, drinkResult.Event },
            ProtocolJson.Options));
    }

    /// <summary>
    /// A well (or any structure effect of type <c>water_access</c>) makes its volume drinkable, so an
    /// agent standing inside it can drink without an adjacent water block.
    /// </summary>
    private ConsumptionActionResult? TryDrinkFromWaterAccess(AgentBody agent)
    {
        if (!_ablation.WaterAccess || _structureEffects is null || _waterMaterialId is null)
        {
            return null;
        }

        var access = _structureEffects.QueryWaterAccess(
            agent.AgentId,
            new BlockCoordinate(agent.Position.CellX, agent.Position.CellY, agent.Position.FeetZ));
        return access is null
            ? null
            : ConsumptionSystem.DrinkFromStructure(
                agent,
                _consumptionRules,
                _waterMaterialId.Value,
                access.SiteId);
    }

    private CommandExecutionResult Transfer(CommandEnvelope command)
    {
        if (!TryReadUInt64(command.Payload, ["recipientAgentId", "recipientId"], out var recipientId) ||
            !TryReadId(command.Payload, ["item", "itemId", "itemTypeId"], _content.Registries.Items, out var itemId) ||
            !TryReadInt(command.Payload, "quantity", out var quantity))
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        if (!_lifecycle.TryGetAgent(recipientId, out var recipient))
        {
            return CommandExecutionResult.Failure("agent_not_found");
        }

        var result = TransferActionSystem.Transfer(
            GetAgent(command.AgentId),
            recipient!,
            _blockRules,
            itemId,
            quantity,
            recipientPerceived: _lifecycle.HasPerceivedAgent(command.AgentId, recipientId));
        if (result.Succeeded)
        {
            _lifecycle.NotifyAgentMutation();
        }

        return ToResult(result.Succeeded, result.ErrorCode, new { result.Quantity });
    }

    private CommandExecutionResult TransferToContainer(
        CommandEnvelope command,
        AgentBody agent,
        bool deposit)
    {
        if (!TryReadOptionalCoordinate(command.Payload, ["container", "position", "target"], out var position) ||
            position is null ||
            !TryReadId(command.Payload, ["item", "itemId", "itemTypeId"], _content.Registries.Items, out var itemId) ||
            !TryReadInt(command.Payload, "quantity", out var quantity) ||
            quantity <= 0)
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        if (!_containers.TryGet(position.Value, out var container))
        {
            return CommandExecutionResult.Failure("container_not_found");
        }

        var result = deposit
            ? ContainerActionSystem.Deposit(
                agent,
                container!,
                _blockRules,
                itemId,
                quantity,
                targetPerceived: _lifecycle.HasPerceivedBlock(command.AgentId, position.Value))
            : ContainerActionSystem.Take(
                agent,
                container!,
                _blockRules,
                itemId,
                quantity,
                targetPerceived: _lifecycle.HasPerceivedBlock(command.AgentId, position.Value),
                tick: command.Tick,
                ledger: _ledger);
        if (result.Succeeded)
        {
            _lifecycle.NotifyAgentMutation();
        }

        return ToResult(result.Succeeded, result.ErrorCode, new { result.Quantity });
    }

    private CommandExecutionResult SetContainerAccess(CommandEnvelope command, AgentBody agent)
    {
        if (!TryReadOptionalCoordinate(command.Payload, ["container", "position", "target"], out var position) ||
            position is null ||
            !command.Payload.TryGetProperty("rules", out var rulesElement) ||
            rulesElement.ValueKind != JsonValueKind.Array)
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        if (!_containers.TryGet(position.Value, out var container))
        {
            return CommandExecutionResult.Failure("container_not_found");
        }

        if (!_lifecycle.HasPerceivedBlock(command.AgentId, position.Value))
        {
            return CommandExecutionResult.Failure("not_perceived");
        }

        if (!BlockActionSystem.IsAdjacent(agent.Position, position.Value))
        {
            return CommandExecutionResult.Failure("not_adjacent");
        }

        var rules = new List<ContainerAccessRule>();
        var principals = new HashSet<AccessPrincipal>();
        foreach (var element in rulesElement.EnumerateArray())
        {
            if (!ContainerAccessRuleParser.TryParse(element, out var rule) ||
                !principals.Add(rule.Principal))
            {
                return CommandExecutionResult.Failure("invalid_payload");
            }

            rules.Add(rule);
        }

        var result = container!.SetAccessList(
            AccessPrincipal.ForAgent(command.AgentId),
            rules);
        if (!result.Succeeded)
        {
            return CommandExecutionResult.Failure(result.ErrorCode!);
        }

        return CommandExecutionResult.Success(JsonSerializer.SerializeToElement(
            new { accessUpdated = true, ruleCount = rules.Count },
            ProtocolJson.Options));
    }

    private int GetRecipeDurationTicks(ushort recipeId)
    {
        foreach (var recipe in _content.Recipes)
        {
            if (_content.Registries.Recipes.GetId(recipe.Id) == recipeId)
            {
                return recipe.DurationTicks;
            }
        }

        return 0;
    }

    private bool TryGetRecipeMasteryCategory(ushort recipeId, out string category)
    {
        foreach (var recipe in _content.Recipes)
        {
            if (_content.Registries.Recipes.GetId(recipe.Id) != recipeId)
            {
                continue;
            }

            category = recipe.MasteryCategory ?? string.Empty;
            return !string.IsNullOrWhiteSpace(category);
        }

        category = string.Empty;
        return false;
    }

    private bool IsContainerItem(ushort itemId)
    {
        var item = _content.Items.Single(definition =>
            _content.Registries.Items.GetId(definition.Id) == itemId);
        var tags = item.Tags.ToHashSet(StringComparer.Ordinal);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var tag in _content.Tags)
            {
                if (tag.Parent is not null && tags.Contains(tag.Id))
                {
                    changed |= tags.Add(tag.Parent);
                }
            }
        }

        return tags.Contains("container");
    }

    private AgentBody GetAgent(ulong agentId) =>
        _lifecycle.TryGetAgent(agentId, out var agent)
            ? agent!
            : throw new InvalidOperationException("The command agent disappeared during the active tick.");

    private static void AddConsumptionEvent(
        WorldTickContext context,
        CommandEnvelope command,
        ItemConsumedEvent consumedEvent)
    {
        var payload = JsonSerializer.SerializeToElement(consumedEvent, ProtocolJson.Options);
        context.AddEvent(new EventEnvelope(
            ProtocolVersion.Current.ToString(),
            context.Tick,
            $"{command.CommandId}:item_consumed",
            "item_consumed",
            command.AgentId,
            payload));
    }

    private static CommandExecutionResult ToResult<T>(bool succeeded, string? errorCode, T result) =>
        succeeded
            ? CommandExecutionResult.Success(JsonSerializer.SerializeToElement(result, ProtocolJson.Options))
            : CommandExecutionResult.Failure(errorCode ?? "action_failed");

    private static bool TryReadCoordinate(JsonElement payload, out BlockCoordinate coordinate)
    {
        coordinate = default;
        if (!TryReadOptionalCoordinate(payload, ["target", "position", "parcel", "source"], out var value) ||
            value is null)
        {
            return false;
        }

        coordinate = value.Value;
        return true;
    }

    private static bool TryReadOptionalCoordinate(
        JsonElement payload,
        IReadOnlyList<string> propertyNames,
        out BlockCoordinate? coordinate)
    {
        coordinate = null;
        JsonElement value = default;
        var found = false;
        foreach (var propertyName in propertyNames)
        {
            if (!payload.TryGetProperty(propertyName, out value))
            {
                continue;
            }

            found = true;
            break;
        }

        if (!found)
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Object ||
            !TryReadInt(value, "x", out var x) ||
            !TryReadInt(value, "y", out var y) ||
            !TryReadInt(value, "z", out var z))
        {
            return false;
        }

        coordinate = new BlockCoordinate(x, y, z);
        return true;
    }

    private static bool TryReadId(
        JsonElement payload,
        IReadOnlyList<string> propertyNames,
        StableIdRegistry registry,
        out ushort id)
    {
        id = 0;
        foreach (var propertyName in propertyNames)
        {
            if (!payload.TryGetProperty(propertyName, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.TryGetUInt16(out id);
            }

            return value.ValueKind == JsonValueKind.String &&
                   registry.TryGetId(value.GetString()!, out id);
        }

        return false;
    }

    private static bool TryReadInt(JsonElement payload, string propertyName, out int value)
    {
        value = 0;
        return payload.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out value);
    }

    private static bool TryReadUInt64(
        JsonElement payload,
        IReadOnlyList<string> propertyNames,
        out ulong value)
    {
        value = 0;
        foreach (var propertyName in propertyNames)
        {
            if (payload.TryGetProperty(propertyName, out var property))
            {
                return property.ValueKind == JsonValueKind.Number && property.TryGetUInt64(out value);
            }
        }

        return false;
    }
}
