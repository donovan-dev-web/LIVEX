using System.Text.Json;
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
    ushort? RecipeId = null,
    bool UsesStation = false);

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
    private const int CheckpointVersion = 1;
    private static readonly JsonSerializerOptions CheckpointJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<ulong, ObtainState> _active = [];
    private readonly Dictionary<ulong, SequenceState> _sequences = [];

    public IReadOnlyCollection<ulong> ActiveAgentIds =>
        _active.Keys.Concat(_sequences.Keys).Distinct().Order().ToArray();

    public bool IsWaitingForHarvest(ulong agentId) =>
        _sequences.TryGetValue(agentId, out var state) && state.WaitingForHarvest;

    public bool TryGetWaitingCrop(ulong agentId, out BlockCoordinate position)
    {
        position = default;
        if (!_sequences.TryGetValue(agentId, out var state) ||
            !state.WaitingForHarvest ||
            state.Steps.Count == 0)
        {
            return false;
        }

        position = state.Steps[^1].Target;
        return true;
    }

    public bool Cancel(ulong agentId) =>
        _active.Remove(agentId) || _sequences.Remove(agentId);

    public byte[] CaptureState()
    {
        var checkpoint = new MacroActionCheckpoint(
            CheckpointVersion,
            _active.OrderBy(static entry => entry.Key)
                .Select(static entry => new ObtainCheckpoint(
                    entry.Key,
                    entry.Value.Request,
                    entry.Value.Acquired,
                    entry.Value.Source is { } source
                        ? new SourceCheckpoint(
                            source.SourceId,
                            source.Kind,
                            source.Position,
                            source.ItemTypeId,
                            source.AvailableQuantity,
                            source.Tags.Order(StringComparer.Ordinal).ToArray())
                        : null,
                    entry.Value.CurrentStep,
                    entry.Value.ExhaustedSources.Order().ToArray(),
                    entry.Value.AcquiredBySource.OrderBy(static value => value.Key)
                        .Select(static value => new AcquiredSource(value.Key, value.Value))
                        .ToArray()))
                .ToArray(),
            _sequences.OrderBy(static entry => entry.Key)
                .Select(static entry => new SequenceCheckpoint(
                    entry.Key,
                    entry.Value.Steps.ToArray(),
                    entry.Value.Index,
                    entry.Value.WaitingForHarvest))
                .ToArray());
        return JsonSerializer.SerializeToUtf8Bytes(checkpoint, CheckpointJsonOptions);
    }

    public void RestoreState(ReadOnlySpan<byte> checkpointBytes)
    {
        if (_active.Count != 0 || _sequences.Count != 0)
        {
            throw new InvalidOperationException("Macro-action state can only be restored into a new system.");
        }

        var checkpoint = JsonSerializer.Deserialize<MacroActionCheckpoint>(
                             checkpointBytes,
                             CheckpointJsonOptions)
                         ?? throw new InvalidDataException("Macro-action checkpoint is empty.");
        if (checkpoint.Version != CheckpointVersion ||
            checkpoint.Obtains is null ||
            checkpoint.Sequences is null)
        {
            throw new InvalidDataException("Macro-action checkpoint version or contents are invalid.");
        }

        var agentIds = new HashSet<ulong>();
        foreach (var obtain in checkpoint.Obtains)
        {
            if (obtain is null ||
                obtain.Request is null ||
                obtain.ExhaustedSources is null ||
                obtain.AcquiredBySource is null ||
                obtain.Acquired < 0 ||
                obtain.Request.Quantity <= 0 ||
                obtain.Acquired > obtain.Request.Quantity ||
                !agentIds.Add(obtain.AgentId))
            {
                throw new InvalidDataException("Macro-action checkpoint contains an invalid obtain action.");
            }

            var state = new ObtainState(obtain.Request)
            {
                Acquired = obtain.Acquired,
                Source = obtain.Source is { } source
                    ? new PerceivedMacroSource(
                        source.SourceId,
                        source.Kind,
                        source.Position,
                        source.ItemTypeId,
                        source.AvailableQuantity,
                        new HashSet<string>(source.Tags, StringComparer.Ordinal))
                    : null,
                CurrentStep = obtain.CurrentStep,
            };
            foreach (var sourceId in obtain.ExhaustedSources)
            {
                if (!state.ExhaustedSources.Add(sourceId))
                {
                    throw new InvalidDataException("Macro-action checkpoint contains duplicate exhausted sources.");
                }
            }

            foreach (var acquired in obtain.AcquiredBySource)
            {
                if (acquired.Quantity <= 0 || !state.AcquiredBySource.TryAdd(acquired.SourceId, acquired.Quantity))
                {
                    throw new InvalidDataException("Macro-action checkpoint contains invalid source progress.");
                }
            }

            _active.Add(obtain.AgentId, state);
        }

        foreach (var sequence in checkpoint.Sequences)
        {
            if (sequence is null ||
                sequence.Steps is null ||
                sequence.Steps.Length == 0 ||
                sequence.Index < 0 ||
                sequence.Index > sequence.Steps.Length ||
                sequence.WaitingForHarvest && sequence.Index == 0 ||
                !agentIds.Add(sequence.AgentId))
            {
                throw new InvalidDataException("Macro-action checkpoint contains an invalid sequence.");
            }

            var state = new SequenceState(sequence.Steps)
            {
                Index = sequence.Index,
                WaitingForHarvest = sequence.WaitingForHarvest,
            };
            _sequences.Add(sequence.AgentId, state);
        }
    }

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

        steps.Add(new MacroActionStep(
        MacroStepKind.Craft,
        target,
        0,
        0,
        0,
        recipeId,
        stationPosition is not null));
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
            if (parcel.Z == 0)
            {
                return MacroActionResult.Failure("soil_unsuitable", 0);
            }

            steps.Add(new MacroActionStep(
                MacroStepKind.Till,
                parcel with { Z = parcel.Z - 1 },
                0,
                seedItemTypeId,
                0));
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
                if (quantityAcquired < 0 || quantityAcquired > completedStep.Quantity && completedStep.Quantity > 0)
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
            if (quantityAcquired < 0 || quantityAcquired > step.Quantity)
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

    private sealed record MacroActionCheckpoint(
        int Version,
        ObtainCheckpoint[] Obtains,
        SequenceCheckpoint[] Sequences);

    private sealed record ObtainCheckpoint(
        ulong AgentId,
        ObtainObjectRequest Request,
        int Acquired,
        SourceCheckpoint? Source,
        MacroActionStep? CurrentStep,
        ulong[] ExhaustedSources,
        AcquiredSource[] AcquiredBySource);

    private sealed record SequenceCheckpoint(
        ulong AgentId,
        MacroActionStep[] Steps,
        int Index,
        bool WaitingForHarvest);

    private sealed record AcquiredSource(ulong SourceId, int Quantity);

    private sealed record SourceCheckpoint(
        ulong SourceId,
        MacroSourceKind Kind,
        BlockCoordinate Position,
        ushort ItemTypeId,
        int AvailableQuantity,
        string[] Tags);
}
