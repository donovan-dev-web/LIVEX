using System.Text.Json;
using System.Threading.Channels;
using Prism.Content;
using Prism.Contracts;
using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.Time;
using Prism.Core.World;
using Prism.Host.Persistence;
using Prism.Host.Ticking;

namespace Prism.Host.Hosting;

public enum PrismHostState
{
    Idle,
    Loading,
    Ready,
    Running,
    Paused,
    Stopped,
    Failed,
}

public sealed record PrepareWorldRequest(
    ulong Seed,
    string ContentHash,
    string? ContentDirectory = null);

public sealed record StartWorldRequest(long? MaxTicks = null);

public sealed record CheckpointWorldRequest(string CheckpointId);

public sealed record RestoreWorldRequest(string CheckpointId, string ContentHash);

public sealed record PrismHostStatus(
    string State,
    long LastCompletedTick,
    ulong? Seed,
    string? ContentHash,
    string? Error);

public sealed record ReadStreamEnvelope(
    string ProtocolVersion,
    long Sequence,
    long Tick,
    string Kind,
    JsonElement Payload);

public sealed record WorldRegionDelta(
    int RegionId,
    int MinX,
    int MinY,
    int Width,
    int Height,
    SurfaceColumn[] SurfaceColumns,
    KeyValuePair<BlockCoordinate, BlockOverride>[] Overrides,
    ulong RegionHash);

public sealed record WorldDeltaSnapshot(
    long Tick,
    ulong WorldHash,
    WorldRegionDelta[] Regions);

public sealed record WorldChecksumSnapshot(long Tick, ulong WorldHash);

public sealed record ItemStockMetric(
    ushort ItemTypeId,
    long Agents,
    long Containers,
    long GroundItems);

public sealed record MatterRuleMetric(
    ushort ItemTypeId,
    string RuleId,
    long Created,
    long Destroyed);

public sealed record StructureMetric(
    string SiteId,
    string TemplateId,
    int ProgressPermille,
    int IntegrityPermille,
    bool EffectsActive);

public sealed record CommandFailureMetric(string Reason, long Count);

public sealed record WorldMetricsSnapshot(
    long Tick,
    ItemStockMetric[] Stocks,
    MatterRuleMetric[] Matter,
    StructureMetric[] Structures,
    int ReservedStations,
    int ActiveStructureEffects,
    CommandFailureMetric[] Failures);

public sealed record GeneratedWorldSnapshot(
    long Tick,
    int WorldGenVersion,
    ulong Seed,
    int Attempt,
    string ContentHash,
    ulong WorldHash,
    int OreBlocksPlaced,
    int WaterBlocksPlaced,
    int Width,
    int Height,
    int VerticalSize,
    int RegionSize,
    WorldZone[] Zones,
    StrataProfile[] StrataProfiles,
    SurfaceColumn[] SurfaceColumns,
    KeyValuePair<BlockCoordinate, BlockOverride>[] Overrides,
    PlacedTree[] Trees,
    BlockCoordinate[] SpawnSites,
    StructureConstructionSite[] ConstructionSites,
    StructureIntegritySnapshot[] StructureIntegrity,
    ActiveStructureEffect[] ActiveStructureEffects,
    AgentEntitySnapshot[]? Agents = null,
    GroundItem[]? GroundItems = null);

public sealed record AgentEntitySnapshotEnvelope(
    long Tick,
    AgentEntitySnapshot[] Agents,
    GroundItem[] GroundItems);

public sealed class PrismHostRuntime : IWorldHost
{
    private readonly object _gate = new();
    private readonly Func<IWorldHost>? _customWorldHostFactory;
    private readonly ReadStreamHub _readStream;
    private readonly WorldCheckpointStore? _checkpointStore;
    private readonly long _automaticCheckpointIntervalTicks;
    private readonly int _automaticCheckpointsToKeep;
    private readonly long _checksumIntervalTicks;
    private readonly bool _checkpointOnStop;
    private WorldGenerationResult? _generatedWorld;
    private CompiledContentPack? _compiledPack;
    private StructureConstructionSystem? _construction;
    private MatterLedger? _ledger;
    private WorldContainerStore? _containers;
    private CraftingStationRegistry? _stations;
    private readonly SortedDictionary<string, long> _failures = new(StringComparer.Ordinal);
    private AgentLifecycleSystem? _agentLifecycle;
    private PrismHostState _state = PrismHostState.Idle;
    private IWorldHost? _worldHost;
    private ulong? _seed;
    private string? _contentHash;
    private string? _error;
    private long _lastCompletedTick = -1;
    private long? _maxTicks;

    public PrismHostRuntime(
        Func<IWorldHost>? worldHostFactory = null,
        int readStreamCapacity = 128,
        WorldCheckpointStore? checkpointStore = null,
        long automaticCheckpointIntervalTicks = 500,
        int automaticCheckpointsToKeep = 3,
        bool checkpointOnStop = true,
        long checksumIntervalTicks = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(automaticCheckpointIntervalTicks);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(automaticCheckpointsToKeep);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(checksumIntervalTicks);
        _customWorldHostFactory = worldHostFactory;
        _readStream = new ReadStreamHub(readStreamCapacity);
        _checkpointStore = checkpointStore;
        _automaticCheckpointIntervalTicks = automaticCheckpointIntervalTicks;
        _automaticCheckpointsToKeep = automaticCheckpointsToKeep;
        _checksumIntervalTicks = checksumIntervalTicks;
        _checkpointOnStop = checkpointOnStop;
    }

