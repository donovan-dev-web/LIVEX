using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Prism.Core.Diagnostics;
using Prism.Core.World;

namespace Prism.Core.Perception;

public sealed record PerceptionMaterialRule(
    bool Solid,
    bool Liquid,
    bool Perceivable,
    uint TagMask);

public sealed class PerceptionRules
{
    private readonly IReadOnlyDictionary<ushort, PerceptionMaterialRule> _materials;

    public PerceptionRules(
        IReadOnlyDictionary<ushort, PerceptionMaterialRule> materials,
        ushort airMaterialId)
    {
        ArgumentNullException.ThrowIfNull(materials);
        _materials = new Dictionary<ushort, PerceptionMaterialRule>(materials);
        if (!_materials.ContainsKey(airMaterialId))
        {
            throw new ArgumentException("Perception rules must include the air material.", nameof(materials));
        }

        AirMaterialId = airMaterialId;
    }

    public ushort AirMaterialId { get; }
    public bool ContainsMaterial(ushort materialId) => _materials.ContainsKey(materialId);
    public int MaterialCount => _materials.Count;

    internal KeyValuePair<ushort, PerceptionMaterialRule>[] SnapshotMaterials() =>
        _materials.OrderBy(static entry => entry.Key).ToArray();

    internal PerceptionMaterialRule GetMaterial(ushort materialId) =>
        _materials.TryGetValue(materialId, out var rule)
            ? rule
            : throw new InvalidOperationException($"Perception rules do not define material '{materialId}'.");
}

public sealed record PerceptionSettings(
    int NearRadius = 16,
    int MidRadiusRegions = 3,
    int MaximumObservedPerAgent = 256)
{
    public void Validate()
    {
        if (NearRadius is < 0 or >= 64)
        {
            throw new ArgumentOutOfRangeException(nameof(NearRadius));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(MidRadiusRegions);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumObservedPerAgent);
    }
}

public enum PerceptionEntityKind : byte
{
    Block,
    GroundItem,
    Agent,
    RegionSummary,
    ConstructionSite,
}

public enum PerceptionChangeKind : byte
{
    Appeared,
    Changed,
    Disappeared,
}

public readonly record struct PerceptionEntry(
    PerceptionEntityKind Kind,
    int X,
    int Y,
    int Z,
    ulong Id,
    ushort MaterialId,
    byte State,
    ushort ItemTypeId,
    int Quantity,
    uint TagMask,
    string? SiteId = null,
    int WorkRequired = 0);

public readonly record struct PerceptionChange(
    PerceptionChangeKind Change,
    PerceptionEntry Entry);

public sealed record SelfObservation(
    AgentPosition Position,
    IReadOnlyList<AgentItemStack> Inventory,
    bool Sheltered = false,
    string? ShelterSiteId = null,
    int FatigueRecoveryBonusPermille = 0);

public sealed record PerceptionFrame(
    IReadOnlyList<PerceptionChange> Changes,
    SelfObservation Self);

public sealed class PerceptionSystem
{
    private const int CheckpointVersion = 1;
    private static readonly JsonSerializerOptions CheckpointJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<ulong, AgentView> _views = [];
    private readonly Dictionary<int, CachedRegionMask> _regionMasks = [];

    /// <summary>
    /// Entries one column contributed the last time it was read, keyed by column and checked
    /// against that column's write counter (PRISM-119). A column's contribution depends only on
    /// its own data and its four cardinal neighbours, which is exactly the set a write bumps, so
    /// the counter is an exact key. The cache is shared by every agent of the system: 1 000 agents
    /// standing in the same region read a column once, not 1 000 times.
    /// </summary>
    private readonly Dictionary<long, CachedColumnEntries> _columnEntries = [];

    /// <summary>Working dictionary of one column read, reused so a miss allocates only its array.</summary>
    private readonly Dictionary<ObservationKey, PerceptionEntry> _columnScratch = [];

    /// <summary>Separate scratch for <c>PRISM_VERIFY_CACHES=1</c>, so a check never clobbers a read.</summary>
    private readonly Dictionary<ObservationKey, PerceptionEntry> _columnVerifyScratch = [];

    /// <summary>
    /// Candidats du voisinage, réutilisés d'un agent à l'autre : l'index spatial remplit ces
    /// listes au lieu d'allouer un parcours par agent. <see cref="Observe"/> est appelée en boucle
    /// par le tick séquentiel, donc une seule boucle touche ces listes à la fois (PRISM-141).
    /// </summary>
    private readonly List<AgentBody> _agentScratch = [];
    private readonly List<GroundItem> _itemScratch = [];

    /// <summary>
    /// Tests de rayon exacts effectués depuis le dernier <see cref="BeginTick"/>, agents et objets
    /// séparément. Diagnostics seuls : aucun n'est lu par la simulation, donc aucun ne peut
    /// déplacer une trajectoire. C'est ce qui rend « le balayage complet examine N agents par
    /// agent, l'index quelques seaux » mesurable au lieu d'être supposé (PRISM-141).
    /// </summary>
    public long AgentCandidatesExamined { get; private set; }
    public long ItemCandidatesExamined { get; private set; }
    private readonly bool _verifyCaches;
    private readonly bool _slideWindow;
    private long _diagnosticTick = -1;

    // TEMPORARY PRISM-141 probes; removed once the allocation split is known.
    public static readonly bool AllocProbe =
        Environment.GetEnvironmentVariable("PRISM_ALLOC_PROBE") == "1";
    public static long ProbeWindowBytes;
    public static long ProbeGroundBytes;
    public static long ProbeAgentsBytes;
    public static long ProbeSitesBytes;
    public static long ProbeSummariesBytes;
    public static long ProbeCollectBytes;
    public static long ProbeTailBytes;
    public static long ProbeCollectChangesBytes;
    public static long ProbeDisappearBytes;
    public static long ProbeSortBytes;
    public static long ProbeCopyBytes;
    public static long ProbeUpdateBytes;
    public static long ProbePendingCount;
    public static long ProbeEntriesCount;
    public static long ProbeBlocksCount;
    public static long ProbeExtrasCount;
    public static long ProbeSelectedCount;
    public static long ProbeCompareCount;

    // Same probe, in timestamp ticks: the allocation split says what is allocated, not what costs
    // time. These four buckets are the whole of Observe (window, extras, diff, tail).
    public static long ProbeWindowTicks;
    public static long ProbeExtrasTicks;
    public static long ProbeDiffTicks;
    public static long ProbeTailTicks;
    public static long ProbeCollectTicks;
    public static long ProbeSortTicks;
    public static long ProbeCopyTicks;
    public static long ProbeUpdateTicks;
    public static long ProbeSetupTicks;
    public static long ProbeShelterTicks;
    public static long ProbeObserveCallTicks;
    public static long ProbeSnapshotTicks;

    /// <param name="verifyCaches">
    /// <c>PRISM_VERIFY_CACHES=1</c> (PRISM-108): on a block-window hit, recompute the window without
    /// the cache and compare; on a region-mask hit, recompute the mask and compare. The first
    /// divergence throws a <see cref="CacheVerificationException"/>. Off by default, and free when
    /// off: nothing is recomputed and no state differs.
    /// </param>
    /// <param name="slideWindow">
    /// <c>true</c> (default): a one-cell step whose covered regions were not written slides the
    /// cached block window instead of rescanning it (PRISM-105). <c>false</c> forces a full
    /// rebuild on every step, which is the reference a test compares the slide against — the
    /// two must emit the same changes byte for byte.
    /// </param>
    public PerceptionSystem(bool verifyCaches = false, bool slideWindow = true)
    {
        _verifyCaches = verifyCaches;
        _slideWindow = slideWindow;
    }

    public IReadOnlyCollection<ulong> KnownAgents => _views.Keys.ToArray();

    /// <summary>Remet les compteurs de candidats à zéro (début d'un tick, PRISM-141).</summary>
    internal void BeginTick()
    {
        AgentCandidatesExamined = 0;
        ItemCandidatesExamined = 0;
    }

    /// <summary>
    /// Number of block-window rebuilds since the system was created. Diagnostics only: it is
    /// incremented when a window is actually recomputed and is never read by the simulation, so it
    /// cannot move a trajectory. It is what makes "a write elsewhere does not invalidate this
    /// agent" observable, in tests and in a profile (PRISM-109).
    /// </summary>
    public long BlockWindowBuilds { get; private set; }

    /// <summary>
    /// Number of region tag-mask recomputations since the system was created. Diagnostics only,
    /// same contract as <see cref="BlockWindowBuilds"/> (PRISM-109).
    /// </summary>
    public long RegionMaskRebuilds { get; private set; }

