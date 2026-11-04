using System.Text.Json;
using Prism.Content;
using Prism.Contracts;
using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Host.Ticking;

public sealed class MacroActionTickPhase : IOptionalCheckpointableWorldTickPhaseHandler
{
    private readonly MacroActionSystem _macros;
    private readonly AgentLifecycleSystem? _lifecycle;
    private readonly CompiledContentPack? _content;
    private readonly WorldGrid? _world;
    private readonly CropActionRules? _cropRules;
    private readonly WorldContainerStore _containers;
    private readonly BlockActionRules? _blockRules;
    private readonly IReadOnlyDictionary<string, IWorldCommandHandler> _commandHandlers;
    private readonly Dictionary<ulong, string> _commandIds;
    private readonly DeferredActionResolutions? _deferred;
    private readonly CommandDispatchPhase? _dispatch;

    public MacroActionTickPhase(
        MacroActionSystem macros,
        AgentLifecycleSystem? lifecycle,
        CompiledContentPack? content,
        WorldGrid? world,
        CropActionRules? cropRules,
        BlockActionRules? blockRules,
        WorldContainerStore containers,
        Dictionary<ulong, string> commandIds,
        IEnumerable<IWorldCommandHandler> commandHandlers,
        ActionDurationTracker? durations = null,
        DeferredActionResolutions? deferred = null,
        CommandDispatchPhase? dispatch = null)
    {
        ArgumentNullException.ThrowIfNull(macros);
        ArgumentNullException.ThrowIfNull(containers);
        ArgumentNullException.ThrowIfNull(commandIds);
        ArgumentNullException.ThrowIfNull(commandHandlers);
        _macros = macros;
        _lifecycle = lifecycle;
        _content = content;
        _world = world;
        _cropRules = cropRules;
        _blockRules = blockRules;
        _containers = containers;
        _commandIds = commandIds;
        _durations = durations;
        _deferred = deferred;
        _dispatch = dispatch;
        _commandHandlers = commandHandlers.ToDictionary(handler => handler.Kind, StringComparer.Ordinal);
    }

    private readonly ActionDurationTracker? _durations;

    public WorldTickPhase Phase => WorldTickPhase.AdvanceMacroActions;

    public void Execute(WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_lifecycle is null || _content is null || _world is null || _cropRules is null || _blockRules is null)
        {
            return;
        }

        if (_deferred is not null)
        {
            _deferred.Prune(_macros.ActiveAgentIds);
        }

