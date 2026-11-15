using System.Text.Json;
using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.Diagnostics;
using Prism.Core.Perception;
using Prism.Core.Navigation;

namespace Prism.Core.World;

public sealed record AgentEntitySnapshot(
    ulong AgentId,
    AgentPosition Position,
    AgentBodyProfile Profile,
    AgentItemStack[] Inventory);

public sealed record AgentPerceptionSnapshot(ulong AgentId, PerceptionFrame Frame);

public sealed record AgentSpoilage(ulong AgentId, Accounting.SpoilageEvent Spoilage);

public sealed record MoveOrderStatus(
    ulong AgentId,
    string CommandId,
    bool Completed,
    string? ErrorCode,
    int StepsRemaining,
    BlockCoordinate Target);

public sealed class AgentLifecycleSystem
{
    private const int CheckpointVersion = 2;
    private static readonly JsonSerializerOptions CheckpointJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WorldGrid _world;
    private StructureEffectConsumer? _structureEffects;
    private MovementGraph _movement;
    private readonly PerceptionSystem _perception;

    /// <summary>
    /// Index spatial du voisinage, reconstruit une fois par <see cref="ObserveAgents"/> : sans lui,
    /// chaque agent parcourait tous les agents et copiait tous les objets au sol du monde
    /// (PRISM-141). Il ne filtre que des candidats, jamais des entrées : l'ensemble observé reste
    /// celui d'un balayage complet.
    /// </summary>
    private readonly PerceptionSpatialIndex _perceptionIndex = new();

    /// <summary>
    /// <c>PRISM_PERCEPTION_FULL_SCAN=1</c> force l'ancien balayage complet (PRISM-141). Le chemin
    /// ne rend pas d'entrées différentes — un test compare les deux — il sert à mesurer le gain
    /// sur le même binaire, donc sans confondre l'effet avec la dérive d'une machine partagée.
    /// </summary>
    private static readonly bool FullPerceptionScan =
        Environment.GetEnvironmentVariable("PRISM_PERCEPTION_FULL_SCAN") == "1";
    private readonly bool _verifyCaches;
    private PerceptionRules? _perceptionRules;
    private readonly PerceptionSettings _perceptionSettings;
    private AgentRegistry _agents = new();
    private GroundItemStore _groundItems = new();
    private readonly SortedDictionary<ulong, MoveOrderState> _moveOrders = [];
    private long _revision;

    /// <summary>
    /// Chemin déjà calculé vers une cible, par agent. Sans ce cache, chaque pas
    /// d'un agent relançait une recherche A* complète (budget : le monde entier),
    /// et une cible inatteignable épuisait ce budget à chaque tick — 1,9 s/tick
    /// mesuré à 100 agents en production.
    /// <para>
    /// PRISM-104 : l'entrée ne porte plus la révision globale du monde mais
    /// <see cref="RegionReadSet"/>, l'ensemble des régions que la recherche a lues. C'est la
    /// condition exacte de validité : un résultat d'A* dépend de chaque case qu'il a
    /// <em>évaluée</em>, pas seulement des cases du chemin retenu, et une région est invalidée
    /// en bloc dès qu'une de ses cases est écrite. Une révision globale périmait le chemin de
    /// <b>tous</b> les agents à chaque minage, donc chaque tick en production relançait toutes
    /// les recherches ; une invalidation par le seul chemin retenu aurait été trop étroite et
    /// aurait changé des trajectoires en silence.
    /// </para>
    /// <para>
    /// PRISM-109 : l'entrée retient aussi la <b>case de départ</b> de la recherche. Un échec n'est
    /// vrai que depuis cette case ; <see cref="StartMoveOrder"/> déplace l'agent sans passer par ce
    /// cache, donc réutiliser un échec depuis une autre case aurait fait renoncer un agent à une
    /// route qui existe de là où il se trouve.
    /// </para>
    /// </summary>
    private readonly Dictionary<ulong, CachedPath> _paths = [];

    /// <summary>
    /// Champs de distance par classe de cible (PRISM-143). Caches par (classe, revision du monde) :
    /// un champ survit tant que le monde n'a pas ete ecrit, et est recalcule une seule fois par
    /// classe et par revision, au lieu d'une recherche A* par agent. Active via
    /// <c>PRISM_DISTANCE_FIELDS=1</c> pour ne pas changer les trajectoires existantes tant que
    /// la mesure n'est pas faite.
    /// </summary>
    private readonly Dictionary<string, (long Revision, int SourceHash, DistanceField Field)> _distanceFields = new();
    private static readonly bool DistanceFieldsEnabled =
        Environment.GetEnvironmentVariable("PRISM_DISTANCE_FIELDS") == "1";
    /// <summary>
    /// Résultats de recherche partagés par tous les agents (PRISM-104). Deux agents qui
    /// interrogent la même (case de départ, cible) ne développent plus les nœuds deux fois, et
    /// une entrée survit tant qu'aucune région lue n'a été écrite — au lieu de mourir à la
    /// première écriture du monde.
    /// </summary>
    private readonly PathfindingCache _searchCache = new(PathCacheCapacity);

    /// <summary>
    /// Compteurs exposés au profil opt-in : recherches exécutées, servies par le cache, nœuds
    /// développés, échecs et budgets épuisés (PRISM-104).
    /// </summary>
    private readonly PathfindingMetrics _pathfinding = new();

    private const int PathCacheCapacity = 4096;