    /// <summary>
    /// Number of block windows that were slid by one cell instead of rebuilt (PRISM-105).
    /// Diagnostics only, same contract as <see cref="BlockWindowBuilds"/>.
    /// </summary>
    public long BlockWindowSlides { get; private set; }

    /// <summary>
    /// Number of steps that could not be slid and fell back to a full rebuild — a write in a
    /// covered region, a radius change, or a jump wider than one cell. Diagnostics only: this is
    /// what makes "the fallback is rare" measurable instead of assumed (PRISM-105).
    /// </summary>
    public long BlockWindowMoveRebuilds { get; private set; }

    /// <summary>
    /// Number of block windows re-read column by column after a write landed in a region they
    /// cover, instead of being rebuilt whole (PRISM-119). Diagnostics only, same contract as
    /// <see cref="BlockWindowBuilds"/>.
    /// </summary>
    public long BlockWindowPatches { get; private set; }

    public PerceptionFrame Observe(
        AgentBody observer,
        WorldGrid world,
        AgentRegistry agents,
        GroundItemStore groundItems,
        PerceptionRules rules,
        PerceptionSettings settings,
        IReadOnlyList<StructureConstructionSite>? constructionSites = null,
        StructureUsage? shelter = null,
        long tick = -1,
        PerceptionSpatialIndex? spatialIndex = null)
    {
        _diagnosticTick = tick;
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(groundItems);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        // Le monde ne change qu'en quelques cases par tick : la fenêtre de blocs
        // d'un agent est donc mémorisée tant que les régions qu'elle couvre et sa
        // propre case n'ont pas bougé. Sans ce cache, chaque agent relisait ses
        // 1 089 cases à chaque tick pour ne découvrir presque toujours rien
        // (5 à 6 ms par agent). La clé est la révision des régions lues — jamais la
        // révision globale : un minage à l'autre bout du monde ne doit pas périmer
        // le cache des 1 000 agents (PRISM-109).
        AgentView view = _views.GetOrAdd(observer.AgentId, static _ => new AgentView());
        int radius = settings.NearRadius;
        long probeMark = AllocProbe ? GC.GetAllocatedBytesForCurrentThread() : 0;
        long stamp = AllocProbe ? Stopwatch.GetTimestamp() : 0;
        int cellX = observer.Position.CellX;
        int cellY = observer.Position.CellY;
        bool sameCell = view.BlocksRadius == radius &&
                        view.BlocksCellX == cellX &&
                        view.BlocksCellY == cellY;
        // The stored coverage is the last range captured, which always belongs to the last
        // position this view observed. When nothing that range covers was written, the cached
        // content is still the world's content — that is the whole invalidation rule.
        bool coverageUnchanged = view.BlocksRegionCount > 0 && StoredCoverageUnchanged(world, view);
        bool blocksUnchanged = sameCell && coverageUnchanged && view.BlocksInSync;

        if (sameCell && coverageUnchanged)
        {
            if (_verifyCaches)
            {
                // A freshly built window is its own ground truth: only the reuse of a cached one
                // can lie, so only the reuse path pays the recomputation (PRISM-108).
                VerifyBlockWindow(observer.AgentId, observer.Position, world, rules, radius, view.Blocks);
            }
        }
        else if (_slideWindow && coverageUnchanged && CanSlideWindow(view, radius, cellX, cellY))
        {
            // A one-cell step renames one edge column per row: the rest of the disc is still the
            // world's content, and the entering band is read from the world. 33 to 66 cells are
            // read instead of 1 089, and the emitted changes are the same (PRISM-105).
            SlideBlockWindow(view, observer.Position, world, rules, radius);
            view.BlocksCellX = cellX;
            view.BlocksCellY = cellY;
            CaptureWindowCoverage(world, observer.Position, radius, view);
            CaptureColumnCoverage(world, observer.Position, radius, view);
            BlockWindowSlides++;
            if (_verifyCaches)
            {
                VerifyBlockWindow(observer.AgentId, observer.Position, world, rules, radius, view.Blocks);
            }
        }
        else if (_slideWindow && TryPatchBlockWindow(view, observer.Position, world, rules, radius))
        {
            // A write landed in a region the window covers, but the window only depends on the
            // written column and its four neighbours: those columns are re-read and the rest of
            // the disc is kept. Before PRISM-119 any such write rebuilt all 1 089 columns, which
            // is what kept perception at 425 ms instead of under 250 ms at 1 000 agents.
            BlockWindowPatches++;
            if (_verifyCaches)
            {
                VerifyBlockWindow(observer.AgentId, observer.Position, world, rules, radius, view.Blocks);
            }
        }
        else
        {
            if (view.BlocksRegionCount > 0 && view.BlocksRadius == radius && !sameCell)
            {
                BlockWindowMoveRebuilds++;
            }

            view.Blocks.Clear();
            AddNearbyBlocks(view.Blocks, observer.Position, world, rules, radius);
            view.BlocksCellX = cellX;
            view.BlocksCellY = cellY;
            view.BlocksRadius = radius;
            CaptureWindowCoverage(world, observer.Position, radius, view);
            CaptureColumnCoverage(world, observer.Position, radius, view);
            BlockWindowBuilds++;
        }

        if (AllocProbe)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            ProbeWindowBytes += now - probeMark;
            probeMark = now;
            var section = Stopwatch.GetTimestamp();
            ProbeWindowTicks += section - stamp;
            stamp = section;
        }

