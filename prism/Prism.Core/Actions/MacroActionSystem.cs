using Prism.Core.World;

namespace Prism.Core.Actions;

public enum MacroSourceKind : byte
{
    Block,
    Crop,
    GroundItem,
    Container,
}

public enum MacroStepKind : byte
{
    Move,
    Mine,
    Harvest,
    Take,
    Craft,
    Till,
    Plant,
    WaitForRipe,
}

public sealed record PerceivedMacroSource(
    ulong SourceId,
    MacroSourceKind Kind,
    BlockCoordinate Position,
    ushort ItemTypeId,
    int AvailableQuantity,
    IReadOnlySet<string> Tags);

public sealed record ObtainObjectRequest(
    ushort? ItemTypeId,
    string? TagId,
    int Quantity,
    BlockCoordinate? Hint = null,
    int HintRadius = 0);

public sealed record MacroActionStep(
    MacroStepKind Kind,
    BlockCoordinate Target,
    ulong SourceId,
    ushort ItemTypeId,
    int Quantity,
    ushort? RecipeId = null);

public sealed record MacroActionResult(
    bool Succeeded,
    bool Completed,
    string? ErrorCode,
    int QuantityAcquired,
    MacroActionStep? Step)
{
    public static MacroActionResult Failure(string errorCode, int acquired) =>
        new(false, true, errorCode, acquired, null);
}

public sealed class MacroActionSystem
{
    private readonly Dictionary<ulong, ObtainState> _active = [];
    private readonly Dictionary<ulong, SequenceState> _sequences = [];

    public MacroActionResult StartObtainObject(
        AgentBody agent,
        ObtainObjectRequest request,
        IEnumerable<PerceivedMacroSource> perceivedSources)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(perceivedSources);
        var sourceSnapshot = perceivedSources.ToArray();
        if (request.ItemTypeId is null && string.IsNullOrWhiteSpace(request.TagId) ||
            request.ItemTypeId is not null && !string.IsNullOrWhiteSpace(request.TagId) ||
            request.Quantity <= 0 ||
            request.HintRadius < 0 ||
            request.Hint is null && request.HintRadius != 0)
        {
            throw new ArgumentException("The obtain-object request is invalid.", nameof(request));
        }

        if (_active.ContainsKey(agent.AgentId) || _sequences.ContainsKey(agent.AgentId))
        {
            return MacroActionResult.Failure("action_already_active", 0);
        }

        var state = new ObtainState(request);
        if (!TrySelectSource(state, agent.Position, sourceSnapshot, out var source))
        {
            return MacroActionResult.Failure("source_not_found", 0);
        }