    public PrismHostStatus Status
    {
        get
        {
            lock (_gate)
            {
                return CreateStatus();
            }
        }
    }

    public bool IsReady
    {
        get
        {
            lock (_gate)
            {
                return _state is PrismHostState.Ready or PrismHostState.Running or PrismHostState.Paused;
            }
        }
    }

    public GeneratedWorldSnapshot? WorldSnapshot
    {
        get
        {
            lock (_gate)
            {
                return _generatedWorld is null
                    ? null
                    : CreateGeneratedWorldSnapshot(
                        _generatedWorld,
                        _lastCompletedTick,
                        _construction,
                        _agentLifecycle);
            }
        }
    }

    public void Prepare(PrepareWorldRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ContentHash);
        lock (_gate)
        {
            if (_state is not (PrismHostState.Idle or PrismHostState.Stopped))
            {
                throw new HostLifecycleException("invalid_state", $"Cannot prepare a host in state '{_state}'.");
            }

            _state = PrismHostState.Loading;
            try
            {
                WorldGenerationResult? generatedWorld = null;
                CompiledContentPack? compiledPack = null;
                MovementRules? movementRules = null;
                Prism.Core.Perception.PerceptionRules? perceptionRules = null;
                IReadOnlyDictionary<string, StructureTemplateRule> templates =
                    new Dictionary<string, StructureTemplateRule>(StringComparer.Ordinal);
                if (request.ContentDirectory is not null)
                {
                    var compilation = new Prism.Content.ContentPackCompiler().CompileDirectory(request.ContentDirectory);
                    if (!compilation.Succeeded)
                    {
                        var details = string.Join("; ", compilation.Errors.Select(error => $"{error.Code}: {error.Path}"));
                        throw new HostLifecycleException("content_invalid", details);
                    }

                    if (!StringComparer.OrdinalIgnoreCase.Equals(compilation.Pack!.ContentHash, request.ContentHash))
                    {
                        throw new HostLifecycleException(
                            "content_hash_mismatch",
                            "The requested contentHash does not match the compiled pack.");
                    }

                    compiledPack = compilation.Pack!;
                    var generationConfig = WorldGenerationConfigCompiler.Compile(compiledPack);
                    generatedWorld = WorldGenerator.Generate(request.Seed, generationConfig);
                    templates = StructureTemplateRuntimeCompiler.Compile(compiledPack);
                    movementRules = MovementRuleRuntimeCompiler.Compile(compiledPack);
                    perceptionRules = PerceptionRuleRuntimeCompiler.Compile(compiledPack);
                    ClearDirtyRegions(generatedWorld.Grid);
                }

                _worldHost = CreateWorldHost(
                    generatedWorld,
                    templates,
                    movementRules,
                    perceptionRules,
                    compiledPack,
                    out var construction,
                    out var agentLifecycle,
                    out var matterLedger,
                    out var containerStore,
                    out var stationRegistry);
                _construction = construction;
                _agentLifecycle = agentLifecycle;
                _ledger = matterLedger;
                _containers = containerStore;
                _stations = stationRegistry;
                _failures.Clear();
                _generatedWorld = generatedWorld;
                _compiledPack = compiledPack;
                _seed = request.Seed;
                _contentHash = request.ContentHash;
                _lastCompletedTick = -1;
                _maxTicks = null;
                _error = null;
                _state = PrismHostState.Ready;
                PublishState("world_ready");
            }
            catch (Exception exception)
            {
                _generatedWorld = null;
                _compiledPack = null;
                _construction = null;
                _agentLifecycle = null;
                _ledger = null;
                _containers = null;
                _stations = null;
                _failures.Clear();
                _state = PrismHostState.Failed;
                _error = exception.Message;
                throw;
            }
        }
    }

    public void Start(StartWorldRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MaxTicks is <= 0)
        {
            throw new HostLifecycleException("invalid_max_ticks", "maxTicks must be positive when specified.");
        }

        lock (_gate)
        {
            RequireState(PrismHostState.Ready, "start");
            _maxTicks = request.MaxTicks;
            _state = PrismHostState.Running;
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            RequireState(PrismHostState.Running, "pause");
            _state = PrismHostState.Paused;
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            RequireState(PrismHostState.Paused, "resume");
            _state = PrismHostState.Running;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_state is PrismHostState.Idle or PrismHostState.Stopped)
            {
                throw new HostLifecycleException("invalid_state", $"Cannot stop a host in state '{_state}'.");
            }

            if (_checkpointOnStop && _state != PrismHostState.Failed)
            {
                SaveAutomaticCheckpoint();
            }

            _state = PrismHostState.Stopped;
            PublishState("run_stopped");
        }
    }

    public void SaveCheckpoint(string checkpointId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);
        lock (_gate)
        {
            if (_state is PrismHostState.Idle or PrismHostState.Loading or PrismHostState.Failed ||
                _worldHost is not ICheckpointableWorldHost checkpointable ||
                _seed is null ||
                _contentHash is null)
            {
                throw new HostLifecycleException("checkpoint_unavailable", "The current host cannot be checkpointed.");
            }

            if (_checkpointStore is null)
            {
                throw new HostLifecycleException("persistence_unavailable", "No checkpoint database is configured.");
            }

            var hostState = checkpointable.CaptureCheckpoint();
            if (checkpointable.LastCompletedTick != _lastCompletedTick)
            {
                throw new InvalidOperationException("The host tick and runtime tick are inconsistent.");
            }

            _checkpointStore.Save(new WorldCheckpoint(
                checkpointId,
                _lastCompletedTick,
                _seed.Value,
                _contentHash,
                hostState,
                _generatedWorld is null
                    ? null
                    : JsonSerializer.SerializeToUtf8Bytes(
                    new PersistedWorldState(
                        1,
                        CreateGeneratedWorldSnapshot(
                            _generatedWorld,
                            _lastCompletedTick,
                            _construction,
                            _agentLifecycle),
                        ToContentDefinition(_compiledPack)),
                    ProtocolJson.Options)));
        }
    }

    public void RestoreCheckpoint(string checkpointId, string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        lock (_gate)
        {
            if (_state is not (PrismHostState.Idle or PrismHostState.Stopped))
            {
                throw new HostLifecycleException("invalid_state", $"Cannot restore a host in state '{_state}'.");
            }

            if (_checkpointStore is null)
            {
                throw new HostLifecycleException("persistence_unavailable", "No checkpoint database is configured.");
            }

            WorldCheckpoint checkpoint;
            try
            {
                checkpoint = _checkpointStore.Load(checkpointId, contentHash);
            }
            catch (CheckpointContentMismatchException exception)
            {
                throw new HostLifecycleException("content_hash_mismatch", exception.Message);
            }

            var (generatedWorld, compiledPack) = checkpoint.WorldState is null
                ? (null, null)
                : RestoreGeneratedWorldState(checkpoint.WorldState, checkpoint);
            IReadOnlyDictionary<string, StructureTemplateRule> templates =
                new Dictionary<string, StructureTemplateRule>(StringComparer.Ordinal);
            MovementRules? movementRules = null;
            Prism.Core.Perception.PerceptionRules? perceptionRules = null;
            if (compiledPack is not null)
            {
                templates = StructureTemplateRuntimeCompiler.Compile(compiledPack);
                movementRules = MovementRuleRuntimeCompiler.Compile(compiledPack);
                perceptionRules = PerceptionRuleRuntimeCompiler.Compile(compiledPack);
            }

            var host = CreateWorldHost(
                generatedWorld,
                templates,
                movementRules,
                perceptionRules,
                compiledPack,
                out var construction,
                out var agentLifecycle,
                out var matterLedger,
                out var containerStore,
                out var stationRegistry);
            if (host is not ICheckpointableWorldHost checkpointable)
            {
                throw new HostLifecycleException(
                    "checkpoint_unsupported",
                    "The configured world host does not support restoration.");
            }

            checkpointable.RestoreCheckpoint(checkpoint.HostState);
            if (checkpointable.LastCompletedTick != checkpoint.Tick)
            {
                throw new InvalidDataException("Checkpoint tick does not match the restored world host.");
            }

            _worldHost = host;
            _generatedWorld = generatedWorld;
            _compiledPack = compiledPack;
            _construction = construction;
            _agentLifecycle = agentLifecycle;
            _ledger = matterLedger;
            _containers = containerStore;
            _stations = stationRegistry;
            _failures.Clear();
            _seed = checkpoint.Seed;
            _contentHash = checkpoint.ContentHash;
            _lastCompletedTick = checkpoint.Tick;
            _maxTicks = null;
            _error = null;
            _state = PrismHostState.Ready;
            PublishState("world_ready");
        }
    }

    public TickResult Step(TickRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            RequireState(PrismHostState.Running, "step");
            ProtocolJson.Serialize(request);
            if (request.Tick != checked(_lastCompletedTick + 1))
            {
                throw new HostLifecycleException(
                    "unexpected_tick",
                    $"Expected tick {_lastCompletedTick + 1}, received {request.Tick}.");
            }

            var tickCommitted = false;
            try
            {
                var previousWorldRevision = _generatedWorld?.Grid.Revision;
                var previousAgentRevision = _agentLifecycle?.Revision;
                var result = _worldHost!.Step(request);
                tickCommitted = true;
                _lastCompletedTick = result.Tick;
                TrackCommandFailures(result);
                if (result.Tick % _automaticCheckpointIntervalTicks ==
                    _automaticCheckpointIntervalTicks - 1)
                {
                    SaveAutomaticCheckpoint();
                }

                if (_generatedWorld is not null &&
                    previousWorldRevision != _generatedWorld.Grid.Revision)
                {
                    var delta = CreateWorldDelta(_generatedWorld, _lastCompletedTick);
                    if (delta is not null)
                    {
                        _readStream.Publish(
                            "region_delta",
                            JsonSerializer.SerializeToElement(delta, ProtocolJson.Options),
                            CreateSnapshot);
                    }
                }

                if (_agentLifecycle is not null &&
                    previousAgentRevision != _agentLifecycle.Revision)
                {
                    var entities = new AgentEntitySnapshotEnvelope(
                        _lastCompletedTick,
                        _agentLifecycle.SnapshotAgents(),
                        _agentLifecycle.SnapshotGroundItems());
                    _readStream.Publish(
                        "entity_snapshot",
                        JsonSerializer.SerializeToElement(entities, ProtocolJson.Options),
                        CreateSnapshot);
                }

                var payload = JsonSerializer.SerializeToElement(result, ProtocolJson.Options);
                _readStream.Publish("tick_result", payload, CreateSnapshot);
                if (_generatedWorld is not null &&
                    result.Tick % _checksumIntervalTicks == _checksumIntervalTicks - 1)
                {
                    var checksum = new WorldChecksumSnapshot(
                        result.Tick,
                        WorldGridHasher.ComputeWorldHash(
                            _generatedWorld.Grid,
                            _generatedWorld.WorldGenVersion,
                            _generatedWorld.Seed,
                            _generatedWorld.Attempt,
                            _generatedWorld.Zones,
                            _generatedWorld.SpawnSites,
                            _generatedWorld.ContentHash));
                    _readStream.Publish(
                        "checksum",
                        JsonSerializer.SerializeToElement(checksum, ProtocolJson.Options),
                        CreateSnapshot);
                    _readStream.Publish(
                        "metrics",
                        JsonSerializer.SerializeToElement(CreateMetrics(), ProtocolJson.Options),
                        CreateSnapshot);
                }

                if (_maxTicks is { } maxTicks && result.Tick + 1 >= maxTicks)
                {
                    if (_checkpointOnStop)
                    {
                        SaveAutomaticCheckpoint();
                    }

                    _state = PrismHostState.Stopped;
                    PublishState("run_stopped");
                }

                return result;
            }
            catch (Exception exception)
            {
                if (tickCommitted ||
                    _worldHost is not OrderedWorldHost orderedHost ||
                    orderedHost.IsFaulted)
                {
                    _state = PrismHostState.Failed;
                    _error = exception.Message;
                }

                throw;
            }
        }
    }

    public ReadStreamSubscription SubscribeReadStream()
    {
        lock (_gate)
        {
            return _readStream.Subscribe(CreateSnapshot());
        }
    }

    public WorldMetricsSnapshot? CurrentMetrics
    {
        get
        {
            lock (_gate)
            {
                return _agentLifecycle is null && _construction is null && _ledger is null
                    ? null
                    : CreateMetrics();
            }
        }
    }

    private void TrackCommandFailures(TickResult result)
    {
        foreach (var eventEnvelope in result.Events)
        {
            if (!StringComparer.Ordinal.Equals(eventEnvelope.Kind, "action_failed"))
            {
                continue;
            }

            var reason = eventEnvelope.Payload.ValueKind == JsonValueKind.Object &&
                         eventEnvelope.Payload.TryGetProperty("reason", out var reasonElement) &&
                         reasonElement.ValueKind == JsonValueKind.String
                ? reasonElement.GetString() ?? "unspecified"
                : "unspecified";
            _failures[reason] = checked(_failures.GetValueOrDefault(reason) + 1);
        }
    }

    private WorldMetricsSnapshot CreateMetrics()
    {
        var agentStocks = new SortedDictionary<ushort, long>();
        var containerStocks = new SortedDictionary<ushort, long>();
        var groundStocks = new SortedDictionary<ushort, long>();
        if (_agentLifecycle is not null)
        {
            foreach (var agent in _agentLifecycle.SnapshotAgents())
            {
                foreach (var stack in agent.Inventory)
                {
                    AddStock(agentStocks, stack.ItemTypeId, stack.Quantity);
                }
            }

            foreach (var groundItem in _agentLifecycle.SnapshotGroundItems())
            {
                AddStock(groundStocks, groundItem.Stack.ItemTypeId, groundItem.Stack.Quantity);
            }
        }

        if (_containers is not null)
        {
            foreach (var container in _containers.Snapshot())
            {
                foreach (var stack in container.Snapshot())
                {
                    AddStock(containerStocks, stack.ItemTypeId, stack.Quantity);
                }
            }
        }

        var itemIds = new SortedSet<ushort>(agentStocks.Keys);
        itemIds.UnionWith(containerStocks.Keys);
        itemIds.UnionWith(groundStocks.Keys);
        var stocks = itemIds
            .Select(itemTypeId => new ItemStockMetric(
                itemTypeId,
                agentStocks.GetValueOrDefault(itemTypeId),
                containerStocks.GetValueOrDefault(itemTypeId),
                groundStocks.GetValueOrDefault(itemTypeId)))
            .ToArray();

        var matter = _ledger is null
            ? []
            : _ledger.EnumerateEntries()
                .OrderBy(static entry => entry.ItemTypeId)
                .ThenBy(static entry => entry.RuleId, StringComparer.Ordinal)
                .Select(static entry => new MatterRuleMetric(
                    entry.ItemTypeId,
                    entry.RuleId,
                    entry.Created,
                    entry.Destroyed))
                .ToArray();

        var structures = _construction is null
            ? []
            : _construction.Sites
                .OrderBy(static site => site.Id, StringComparer.Ordinal)
                .Select(site =>
                {
                    var integrity = _construction.InspectIntegrity(site.Id);
                    var progress = site.WorkRequired == 0
                        ? 1000
                        : (int)((long)site.WorkCompleted * 1000 / site.WorkRequired);
                    return new StructureMetric(
                        site.Id,
                        site.TemplateId,
                        Math.Clamp(progress, 0, 1000),
                        integrity.IntegrityPermille,
                        integrity.EffectsActive);
                })
                .ToArray();

        return new WorldMetricsSnapshot(
            _lastCompletedTick,
            stocks,
            matter,
            structures,
            _stations?.ReservationCount ?? 0,
            _construction?.GetActiveEffects().Count() ?? 0,
            _failures
                .Select(static entry => new CommandFailureMetric(entry.Key, entry.Value))
                .ToArray());
    }

    private static void AddStock(SortedDictionary<ushort, long> stocks, ushort itemTypeId, int quantity) =>
        stocks[itemTypeId] = checked(stocks.GetValueOrDefault(itemTypeId) + quantity);

    private void RequireState(PrismHostState required, string operation)
    {
        if (_state != required)
        {
            throw new HostLifecycleException("invalid_state", $"Cannot {operation} a host in state '{_state}'.");
        }
    }

    private PrismHostStatus CreateStatus() =>
        new(_state.ToString().ToLowerInvariant(), _lastCompletedTick, _seed, _contentHash, _error);

    private void SaveAutomaticCheckpoint()
    {
        if (_checkpointStore is null)
        {
            return;
        }

        if (_seed is null || _contentHash is null)
        {
            throw new HostLifecycleException("checkpoint_unavailable", "The current host has no run identity.");
        }

        var prefix = $"auto:{_contentHash}:{_seed.Value}:";
        var checkpointId = $"{prefix}{_lastCompletedTick}";
        SaveCheckpoint(checkpointId);
        _checkpointStore.PruneAutomaticCheckpoints(prefix, _automaticCheckpointsToKeep);
    }

    private JsonElement CreateSnapshot()
    {
        var status = CreateStatus();
        var world = _generatedWorld is null
            ? null
            : CreateGeneratedWorldSnapshot(
                _generatedWorld,
                _lastCompletedTick,
                _construction,
                _agentLifecycle);
        return JsonSerializer.SerializeToElement(
            new
            {
                status.State,
                status.LastCompletedTick,
                status.Seed,
                status.ContentHash,
                status.Error,
                world,
            },
            ProtocolJson.Options);
    }

    private static GeneratedWorldSnapshot CreateGeneratedWorldSnapshot(
        WorldGenerationResult generatedWorld,
        long tick,
        StructureConstructionSystem? construction,
        AgentLifecycleSystem? agentLifecycle)
    {
        var grid = generatedWorld.Grid;
        var columns = new SurfaceColumn[checked(grid.Width * grid.Height)];
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                columns[x + grid.Width * y] = grid.GetSurfaceColumn(x, y);
            }
        }

        var worldHash = WorldGridHasher.ComputeWorldHash(
            grid,
            generatedWorld.WorldGenVersion,
            generatedWorld.Seed,
            generatedWorld.Attempt,
            generatedWorld.Zones,
            generatedWorld.SpawnSites,
            generatedWorld.ContentHash);
        return new GeneratedWorldSnapshot(
            tick,
            generatedWorld.WorldGenVersion,
            generatedWorld.Seed,
            generatedWorld.Attempt,
            generatedWorld.ContentHash,
            worldHash,
            generatedWorld.OreBlocksPlaced,
            generatedWorld.WaterBlocksPlaced,
            grid.Width,
            grid.Height,
            grid.VerticalSize,
            grid.RegionSize,
            generatedWorld.Zones.ToArray(),
            generatedWorld.StrataProfiles.ToArray(),
            columns,
            grid.EnumerateOverrides().ToArray(),
            generatedWorld.Trees.ToArray(),
            generatedWorld.SpawnSites.ToArray(),
            construction?.Sites.ToArray() ?? [],
            construction?.Sites
                .Select(site => construction.InspectIntegrity(site.Id))
                .ToArray() ?? [],
            construction?.GetActiveEffects().ToArray() ?? [],
            agentLifecycle?.SnapshotAgents() ?? [],
            agentLifecycle?.SnapshotGroundItems() ?? []);
    }

    private static WorldDeltaSnapshot? CreateWorldDelta(
        WorldGenerationResult generatedWorld,
        long tick)
    {
        var grid = generatedWorld.Grid;
        var changedRegions = new List<WorldRegionDelta>();
        for (var regionId = 0; regionId < grid.RegionCount; regionId++)
        {
            if (!grid.IsRegionDirty(regionId))
            {
                continue;
            }

            var minX = (regionId % grid.RegionCountX) * grid.RegionSize;
            var minY = (regionId / grid.RegionCountX) * grid.RegionSize;
            var width = Math.Min(grid.RegionSize, grid.Width - minX);
            var height = Math.Min(grid.RegionSize, grid.Height - minY);
            var columns = new SurfaceColumn[checked(width * height)];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    columns[x + width * y] = grid.GetSurfaceColumn(minX + x, minY + y);
                }
            }

            changedRegions.Add(new WorldRegionDelta(
                regionId,
                minX,
                minY,
                width,
                height,
                columns,
                grid.EnumerateRegionOverrides(regionId).ToArray(),
                WorldGridHasher.ComputeRegionHash(grid, regionId)));
            grid.ClearRegionDirty(regionId);
        }

        if (changedRegions.Count == 0)
        {
            return null;
        }

        var worldHash = WorldGridHasher.ComputeWorldHash(
            grid,
            generatedWorld.WorldGenVersion,
            generatedWorld.Seed,
            generatedWorld.Attempt,
            generatedWorld.Zones,
            generatedWorld.SpawnSites,
            generatedWorld.ContentHash);
        return new WorldDeltaSnapshot(tick, worldHash, changedRegions.ToArray());
    }

    private static void ClearDirtyRegions(WorldGrid grid)
    {
        for (var regionId = 0; regionId < grid.RegionCount; regionId++)
        {
            if (grid.IsRegionDirty(regionId))
            {
                grid.ClearRegionDirty(regionId);
            }
        }
    }

    private static (WorldGenerationResult? World, CompiledContentPack? ContentPack) RestoreGeneratedWorldState(
        ReadOnlySpan<byte> serializedState,
        WorldCheckpoint checkpoint)
    {
        var persistedState = JsonSerializer.Deserialize<PersistedWorldState>(
            serializedState,
            ProtocolJson.Options);
        GeneratedWorldSnapshot snapshot;
        ContentPackDefinition? contentDefinition;
        if (persistedState is { FormatVersion: 1, World: not null })
        {
            snapshot = persistedState.World;
            contentDefinition = persistedState.ContentDefinition;
        }
        else
        {
            snapshot = JsonSerializer.Deserialize<GeneratedWorldSnapshot>(
                           serializedState,
                           ProtocolJson.Options)
                       ?? throw new InvalidDataException("Checkpoint world snapshot is empty.");
            contentDefinition = null;
        }

        if (snapshot.Tick != checkpoint.Tick ||
            snapshot.Seed != checkpoint.Seed ||
            !StringComparer.Ordinal.Equals(snapshot.ContentHash, checkpoint.ContentHash) ||
            snapshot.Width <= 0 ||
            snapshot.Height <= 0 ||
            snapshot.VerticalSize <= 0 ||
            snapshot.RegionSize <= 0 ||
            snapshot.Zones is null ||
            snapshot.StrataProfiles is null ||
            snapshot.SurfaceColumns is null ||
            snapshot.Overrides is null ||
            snapshot.Trees is null ||
            snapshot.SpawnSites is null ||
            snapshot.SurfaceColumns.Length != checked(snapshot.Width * snapshot.Height) ||
            snapshot.Zones.Length != snapshot.SurfaceColumns.Length ||
            snapshot.StrataProfiles.Length == 0)
        {
            throw new InvalidDataException("Checkpoint world snapshot metadata or dimensions are inconsistent.");
        }

        CompiledContentPack? contentPack = null;
        if (contentDefinition is not null)
        {
            var compilation = new ContentPackCompiler().Compile(contentDefinition);
            if (!compilation.Succeeded)
            {
                throw new InvalidDataException(
                    $"Checkpoint content definition is invalid: {string.Join("; ", compilation.Errors.Select(error => error.Code))}");
            }

            contentPack = compilation.Pack;
        }

        var grid = new WorldGrid(snapshot.Width, snapshot.Height, snapshot.RegionSize, snapshot.VerticalSize);
        for (var y = 0; y < snapshot.Height; y++)
        {
            for (var x = 0; x < snapshot.Width; x++)
            {
                grid.SetSurfaceColumn(x, y, snapshot.SurfaceColumns[x + snapshot.Width * y]);
            }
        }

        foreach (var (coordinate, block) in snapshot.Overrides)
        {
            grid.SetBlockOverride(coordinate, block);
        }

        var restoredHash = WorldGridHasher.ComputeWorldHash(
            grid,
            snapshot.WorldGenVersion,
            snapshot.Seed,
            snapshot.Attempt,
            snapshot.Zones,
            snapshot.SpawnSites,
            snapshot.ContentHash);
        if (restoredHash != snapshot.WorldHash)
        {
            throw new InvalidDataException("Checkpoint world snapshot checksum does not match its contents.");
        }

        ClearDirtyRegions(grid);
        var world = new WorldGenerationResult(
            grid,
            snapshot.Zones,
            snapshot.StrataProfiles,
            snapshot.Trees,
            snapshot.OreBlocksPlaced,
            snapshot.WaterBlocksPlaced,
            snapshot.WorldGenVersion,
            snapshot.Seed,
            snapshot.Attempt,
            snapshot.SpawnSites,
            snapshot.ContentHash,
            snapshot.WorldHash);
        return (world, contentPack);
    }

    private void PublishState(string kind)
    {
        var payload = kind == "world_ready" ? CreateSnapshot() : CreateStatusSnapshot();
        _readStream.Publish(kind, payload, CreateSnapshot);
    }

    private JsonElement CreateStatusSnapshot() =>
        JsonSerializer.SerializeToElement(CreateStatus(), ProtocolJson.Options);

    private IWorldHost CreateWorldHost(
        WorldGenerationResult? generatedWorld,
        IReadOnlyDictionary<string, StructureTemplateRule> templates,
        MovementRules? movementRules,
        Prism.Core.Perception.PerceptionRules? perceptionRules,
        CompiledContentPack? compiledPack,
        out StructureConstructionSystem? construction,
        out AgentLifecycleSystem? agentLifecycle,
        out MatterLedger? matterLedgerOut,
        out WorldContainerStore? containerStoreOut,
        out CraftingStationRegistry? stationRegistryOut)
    {
        if (_customWorldHostFactory is not null)
        {
            construction = null;
            agentLifecycle = null;
            matterLedgerOut = null;
            containerStoreOut = null;
            stationRegistryOut = null;
            return _customWorldHostFactory();
        }

        return CreateDefaultWorldHost(
            generatedWorld,
            templates,
            movementRules,
            perceptionRules,
            compiledPack,
            out construction,
            out agentLifecycle,
            out matterLedgerOut,
            out containerStoreOut,
            out stationRegistryOut);
    }

    private static IWorldHost CreateDefaultWorldHost(
        WorldGenerationResult? generatedWorld,
        IReadOnlyDictionary<string, StructureTemplateRule> templates,
        MovementRules? movementRules,
        Prism.Core.Perception.PerceptionRules? perceptionRules,
        CompiledContentPack? compiledPack,
        out StructureConstructionSystem? construction,
        out AgentLifecycleSystem? agentLifecycle,
        out MatterLedger? matterLedgerOut,
        out WorldContainerStore? containerStoreOut,
        out CraftingStationRegistry? stationRegistryOut)
    {
        var constructionSystem = generatedWorld is null
            ? null
            : new StructureConstructionSystem(generatedWorld.Grid, templates);
        construction = constructionSystem;
        var perceptionSettings = compiledPack is null
            ? null
            : Prism.Content.PerceptionSettingsRuntimeCompiler.Compile(compiledPack);
        var agentLifecycleSystem = generatedWorld is null
            ? null
            : new AgentLifecycleSystem(
                generatedWorld.Grid,
                movementRules ?? new MovementRules([], []),
                perceptionRules,
                perceptionSettings);
        agentLifecycle = agentLifecycleSystem;
        var ledger = new MatterLedger();
        var stations = new CraftingStationRegistry();
        var containers = new WorldContainerStore();
        var containerDecayRules = compiledPack is null
            ? null
            : ContainerDecayRuntimeCompiler.Compile(compiledPack);
        matterLedgerOut = ledger;
        containerStoreOut = containers;
        stationRegistryOut = stations;
        var macros = new MacroActionSystem();
        var actionDurations = new ActionDurationTracker();
        var deferredResolutions = new DeferredActionResolutions();
        var macroCommandIds = new Dictionary<ulong, string>();
        var eventWheel = new EventWheel();
        var calendar = CreateCalendar(compiledPack);
        var mastery = new MasterySystem(
            compiledPack is null ? null : MasteryRuleRuntimeCompiler.Compile(compiledPack));
        var handlers = new List<IWorldCommandHandler>();
        if (constructionSystem is not null)
        {
            handlers.Add(new StructureConstructionCommandHandler(
                constructionSystem, "DemarrerChantier", agentLifecycleSystem, mastery));
            handlers.Add(new StructureConstructionCommandHandler(
                constructionSystem, "Contribuer", agentLifecycleSystem, mastery));
            handlers.Add(new StructureConstructionCommandHandler(
                constructionSystem, "DefinirAcces", agentLifecycleSystem, mastery));
        }

        if (agentLifecycleSystem is not null)
        {
            handlers.Add(new AgentLifecycleCommandHandler(agentLifecycleSystem, "Incarner"));
            handlers.Add(new AgentLifecycleCommandHandler(agentLifecycleSystem, "Retirer"));
            handlers.Add(new AgentLifecycleCommandHandler(agentLifecycleSystem, "Aller"));
        }

        CropActionRules? cropActionRules = null;
        if (compiledPack is not null && agentLifecycleSystem is not null && generatedWorld is not null)
        {
            var blockRules = BlockActionRuleRuntimeCompiler.Compile(compiledPack);
            var craftingRules = CraftingRuleRuntimeCompiler.Compile(compiledPack);
            cropActionRules = CropActionRuleRuntimeCompiler.Compile(compiledPack);
            var consumptionRules = ConsumptionRuleRuntimeCompiler.Compile(compiledPack);
            foreach (var kind in WorldActionCommandHandler.SupportedKinds)
            {
                handlers.Add(new WorldActionCommandHandler(
                    agentLifecycleSystem,
                    compiledPack,
                    generatedWorld.Grid,
                    blockRules,
                    craftingRules,
                    cropActionRules,
                    consumptionRules,
                    stations,
                    containers,
                    ledger,
                    eventWheel,
                    calendar,
                    mastery,
                    generatedWorld.Seed,
                    kind,
                    actionDurations,
                    constructionSystem is null ? null : new StructureEffectConsumer(constructionSystem),
                    containerDecayRules));
            }

            foreach (var kind in new[] { "ObtenirObjet", "FabriquerMacro", "Cultiver", "Annuler" })
            {
                handlers.Add(new MacroActionCommandHandler(
                    agentLifecycleSystem,
                    compiledPack,
                    generatedWorld.Grid,
                    cropActionRules,
                    macros,
                    macroCommandIds,
                    kind));
            }
        }

        var dispatch = new CommandDispatchPhase(
            handlers,
            durations: actionDurations,
            resolutions: deferredResolutions);
        var actionState = new WorldActionStateTickPhase(ledger, stations, containers, mastery);
        var macroActions = new MacroActionTickPhase(
            macros,
            agentLifecycleSystem,
            compiledPack,
            generatedWorld?.Grid,
            cropActionRules,
            compiledPack is null ? null : BlockActionRuleRuntimeCompiler.Compile(compiledPack),
            containers,
            macroCommandIds,
            handlers,
            actionDurations,
            deferredResolutions,
            dispatch);
        var phases = Enum.GetValues<WorldTickPhase>()
            .Select(phase => phase switch
            {
                WorldTickPhase.Receive => (IWorldTickPhaseHandler)dispatch.ReceivePhase,
                WorldTickPhase.Lifecycle => new AgentLifecycleTickPhase(agentLifecycleSystem, ledger),
                WorldTickPhase.Arbitration => actionState,
                WorldTickPhase.StartOrCancelActions => dispatch,
                WorldTickPhase.AdvanceMacroActions => macroActions,
                WorldTickPhase.ApplyWrites => new ApplyWorldWritesTickPhase(),
                WorldTickPhase.ScheduledUpdates => new CropGrowthTickPhase(
                    generatedWorld?.Grid,
                    eventWheel,
                    generatedWorld?.Seed ?? 0,
                    calendar,
                    cropActionRules?.CropRules.Select(static rule => rule.Growth).ToArray() ?? []),
                WorldTickPhase.Perception => new AgentPerceptionTickPhase(agentLifecycleSystem, constructionSystem),
                WorldTickPhase.WearExpiryAndConstruction => new StructureConstructionTickPhase(
                    constructionSystem,
                    containers,
                    ledger,
                    constructionSystem is null ? null : new StructureEffectConsumer(constructionSystem),
                    containerDecayRules),
                WorldTickPhase.Calendar => new CalendarEventTickPhase(calendar),
                _ => new NoOpWorldTickPhase(phase),
            });
        return new OrderedWorldHost(phases);
    }

    private static WorldCalendar CreateCalendar(CompiledContentPack? pack)
    {
        var dayLengthTicks = ReadTimeParameter(pack?.Parameters, "dayLengthTicks", 480);
        var seasonLengthDays = ReadTimeParameter(pack?.Parameters, "seasonLengthDays", 4);
        return new WorldCalendar(dayLengthTicks, seasonLengthDays);
    }

    private static int ReadTimeParameter(JsonElement? parameters, string name, int defaultValue)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty("time", out var time))
        {
            return defaultValue;
        }

        if (time.ValueKind != JsonValueKind.Object ||
            !time.TryGetProperty(name, out var value) ||
            !value.TryGetInt32(out var result) ||
            result <= 0)
        {
            throw new InvalidOperationException($"The compiled time.{name} parameter must be positive.");
        }

        return result;
    }

    private static ContentPackDefinition? ToContentDefinition(CompiledContentPack? pack) =>
        pack is null
            ? null
            : new ContentPackDefinition(
                pack.Manifest,
                pack.Tags,
                pack.Materials,
                pack.Items,
                pack.Recipes,
                pack.Templates,
                pack.GrowthRules,
                pack.Effects,
                pack.Parameters);

    private sealed record PersistedWorldState(
        int FormatVersion,
        GeneratedWorldSnapshot World,
        ContentPackDefinition? ContentDefinition);

    private sealed class NoOpWorldTickPhase(WorldTickPhase phase) : IWorldTickPhaseHandler
    {
        public WorldTickPhase Phase => phase;
        public void Execute(WorldTickContext context) => ArgumentNullException.ThrowIfNull(context);
    }
}