        foreach (var agentId in _macros.ActiveAgentIds)
        {
            if (_deferred is not null && _deferred.TryTake(agentId, out var resolution))
            {
                CompleteDeferredStep(context, agentId, resolution);
                continue;
            }

            if (_deferred is not null && _deferred.IsPending(agentId) ||
                _durations is not null && _durations.IsBusy(agentId, context.Tick))
            {
                continue;
            }

            if (!_lifecycle.TryGetAgent(agentId, out var agent))
            {
                var commandId = _commandIds.GetValueOrDefault(agentId, $"macro:{agentId}");
                _commandIds.Remove(agentId);
                AddMacroEvent(context, commandId, agentId, "action_failed", "agent_not_found", null);
                _macros.Cancel(agentId);
                continue;
            }

            var sources = MacroPerceptionCompiler.Compile(
                agentId,
                _lifecycle,
                _content,
                _world,
                _cropRules,
                _containers);
            var next = _macros.GetNextStep(agentId, agent!.Position, sources);
            if (!next.Succeeded)
            {
                var failedId = _commandIds.GetValueOrDefault(agentId, $"macro:{agentId}");
                _commandIds.Remove(agentId);
                AddMacroEvent(
                    context,
                    failedId,
                    agentId,
                    "action_failed",
                    next.ErrorCode,
                    new { quantityAcquired = next.QuantityAcquired });
                continue;
            }

            if (next.Completed)
            {
                var completedId = _commandIds.GetValueOrDefault(agentId, $"macro:{agentId}");
                _commandIds.Remove(agentId);
                AddMacroEvent(
                    context,
                    completedId,
                    agentId,
                    "action_completed",
                    null,
                    new { quantityAcquired = next.QuantityAcquired, completed = true });
                continue;
            }

            if (next.Step is not { } step)
            {
                if (_macros.TryGetWaitingCrop(agentId, out var cropPosition) &&
                    IsRipe(cropPosition))
                {
                    var harvest = _macros.RequestCultivateHarvest(agentId);
                    if (harvest.Succeeded && harvest.Step is { } harvestStep)
                    {
                        QueueStep(context, agentId, harvestStep, sources);
                    }
                }

                continue;
            }

            if (step.Kind == MacroStepKind.WaitForRipe)
            {
                var wait = _macros.CompleteCurrentStep(agentId, succeeded: true);
                if (!wait.Succeeded)
                {
                    FailMacro(context, agentId, wait.ErrorCode, wait.QuantityAcquired);
                }

                continue;
            }

            QueueStep(context, agentId, step, sources);
        }
    }

    public JsonElement CaptureState() =>
        JsonSerializer.SerializeToElement(
            new MacroPhaseCheckpoint(
                _macros.CaptureState(),
                _commandIds.OrderBy(static entry => entry.Key)
                    .Select(static entry => new MacroCommandId(entry.Key, entry.Value))
                    .ToArray()),
            ProtocolJson.Options);

    public void RestoreEmptyState()
    {
        if (_macros.ActiveAgentIds.Count != 0 || _commandIds.Count != 0)
        {
            throw new JsonException("Macro-action state can only be initialized as empty in a new runtime.");
        }
    }

    public void RestoreState(JsonElement state)
    {
        var checkpoint = state.Deserialize<MacroPhaseCheckpoint>(ProtocolJson.Options)
                         ?? throw new JsonException("The macro-action checkpoint is empty.");
        if (checkpoint.Macros is null || checkpoint.Commands is null)
        {
            throw new JsonException("The macro-action checkpoint is incomplete.");
        }

        _macros.RestoreState(checkpoint.Macros);
        foreach (var command in checkpoint.Commands)
        {
            if (command is null ||
                string.IsNullOrWhiteSpace(command.CommandId) ||
                !_commandIds.TryAdd(command.AgentId, command.CommandId))
            {
                throw new JsonException("The macro-action checkpoint contains invalid command state.");
            }
        }

        if (!_commandIds.Keys.Order().SequenceEqual(_macros.ActiveAgentIds.Order()))
        {
            throw new JsonException("Macro-action command IDs do not match active macro states.");
        }
    }

    private void QueueStep(
        WorldTickContext context,
        ulong agentId,
        MacroActionStep step,
        IReadOnlyList<PerceivedMacroSource> perceivedSources)
    {
        var commandId = _commandIds.GetValueOrDefault(agentId, $"macro:{agentId}");
        context.QueueWorldWrite(() =>
        {
            var execution = ExecuteStep(context, agentId, step, perceivedSources);
            if (execution.Deferred)
            {
                AddMacroEvent(
                    context,
                    commandId,
                    agentId,
                    "macro_step_deferred",
                    null,
                    new { step = step.Kind.ToString() });
                return;
            }

            var completed = _macros.CompleteCurrentStep(
                agentId,
                execution.Succeeded,
                execution.QuantityAcquired,
                execution.ErrorCode);
            if (!execution.Succeeded || !completed.Succeeded)
            {
                _commandIds.Remove(agentId);
                AddMacroEvent(
                    context,
                    commandId,
                    agentId,
                    "action_failed",
                    execution.ErrorCode ?? completed.ErrorCode,
                    new { quantityAcquired = completed.QuantityAcquired });
                return;
            }

            AddMacroEvent(
                context,
                commandId,
                agentId,
                "macro_step_completed",
                null,
                new { step = step.Kind.ToString(), quantityAcquired = execution.QuantityAcquired });
        });
    }

    private StepExecution ExecuteStep(
        WorldTickContext context,
        ulong agentId,
        MacroActionStep step,
        IReadOnlyList<PerceivedMacroSource> sources)
    {
        if (step.Kind == MacroStepKind.Move)
        {
            var moved = _lifecycle!.TryMoveOneStep(agentId, step.Target, out var moveError);
            return new StepExecution(moved, 0, moveError);
        }

        if (step.Kind == MacroStepKind.Take)
        {
            var source = sources.FirstOrDefault(candidate => candidate.SourceId == step.SourceId);
            if (source is null)
            {
                return new StepExecution(false, 0, "source_not_found");
            }

            if (source.Kind == MacroSourceKind.GroundItem)
            {
                var taken = _lifecycle!.TakeGroundItem(
                    agentId,
                    source.SourceId,
                    step.Quantity,
                    _blockRules!,
                    out var takeError);
                return new StepExecution(taken, taken ? step.Quantity : 0, takeError);
            }

            return InvokeActionHandler(
                "Prendre",
                context,
                agentId,
                JsonSerializer.SerializeToElement(
                    new { container = step.Target, item = step.ItemTypeId, quantity = step.Quantity },
                    ProtocolJson.Options),
                step);
        }

        var (kind, payload) = step.Kind switch
        {
            MacroStepKind.Mine => ("Miner", new { target = step.Target }),
            MacroStepKind.Harvest => ("Recolter", new { target = step.Target }),
            MacroStepKind.Craft => ("Fabriquer", step.UsesStation
                ? (object)new { recipeId = step.RecipeId, station = step.Target }
                : new { recipeId = step.RecipeId }),
            MacroStepKind.Till => ("Labourer", new { target = step.Target }),
            MacroStepKind.Plant => ("Planter", new { target = step.Target, seed = step.ItemTypeId }),
            _ => (string.Empty, (object)new { }),
        };
        if (string.IsNullOrEmpty(kind))
        {
            return new StepExecution(false, 0, "unsupported_macro_step");
        }

        return InvokeActionHandler(
            kind,
            context,
            agentId,
            JsonSerializer.SerializeToElement(payload, ProtocolJson.Options),
            step);
    }

    private StepExecution InvokeActionHandler(
        string kind,
        WorldTickContext context,
        ulong agentId,
        JsonElement payload,
        MacroActionStep step)
    {
        if (!_commandHandlers.TryGetValue(kind, out var handler))
        {
            return new StepExecution(false, 0, "unknown_command");
        }

        var synthetic = new CommandEnvelope(
            ProtocolVersion.Current.ToString(),
            context.Tick,
            $"macro:{agentId}:{context.Tick}:{kind}",
            agentId,
            kind,
            payload);
        var execution = handler.Execute(synthetic, context);
        if (execution.Deferred)
        {
            if (_dispatch is null || _deferred is null)
            {
                return new StepExecution(false, 0, "deferred_channel_unavailable");
            }

            _dispatch.Schedule(synthetic, execution);
            _deferred.Expect(agentId, synthetic.CommandId, step);
            return new StepExecution(true, 0, null, Deferred: true);
        }

        return execution.Succeeded
            ? Interpret(step.Kind, step.ItemTypeId, step.Quantity, execution.Payload)
            : new StepExecution(false, 0, execution.ErrorCode);
    }

    /// <summary>
    /// Reads the quantity the macro asked for out of an action payload, whether the action was
    /// applied immediately or resolved later by the dispatch.
    /// </summary>
    private static StepExecution Interpret(
        MacroStepKind kind,
        ushort itemTypeId,
        int requestedQuantity,
        JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return new StepExecution(true, 0, null);
        }

        if (kind is MacroStepKind.Mine or MacroStepKind.Harvest)
        {
            if (!payload.TryGetProperty("produced", out var produced) ||
                produced.ValueKind != JsonValueKind.Array)
            {
                return new StepExecution(true, 0, null);
            }

            var acquired = produced.EnumerateArray()
                .Where(stack =>
                    stack.ValueKind == JsonValueKind.Object &&
                    stack.TryGetProperty("itemTypeId", out var stackItem) &&
                    stackItem.ValueKind == JsonValueKind.Number &&
                    stackItem.GetUInt16() == itemTypeId &&
                    stack.TryGetProperty("quantity", out var stackQuantity) &&
                    stackQuantity.TryGetInt32(out _))
                .Sum(stack => stack.GetProperty("quantity").GetInt32());
            return new StepExecution(
                true,
                requestedQuantity > 0 ? Math.Min(acquired, requestedQuantity) : acquired,
                null);
        }

        if (kind is MacroStepKind.Take)
        {
            return payload.TryGetProperty("quantity", out var quantity) &&
                   quantity.TryGetInt32(out var taken)
                ? new StepExecution(true, taken, null)
                : new StepExecution(true, 0, null);
        }

        return new StepExecution(true, 0, null);
    }

    private void CompleteDeferredStep(
        WorldTickContext context,
        ulong agentId,
        ResolvedMacroStep resolution)
    {
        var commandId = _commandIds.GetValueOrDefault(agentId, resolution.Step.CommandId);
        var execution = resolution.Succeeded
            ? Interpret(
                resolution.Step.Kind,
                resolution.Step.ItemTypeId,
                resolution.Step.Quantity,
                resolution.Payload)
            : new StepExecution(false, 0, resolution.ErrorCode ?? "action_failed");
        var completed = _macros.CompleteCurrentStep(
            agentId,
            execution.Succeeded,
            execution.QuantityAcquired,
            execution.ErrorCode);
        if (!execution.Succeeded || !completed.Succeeded)
        {
            _commandIds.Remove(agentId);
            AddMacroEvent(
                context,
                commandId,
                agentId,
                "action_failed",
                execution.ErrorCode ?? completed.ErrorCode,
                new { quantityAcquired = completed.QuantityAcquired });
            return;
        }

        AddMacroEvent(
            context,
            commandId,
            agentId,
            "macro_step_completed",
            null,
            new { step = resolution.Step.Kind.ToString(), quantityAcquired = execution.QuantityAcquired });
    }

    private bool IsRipe(BlockCoordinate position)
    {
        if (_world is null || _cropRules is null ||
            !_world.TryGetBlockOverride(position, out var block) ||
            block.IsAir)
        {
            return false;
        }

        return _cropRules.CropRules.Any(rule =>
            rule.Growth.CropMaterialId == block.MaterialId &&
            block.State >= rule.Growth.Stages - 1);
    }

    private void FailMacro(WorldTickContext context, ulong agentId, string? errorCode, int quantityAcquired)
    {
        var commandId = _commandIds.GetValueOrDefault(agentId, $"macro:{agentId}");
        _commandIds.Remove(agentId);
        AddMacroEvent(
            context,
            commandId,
            agentId,
            "action_failed",
            errorCode,
            new { quantityAcquired });
    }

    private static void AddMacroEvent(
        WorldTickContext context,
        string commandId,
        ulong agentId,
        string kind,
        string? reason,
        object? result)
    {
        var payload = JsonSerializer.SerializeToElement(
            new { commandId, reason, result },
            ProtocolJson.Options);
        context.AddEvent(new EventEnvelope(
            ProtocolVersion.Current.ToString(),
            context.Tick,
            $"{commandId}:{kind}:{context.Tick}",
            kind,
            agentId,
            payload));
    }

    private sealed record StepExecution(
        bool Succeeded,
        int QuantityAcquired,
        string? ErrorCode,
        bool Deferred = false);
    private sealed record MacroPhaseCheckpoint(byte[] Macros, MacroCommandId[] Commands);
    private sealed record MacroCommandId(ulong AgentId, string CommandId);
}