        // Le reste (objets au sol, agents, sites, résumés de région) change
        // souvent et reste peu nombreux : il est relu à chaque tick, dans un
        // dictionnaire réutilisé.
        view.Extras.Clear();
        AddNearbyGroundItems(view.Extras, observer.Position, groundItems, radius, spatialIndex, _itemScratch);
        if (AllocProbe)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            ProbeGroundBytes += now - probeMark;
            probeMark = now;
        }
        AddNearbyAgents(view.Extras, observer, agents, radius, spatialIndex, _agentScratch);
        if (AllocProbe)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            ProbeAgentsBytes += now - probeMark;
            probeMark = now;
        }
        AddNearbyConstructionSites(view.Extras, observer.Position, constructionSites, radius);
        if (AllocProbe)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            ProbeSitesBytes += now - probeMark;
            probeMark = now;
        }
        AddRegionSummaries(view.Extras, observer.Position, world, rules, settings.MidRadiusRegions);
        if (AllocProbe)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            ProbeSummariesBytes += now - probeMark;
            probeMark = now;
            var section = Stopwatch.GetTimestamp();
            ProbeExtrasTicks += section - stamp;
            stamp = section;
        }

        List<(ObservationKey Key, PerceptionChange Change)> pendingChanges = view.Pending;
        pendingChanges.Clear();
        if (view.Entries.Count == 0)
        {
            // Première observation : dimensionne une fois les dictionnaires et la liste de
            // travail à la taille qu'ils atteindront (fenêtre + extras). Sans cela, la
            // croissance par doublement des tables domine le tick de convergence (PRISM-141).
            view.Entries.EnsureCapacity(view.Blocks.Count + view.Extras.Count);
            if (pendingChanges.Capacity < view.Blocks.Count + view.Extras.Count)
            {
                pendingChanges.EnsureCapacity(view.Blocks.Count + view.Extras.Count);
            }
        }

        // Fenêtre réutilisée telle quelle et agent immobile : son contenu est celui du
        // tick précédent, donc le diff de blocs est vide par construction. Parcourir
        // quand même les ~870 entrées de la fenêtre et les ~900 entrées connues coûtait
        // deux millions de recherches de dictionnaire par tick à 1 000 agents.
        if (!blocksUnchanged)
        {
            CollectChanges(view.Blocks, view.Entries, pendingChanges);
        }

        CollectChanges(view.Extras, view.Entries, pendingChanges);
        if (AllocProbe)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            ProbeCollectChangesBytes += now - probeMark;
            probeMark = now;
            ProbeBlocksCount += view.Blocks.Count;
            ProbeExtrasCount += view.Extras.Count;
            ProbeEntriesCount += view.Entries.Count;
            ProbePendingCount += pendingChanges.Count;
            var collectSection = Stopwatch.GetTimestamp();
            ProbeCollectTicks += collectSection - stamp;
            stamp = collectSection;
        }
        foreach (var (key, entry) in view.Entries)
        {
            // Même raison : si la fenêtre n'a pas bougé, aucun bloc connu ne peut
            // avoir disparu du champ de vision.
            if (blocksUnchanged && entry.Kind == PerceptionEntityKind.Block)
            {
                continue;
            }

            if (!view.Blocks.ContainsKey(key) && !view.Extras.ContainsKey(key))
            {
                pendingChanges.Add((key, new PerceptionChange(PerceptionChangeKind.Disappeared, entry)));
            }
        }
        if (AllocProbe)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            ProbeDisappearBytes += now - probeMark;
            probeMark = now;
            var section = Stopwatch.GetTimestamp();
            ProbeDiffTicks += section - stamp;
            stamp = section;
        }

        // Même ordre qu'avant : les disparitions d'abord, puis la priorité du
        // genre, puis la clé — l'ordre total rend le tri stable identique.
        pendingChanges.Sort(PendingOrder);
        int selected = Math.Min(settings.MaximumObservedPerAgent, pendingChanges.Count);
        if (AllocProbe)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            ProbeSortBytes += now - probeMark;
            probeMark = now;
            ProbeSelectedCount += selected;
            var sortSection = Stopwatch.GetTimestamp();
            ProbeSortTicks += sortSection - stamp;
            stamp = sortSection;
        }
        var changes = new PerceptionChange[selected];
        for (var index = 0; index < selected; index++)
        {
            changes[index] = pendingChanges[index].Change;
        }

        Array.Sort(changes, CompareChanges);
        if (AllocProbe)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            ProbeCopyBytes += now - probeMark;
            probeMark = now;
            var copySection = Stopwatch.GetTimestamp();
            ProbeCopyTicks += copySection - stamp;
            stamp = copySection;
        }

        // L'état connu est mis à jour en place : une entrée présente mais non
        // retenue (au-delà du plafond) garde son ancienne valeur, comme lorsque
        // le dictionnaire était reconstruit à chaque tick.
        for (var index = 0; index < selected; index++)
        {
            var (key, change) = pendingChanges[index];
            if (change.Change == PerceptionChangeKind.Disappeared)
            {
                view.Entries.Remove(key);
            }
            else
            {
                view.Entries[key] = change.Entry;
            }
        }

        // Le diff de blocs n'est sautable au tick suivant que si rien n'a été tronqué
        // maintenant : une entrée tombée au-delà du plafond doit pouvoir être réémise.
        view.BlocksInSync = pendingChanges.Count <= selected;

        if (AllocProbe)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            ProbeUpdateBytes += now - probeMark;
            probeMark = now;
            ProbeCollectBytes += 0;
            var updateSection = Stopwatch.GetTimestamp();
            ProbeUpdateTicks += updateSection - stamp;
            stamp = updateSection;
        }

        var frame = new PerceptionFrame(
            Array.AsReadOnly(changes),
            new SelfObservation(
                observer.Position,
                Array.AsReadOnly(observer.Inventory.Snapshot().ToArray()),
                shelter is not null,
                shelter?.SiteId,
                shelter?.FatigueRecoveryBonusPermille ?? 0));
        if (AllocProbe)
        {
            ProbeTailBytes += GC.GetAllocatedBytesForCurrentThread() - probeMark;
            ProbeTailTicks += Stopwatch.GetTimestamp() - stamp;
        }

        return frame;
    }

    public bool ForgetAgent(ulong agentId) => _views.TryRemove(agentId, out _);

    public bool HasPerceivedBlock(ulong agentId, BlockCoordinate position) =>
        _views.TryGetValue(agentId, out var view) &&
        view.Entries.Values.Any(entry =>
            entry.Kind == PerceptionEntityKind.Block &&
            entry.X == position.X &&
            entry.Y == position.Y &&
            entry.Z == position.Z);

    public bool HasPerceivedAgent(ulong observerAgentId, ulong targetAgentId) =>
        _views.TryGetValue(observerAgentId, out var view) &&
        view.Entries.Values.Any(entry =>
            entry.Kind == PerceptionEntityKind.Agent &&
            entry.Id == targetAgentId);

    /// <summary>
    /// Entrées connues d'un agent, dans l'ordre canonique des clés. L'ordre est
    /// rendu explicitement : le stockage en table de hachage ne l'impose pas, et
    /// un appelant qui s'appuie dessus verrait une sortie instable.
    /// </summary>
    public IReadOnlyList<PerceptionEntry> GetPerceivedEntries(ulong agentId) =>
        _views.TryGetValue(agentId, out var view)
            ? view.Entries.OrderBy(static entry => entry.Key).Select(static entry => entry.Value).ToArray()
            : [];

    public byte[] CaptureState()
    {
        var checkpoint = new PerceptionCheckpoint(
            CheckpointVersion,
            _views
                .OrderBy(static entry => entry.Key)
                .Select(static entry => new AgentPerceptionCheckpoint(
                    entry.Key,
                    entry.Value.Entries
                        .OrderBy(static entry => entry.Key)
                        .Select(static entry => entry.Value)
                        .ToArray()))
                .ToArray());
        return JsonSerializer.SerializeToUtf8Bytes(checkpoint, CheckpointJsonOptions);
    }

    public void RestoreState(ReadOnlySpan<byte> checkpointBytes)
    {
        if (!_views.IsEmpty)
        {
            throw new InvalidOperationException("Perception state can only be restored into a new system.");
        }

        _regionMasks.Clear();
        var checkpoint = JsonSerializer.Deserialize<PerceptionCheckpoint>(
                             checkpointBytes,
                             CheckpointJsonOptions)
                         ?? throw new InvalidDataException("Perception checkpoint is empty.");
        if (checkpoint.Version != CheckpointVersion || checkpoint.Agents is null)
        {
            throw new InvalidDataException("Perception checkpoint version or contents are invalid.");
        }

        var restored = new Dictionary<ulong, AgentView>();
        foreach (var agent in checkpoint.Agents)
        {
            if (agent is null || agent.Entries is null || restored.ContainsKey(agent.AgentId))
            {
                throw new InvalidDataException("Perception checkpoint contains an invalid or duplicate agent.");
            }

            var view = new AgentView();
            foreach (var entry in agent.Entries)
            {
                if (!Enum.IsDefined(entry.Kind))
                {
                    throw new InvalidDataException("Perception checkpoint contains an invalid entity kind.");
                }

                var key = new ObservationKey(entry.Kind, entry.X, entry.Y, entry.Z, entry.Id);
                if (!view.Entries.TryAdd(key, entry))
                {
                    throw new InvalidDataException("Perception checkpoint contains duplicate observations.");
                }
            }

            restored.Add(agent.AgentId, view);
        }

        foreach (var entry in restored)
        {
            if (!_views.TryAdd(entry.Key, entry.Value))
            {
                throw new InvalidOperationException("Perception checkpoint restoration conflicted with existing state.");
            }
        }
    }

    /// <summary>
    /// Ordre de sélection des changements d'un tick : les disparitions passent
    /// avant le reste (un agent doit apprendre qu'une ressource a cessé d'être
    /// là), puis le genre décide de la priorité, puis la clé — un ordre total,
    /// donc indépendant de l'ordre d'itération des dictionnaires.
    /// </summary>
    private static int PendingOrder(
        (ObservationKey Key, PerceptionChange Change) left,
        (ObservationKey Key, PerceptionChange Change) right)
    {
        if (AllocProbe)
        {
            ProbeCompareCount++;
        }

        var comparison = (left.Change.Change == PerceptionChangeKind.Disappeared ? 0 : 1)
            .CompareTo(right.Change.Change == PerceptionChangeKind.Disappeared ? 0 : 1);
        if (comparison != 0) return comparison;
        comparison = GetPriority(left.Change.Entry.Kind).CompareTo(GetPriority(right.Change.Entry.Kind));
        return comparison != 0 ? comparison : left.Key.CompareTo(right.Key);
    }

    private static void CollectChanges(
        Dictionary<ObservationKey, PerceptionEntry> candidates,
        Dictionary<ObservationKey, PerceptionEntry> known,
        List<(ObservationKey Key, PerceptionChange Change)> pending)
    {
        foreach (var (key, entry) in candidates)
        {
            if (!known.TryGetValue(key, out var oldEntry))
            {
                pending.Add((key, new PerceptionChange(PerceptionChangeKind.Appeared, entry)));
            }
            else if (oldEntry != entry)
            {
                pending.Add((key, new PerceptionChange(PerceptionChangeKind.Changed, entry)));
            }
        }
    }

    private static int CompareChanges(PerceptionChange left, PerceptionChange right)
    {
        var comparison = GetPriority(left.Entry.Kind).CompareTo(GetPriority(right.Entry.Kind));
        if (comparison != 0) return comparison;
        comparison = left.Entry.Z.CompareTo(right.Entry.Z);
        if (comparison != 0) return comparison;
        comparison = left.Entry.Y.CompareTo(right.Entry.Y);
        if (comparison != 0) return comparison;
        comparison = left.Entry.X.CompareTo(right.Entry.X);
        if (comparison != 0) return comparison;
        comparison = left.Entry.Id.CompareTo(right.Entry.Id);
        return comparison != 0 ? comparison : ((byte)left.Change).CompareTo((byte)right.Change);
    }

    private static int GetPriority(PerceptionEntityKind kind) => kind switch
    {
        PerceptionEntityKind.Agent => 0,
        PerceptionEntityKind.ConstructionSite => 1,
        PerceptionEntityKind.GroundItem => 2,
        PerceptionEntityKind.Block => 3,
        PerceptionEntityKind.RegionSummary => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// Region rectangle every read of <see cref="AddNearbyBlocks"/> can touch: the cells within
    /// the radius, plus the one-cell rim the exposure test reads — a neighbour's floor decides
    /// whether a block at the edge of the window is visible, and an override in that rim can hide
    /// it. Writes outside this rectangle cannot change the window; writes inside it must rebuild
    /// it, even when they cancel out, because a revision is a write counter and not a content
    /// hash (a rebuild is the safe side of that trade). Region coordinates, not cells: the
    /// window's dependency is stated in the same unit as the invalidation key.
    /// </summary>
    private static (int MinX, int MinY, int MaxX, int MaxY) WindowRegionRange(
        WorldGrid world,
        AgentPosition position,
        int radius)
    {
        var reach = (long)radius + 1;
        var minX = (int)(Math.Max(0L, (long)position.CellX - reach) / world.RegionSize);
        var maxX = (int)Math.Min(
            world.RegionCountX - 1L,
            ((long)position.CellX + reach) / world.RegionSize);
        var minY = (int)(Math.Max(0L, (long)position.CellY - reach) / world.RegionSize);
        var maxY = (int)Math.Min(
            world.RegionCountY - 1L,
            ((long)position.CellY + reach) / world.RegionSize);
        return (minX, minY, maxX, maxY);
    }

    /// <summary>
    /// True when every region the cached window was built from still carries the revision it was
    /// read at. This is the whole invalidation rule: the agent's cell and radius are compared
    /// separately by the caller, and nothing else can change what the window contains.
    /// </summary>
    private static bool StoredCoverageUnchanged(WorldGrid world, AgentView view)
    {
        if (view.BlocksRegionCount == 0)
        {
            return false;
        }

        var width = view.BlocksRegionMaxX - view.BlocksRegionMinX + 1;
        var index = 0;
        for (var regionY = view.BlocksRegionMinY; regionY <= view.BlocksRegionMaxY; regionY++)
        {
            for (var regionX = view.BlocksRegionMinX; regionX <= view.BlocksRegionMaxX; regionX++)
            {
                if (index >= view.BlocksRegionCount ||
                    view.BlocksRegionRevisions[index] !=
                    world.GetRegionRevision(regionX + world.RegionCountX * regionY))
                {
                    return false;
                }

                index++;
            }
        }

        return index == view.BlocksRegionCount &&
               index == width * (view.BlocksRegionMaxY - view.BlocksRegionMinY + 1);
    }

    /// <summary>
    /// A window can slide when the previous one exists, the radius did not change and the step is
    /// at most one cell on each axis — a diagonal step is two one-cell slides at once, and the
    /// band it enters is read the same way. Anything else (a jump, a radius change) falls back to
    /// a full rebuild, which is the safe side of the trade.
    /// </summary>
    private static bool CanSlideWindow(AgentView view, int radius, int cellX, int cellY)
    {
        if (view.BlocksRadius != radius || view.BlocksRegionCount == 0)
        {
            return false;
        }

        var deltaX = cellX - view.BlocksCellX;
        var deltaY = cellY - view.BlocksCellY;
        return Math.Abs(deltaX) <= 1 && Math.Abs(deltaY) <= 1 && (deltaX != 0 || deltaY != 0);
    }

    /// <summary>Stores the revisions a freshly built window was derived from.</summary>
    private static void CaptureWindowCoverage(
        WorldGrid world,
        AgentPosition position,
        int radius,
        AgentView view)
    {
        var (minX, minY, maxX, maxY) = WindowRegionRange(world, position, radius);
        var count = checked((maxX - minX + 1) * (maxY - minY + 1));
        if (view.BlocksRegionRevisions.Length < count)
        {
            view.BlocksRegionRevisions = new long[count];
        }

        view.BlocksRegionMinX = minX;
        view.BlocksRegionMinY = minY;
        view.BlocksRegionMaxX = maxX;
        view.BlocksRegionMaxY = maxY;

        var index = 0;
        for (var regionY = minY; regionY <= maxY; regionY++)
        {
            for (var regionX = minX; regionX <= maxX; regionX++)
            {
                view.BlocksRegionRevisions[index++] =
                    world.GetRegionRevision(regionX + world.RegionCountX * regionY);
            }
        }

        view.BlocksRegionCount = count;
    }

    private void AddNearbyBlocks(
        Dictionary<ObservationKey, PerceptionEntry> candidates,
        AgentPosition position,
        WorldGrid world,
        PerceptionRules rules,
        int radius)
    {
        var centerX = position.CellX;
        var centerY = position.CellY;
        var radiusSquared = (long)radius * radius;
        var minX = (int)Math.Max(0L, (long)centerX - radius);
        var maxX = (int)Math.Min(world.Width - 1L, (long)centerX + radius);
        var minY = (int)Math.Max(0L, (long)centerY - radius);
        var maxY = (int)Math.Min(world.Height - 1L, (long)centerY + radius);

        // One column at a time, through the same code path a sliding window uses: a full rebuild
        // and a slide must agree cell for cell, otherwise the optimisation would be a second
        // definition of what an agent sees.
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!WithinRadius(centerX, centerY, x, y, radiusSquared))
                {
                    continue;
                }

                AddColumnBlocks(candidates, x, y, world, rules);
            }
        }
    }

    /// <summary>
    /// Adds one column's contribution to the window, from the shared column cache when the column
    /// was not written since it was last read (PRISM-119). This is the hot path of a sliding
    /// window: a band of 33 to 66 columns per agent and per tick, a thousand agents, every tick.
    /// </summary>
    private void AddColumnBlocks(
        Dictionary<ObservationKey, PerceptionEntry> candidates,
        int x,
        int y,
        WorldGrid world,
        PerceptionRules rules)
    {
        var key = ColumnCacheKey(x, y);
        var revision = world.GetColumnRevision(x, y);
        if (_columnEntries.TryGetValue(key, out var cached) &&
            cached.Revision == revision &&
            ReferenceEquals(cached.World, world) &&
            ReferenceEquals(cached.Rules, rules))
        {
            if (_verifyCaches)
            {
                VerifyColumnEntries(x, y, world, rules, cached.Entries);
            }

            AddCachedColumn(candidates, cached.Entries);
            return;
        }

        _columnScratch.Clear();
        CollectColumnBlocks(_columnScratch, x, y, world, rules);
        var entries = new PerceptionEntry[_columnScratch.Count];
        var index = 0;
        foreach (var entry in _columnScratch.Values)
        {
            entries[index++] = entry;
        }

        _columnEntries[key] = new CachedColumnEntries(revision, world, rules, entries);
        AddCachedColumn(candidates, entries);
    }

    private static void AddCachedColumn(
        Dictionary<ObservationKey, PerceptionEntry> candidates,
        PerceptionEntry[] entries)
    {
        foreach (var entry in entries)
        {
            candidates[new ObservationKey(entry.Kind, entry.X, entry.Y, entry.Z, entry.Id)] = entry;
        }
    }

    private static long ColumnCacheKey(int x, int y) => ((long)x << 32) | (uint)y;

    /// <summary>
    /// Recomputes one column without the cache and fails on the first difference. Only called on a
    /// cache hit, and only under <c>PRISM_VERIFY_CACHES=1</c>: the column cache is the one place
    /// where a missing write-counter bump would silently keep an agent from seeing a change.
    /// </summary>
    private void VerifyColumnEntries(
        int x,
        int y,
        WorldGrid world,
        PerceptionRules rules,
        PerceptionEntry[] cached)
    {
        _columnVerifyScratch.Clear();
        CollectColumnBlocks(_columnVerifyScratch, x, y, world, rules);

        var location = $"column ({x},{y})";
        if (_columnVerifyScratch.Count != cached.Length)
        {
            throw new CacheVerificationException(
                "perception.column",
                _diagnosticTick,
                null,
                location,
                $"cache holds {cached.Length} entries, a fresh read holds {_columnVerifyScratch.Count}");
        }

        foreach (var entry in cached)
        {
            var key = new ObservationKey(entry.Kind, entry.X, entry.Y, entry.Z, entry.Id);
            if (!_columnVerifyScratch.TryGetValue(key, out var fresh) || !fresh.Equals(entry))
            {
                throw new CacheVerificationException(
                    "perception.column",
                    _diagnosticTick,
                    null,
                    location,
                    "the cached entry differs from the world's");
            }
        }
    }

    /// <summary>
    /// Entries one column contributes: its surface block (with the override written on that exact
    /// cell) and every override of the column that reaches the air. Reading a column through the
    /// per-column index is what lets the window be slid one band at a time instead of rescanned
    /// (PRISM-105).
    /// </summary>
    private static void CollectColumnBlocks(
        Dictionary<ObservationKey, PerceptionEntry> candidates,
        int x,
        int y,
        WorldGrid world,
        PerceptionRules rules)
    {
        var surface = world.GetSurfaceColumn(x, y);
        var coordinate = new BlockCoordinate(x, y, surface.SurfaceZ);
        var materialId = surface.MaterialId;
        var state = surface.State;
        if (world.TryGetBlockOverride(coordinate, out var surfaceOverride))
        {
            if (!surfaceOverride.IsAir)
            {
                materialId = surfaceOverride.MaterialId;
                state = surfaceOverride.State;
                AddBlockIfVisible(candidates, coordinate, materialId, state, world, rules);
            }
        }
        else
        {
            AddBlockIfVisible(candidates, coordinate, materialId, state, world, rules);
        }

        // Not only the blocks above the surface: an ore vein that reaches a cliff face is as
        // visible as a tree, and skipping everything below the terrain surface made every deposit
        // of the world invisible (seed 1: 229 500 iron blocks, 3 979 of them open to the air, not
        // one perceivable), so no bot could ever mine the iron an iron pickaxe needs.
        // `AddBlockIfVisible` still drops what has no open side, which keeps buried rock out.
        var overrideCount = world.GetColumnOverrideCount(x, y);
        for (var index = 0; index < overrideCount; index++)
        {
            var overridden = world.GetColumnOverrideAt(x, y, index);
            if (!world.TryGetBlockOverride(overridden, out var block) ||
                block.IsAir ||
                !IsPossiblyExposed(world, overridden))
            {
                continue;
            }

            AddBlockIfVisible(candidates, overridden, block.MaterialId, block.State, world, rules);
        }
    }

    /// <summary>
    /// Largest horizontal half-width a disc of <paramref name="radius"/> still covers at a row
    /// <paramref name="delta"/> cells away from its centre, or <c>-1</c> when the row is outside
    /// the disc. Integer arithmetic only: an agent's window is part of the simulated state and
    /// `Prism.Core` forbids floating point.
    /// </summary>
    private static int HalfWidth(int radius, int delta)
    {
        if (delta > radius)
        {
            return -1;
        }

        var remaining = (long)radius * radius - (long)delta * delta;
        var low = 0;
        var high = radius;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if ((long)middle * middle <= remaining)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low;
    }

    /// <summary>
    /// Slides the cached block window by the one-cell step the agent just took (PRISM-105).
    /// <para>
    /// Cells that leave the disc are dropped; the entering column of each row is read from the
    /// world. The retained cells keep their content because the caller checked that no region the
    /// window was built from was written, and every entering cell lies outside the previous disc,
    /// so it cannot already be in the window. The result is the same set the full rebuild would
    /// produce — <c>PerceptionSlidingWindowTests</c> compares both byte for byte.
    /// </para>
    /// </summary>
    private void SlideBlockWindow(
        AgentView view,
        AgentPosition position,
        WorldGrid world,
        PerceptionRules rules,
        int radius)
    {
        var centerX = position.CellX;
        var centerY = position.CellY;
        var radiusSquared = (long)radius * radius;

        view.Removals.Clear();
        foreach (var key in view.Blocks.Keys)
        {
            if (!WithinRadius(centerX, centerY, key.X, key.Y, radiusSquared))
            {
                view.Removals.Add(key);
            }
        }

        foreach (var key in view.Removals)
        {
            view.Blocks.Remove(key);
        }

        var stepX = centerX - view.BlocksCellX;
        var stepY = centerY - view.BlocksCellY;
        var oldCenterX = centerX - stepX;
        var oldCenterY = centerY - stepY;

        // For every row, only the columns the new disc adds can be missing from the cache: the
        // new span and the old span share their middle, and each side differs by at most the
        // step, i.e. one column (or the whole span when the row itself just entered the disc).
        var firstRow = Math.Max(0, centerY - radius);
        var lastRow = Math.Min(world.Height - 1, centerY + radius);
        for (var y = firstRow; y <= lastRow; y++)
        {
            var newHalfWidth = HalfWidth(radius, Math.Abs(y - centerY));
            if (newHalfWidth < 0)
            {
                continue;
            }

            var oldHalfWidth = HalfWidth(radius, Math.Abs(y - oldCenterY));
            if (oldHalfWidth < 0)
            {
                AddSpan(view.Blocks, centerX - newHalfWidth, centerX + newHalfWidth, y, world, rules);
                continue;
            }

            var newLeft = centerX - newHalfWidth;
            var newRight = centerX + newHalfWidth;
            var oldLeft = oldCenterX - oldHalfWidth;
            var oldRight = oldCenterX + oldHalfWidth;

            // Both sides of the span can grow by more than the step on a vertical or diagonal
            // move: a row's half-width depends on its distance to the centre, which itself moves.
            // Reading the exact difference keeps the work proportional to the band actually
            // entered (two crescents, ~2 × diameter cells) instead of the whole disc.
            if (newLeft < oldLeft)
            {
                AddSpan(view.Blocks, newLeft, Math.Min(oldLeft - 1, newRight), y, world, rules);
            }

            if (newRight > oldRight)
            {
                AddSpan(view.Blocks, Math.Max(oldRight + 1, newLeft), newRight, y, world, rules);
            }
        }
    }

    /// <summary>Adds a clipped run of columns of one row; used when a whole row just entered.</summary>
    private void AddSpan(
        Dictionary<ObservationKey, PerceptionEntry> candidates,
        int firstX,
        int lastX,
        int y,
        WorldGrid world,
        PerceptionRules rules)
    {
        var from = Math.Max(firstX, 0);
        var to = Math.Min(lastX, world.Width - 1);
        for (var x = from; x <= to; x++)
        {
            AddColumnBlocks(candidates, x, y, world, rules);
        }
    }

    /// <summary>
    /// Re-reads only the columns whose content changed since the window was captured (PRISM-119).
    /// <para>
    /// A column's contribution — its surface block and its exposed overrides — is a function of
    /// that column and its four cardinal neighbours, because the exposure test asks those
    /// neighbours whether they are open. <see cref="WorldGrid"/> therefore bumps a write counter
    /// for the written column and those four neighbours, and comparing a window's stored
    /// counters with the grid's says exactly which columns must be read again. Before this, a
    /// single write anywhere in the nine regions a window covers rebuilt all 1 089 columns.
    /// </para>
    /// <para>
    /// A move enters new columns: they have no stored counter, so they are read, and the cells
    /// that left the disc are dropped in the same pass. When enough columns changed that patching
    /// would cost more than a rebuild, the method declines and the caller rebuilds — the choice is
    /// about cost, never about the result.
    /// </para>
    /// </summary>
    private bool TryPatchBlockWindow(
        AgentView view,
        AgentPosition position,
        WorldGrid world,
        PerceptionRules rules,
        int radius)
    {
        if (view.BlocksRegionCount == 0 || view.BlocksRadius != radius)
        {
            return false;
        }

        var centerX = position.CellX;
        var centerY = position.CellY;
        var sameCell = view.BlocksCellX == centerX && view.BlocksCellY == centerY;
        if (!sameCell && !CanSlideWindow(view, radius, centerX, centerY))
        {
            return false;
        }

        var size = radius * 2 + 1;
        if (view.ColumnRevisions.Length < size * size)
        {
            return false;
        }

        var dirty = view.DirtyColumns;
        dirty.Clear();
        var oldBaseX = view.BlocksCellX - radius;
        var oldBaseY = view.BlocksCellY - radius;
        var firstRow = Math.Max(0, centerY - radius);
        var lastRow = Math.Min(world.Height - 1, centerY + radius);
        for (var y = firstRow; y <= lastRow; y++)
        {
            var halfWidth = HalfWidth(radius, Math.Abs(y - centerY));
            if (halfWidth < 0)
            {
                continue;
            }

            var from = Math.Max(0, centerX - halfWidth);
            var to = Math.Min(world.Width - 1, centerX + halfWidth);
            if (from > to)
            {
                continue;
            }

            var storedRowOffset = (y - oldBaseY) * size;
            if (sameCell)
            {
                // A window that did not move has one stored counter per column of the row, in the
                // same order as the grid: a bulk compare skips an untouched row without a single
                // per-cell call, and the BCL vectorises it.
                var storedRow = view.ColumnRevisions.AsSpan(storedRowOffset, size);
                var worldRow = world.GetColumnRevisionRow(y);
                if (storedRow.Slice(from - oldBaseX, to - from + 1)
                    .SequenceEqual(worldRow.Slice(from, to - from + 1)))
                {
                    continue;
                }

                for (var x = from; x <= to; x++)
                {
                    if (storedRow[x - oldBaseX] != world.GetColumnRevision(x, y))
                    {
                        dirty.Add((x, y));
                    }
                }
            }
            else
            {
                var oldRadiusSquared = (long)radius * radius;
                for (var x = from; x <= to; x++)
                {
                    var columnX = x - oldBaseX;
                    var columnY = y - oldBaseY;
                    // A column the new disc adds was not in the old disc, so it has no key in the
                    // window even though the square of counters covers it: it must be read.
                    if (!WithinRadius(view.BlocksCellX, view.BlocksCellY, x, y, oldRadiusSquared) ||
                        (uint)columnX >= (uint)size ||
                        (uint)columnY >= (uint)size ||
                        view.ColumnRevisions[storedRowOffset + columnX] != world.GetColumnRevision(x, y))
                    {
                        dirty.Add((x, y));
                    }
                }
            }
        }

        // Re-reading a column costs the same in a patch and in a rebuild, so patching stays the
        // cheaper path up to the whole square: the only extra cost is the single removal pass over
        // the cached keys. The limit is therefore the square itself, and only a radius change or a
        // step wider than one cell falls back to a rebuild (PRISM-119).
        var patchLimit = size * size;
        if (dirty.Count > patchLimit)
        {
            return false;
        }

        if (dirty.Count > 0)
        {
            // One pass drops what left the disc and what belongs to a column being re-read: a
            // column is never half-updated, so no stale block can survive a patch.
            view.Removals.Clear();
            var radiusSquared = (long)radius * radius;
            foreach (var key in view.Blocks.Keys)
            {
                if (!WithinRadius(centerX, centerY, key.X, key.Y, radiusSquared) ||
                    StoredColumnDiffers(view, world, key.X, key.Y, radius))
                {
                    view.Removals.Add(key);
                }
            }

            foreach (var key in view.Removals)
            {
                view.Blocks.Remove(key);
            }

            foreach (var (x, y) in dirty)
            {
                AddColumnBlocks(view.Blocks, x, y, world, rules);
            }
        }

        view.BlocksCellX = centerX;
        view.BlocksCellY = centerY;
        CaptureWindowCoverage(world, position, radius, view);
        CaptureColumnCoverage(world, position, radius, view);
        return true;
    }

    /// <summary>
    /// True when the world's counter for a column differs from the one the cached window was read
    /// at. Reading the grid is the point: the caller is deciding whether to re-read the column.
    /// </summary>
    private static bool StoredColumnDiffers(AgentView view, WorldGrid world, int x, int y, int radius)
    {
        var size = radius * 2 + 1;
        var columnX = x - (view.BlocksCellX - radius);
        var columnY = y - (view.BlocksCellY - radius);
        return (uint)columnX >= (uint)size ||
               (uint)columnY >= (uint)size ||
               view.ColumnRevisions[columnY * size + columnX] != world.GetColumnRevision(x, y);
    }

    /// <summary>
    /// Stores the per-column write counters a window was captured from, over the square around
    /// the agent's cell. Cache only: it decides which columns to re-read and is never serialised
    /// nor part of the agent's knowledge (PRISM-119).
    /// </summary>
    private static void CaptureColumnCoverage(
        WorldGrid world,
        AgentPosition position,
        int radius,
        AgentView view)
    {
        var size = radius * 2 + 1;
        if (view.ColumnRevisions.Length < size * size)
        {
            view.ColumnRevisions = new int[size * size];
        }

        var baseX = position.CellX - radius;
        var baseY = position.CellY - radius;
        for (var row = 0; row < size; row++)
        {
            var revisions = world.GetColumnRevisionRow(baseY + row);
            var offset = row * size;
            if (revisions.IsEmpty)
            {
                Array.Clear(view.ColumnRevisions, offset, size);
                continue;
            }

            for (var column = 0; column < size; column++)
            {
                var worldX = baseX + column;
                view.ColumnRevisions[offset + column] =
                    (uint)worldX < (uint)revisions.Length ? revisions[worldX] : 0;
            }
        }

        view.ColumnsCellX = position.CellX;
        view.ColumnsCellY = position.CellY;
    }

    /// <summary>
    /// Cheap pre-test before the full visibility check. A block can only be seen when it stands
    /// above its own column, or beside a column that stands lower; a vein buried in the rock is
    /// neither. The world holds hundreds of thousands of buried overrides, and running the
    /// six-block visibility test on every one of them made a tick six times slower than the tick
    /// itself. Blocks that pass here are still checked properly by <see cref="AddBlockIfVisible"/>.
    /// </summary>
    private static bool IsPossiblyExposed(WorldGrid world, BlockCoordinate coordinate)
    {
        if (coordinate.Z > world.GetSurfaceColumn(coordinate.X, coordinate.Y).SurfaceZ)
        {
            return true;
        }

        foreach (var (dx, dy) in SideOffsets)
        {
            if (world.TryGetSurfaceColumn(coordinate.X + dx, coordinate.Y + dy, out var neighbour) &&
                neighbour.SurfaceZ < coordinate.Z)
            {
                return true;
            }
        }

        return false;
    }

    private static readonly (int Dx, int Dy)[] SideOffsets =
    [
        (0, -1), (-1, 0), (1, 0), (0, 1),
    ];

    private static void AddBlockIfVisible(
        Dictionary<ObservationKey, PerceptionEntry> candidates,
        BlockCoordinate coordinate,
        ushort materialId,
        byte state,
        WorldGrid world,
        PerceptionRules rules)
    {
        var material = rules.GetMaterial(materialId);
        if (!material.Perceivable || !IsExposed(coordinate, world, rules))
        {
            return;
        }

        var entry = new PerceptionEntry(
            PerceptionEntityKind.Block,
            coordinate.X,
            coordinate.Y,
            coordinate.Z,
            0,
            materialId,
            state,
            0,
            0,
            material.TagMask);
        candidates[new ObservationKey(entry.Kind, coordinate.X, coordinate.Y, coordinate.Z, 0)] = entry;
    }

    private static bool IsExposed(BlockCoordinate coordinate, WorldGrid world, PerceptionRules rules)
    {
        if (IsOpen(coordinate.X, coordinate.Y, coordinate.Z + 1, world, rules))
        {
            return true;
        }

        return IsOpen(coordinate.X - 1, coordinate.Y, coordinate.Z, world, rules) ||
               IsOpen(coordinate.X + 1, coordinate.Y, coordinate.Z, world, rules) ||
               IsOpen(coordinate.X, coordinate.Y - 1, coordinate.Z, world, rules) ||
               IsOpen(coordinate.X, coordinate.Y + 1, coordinate.Z, world, rules);
    }

    private static bool IsOpen(int x, int y, int z, WorldGrid world, PerceptionRules rules)
    {
        if ((uint)z >= (uint)world.VerticalSize || !world.TryGetSurfaceColumn(x, y, out var surface))
        {
            return false;
        }

        if (world.TryGetBlockOverride(new BlockCoordinate(x, y, z), out var block))
        {
            return block.IsAir || IsTransparent(block.MaterialId, rules);
        }

        if (z > surface.SurfaceZ)
        {
            return true;
        }

        if (z == surface.SurfaceZ)
        {
            return IsTransparent(surface.MaterialId, rules);
        }

        return false;
    }

    private static bool IsTransparent(ushort materialId, PerceptionRules rules)
    {
        if (materialId == rules.AirMaterialId)
        {
            return true;
        }

        var material = rules.GetMaterial(materialId);
        return !material.Solid && !material.Liquid;
    }

    private void AddNearbyGroundItems(
        Dictionary<ObservationKey, PerceptionEntry> candidates,
        AgentPosition position,
        GroundItemStore groundItems,
        int radius,
        PerceptionSpatialIndex? index,
        List<GroundItem> scratch)
    {
        var radiusSquared = (long)radius * radius;
        if (index is null)
        {
            // Sans index (appel direct, tests), on parcourt le magasin sans le copier : c'est le
            // comportement d'avant, moins la copie qui coûtait un magasin entier par agent.
            foreach (var item in groundItems.Items)
            {
                AddGroundItemCandidate(candidates, position, item, radiusSquared);
            }

            return;
        }

        index.CollectItems(position.CellX, position.CellY, radius, scratch);
        foreach (var item in scratch)
        {
            AddGroundItemCandidate(candidates, position, item, radiusSquared);
        }
    }

    private void AddGroundItemCandidate(
        Dictionary<ObservationKey, PerceptionEntry> candidates,
        AgentPosition position,
        GroundItem item,
        long radiusSquared)
    {
        ItemCandidatesExamined++;
        if (!WithinRadius(position.CellX, position.CellY, item.Position.X, item.Position.Y, radiusSquared))
        {
            return;
        }

        var entry = new PerceptionEntry(
            PerceptionEntityKind.GroundItem,
            item.Position.X,
            item.Position.Y,
            item.Position.Z,
            item.Id,
            0,
            0,
            item.Stack.ItemTypeId,
            item.Stack.Quantity,
            0);
        candidates[new ObservationKey(entry.Kind, entry.X, entry.Y, entry.Z, entry.Id)] = entry;
    }

    private void AddNearbyAgents(
        Dictionary<ObservationKey, PerceptionEntry> candidates,
        AgentBody observer,
        AgentRegistry agents,
        int radius,
        PerceptionSpatialIndex? index,
        List<AgentBody> scratch)
    {
        var radiusSquared = (long)radius * radius;
        if (index is null)
        {
            foreach (var agent in agents.Agents)
            {
                AddAgentCandidate(candidates, observer, agent, radiusSquared);
            }

            return;
        }

        index.CollectAgents(observer.Position.CellX, observer.Position.CellY, radius, scratch);
        foreach (var agent in scratch)
        {
            AddAgentCandidate(candidates, observer, agent, radiusSquared);
        }
    }

    private void AddAgentCandidate(
        Dictionary<ObservationKey, PerceptionEntry> candidates,
        AgentBody observer,
        AgentBody agent,
        long radiusSquared)
    {
        AgentCandidatesExamined++;
        if (agent.AgentId == observer.AgentId ||
            !WithinRadius(
                observer.Position.CellX,
                observer.Position.CellY,
                agent.Position.CellX,
                agent.Position.CellY,
                radiusSquared))
        {
            return;
        }

        var entry = new PerceptionEntry(
            PerceptionEntityKind.Agent,
            agent.Position.CellX,
            agent.Position.CellY,
            agent.Position.FeetZ,
            agent.AgentId,
            0,
            0,
            0,
            0,
            0);
        candidates[new ObservationKey(entry.Kind, entry.X, entry.Y, entry.Z, entry.Id)] = entry;
    }

    private static void AddNearbyConstructionSites(
        Dictionary<ObservationKey, PerceptionEntry> candidates,
        AgentPosition position,
        IReadOnlyList<StructureConstructionSite>? sites,
        int radius)
    {
        if (sites is null)
        {
            return;
        }

        var radiusSquared = (long)radius * radius;
        foreach (var site in sites)
        {
            if (site.State == StructureConstructionState.Abandoned ||
                !WithinRadius(position.CellX, position.CellY, site.Origin.X, site.Origin.Y, radiusSquared))
            {
                continue;
            }

            var entry = new PerceptionEntry(
                PerceptionEntityKind.ConstructionSite,
                site.Origin.X,
                site.Origin.Y,
                site.Origin.Z,
                StableSiteId(site.Id),
                0,
                (byte)site.State,
                0,
                site.WorkCompleted,
                0,
                site.Id,
                site.WorkRequired);
            candidates[new ObservationKey(entry.Kind, entry.X, entry.Y, entry.Z, entry.Id)] = entry;
        }
    }

    private static ulong StableSiteId(string siteId)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var character in siteId)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }

    private void AddRegionSummaries(
        Dictionary<ObservationKey, PerceptionEntry> candidates,
        AgentPosition position,
        WorldGrid world,
        PerceptionRules rules,
        int radius)
    {
        if (!world.TryGetRegionId(position.CellX, position.CellY, out var centerRegion))
        {
            return;
        }

        var centerX = centerRegion % world.RegionCountX;
        var centerY = centerRegion / world.RegionCountX;
        for (var regionY = (int)Math.Max(0L, (long)centerY - radius);
             regionY <= (int)Math.Min(world.RegionCountY - 1L, (long)centerY + radius);
             regionY++)
        {
            for (var regionX = (int)Math.Max(0L, (long)centerX - radius);
                 regionX <= (int)Math.Min(world.RegionCountX - 1L, (long)centerX + radius);
                 regionX++)
            {
                var regionId = regionX + world.RegionCountX * regionY;
                var tagMask = GetExposedRegionTagMask(world, rules, regionX, regionY, regionId);
                if (tagMask == 0)
                {
                    continue;
                }

                var entry = new PerceptionEntry(
                    PerceptionEntityKind.RegionSummary,
                    regionX,
                    regionY,
                    0,
                    (ulong)regionId,
                    0,
                    0,
                    0,
                    0,
                    tagMask);
                candidates[new ObservationKey(entry.Kind, regionX, regionY, 0, (ulong)regionId)] = entry;
            }
        }
    }

    /// <summary>
    /// Region summaries only depend on the region's contents, so their tag mask is cached against
    /// the revisions of the regions the scan reads. Without it every agent rescanned up to 49
    /// regions' worth of columns on every tick, which dominated the tick cost long before any
    /// agent did real work. The key is per region rather than the global revision: a write in one
    /// region used to force every agent to rescan its whole neighbourhood (PRISM-109).
    /// </summary>
    private uint GetExposedRegionTagMask(
        WorldGrid world,
        PerceptionRules rules,
        int regionX,
        int regionY,
        int regionId)
    {
        var key = CaptureRegionMaskKey(world, rules, regionX, regionY, regionId);
        if (_regionMasks.TryGetValue(regionId, out var cached) &&
            cached.Key == key)
        {
            if (_verifyCaches)
            {
                // Same hypothesis as the block window: the mask only depends on the region's
                // contents, so a revision-keyed cache must match a fresh scan of the region.
                var fresh = ComputeExposedRegionTagMask(world, rules, regionX, regionY, regionId);
                if (fresh != cached.Mask)
                {
                    throw new CacheVerificationException(
                        "perception.regionMask",
                        _diagnosticTick,
                        null,
                        $"region ({regionX},{regionY}) id {regionId}",
                        $"cache says tag mask 0x{cached.Mask:x8}, a fresh scan says 0x{fresh:x8}");
                }
            }

            return cached.Mask;
        }

        var mask = ComputeExposedRegionTagMask(world, rules, regionX, regionY, regionId);
        _regionMasks[regionId] = new CachedRegionMask(key, mask);
        RegionMaskRebuilds++;
        return mask;
    }

    /// <summary>
    /// The cells a region's mask is computed from: the region itself (columns and overrides) and
    /// the four cardinal neighbours, because <see cref="IsExposed"/> asks a block's four
    /// side-neighbours whether they are open. A diagonal neighbour is never read: a block only
    /// tests the cells it shares an edge with.
    /// </summary>
    private static RegionMaskKey CaptureRegionMaskKey(
        WorldGrid world,
        PerceptionRules rules,
        int regionX,
        int regionY,
        int regionId)
    {
        var west = regionX > 0 ? world.GetRegionRevision(regionId - 1) : -1;
        var east = regionX + 1 < world.RegionCountX ? world.GetRegionRevision(regionId + 1) : -1;
        var north = regionY > 0 ? world.GetRegionRevision(regionId - world.RegionCountX) : -1;
        var south = regionY + 1 < world.RegionCountY
            ? world.GetRegionRevision(regionId + world.RegionCountX)
            : -1;
        return new RegionMaskKey(
            world.GetRegionRevision(regionId),
            west,
            east,
            north,
            south,
            rules);
    }

    private uint ComputeExposedRegionTagMask(
        WorldGrid world,
        PerceptionRules rules,
        int regionX,
        int regionY,
        int regionId)
    {
        var mask = 0u;
        var minX = regionX * world.RegionSize;
        var minY = regionY * world.RegionSize;
        var maxX = Math.Min(world.Width, minX + world.RegionSize);
        var maxY = Math.Min(world.Height, minY + world.RegionSize);
        for (var y = minY; y < maxY; y++)
        {
            for (var x = minX; x < maxX; x++)
            {
                var surface = world.GetSurfaceColumn(x, y);
                var coordinate = new BlockCoordinate(x, y, surface.SurfaceZ);
                var materialId = surface.MaterialId;
                if (world.TryGetBlockOverride(coordinate, out var surfaceOverride))
                {
                    if (surfaceOverride.IsAir)
                    {
                        continue;
                    }

                    materialId = surfaceOverride.MaterialId;
                }

                if (IsExposed(coordinate, world, rules))
                {
                    mask |= rules.GetMaterial(materialId).TagMask;
                }
            }
        }

        foreach (var (coordinate, block) in world.EnumerateRegionOverrides(regionId))
        {
            if (block.IsAir || coordinate.Z < world.GetSurfaceColumn(coordinate.X, coordinate.Y).SurfaceZ)
            {
                continue;
            }

            if (coordinate.Z != world.GetSurfaceColumn(coordinate.X, coordinate.Y).SurfaceZ &&
                IsExposed(coordinate, world, rules))
            {
                mask |= rules.GetMaterial(block.MaterialId).TagMask;
            }
        }

        return mask;
    }

    /// <summary>
    /// Recomputes a cached block window without the memo and fails on the first difference, in
    /// canonical key order so two runs report the same cell. Called only on a cache hit.
    /// </summary>
    private void VerifyBlockWindow(
        ulong agentId,
        AgentPosition position,
        WorldGrid world,
        PerceptionRules rules,
        int radius,
        Dictionary<ObservationKey, PerceptionEntry> cached)
    {
        var fresh = new Dictionary<ObservationKey, PerceptionEntry>(cached.Count);
        AddNearbyBlocks(fresh, position, world, rules, radius);

        var keys = new List<ObservationKey>(cached.Count + fresh.Count);
        keys.AddRange(cached.Keys);
        keys.AddRange(fresh.Keys);
        keys.Sort();

        var hasPrevious = false;
        var previous = default(ObservationKey);
        foreach (var key in keys)
        {
            if (hasPrevious && previous.CompareTo(key) == 0)
            {
                continue;
            }

            hasPrevious = true;
            previous = key;

            var inCache = cached.TryGetValue(key, out var cachedEntry);
            var inFresh = fresh.TryGetValue(key, out var freshEntry);
            var location = $"({key.X},{key.Y},{key.Z})";
            if (inCache && !inFresh)
            {
                throw new CacheVerificationException(
                    "perception.window", _diagnosticTick, agentId, location,
                    "the cached window still holds a block the world no longer shows");
            }

            if (!inCache)
            {
                throw new CacheVerificationException(
                    "perception.window", _diagnosticTick, agentId, location,
                    "the cached window misses a block the world shows now");
            }

            if (!cachedEntry.Equals(freshEntry))
            {
                throw new CacheVerificationException(
                    "perception.window", _diagnosticTick, agentId, location,
                    $"the cached entry (material {cachedEntry.MaterialId}, state {cachedEntry.State}) " +
                    $"differs from the world's (material {freshEntry.MaterialId}, state {freshEntry.State})");
            }
        }
    }

    private static bool WithinRadius(int centerX, int centerY, int x, int y, long radiusSquared)
    {
        var deltaX = (long)x - centerX;
        var deltaY = (long)y - centerY;
        return deltaX * deltaX + deltaY * deltaY <= radiusSquared;
    }

    private readonly record struct ObservationKey(
        PerceptionEntityKind Kind,
        int X,
        int Y,
        int Z,
        ulong Id) : IComparable<ObservationKey>
    {
        public int CompareTo(ObservationKey other)
        {
            // enum.CompareTo(object) boxes both operands; on the hot path (every tick,
            // every agent) that box was 400 Mo of allocation at 1 000 agents. Comparing
            // the underlying byte keeps the exact same order (the enum is byte-backed).
            var comparison = ((byte)Kind).CompareTo((byte)other.Kind);
            if (comparison != 0) return comparison;
            comparison = Z.CompareTo(other.Z);
            if (comparison != 0) return comparison;
            comparison = Y.CompareTo(other.Y);
            if (comparison != 0) return comparison;
            comparison = X.CompareTo(other.X);
            return comparison != 0 ? comparison : Id.CompareTo(other.Id);
        }

    }

    private sealed record PerceptionCheckpoint(int Version, AgentPerceptionCheckpoint[] Agents);

    /// <summary>
    /// Key of a cached region tag mask: the write counters of every region the scan reads, plus
    /// the rules instance (the mask is a function of the material tags). Missing neighbours use
    /// <c>-1</c>, a value no revision can take, so the border of the world is stable.
    /// </summary>
    /// <summary>
    /// One column's contribution, captured at a known write counter. <see cref="World"/> and
    /// <see cref="Rules"/> are part of the check because a perception system can be handed another
    /// world or another rule set between two calls; a counter alone would not see that.
    /// </summary>
    private readonly record struct CachedColumnEntries(
        int Revision,
        WorldGrid World,
        PerceptionRules Rules,
        PerceptionEntry[] Entries);

    private readonly record struct RegionMaskKey(
        long Self,
        long West,
        long East,
        long North,
        long South,
        PerceptionRules Rules);

    private readonly record struct CachedRegionMask(RegionMaskKey Key, uint Mask);

    /// <summary>
    /// État de perception d'un agent : ce qu'il sait (<see cref="Entries"/>), la
    /// fenêtre de blocs mémorisée (<see cref="Blocks"/>, tenue à jour seulement
    /// quand une région qu'elle couvre est écrite ou que l'agent se déplace) et la
    /// mémoire de travail du tick (<see cref="Extras"/>, <see cref="Pending"/>),
    /// réutilisée pour ne rien allouer au régime établi.
    /// </summary>
    private sealed class AgentView
    {
        public Dictionary<ObservationKey, PerceptionEntry> Entries { get; } = [];
        public Dictionary<ObservationKey, PerceptionEntry> Blocks { get; } = [];
        public Dictionary<ObservationKey, PerceptionEntry> Extras { get; } = [];
        public List<(ObservationKey Key, PerceptionChange Change)> Pending { get; } = [];
        public int BlocksCellX { get; set; } = int.MinValue;
        public int BlocksCellY { get; set; } = int.MinValue;
        public int BlocksRadius { get; set; } = -1;

        /// <summary>
        /// Vrai quand le dernier diff a tout retenu : les entrées connues sont alors
        /// exactement le contenu de la fenêtre. Si un changement de bloc est tombé
        /// au-delà du plafond, il doit être réémis au tick suivant, donc le diff ne
        /// peut pas être sauté.
        /// </summary>
        public bool BlocksInSync { get; set; }

        /// <summary>
        /// Revisions of the regions <see cref="Blocks"/> was captured from, in the canonical
        /// region order of <see cref="WindowRegionRange"/>. Fixed for a given radius, so the array
        /// is reused and only grows when the radius makes the coverage wider.
        /// </summary>
        public long[] BlocksRegionRevisions { get; set; } = [];

        public int BlocksRegionCount { get; set; }

        /// <summary>
        /// Region rectangle <see cref="BlocksRegionRevisions"/> was captured from. Stored with the
        /// revisions because a sliding window's covered rectangle moves without the view being
        /// rebuilt: an index alone could not be mapped back to a region (PRISM-105).
        /// </summary>
        public int BlocksRegionMinX { get; set; }

        public int BlocksRegionMinY { get; set; }

        public int BlocksRegionMaxX { get; set; }

        public int BlocksRegionMaxY { get; set; }

        /// <summary>Keys dropped by the last slide, reused so a slide allocates nothing.</summary>
        public List<ObservationKey> Removals { get; } = [];

        /// <summary>
        /// Write counters of the columns <see cref="Blocks"/> was built from, over a square around
        /// <see cref="ColumnsCellX"/>/<see cref="ColumnsCellY"/>, row-major. Comparing them with
        /// the grid is what narrows an invalidation to the written columns (PRISM-119).
        /// </summary>
        public int[] ColumnRevisions { get; set; } = [];

        /// <summary>Columns the next patch must re-read, reused so a patch allocates nothing.</summary>
        public List<(int X, int Y)> DirtyColumns { get; } = [];

        /// <summary>Cell the column counters were captured around.</summary>
        public int ColumnsCellX { get; set; } = int.MinValue;

        public int ColumnsCellY { get; set; } = int.MinValue;
    }

    private sealed record AgentPerceptionCheckpoint(ulong AgentId, PerceptionEntry[] Entries);
}