public sealed class HostLifecycleException(string errorCode, string message) : InvalidOperationException(message)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class ReadStreamSubscription : IAsyncDisposable
{
    private readonly ChannelReader<ReadStreamEnvelope> _reader;
    private readonly Action _unsubscribe;

    internal ReadStreamSubscription(ChannelReader<ReadStreamEnvelope> reader, Action unsubscribe)
    {
        _reader = reader;
        _unsubscribe = unsubscribe;
    }

    public IAsyncEnumerable<ReadStreamEnvelope> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _reader.ReadAllAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        _unsubscribe();
        return ValueTask.CompletedTask;
    }
}

internal sealed class ReadStreamHub
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly HashSet<Channel<ReadStreamEnvelope>> _subscribers = [];
    private long _sequence;

    public ReadStreamHub(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    public ReadStreamSubscription Subscribe(JsonElement snapshot)
    {
        var channel = Channel.CreateBounded<ReadStreamEnvelope>(new BoundedChannelOptions(_capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
        lock (_gate)
        {
            _subscribers.Add(channel);
            channel.Writer.TryWrite(CreateEnvelope("world_snapshot", snapshot));
        }

        return new ReadStreamSubscription(channel.Reader, () =>
        {
            lock (_gate)
            {
                _subscribers.Remove(channel);
                channel.Writer.TryComplete();
            }
        });
    }

    public void Publish(string kind, JsonElement payload, Func<JsonElement> snapshotFactory)
    {
        ArgumentNullException.ThrowIfNull(snapshotFactory);
        lock (_gate)
        {
            var envelope = CreateEnvelope(kind, payload);
            ReadStreamEnvelope? snapshotEnvelope = null;
            foreach (var subscriber in _subscribers)
            {
                if (subscriber.Writer.TryWrite(envelope))
                {
                    continue;
                }

                while (subscriber.Reader.TryRead(out _))
                {
                }

                snapshotEnvelope ??= CreateEnvelope("world_snapshot", snapshotFactory());
                subscriber.Writer.TryWrite(snapshotEnvelope);
            }
        }
    }

    private ReadStreamEnvelope CreateEnvelope(string kind, JsonElement payload) =>
        new(
            ProtocolVersion.Current.ToString(),
            _sequence++,
            payload.TryGetProperty("tick", out var tick) && tick.TryGetInt64(out var value)
                ? value
                : payload.TryGetProperty("lastCompletedTick", out var lastTick) && lastTick.TryGetInt64(out value)
                    ? value
                    : -1,
            kind,
            payload.Clone());
}