    /// <remarks>
    /// <c>verifyCaches</c> is <c>PRISM_VERIFY_CACHES=1</c> (PRISM-108): the perception window and
    /// the <c>CanStandAt</c> memo recompute themselves without their memo and fail on the first
    /// divergence. Off by default, and free when off.
    /// </remarks>
    public AgentLifecycleSystem(
        WorldGrid world,
        MovementRules rules,
        PerceptionRules? perceptionRules = null,
        PerceptionSettings? perceptionSettings = null,
        bool verifyCaches = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rules);
        _world = world;
        _verifyCaches = verifyCaches;
        _perception = new PerceptionSystem(verifyCaches);
        _movement = new MovementGraph(
            world, rules, () => _structureEffects?.GetActiveEffects() ?? [], verifyCaches);
        _perceptionRules = perceptionRules;
        _perceptionSettings = perceptionSettings ?? new PerceptionSettings();
        _perceptionSettings.Validate();
    }

    public StructureEffectConsumer? StructureEffects
    {
        get => _structureEffects;
        set => _structureEffects = value;
    }

    public int AgentCount => _agents.Count;
    public int GroundItemCount => _groundItems.Count;
    public int MoveOrderCount => _moveOrders.Count;
    public long Revision => _revision;

    /// <summary>
    /// Compteurs de recherche de chemin (PRISM-104), lus par le profil opt-in. Cumulés depuis la
    /// création du système ; <see cref="PathfindingMetricsSnapshot"/> se soustrait pour obtenir le
    /// delta d'un tick.
    /// </summary>
    public PathfindingMetrics Pathfinding => _pathfinding;

    /// <summary>Cache de chemins partagé par les agents, pour le profil (hits / invalidations).</summary>
    public PathCacheStatistics PathCache => _searchCache.Statistics;

    public AgentEntitySnapshot[] SnapshotAgents() =>
        _agents.Agents
            .Select(agent => new AgentEntitySnapshot(
                agent.AgentId,
                agent.Position,
                agent.Profile,
                agent.Inventory.Snapshot().ToArray()))
            .ToArray();

    public GroundItem[] SnapshotGroundItems() => _groundItems.Snapshot().ToArray();

    public bool TryGetAgent(ulong agentId, out AgentBody? agent) =>
        _agents.TryGet(agentId, out agent);

    public bool HasPerceivedBlock(ulong agentId, BlockCoordinate position) =>
        _perception.HasPerceivedBlock(agentId, position);

    public bool HasPerceivedAgent(ulong observerAgentId, ulong targetAgentId) =>
        _perception.HasPerceivedAgent(observerAgentId, targetAgentId);

    public IReadOnlyList<PerceptionEntry> GetPerceivedEntries(ulong agentId) =>
        _perception.GetPerceivedEntries(agentId);

    public bool TryMoveOneStep(ulong agentId, BlockCoordinate target, out string? errorCode)
    {
        errorCode = null;
        if (!_agents.TryGet(agentId, out var agent))
        {
            errorCode = "agent_not_found";
            return false;
        }

        if (_paths.TryGetValue(agentId, out CachedPath? cached) &&
            !cached.Reads.IsStillValid(_world))
        {
            _paths.Remove(agentId);
            cached = null;
        }

        if (cached is not null && cached.Target == target)
        {
            if (cached.Failed)
            {
                // Un échec ne vaut que depuis la case où la recherche est partie : un ordre de
                // déplacement (`IssueMoveOrder`) écrit la position sans toucher `_paths`, et la
                // route absente d'ici peut exister de là-bas. La case de départ fait donc partie
                // de la clé de validité, comme la cible et les régions lues.
                if (cached.StartX == agent!.Position.CellX &&
                    cached.StartY == agent.Position.CellY)
                {
                    if (_verifyCaches)
                    {
                        VerifyCachedPath(agentId, cached);
                    }

                    errorCode = cached.ErrorCode;
                    return false;
                }
            }

            // Le chemin est une suite de cases déjà validée : on ne repart de la
            // position courante toute la suite n'est plus la bonne.
            else if (cached.Offset < cached.Cells.Length &&
                     cached.Cells[cached.Offset].X == agent!.Position.CellX &&
                     cached.Cells[cached.Offset].Y == agent.Position.CellY)
            {
                if (_verifyCaches)
                {
                    VerifyCachedPath(agentId, cached);
                }

                if (cached.Cells.Length - cached.Offset <= 1)
                {
                    return true;
                }

                EnsureRevisionAvailable();
                var step = cached.Cells[cached.Offset + 1];
                agent!.Position = AgentPosition.AtCell(step.X, step.Y, step.FeetZ);
                _revision++;
                cached.Offset++;
                return true;
            }

            _paths.Remove(agentId);
        }

        var startX = agent!.Position.CellX;
        var startY = agent.Position.CellY;
        var path = AStarPathfinder.FindPath(
            _movement,
            startX,
            startY,
            target.X,
            target.Y,
            MaximumSearchNodes,
            _searchCache,
            _pathfinding);
        if (path.Status != PathfindingStatus.Found)
        {
            errorCode = path.Status == PathfindingStatus.NodeBudgetExceeded
                ? "path_budget_exceeded"
                : "path_not_found";

            // Un échec ne dépend que des régions lues et de la cible : le mémoriser
            // évite de relancer la même recherche impossible à chaque tick. Un échec
            // sans lecture (`Reads` nul) n'a lancé aucune recherche — la cible n'est
            // pas une case où l'on peut se tenir —, donc il n'y a rien à mémoriser :
            // le refus est rendu par le même `GetNode` bon marché au tick suivant.
            if (path.Reads is { } failureReads)
            {
                _paths[agentId] = new CachedPath(
                    target, startX, startY, failureReads, [], 0, true, errorCode);
            }

            return false;
        }

        var reads = path.Reads ?? throw new InvalidOperationException(
            "A path search that found a route must report the regions it read.");

        if (path.Path.Count <= 1)
        {
            _paths[agentId] = new CachedPath(
                target,
                startX,
                startY,
                reads,
                path.Path.Select(static cell => (cell.X, cell.Y, cell.FeetZ)).ToArray(),
                0,
                false,
                null);
            return true;
        }

        EnsureRevisionAvailable();
        var next = path.Path[1];
        agent.Position = AgentPosition.AtCell(next.X, next.Y, next.FeetZ);
        _revision++;
        _paths[agentId] = new CachedPath(
            target,
            startX,
            startY,
            reads,
            path.Path.Select(static cell => (cell.X, cell.Y, cell.FeetZ)).ToArray(),
            1,
            false,
            null);
        return true;
    }

    /// <summary>
    /// PRISM-108 : rejoue un résultat mémorisé depuis <em>sa propre</em> case de départ et lève à la
    /// première différence.
    /// </summary>
    /// <remarks>
    /// La vérification part de la case d'origine, pas de la case courante : A* est déterministe à
    /// graphe, départ, cible et budget identiques, donc le rejeu doit rendre exactement le chemin
    /// mémorisé — y compris les égalités de coût. Une recherche relancée depuis la case courante
    /// pourrait légitimement rendre un autre suffixe de même coût (l'ordre de la file dépend de
    /// l'historique d'exploration) et signaler une faute qui n'en est pas une.
    /// <para>
    /// Ce que la vérification attrape est précisément l'invalidation trop étroite : si une écriture
    /// a changé une région absente de <see cref="RegionReadSet"/>, l'ensemble lu reste valide, le
    /// chemin mémorisé est suivi, mais le rejeu ne le retrouve plus. C'est le seul défaut de cette
    /// optimisation qui ne se voit pas autrement.
    /// </para>
    /// </remarks>
    private void VerifyCachedPath(ulong agentId, CachedPath cached)
    {
        var fresh = AStarPathfinder.FindPath(
            _movement,
            cached.StartX,
            cached.StartY,
            cached.Target.X,
            cached.Target.Y,
            MaximumSearchNodes);

        var location =
            $"({cached.StartX},{cached.StartY}) -> ({cached.Target.X},{cached.Target.Y})";

        if (cached.Failed)
        {
            var expected = cached.ErrorCode == "path_budget_exceeded"
                ? PathfindingStatus.NodeBudgetExceeded
                : PathfindingStatus.NotFound;
            if (fresh.Status != expected)
            {
                throw new CacheVerificationException(
                    "lifecycle.pathCache",
                    _movement.Tick,
                    agentId,
                    location,
                    $"the memo holds the failure '{cached.ErrorCode}', a fresh search answers " +
                    $"{DescribePathStatus(fresh.Status)}");
            }

            return;
        }

        if (fresh.Status != PathfindingStatus.Found)
        {
            throw new CacheVerificationException(
                "lifecycle.pathCache",
                _movement.Tick,
                agentId,
                location,
                $"the memo holds a route of {cached.Cells.Length} cells, a fresh search answers " +
                $"{DescribePathStatus(fresh.Status)}");
        }

        if (fresh.Path.Count != cached.Cells.Length)
        {
            throw new CacheVerificationException(
                "lifecycle.pathCache",
                _movement.Tick,
                agentId,
                location,
                $"the memo holds a route of {cached.Cells.Length} cells, a fresh search " +
                $"returns {fresh.Path.Count}");
        }

        for (var index = 0; index < cached.Cells.Length; index++)
        {
            var memoized = cached.Cells[index];
            var replayed = fresh.Path[index];
            if (memoized.X != replayed.X ||
                memoized.Y != replayed.Y ||
                memoized.FeetZ != replayed.FeetZ)
            {
                throw new CacheVerificationException(
                    "lifecycle.pathCache",
                    _movement.Tick,
                    agentId,
                    location,
                    $"the memo steps through ({memoized.X},{memoized.Y},{memoized.FeetZ}) at " +
                    $"index {index}, a fresh search through " +
                    $"({replayed.X},{replayed.Y},{replayed.FeetZ})");
            }
        }
    }

    private static string DescribePathStatus(PathfindingStatus status) => status switch
    {
        PathfindingStatus.Found => "Found",
        PathfindingStatus.NotFound => "NotFound",
        _ => "NodeBudgetExceeded",
    };

    /// <summary>
    /// The approach cell already chosen for an (agent, block) pair. <see cref="FindApproachCell"/>
    /// is recomputed from the agent's position, so as the agent walks the "nearest" neighbour
    /// flips between two cells of a cliff: the macro then emitted a <c>Move</c> per tick, each one
    /// a fresh A* search, and the agent shuttled between two altitudes for the rest of the run
    /// (0,7 s/tick measured on R1). Keeping the first approach cell makes the walk monotone; a cell
    /// that turns out unreachable fails once and the macro exhausts the source instead of looping.
    /// </summary>
    private readonly Dictionary<(ulong AgentId, BlockCoordinate Block), BlockCoordinate> _approachCells = [];

    /// <summary>Chemin mémorisé vers une cible, ou échec mémorisé tant que ses régions sont intactes.</summary>
    private sealed record CachedPath(
        BlockCoordinate Target,
        int StartX,
        int StartY,
        RegionReadSet Reads,
        (int X, int Y, int FeetZ)[] Cells,
        int Offset,
        bool Failed,
        string? ErrorCode)
    {
        public int Offset { get; set; } = Offset;
    }

    /// <summary>
    /// Budget d'une recherche : le monde entier, comme avant PRISM-104. Le borner par la distance a
    /// été essayé le 10 novembre 2026 et cassait deux empreintes (campagnes dorées, graines 1 et 3,
    /// bot <c>random</c>) : une recherche qui aurait abouti échouait plus tôt, donc l'agent
    /// renonçait au lieu d'arriver. Le budget reste donc l'état du monde ; ce sont les
    /// <b>recherches inutiles</b> qu'on supprime, pas la portée de celles qui servent.
    /// </summary>
    private int MaximumSearchNodes => Math.Max(1, checked(_world.Width * _world.Height));

    /// <summary>
    /// Walks one step toward a block the agent has to stand <em>next to</em>.
    /// Mining, harvesting and crafting at a station all require adjacency
    /// (<see cref="BlockActionSystem.IsAdjacent"/>), and the block's own cell is never walkable,
    /// so a path to the block itself can only fail. The nearest walkable neighbour whose feet are
    /// within one level of the block (feet level, head level, or one below) is the cell that makes
    /// the action legal.
    /// </summary>
    public bool TryMoveAdjacentToOneStep(ulong agentId, BlockCoordinate block, out string? errorCode)
    {
        errorCode = null;
        if (!_agents.TryGet(agentId, out var agent))
        {
            errorCode = "agent_not_found";
            return false;
        }

        if (BlockActionSystem.IsAdjacent(agent!.Position, block))
        {
            return true;
        }

        var key = (agentId, block);
        if (!_approachCells.TryGetValue(key, out var target) ||
            !_movement.CanStandAt(target.X, target.Y, out var standingZ) ||
            standingZ != target.Z)
        {
            var approach = FindApproachCell(agent.Position, block);
            if (approach is not { } fresh)
            {
                // No neighbour is standable at the block's height: from here the block cannot be
                // touched at all (an upper tree trunk, a block over a cliff). Reported as a path
                // failure so the macro-action exhausts this source instead of looping on it.
                errorCode = "path_not_found";
                return false;
            }

            target = fresh;
            _approachCells[key] = target;
        }

        var moved = TryMoveOneStep(agentId, target, out errorCode);
        if (!moved)
        {
            // A target that cannot be reached must not be retried for ever: dropping it lets the
            // next call answer `path_not_found` and the macro move on to another source.
            _approachCells.Remove(key);
        }
        else if (BlockActionSystem.IsAdjacent(agent.Position, block))
        {
            _approachCells.Remove(key);
        }

        return moved;
    }

    /// <summary>
    /// The walkable cell from which <paramref name="block"/> is adjacent, or null when none
    /// exists. Deterministic: nearest to the agent, then lowest Y, then lowest X.
    /// </summary>
    public BlockCoordinate? FindApproachCell(AgentPosition position, BlockCoordinate block)
    {
        BlockCoordinate? best = null;
        var bestDistance = long.MaxValue;
        foreach (var (dx, dy) in ApproachOffsets)
        {
            var x = block.X + dx;
            var y = block.Y + dy;
            if (x < 0 || y < 0 || x >= _world.Width || y >= _world.Height)
            {
                continue;
            }

            // The approach cell has to be one the action accepts. <see
            // cref="BlockActionSystem.IsAdjacent"/> is a 3×3×3 cube centred on the feet, so a
            // neighbouring column is adjacent when its feet are at the block's level (dz = 0),
            // one below (block at head level, dz = -1) or one above (block at feet-1, dz = +1).
            // Allowing only { z, z+1 } made head-level blocks unreachable: the block above a tree
            // base, or an ore face one step above the ground beside it, was visible to the agent
            // and adjacent by the action rule, yet no approach cell was ever chosen, so the
            // obtain exhausted it as `path_not_found`.
            if (!_movement.CanStandAt(x, y, out var feetZ) || feetZ < block.Z - 1 || feetZ > block.Z + 1)
            {
                continue;
            }

            var distance = Math.Abs((long)position.CellX - x) + Math.Abs((long)position.CellY - y);
            if (distance < bestDistance ||
                distance == bestDistance && best is { } current &&
                (y < current.Y || y == current.Y && x < current.X))
            {
                best = new BlockCoordinate(x, y, feetZ);
                bestDistance = distance;
            }
        }

        return best;
    }

    private static readonly (int Dx, int Dy)[] ApproachOffsets =
    [
        (0, -1), (-1, 0), (1, 0), (0, 1),
    ];

    public bool StartMoveOrder(
        ulong agentId,
        string commandId,
        BlockCoordinate target,
        out string? errorCode,
        out int stepCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        errorCode = null;
        stepCount = 0;
        if (!_agents.TryGet(agentId, out var agent))
        {
            errorCode = "agent_not_found";
            return false;
        }

        if ((uint)target.X >= (uint)_world.Width || (uint)target.Y >= (uint)_world.Height)
        {
            errorCode = "target_out_of_bounds";
            return false;
        }

        var path = AStarPathfinder.FindPath(
            _movement,
            agent!.Position.CellX,
            agent.Position.CellY,
            target.X,
            target.Y,
            MaximumSearchNodes,
            _searchCache,
            _pathfinding);
        if (path.Status != PathfindingStatus.Found)
        {
            errorCode = path.Status == PathfindingStatus.NodeBudgetExceeded
                ? "path_budget_exceeded"
                : "path_not_found";
            return false;
        }

        var start = new BlockCoordinate(agent.Position.CellX, agent.Position.CellY, agent.Position.FeetZ);
        var steps = path.Path
            .Skip(1)
            .Select(static node => new BlockCoordinate(node.X, node.Y, node.FeetZ))
            .ToArray();
        EnsureRevisionAvailable();
        _moveOrders[agentId] = new MoveOrderState(
            commandId,
            target,
            start,
            steps,
            NextStep: 0);
        stepCount = steps.Length;
        _revision++;
        return true;
    }

    public bool CancelMoveOrder(ulong agentId) => _moveOrders.Remove(agentId);

    public IReadOnlyList<MoveOrderStatus> AdvanceMoveOrders()
    {
        if (_moveOrders.Count == 0)
        {
            return [];
        }

        var statuses = new List<MoveOrderStatus>();
        foreach (var agentId in _moveOrders.Keys.ToArray())
        {
            var order = _moveOrders[agentId];
            if (!_agents.TryGet(agentId, out var agent))
            {
                _moveOrders.Remove(agentId);
                statuses.Add(new MoveOrderStatus(
                    agentId,
                    order.CommandId,
                    Completed: false,
                    "agent_not_found",
                    order.Steps.Length - order.NextStep,
                    order.Target));
                continue;
            }

            if (order.NextStep >= order.Steps.Length)
            {
                _moveOrders.Remove(agentId);
                statuses.Add(new MoveOrderStatus(
                    agentId,
                    order.CommandId,
                    Completed: true,
                    null,
                    0,
                    order.Target));
                continue;
            }

            var expected = order.NextStep == 0
                ? order.Start
                : order.Steps[order.NextStep - 1];
            if (agent!.Position.CellX != expected.X ||
                agent.Position.CellY != expected.Y ||
                agent.Position.FeetZ != expected.Z)
            {
                _moveOrders.Remove(agentId);
                statuses.Add(new MoveOrderStatus(
                    agentId,
                    order.CommandId,
                    Completed: false,
                    "path_interrupted",
                    order.Steps.Length - order.NextStep,
                    order.Target));
                continue;
            }

            var next = order.Steps[order.NextStep];
            EnsureRevisionAvailable();
            agent.Position = AgentPosition.AtCell(next.X, next.Y, next.Z);
            _revision++;
            var remaining = order.Steps.Length - (order.NextStep + 1);
            if (remaining == 0)
            {
                _moveOrders.Remove(agentId);
                statuses.Add(new MoveOrderStatus(
                    agentId,
                    order.CommandId,
                    Completed: true,
                    null,
                    0,
                    order.Target));
                continue;
            }

            _moveOrders[agentId] = order with { NextStep = order.NextStep + 1 };
        }

        return statuses;
    }

    public bool TakeGroundItem(
        ulong agentId,
        ulong groundItemId,
        int quantity,
        BlockActionRules rules,
        out string? errorCode)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        errorCode = null;
        if (!_agents.TryGet(agentId, out var agent))
        {
            errorCode = "agent_not_found";
            return false;
        }

        if (!_groundItems.TryGet(groundItemId, out var groundItem) ||
            !_perception.GetPerceivedEntries(agentId).Any(entry =>
                entry.Kind == PerceptionEntityKind.GroundItem &&
                entry.Id == groundItemId))
        {
            errorCode = "source_not_found";
            return false;
        }

        if (!rules.TryGetItem(groundItem.Stack.ItemTypeId, out var itemRule))
        {
            errorCode = "unknown_item";
            return false;
        }

        if (!BlockActionSystem.IsAdjacent(agent!.Position, groundItem.Position))
        {
            errorCode = "not_adjacent";
            return false;
        }

        if (quantity > groundItem.Stack.Quantity)
        {
            errorCode = "item_missing";
            return false;
        }

        if (groundItem.Stack.MaxStackSize != itemRule.MaxStackSize)
        {
            errorCode = "inventory_stack_limit_mismatch";
            return false;
        }

        var stack = groundItem.Stack with { Quantity = quantity };
        if (!agent.Inventory.CanAdd(stack.ItemTypeId, quantity, stack.MaxStackSize))
        {
            errorCode = "capacity_reached";
            return false;
        }

        EnsureRevisionAvailable();
        if (!_groundItems.TryTake(groundItemId, quantity, out var removed) ||
            !agent.Inventory.TryAddStacks([removed]))
        {
            throw new InvalidOperationException("Ground-item pickup changed after successful preflight.");
        }

        _revision++;
        return true;
    }

    public bool IsWithinPerceptionRadius(ulong agentId, BlockCoordinate position)
    {
        if (!_agents.TryGet(agentId, out var agent))
        {
            return false;
        }

        var dx = (long)agent!.Position.CellX - position.X;
        var dy = (long)agent.Position.CellY - position.Y;
        var radius = _perceptionSettings.NearRadius;
        return dx * dx + dy * dy <= (long)radius * radius;
    }

    public BlockActionResult MineBlock(
        ulong agentId,
        BlockActionRules rules,
        MatterLedger ledger,
        ulong runSeed,
        long tick,
        BlockCoordinate target,
        int masteryPermille = 0)
    {
        if (!_agents.TryGet(agentId, out var agent))
        {
            return BlockActionResult.Failure("agent_not_found");
        }

        var result = BlockActionSystem.Mine(
            _world,
            agent!,
            _groundItems,
            rules,
            ledger,
            runSeed,
            tick,
            target,
            _perception.HasPerceivedBlock(agentId, target),
            masteryPermille);
        if (result.Succeeded)
        {
            _revision = checked(_revision + 1);
        }

        return result;
    }

    public BlockActionResult PlaceBlock(
        ulong agentId,
        BlockActionRules rules,
        MatterLedger ledger,
        BlockCoordinate target,
        ushort itemTypeId,
        ushort materialId)
    {
        if (!_agents.TryGet(agentId, out var agent))
        {
            return BlockActionResult.Failure("agent_not_found");
        }

        var result = BlockActionSystem.Place(
            _world,
            agent!,
            rules,
            ledger,
            target,
            itemTypeId,
            materialId,
            _agents,
            IsWithinPerceptionRadius(agentId, target));
        if (result.Succeeded)
        {
            _revision = checked(_revision + 1);
        }

        return result;
    }

    public void NotifyAgentMutation() => _revision = checked(_revision + 1);

    /// <summary>
    /// Compteurs de la fenêtre de perception (PRISM-105/119) : reconstructions, glissades, patchs
    /// et replis sur un saut. Diagnostics seuls : aucun n'est lu par la simulation, donc aucun ne
    /// peut déplacer une trajectoire — c'est ce qui rend « la glissade domine » mesurable au lieu
    /// d'être supposé.
    /// </summary>
    /// <summary>
    /// Tests de rayon exacts effectués au dernier tick, agents et objets séparément (PRISM-141).
    /// Diagnostics seuls, même contrat que <see cref="PerceptionWindowCounters"/>.
    /// </summary>
    public (long Agents, long Items) PerceptionCandidates =>
        (_perception.AgentCandidatesExamined, _perception.ItemCandidatesExamined);

    public (long Builds, long Slides, long Patches, long MoveRebuilds) PerceptionWindowCounters =>
        (_perception.BlockWindowBuilds, _perception.BlockWindowSlides,
            _perception.BlockWindowPatches, _perception.BlockWindowMoveRebuilds);

    /// <summary>
    /// Observes every live agent. <paramref name="tick"/> is only used by the cache-verification
    /// diagnostics (PRISM-108) and by the movement memo's own diagnostic; it never enters the
    /// perception frame.
    /// </summary>
    public AgentPerceptionSnapshot[] ObserveAgents(
        IReadOnlyList<StructureConstructionSite>? constructionSites = null,
        StructureEffectConsumer? structureEffects = null,
        GroupMembership? groupMembership = null,
        long tick = -1)
    {
        _movement.Tick = tick;
        if (_perceptionRules is null)
        {
            return [];
        }

        long setupStamp = PerceptionSystem.AllocProbe
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0;
        var activeAgents = _agents.Agents.ToArray();
        var activeIds = new HashSet<ulong>(activeAgents.Length);
        foreach (var agent in activeAgents)
        {
            activeIds.Add(agent.AgentId);
        }

        foreach (var previousAgentId in _perception.KnownAgents.Where(id => !activeIds.Contains(id)))
        {
            _perception.ForgetAgent(previousAgentId);
        }

        // Reconstruit une fois pour tout le tick : coût linéaire en agents + objets, au lieu d'un
        // parcours complet par agent. Les candidats sont ensuite filtrés par le test de rayon
        // exact, donc l'ensemble des entrées observées est inchangé.
        if (!FullPerceptionScan)
        {
            _perceptionIndex.Rebuild(activeAgents, _groundItems);
        }

        _perception.BeginTick();

        var snapshots = new AgentPerceptionSnapshot[activeAgents.Length];
        if (PerceptionSystem.AllocProbe)
        {
            PerceptionSystem.ProbeSetupTicks += System.Diagnostics.Stopwatch.GetTimestamp() - setupStamp;
        }

        for (var index = 0; index < activeAgents.Length; index++)
        {
            var agent = activeAgents[index];
            StructureUsage? shelter = null;
            long shelterStamp = PerceptionSystem.AllocProbe
                ? System.Diagnostics.Stopwatch.GetTimestamp()
                : 0;
            if (structureEffects is not null)
            {
                shelter = groupMembership is null
                    ? structureEffects.QueryShelter(
                        agent.AgentId,
                        new BlockCoordinate(
                            agent.Position.CellX,
                            agent.Position.CellY,
                            agent.Position.FeetZ))
                    : structureEffects.QueryShelterWithGroups(
                        agent.AgentId,
                        new BlockCoordinate(
                            agent.Position.CellX,
                            agent.Position.CellY,
                            agent.Position.FeetZ),
                        groupMembership);
            }

            if (PerceptionSystem.AllocProbe)
            {
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                PerceptionSystem.ProbeShelterTicks += now - shelterStamp;
                shelterStamp = now;
            }

            var frame = _perception.Observe(
                agent,
                _world,
                _agents,
                _groundItems,
                _perceptionRules,
                _perceptionSettings,
                constructionSites,
                shelter,
                tick,
                FullPerceptionScan ? null : _perceptionIndex);

            if (PerceptionSystem.AllocProbe)
            {
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                PerceptionSystem.ProbeObserveCallTicks += now - shelterStamp;
                shelterStamp = now;
            }

            snapshots[index] = new AgentPerceptionSnapshot(agent.AgentId, frame);
            if (PerceptionSystem.AllocProbe)
            {
                PerceptionSystem.ProbeSnapshotTicks +=
                    System.Diagnostics.Stopwatch.GetTimestamp() - shelterStamp;
            }
        }

        return snapshots;
    }

    /// <summary>
    /// Retourne le champ de distance pour la classe de cible donnee, en le recalculant si le
    /// monde a change depuis le dernier calcul. Retourne null si les champs ne sont pas actives
    /// (<c>PRISM_DISTANCE_FIELDS=1</c>) ou si la classe n'a pas de source.
    /// </summary>
    public DistanceField? GetDistanceField(
        string classKey,
        Func<int, int, bool> isSource,
        Func<int, int, bool> isWalkable,
        int maxDistance = 0)
    {
        if (!DistanceFieldsEnabled || string.IsNullOrEmpty(classKey))
        {
            return null;
        }

        if (_distanceFields.TryGetValue(classKey, out var cached) && cached.Revision == _world.Revision)
        {
            return cached.Field;
        }

        var field = DistanceField.Compute(_world.Width, _world.Height, isSource, isWalkable, maxDistance);
        _distanceFields[classKey] = (_world.Revision, 0, field);
        return field;
    }

    /// <summary>
    /// Construit (ou reutilise) le champ de distance pour la classe donnee, a partir de la liste
    /// des sources perçues. Renvoie null si les champs ne sont pas actives, si la classe est vide
    /// ou s'il n'y a aucune source. Le cache est invalide des que la revision du monde change ou
    /// que l'ensemble des sources change (hash), donc un champ ne survit pas a une ecriture ni a
    /// un changement de perception.
    /// </summary>
    public DistanceField? TryGetDistanceField(string? classKey, IEnumerable<BlockCoordinate> sources)
    {
        if (!DistanceFieldsEnabled || string.IsNullOrEmpty(classKey))
        {
            return null;
        }

        var sourceList = sources as IReadOnlyList<BlockCoordinate> ?? sources.ToArray();
        if (sourceList.Count == 0)
        {
            return null;
        }

        var sourceHash = 0;
        foreach (var source in sourceList)
        {
            sourceHash = HashCode.Combine(sourceHash, source.X, source.Y, source.Z);
        }

        if (_distanceFields.TryGetValue(classKey!, out var cached) &&
            cached.Revision == _world.Revision &&
            cached.SourceHash == sourceHash)
        {
            return cached.Field;
        }

        var field = DistanceField.Compute(
            _world.Width,
            _world.Height,
            (x, y) => sourceList.Any(s => s.X == x && s.Y == y),
            (x, y) => _movement.CanStandAt(x, y, out _));

        _distanceFields[classKey!] = (_world.Revision, sourceHash, field);
        return field;
    }

    /// <summary>Invalide explicitement tous les champs (ex. apres une ecriture hors revision).</summary>
    public void InvalidateDistanceFields() => _distanceFields.Clear();

    public AgentBody Incarnate(ulong agentId, AgentPosition position, AgentBodyProfile profile)
    {
        EnsureRevisionAvailable();
        var agent = _agents.Incarnate(agentId, position, profile, _movement);
        _revision++;
        return agent;
    }

    public bool Retire(ulong agentId)
    {
        if (!_agents.TryGet(agentId, out _))
        {
            return false;
        }

        EnsureRevisionAvailable();
        if (!_agents.Retire(
                agentId,
                _groundItems))
        {
            return false;
        }

        _revision++;
        return true;
    }

    private void EnsureRevisionAvailable()
    {
        if (_revision == long.MaxValue)
        {
            throw new InvalidOperationException("The agent lifecycle revision space is exhausted.");
        }
    }

    public IReadOnlyList<AgentSpoilage> AdvanceFreshness(
        long tick,
        int decayPermillePerTick,
        Accounting.MatterLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        var spoiled = new List<AgentSpoilage>();
        foreach (var agent in _agents.Agents)
        {
            foreach (var spoilage in agent.Inventory.ApplyDecay(tick, decayPermillePerTick, ledger))
            {
                spoiled.Add(new AgentSpoilage(agent.AgentId, spoilage));
            }
        }

        return spoiled;
    }

    public byte[] CaptureState()
    {
        var rules = _movement.Rules;
        var checkpoint = new AgentLifecycleCheckpoint(
            CheckpointVersion,
            rules.SolidMaterials.Order().ToArray(),
            rules.LiquidMaterials.Order().ToArray(),
            rules.MaximumAscent,
            rules.MaximumDescent,
            rules.PathMaterials.Order().ToArray(),
            rules.PathSpeedupPermille,
            _revision,
            _agents.Agents
                .Select(agent => new AgentCheckpoint(
                    agent.AgentId,
                    agent.Position,
                    agent.Profile,
                    agent.Inventory.CaptureSlots()))
                .ToArray(),
            _groundItems.Snapshot().ToArray(),
            _groundItems.NextId,
            _perceptionRules?.AirMaterialId,
            _perceptionRules?.SnapshotMaterials()
                .Select(static entry => new PerceptionMaterialCheckpoint(entry.Key, entry.Value))
                .ToArray(),
            _perception.CaptureState(),
            _moveOrders
                .Select(static entry => new MoveOrderCheckpoint(
                    entry.Key,
                    entry.Value.CommandId,
                    entry.Value.Target,
                    entry.Value.Start,
                    entry.Value.Steps,
                    entry.Value.NextStep))
                .ToArray());
        return JsonSerializer.SerializeToUtf8Bytes(checkpoint, CheckpointJsonOptions);
    }

    public void RestoreState(ReadOnlySpan<byte> checkpointBytes)
    {
        if (_agents.Count != 0 || _groundItems.Count != 0 || _moveOrders.Count != 0 || _revision != 0)
        {
            throw new InvalidOperationException("Agent lifecycle state can only be restored into a new system.");
        }

        var checkpoint = JsonSerializer.Deserialize<AgentLifecycleCheckpoint>(
                             checkpointBytes,
                             CheckpointJsonOptions)
                         ?? throw new InvalidDataException("Agent lifecycle checkpoint is empty.");
        if (checkpoint.Version is not (1 or CheckpointVersion) ||
            checkpoint.SolidMaterials is null ||
            checkpoint.LiquidMaterials is null ||
            checkpoint.PathMaterials is null ||
            checkpoint.Agents is null ||
            checkpoint.GroundItems is null ||
            checkpoint.Revision < 0)
        {
            throw new InvalidDataException("Agent lifecycle checkpoint version or contents are invalid.");
        }

        MovementRules rules;
        try
        {
            rules = new MovementRules(
                checkpoint.SolidMaterials,
                checkpoint.LiquidMaterials,
                checkpoint.MaximumAscent,
                checkpoint.MaximumDescent,
                checkpoint.PathMaterials,
                checkpoint.PathSpeedupPermille);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Agent lifecycle checkpoint movement rules are invalid.", exception);
        }

        var movement = new MovementGraph(
            _world, rules, () => _structureEffects?.GetActiveEffects() ?? [], _verifyCaches);
        var agents = new AgentRegistry();
        foreach (var entry in checkpoint.Agents)
        {
            if (entry is null || entry.Profile is null || entry.InventorySlots is null)
            {
                throw new InvalidDataException("Agent lifecycle checkpoint contains an invalid agent.");
            }

            AgentBody agent;
            try
            {
                entry.Profile.Validate();
                agent = agents.Incarnate(entry.AgentId, entry.Position, entry.Profile, movement);
            }
            catch (Exception exception) when (exception is AgentLifecycleException or ArgumentException)
            {
                throw new InvalidDataException(
                    $"Agent lifecycle checkpoint contains invalid agent '{entry.AgentId}'.",
                    exception);
            }

            agent.Inventory.RestoreSlots(entry.InventorySlots);
        }

        var groundItems = new GroundItemStore();
        groundItems.Restore(checkpoint.GroundItems, checkpoint.NextGroundItemId);
        if (checkpoint.GroundItems.Any(item =>
                (uint)item.Position.X >= (uint)_world.Width ||
                (uint)item.Position.Y >= (uint)_world.Height ||
                (uint)item.Position.Z >= (uint)_world.VerticalSize))
        {
            throw new InvalidDataException("Agent lifecycle checkpoint contains ground items outside the world.");
        }

        PerceptionRules? perceptionRules = null;
        if (checkpoint.PerceptionAirMaterialId is { } airMaterialId ||
            checkpoint.PerceptionMaterials is not null)
        {
            if (checkpoint.PerceptionAirMaterialId is not { } validAirMaterialId ||
                checkpoint.PerceptionMaterials is null)
            {
                throw new InvalidDataException("Agent lifecycle checkpoint perception rules are incomplete.");
            }

            var materials = new Dictionary<ushort, PerceptionMaterialRule>();
            foreach (var material in checkpoint.PerceptionMaterials)
            {
                if (material is null || !materials.TryAdd(material.MaterialId, material.Rule))
                {
                    throw new InvalidDataException("Agent lifecycle checkpoint perception rules are invalid.");
                }
            }

            try
            {
                perceptionRules = new PerceptionRules(materials, validAirMaterialId);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Agent lifecycle checkpoint perception rules are invalid.", exception);
            }
        }

        if (checkpoint.PerceptionState is not null)
        {
            _perception.RestoreState(checkpoint.PerceptionState);
        }

        if (checkpoint.MoveOrders is not null)
        {
            foreach (var order in checkpoint.MoveOrders)
            {
                if (order is null ||
                    string.IsNullOrWhiteSpace(order.CommandId) ||
                    order.Steps is null ||
                    order.NextStep < 0 ||
                    order.NextStep > order.Steps.Length ||
                    !agents.TryGet(order.AgentId, out _) ||
                    !_moveOrders.TryAdd(order.AgentId, new MoveOrderState(
                        order.CommandId,
                        order.Target,
                        order.Start,
                        order.Steps,
                        order.NextStep)))
                {
                    throw new InvalidDataException(
                        "Agent lifecycle checkpoint contains an invalid move order.");
                }
            }
        }

        _movement = movement;
        _agents = agents;
        _groundItems = groundItems;
        _perceptionRules = perceptionRules;
        _revision = checkpoint.Revision;
    }

    private sealed record AgentLifecycleCheckpoint(
        int Version,
        ushort[] SolidMaterials,
        ushort[] LiquidMaterials,
        int MaximumAscent,
        int MaximumDescent,
        ushort[] PathMaterials,
        int PathSpeedupPermille,
        long Revision,
        AgentCheckpoint[] Agents,
        GroundItem[] GroundItems,
        ulong NextGroundItemId,
        ushort? PerceptionAirMaterialId = null,
        PerceptionMaterialCheckpoint[]? PerceptionMaterials = null,
        byte[]? PerceptionState = null,
        MoveOrderCheckpoint[]? MoveOrders = null);

    private sealed record MoveOrderCheckpoint(
        ulong AgentId,
        string CommandId,
        BlockCoordinate Target,
        BlockCoordinate Start,
        BlockCoordinate[] Steps,
        int NextStep);

    private sealed record MoveOrderState(
        string CommandId,
        BlockCoordinate Target,
        BlockCoordinate Start,
        BlockCoordinate[] Steps,
        int NextStep);

    private sealed record AgentCheckpoint(
        ulong AgentId,
        AgentPosition Position,
        AgentBodyProfile Profile,
        AgentItemStack?[] InventorySlots);

    private sealed record PerceptionMaterialCheckpoint(
        ushort MaterialId,
        PerceptionMaterialRule Rule);
}
