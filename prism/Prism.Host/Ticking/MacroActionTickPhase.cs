using System.Text.Json;
using Prism.Content;
using Prism.Contracts;
using Prism.Core.Actions;
using Prism.Core.Navigation;
using Prism.Core.World;

namespace Prism.Host.Ticking;

public sealed class MacroActionTickPhase : IOptionalCheckpointableWorldTickPhaseHandler
{
    /// <summary>
    /// Per-step tracing is opt-in: it used to be printed unconditionally, which made a full R1
    /// campaign spend most of its wall clock writing tens of thousands of stderr lines (and made
    /// every host test noisy). Set <c>PRISM_DEBUG_MACRO=1</c> to get the old trace back.
    /// </summary>
    private static readonly bool TraceEnabled =
        Environment.GetEnvironmentVariable("PRISM_DEBUG_MACRO") is { Length: > 0 };

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

            if (next.Step is { } stepDebug && TraceEnabled)
            {
                Console.Error.WriteLine($"[macro] agent {agentId} tick {context.Tick}: next step {stepDebug.Kind} at {stepDebug.Target}");
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

            if (step.Kind == MacroStepKind.Plant && TraceEnabled)
            {
                Console.Error.WriteLine($"[macro] agent {agentId} tick {context.Tick}: queuing Plant at {step.Target}");
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
        string label = $"agent {agentId} {step.Kind}";
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

            if (step.Kind == MacroStepKind.Move &&
                execution.Succeeded &&
                _lifecycle!.TryGetAgent(agentId, out var walker) &&
                !BlockActionSystem.IsAdjacent(walker!.Position, step.Target))
            {
                // The move step walks at most one cell per tick, but it is only complete once
                // the target is inside the action's adjacency cube. Completing it after a
                // single step made the next step (Till, Plant, Craft) fire from several blocks
                // away and killed the whole sequence with not_adjacent; the planner then
                // restarted it from scratch and the bot never reached the parcel.
                if (TraceEnabled)
                {
                    Console.Error.WriteLine(
                        $"[macro-walk] agent {agentId} tick {context.Tick}: heading to {step.Target} from {walker.Position.CellX},{walker.Position.CellY},{walker.Position.FeetZ}");
                }

                return;
            }

            var completed = _macros.CompleteCurrentStep(
                agentId,
                execution.Succeeded,
                execution.QuantityAcquired,
                execution.ErrorCode);
            if (!completed.Succeeded)
            {
                if (TraceEnabled)
                {
                    Console.Error.WriteLine($"[macro-fail] agent {agentId} tick {context.Tick}: step={step.Kind} err={execution.ErrorCode ?? completed.ErrorCode}");
                }

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

            if (!execution.Succeeded)
            {
                // The macro is still active: it declared this source unreachable
                // (or unusable) and will pick the next perceived one on the next
                // tick. Emitting action_failed here would kill a macro that has
                // other sources left, which is exactly what an agent cannot see.
                return;
            }

            AddMacroEvent(
                context,
                commandId,
                agentId,
                "macro_step_completed",
                null,
                new { step = step.Kind.ToString(), quantityAcquired = execution.QuantityAcquired });
        }, label);
    }

    private StepExecution ExecuteStep(
        WorldTickContext context,
        ulong agentId,
        MacroActionStep step,
        IReadOnlyList<PerceivedMacroSource> sources)
    {
        if (step.Kind == MacroStepKind.Move)
        {
            _lifecycle!.TryGetAgent(agentId, out var mover);
            var before = mover?.Position;

            // PRISM-143 : rejet precoce via champ de distance — si l'agent ne peut atteindre
            // aucune source de la classe visee, inutile de payer un A* qui echouera de toute
            // facon. Les trajectoires restent identiques (A* n'est evite que sur les echecs).
            var moved = false;
            var moveError = (string?)null;
            var targetClassKey = _macros.GetTargetClassKey(agentId);
            if (!string.IsNullOrEmpty(targetClassKey) && mover is not null)
            {
                var classSources = sources.Where(s => s.Tags.Contains(targetClassKey!))
                    .Select(s => s.Position)
                    .ToArray();
                var field = _lifecycle.TryGetDistanceField(targetClassKey!, classSources);
                if (field is not null && field.Get(mover.Position.CellX, mover.Position.CellY) == DistanceField.Unreachable)
                {
                    moveError = "path_not_found";
                    moved = false;
                }
                else
                {
                    moved = _lifecycle.TryMoveOneStep(agentId, step.Target, out moveError);
                }
            }
            else
            {
                moved = _lifecycle.TryMoveOneStep(agentId, step.Target, out moveError);
            }
            if (!moved && StringComparer.Ordinal.Equals(moveError, "path_not_found"))
            {
                // The step targets a block (tree, station, crop): its own cell is never
                // walkable, so the move is retried toward the cell that makes the action
                // adjacent instead of ending the macro-action on a path that cannot exist.
                moved = _lifecycle.TryMoveAdjacentToOneStep(agentId, step.Target, out moveError);
            }
            else if (moved &&
                     _lifecycle.TryGetAgent(agentId, out mover) &&
                     !BlockActionSystem.IsAdjacent(mover!.Position, step.Target) &&
                     before is { } start &&
                     mover.Position.CellX == start.CellX &&
                     mover.Position.CellY == start.CellY &&
                     mover.Position.FeetZ == start.FeetZ)
            {
                // The graph path ended on the target's column without a step to take, but the
                // block is not inside the 3x3x3 adjacency cube from there (typically a block
                // one level above or below the neighbouring ground). Pick the approach cell.
                moved = _lifecycle.TryMoveAdjacentToOneStep(agentId, step.Target, out moveError);
            }

            if (!moved && TraceEnabled)
            {
                var approach = mover is { } walker
                    ? _lifecycle!.FindApproachCell(walker.Position, step.Target)?.ToString() ?? "none"
                    : "no-agent";
                Console.Error.WriteLine(
                    $"[macro-walk-fail] agent {agentId} target={step.Target} approach={approach} from={mover?.Position.ToString() ?? "?"} err={moveError}");
            }

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
        if (TraceEnabled)
        {
            Console.Error.WriteLine($"[macro-invoke] agent {agentId} tick {context.Tick}: invoking {kind}");
        }
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
            if (TraceEnabled)
            {
                Console.Error.WriteLine($"[macro-deferred-sched] agent {agentId} tick {context.Tick}: scheduled {kind} cmdId={synthetic.CommandId} resolveTick={context.Tick + 1}");
            }
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
        if (TraceEnabled)
        {
            Console.Error.WriteLine($"[macro-deferred] agent {agentId} tick {context.Tick}: kind={resolution.Step.Kind} succeeded={execution.Succeeded} completed={completed.Succeeded} err={execution.ErrorCode ?? completed.ErrorCode}");
        }
        if (!completed.Succeeded)
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

        if (!execution.Succeeded)
        {
            // The macro still active is authoritative: it declared this source unreachable (or
            // unusable, e.g. mining the block under the agent's feet answers `support_required`)
            // and will pick the next perceived source on the next tick. Failing the whole macro
            // here made an obtain die on its first refused block — the farm and cooking chains
            // lost `wheat_seed` to the grass the agent was standing on.
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