        state.Source = source;
        _active.Add(agent.AgentId, state);
        return GetNextStep(agent.AgentId, agent.Position, sourceSnapshot);
    }

    public MacroActionResult StartCraft(
        AgentBody agent,
        ushort recipeId,
        BlockCoordinate? stationPosition,
        bool stationPerceived = true)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (_active.ContainsKey(agent.AgentId) || _sequences.ContainsKey(agent.AgentId))
        {
            return MacroActionResult.Failure("action_already_active", 0);
        }

        if (stationPosition is not null && !stationPerceived)
        {
            return MacroActionResult.Failure("not_perceived", 0);
        }

        var target = stationPosition ??
                     new BlockCoordinate(agent.Position.CellX, agent.Position.CellY, agent.Position.FeetZ);
        var steps = new List<MacroActionStep>();
        if (stationPosition is { } station && GetDistance(agent.Position, station) > 1)
        {
            steps.Add(new MacroActionStep(MacroStepKind.Move, station, 0, 0, 0));
        }

        steps.Add(new MacroActionStep(MacroStepKind.Craft, target, 0, 0, 0, recipeId));
        return StartSequence(agent.AgentId, steps);
    }

    public MacroActionResult StartCultivate(
        AgentBody agent,
        BlockCoordinate parcel,
        ushort seedItemTypeId,
        bool needsTilling,
        bool parcelPerceived = true)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (_active.ContainsKey(agent.AgentId) || _sequences.ContainsKey(agent.AgentId))
        {
            return MacroActionResult.Failure("action_already_active", 0);
        }

        if (!parcelPerceived)
        {
            return MacroActionResult.Failure("not_perceived", 0);
        }

        var steps = new List<MacroActionStep>();
        if (GetDistance(agent.Position, parcel) > 1)
        {
            steps.Add(new MacroActionStep(MacroStepKind.Move, parcel, 0, seedItemTypeId, 0));
        }

        if (needsTilling)
        {
            steps.Add(new MacroActionStep(MacroStepKind.Till, parcel, 0, seedItemTypeId, 0));
        }

        steps.Add(new MacroActionStep(MacroStepKind.Plant, parcel, 0, seedItemTypeId, 1));
        steps.Add(new MacroActionStep(MacroStepKind.WaitForRipe, parcel, 0, seedItemTypeId, 0));
        return StartSequence(agent.AgentId, steps);
    }

    public MacroActionResult RequestCultivateHarvest(ulong agentId)
    {
        if (!_sequences.TryGetValue(agentId, out var state) ||
            !state.WaitingForHarvest ||
            state.Steps.Count == 0)
        {
            return MacroActionResult.Failure("not_ripe", 0);
        }

        var planting = state.Steps[^1];
        state.WaitingForHarvest = false;
        state.Steps.Add(new MacroActionStep(
            MacroStepKind.Harvest,
            planting.Target,
            0,
            planting.ItemTypeId,
            0));
        return new MacroActionResult(true, false, null, 0, state.Steps[state.Index]);
    }

    public MacroActionResult GetNextStep(
        ulong agentId,
        AgentPosition position,
        IEnumerable<PerceivedMacroSource> perceivedSources)
    {
        ArgumentNullException.ThrowIfNull(perceivedSources);
        if (_sequences.TryGetValue(agentId, out var sequence))
        {
            if (sequence.WaitingForHarvest)
            {
                return new MacroActionResult(true, false, null, 0, null);
            }

            if (sequence.Index >= sequence.Steps.Count)
            {
                _sequences.Remove(agentId);
                return new MacroActionResult(true, true, null, 0, null);
            }

            return new MacroActionResult(true, false, null, 0, sequence.Steps[sequence.Index]);
        }

        var sourceSnapshot = perceivedSources.ToArray();
        if (!_active.TryGetValue(agentId, out var state))
        {
            return MacroActionResult.Failure("nothing_to_advance", 0);
        }

        if (state.Acquired >= state.Request.Quantity)
        {
            _active.Remove(agentId);
            return new MacroActionResult(true, true, null, state.Acquired, null);
        }

        if (state.Source is null)
        {
            if (!TrySelectSource(state, position, sourceSnapshot, out var selectedSource))
            {
                _active.Remove(agentId);
                return MacroActionResult.Failure("source_not_found", state.Acquired);
            }

            state.Source = selectedSource;
        }

        var source = sourceSnapshot.FirstOrDefault(candidate =>
            candidate.SourceId == state.Source!.SourceId &&
            candidate.Kind == state.Source.Kind &&
            candidate.Position == state.Source.Position &&
            candidate.ItemTypeId == state.Source.ItemTypeId &&
            candidate.AvailableQuantity > 0);
        if (source is null)
        {
            _active.Remove(agentId);
            return MacroActionResult.Failure("source_not_found", state.Acquired);
        }

        state.Source = source;
        if (GetDistance(position, source.Position) > 1)
        {
            state.CurrentStep ??= new MacroActionStep(
                MacroStepKind.Move,
                source.Position,
                source.SourceId,
                state.Request.ItemTypeId ?? source.ItemTypeId,
                0);
        }
        else
        {
            var quantity = Math.Min(
                state.Request.Quantity - state.Acquired,
                RemainingQuantity(state, source));
            if (quantity <= 0)
            {
                state.Source = null;
                return GetNextStep(agentId, position, perceivedSources);
            }

            state.CurrentStep ??= new MacroActionStep(
                source.Kind switch
                {
                    MacroSourceKind.Block => MacroStepKind.Mine,
                    MacroSourceKind.Crop => MacroStepKind.Harvest,
                    MacroSourceKind.GroundItem or MacroSourceKind.Container => MacroStepKind.Take,
                    _ => throw new ArgumentOutOfRangeException(nameof(source.Kind)),
                },
                source.Position,
                source.SourceId,
                state.Request.ItemTypeId ?? source.ItemTypeId,
                quantity);
        }

        return new MacroActionResult(true, false, null, state.Acquired, state.CurrentStep);
    }

    public MacroActionResult CompleteCurrentStep(
        ulong agentId,
        bool succeeded,
        int quantityAcquired = 0,
        string? failureCode = null)
    {
        if (_sequences.TryGetValue(agentId, out var sequence))
        {
            if (sequence.WaitingForHarvest ||
                sequence.Index >= sequence.Steps.Count)
            {
                return MacroActionResult.Failure("nothing_to_advance", 0);
            }

            var completedStep = sequence.Steps[sequence.Index];
            if (!succeeded)
            {
                if (string.IsNullOrWhiteSpace(failureCode))
                {
                    throw new ArgumentException("A failed macro step requires a reason.", nameof(failureCode));
                }

                _sequences.Remove(agentId);
                return MacroActionResult.Failure(failureCode, 0);
            }

            if (completedStep.Kind is MacroStepKind.WaitForRipe)
            {
                if (quantityAcquired != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(quantityAcquired));
                }

                sequence.Index++;
                sequence.WaitingForHarvest = true;
                return new MacroActionResult(true, false, null, 0, null);
            }

            if (completedStep.Kind is MacroStepKind.Harvest or MacroStepKind.Take or MacroStepKind.Mine)
            {
                if (quantityAcquired < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(quantityAcquired));
                }
            }
            else if (quantityAcquired != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantityAcquired));
            }

            sequence.Index++;
            return new MacroActionResult(true, false, null, 0, null);
        }

        if (!_active.TryGetValue(agentId, out var state) || state.CurrentStep is not { } step)
        {
            return MacroActionResult.Failure("nothing_to_advance", 0);
        }

        if (!succeeded)
        {
            if (string.IsNullOrWhiteSpace(failureCode))
            {
                throw new ArgumentException("A failed macro step requires a reason.", nameof(failureCode));
            }

            _active.Remove(agentId);
            return MacroActionResult.Failure(failureCode, state.Acquired);
        }

        if (step.Kind is MacroStepKind.Move)
        {
            if (quantityAcquired != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantityAcquired));
            }
        }
        else
        {
            if (quantityAcquired <= 0 || quantityAcquired > step.Quantity)
            {
                throw new ArgumentOutOfRangeException(nameof(quantityAcquired));
            }

            state.Acquired = checked(state.Acquired + quantityAcquired);
            state.AcquiredBySource[step.SourceId] =
                checked(state.AcquiredBySource.GetValueOrDefault(step.SourceId) + quantityAcquired);
            if (step.Kind is MacroStepKind.Mine or MacroStepKind.Harvest)
            {
                state.ExhaustedSources.Add(step.SourceId);
            }

            state.Source = null;
        }

        state.CurrentStep = null;
        return new MacroActionResult(true, false, null, state.Acquired, null);
    }

    private static bool TrySelectSource(
        ObtainState state,
        AgentPosition position,
        IEnumerable<PerceivedMacroSource> perceivedSources,
        out PerceivedMacroSource source)
    {
        source = perceivedSources
            .Where(candidate =>
                candidate.AvailableQuantity > 0 &&
                candidate.Tags is not null &&
                !state.ExhaustedSources.Contains(candidate.SourceId) &&
                RemainingQuantity(state, candidate) > 0 &&
                IsMatching(state.Request, candidate) &&
                IsWithinHint(state.Request, candidate.Position))
            .OrderBy(candidate => GetDistance(position, candidate.Position))
            .ThenBy(candidate => candidate.Position.Z)
            .ThenBy(candidate => candidate.Position.Y)
            .ThenBy(candidate => candidate.Position.X)
            .ThenBy(candidate => candidate.SourceId)
            .FirstOrDefault()!;
        return source is not null;
    }

    private static bool IsMatching(ObtainObjectRequest request, PerceivedMacroSource source) =>
        request.ItemTypeId is { } itemTypeId
            ? source.ItemTypeId == itemTypeId
            : source.Tags.Contains(request.TagId!);

    private static bool IsWithinHint(ObtainObjectRequest request, BlockCoordinate position)
    {
        if (request.Hint is not { } hint)
        {
            return true;
        }

        return Math.Abs((long)position.X - hint.X) +
               Math.Abs((long)position.Y - hint.Y) +
               Math.Abs((long)position.Z - hint.Z) <= request.HintRadius;
    }

    private static long GetDistance(AgentPosition position, BlockCoordinate target) =>
        Math.Abs((long)position.CellX - target.X) +
        Math.Abs((long)position.CellY - target.Y) +
        Math.Abs((long)position.FeetZ - target.Z);

    private static int RemainingQuantity(ObtainState state, PerceivedMacroSource source) =>
        Math.Max(
            0,
            source.AvailableQuantity - state.AcquiredBySource.GetValueOrDefault(source.SourceId));

    private MacroActionResult StartSequence(
        ulong agentId,
        IReadOnlyList<MacroActionStep> steps)
    {
        var state = new SequenceState(steps);
        _sequences.Add(agentId, state);
        return new MacroActionResult(true, false, null, 0, steps[0]);
    }

    private sealed class ObtainState(ObtainObjectRequest request)
    {
        public ObtainObjectRequest Request { get; } = request;
        public int Acquired { get; set; }
        public PerceivedMacroSource? Source { get; set; }
        public MacroActionStep? CurrentStep { get; set; }
        public HashSet<ulong> ExhaustedSources { get; } = [];
        public Dictionary<ulong, int> AcquiredBySource { get; } = [];
    }

    private sealed class SequenceState(
        IReadOnlyList<MacroActionStep> steps)
    {
        public List<MacroActionStep> Steps { get; } = steps.ToList();
        public int Index { get; set; }
        public bool WaitingForHarvest { get; set; }
    }
}
