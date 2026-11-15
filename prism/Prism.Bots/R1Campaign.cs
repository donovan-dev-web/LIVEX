using Prism.Content;
using Prism.Core.Accounting;
using Prism.Core.World;
using Prism.Host.Hosting;

namespace Prism.Bots;

/// <summary>
/// A place a chain may start from. <paramref name="Anchor"/> is the tree the cell stands next
/// to, when the site was chosen for its wood: two agents must never be handed two cells of the
/// same tree, or the second one answers <c>block_already_mined</c> instead of playing its chain.
/// A tools site is anchored on nothing (it is chosen for stone and exposed ore).
/// </summary>
public sealed record BotSpawnSite(BotCoordinate Cell, BlockCoordinate? Anchor);

/// <summary>One planned step of a chain, as the planner reported it.</summary>
public sealed record R1AgentOutcome(
    ulong AgentId,
    BotCoordinate Spawn,
    string Chain,
    string GoalItem,
    int Quantity,
    int PlanSteps,
    IReadOnlyList<string> RejectedSteps,
    bool PlanPlayed)
{
    /// <summary>
    /// A chain is done when its whole plan was played and no step was refused or skipped at its
    /// deadline. "Plan traversed" alone would count a chain whose steps were all refused, which
    /// is exactly the confusion the campaign reports had before.
    /// </summary>
    public bool Completed => PlanPlayed && RejectedSteps.Count == 0;
}

/// <summary>
/// Outcome of a run R1 pass (PRISM-102): the ordinary bot run plus, per agent, the chain it was
/// asked to play and the steps the world refused, and the matter conservation audit taken after
/// the last tick. <see cref="ConservationGreen"/> is the gate verdict.
/// </summary>
public sealed record R1RunResult(
    ulong Seed,
    long Ticks,
    BotRunResult Run,
    IReadOnlyList<R1AgentOutcome> Agents,
    IReadOnlyList<MatterAuditViolation> Violations)
{
    public bool ConservationGreen => Violations.Count == 0;
}

/// <summary>
/// Run R1 campaign (PRISM-102): a population of planner bots, each assigned one of the five
/// production chains, played through the ordinary contract against the real host, followed by the
/// matter conservation audit. This is the executable form of the G4 gate; the CLI exposes it and
/// the reduced automated test walks the same code path.
/// </summary>
public static class R1Campaign
{
    /// <summary>The five production chains and the item that proves each one was played.</summary>
    public static readonly IReadOnlyList<(string Chain, string GoalItem, int Quantity)> Chains =
    [
        ("tools", "iron_pickaxe", 1),
        ("agriculture", "wheat", 3),
        ("water", "wooden_bucket", 1),
        ("cooking", "flatbread", 1),
        ("storage", "chest", 1),
    ];

    /// <summary>Chain assigned to the agent at <paramref name="index"/> (deterministic round-robin).</summary>
    public static (string Chain, string GoalItem, int Quantity) ChainFor(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return Chains[index % Chains.Count];
    }

    /// <summary>
    /// Goal items a chain has to produce, in order. The tool chain is a ladder: a stone pickaxe
    /// is the only way to mine the ore an iron pickaxe needs, so the plan has to craft both.
    /// </summary>
    public static IReadOnlyList<(string ItemKey, int Quantity)> GoalsFor(string chain)
    {
        if (StringComparer.Ordinal.Equals(chain, "tools"))
        {
            return [("stone_pickaxe", 1), ("iron_pickaxe", 1)];
        }

        foreach (var entry in Chains)
        {
            if (StringComparer.Ordinal.Equals(entry.Chain, chain))
            {
                return [(entry.GoalItem, entry.Quantity)];
            }
        }

        throw new ArgumentException($"Unknown chain '{chain}'.", nameof(chain));
    }

    /// <summary>The spawn point a chain starts from, chosen for what the chain needs to reach.</summary>
    public static BotCoordinate ChooseSpawnPointFor(
        GeneratedWorldSnapshot world,
        string chain,
        int index,
        BotContentMap content)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(content);
        if (StringComparer.Ordinal.Equals(chain, "tools"))
        {
            var stoneSpawns = ChooseStoneSpawnPoints(world, content);
            if (stoneSpawns.Count > 0)
            {
                return stoneSpawns[index % stoneSpawns.Count];
            }
        }

        if (StringComparer.Ordinal.Equals(chain, "agriculture") ||
            StringComparer.Ordinal.Equals(chain, "cooking"))
        {
            var farmSpawns = ChooseFarmSpawnPoints(world, content);
            if (farmSpawns.Count > 0)
            {
                return farmSpawns[index % farmSpawns.Count];
            }
        }

        var treeSpawns = ChooseSpawnPoints(world, Math.Max(1, index + 1));
        return treeSpawns[index % treeSpawns.Count];
    }

    /// <summary>
    /// Standing cells on exposed stone that still have a tree in reach, ordered deterministically.
    /// The minimal pack's coast is sand at the water line and its rock sits on a plateau: a bot on
    /// the beach can never mine stone, and one on bare rock could never build a workbench.
    /// </summary>
    public static IReadOnlyList<BotCoordinate> ChooseStoneSpawnPoints(
        GeneratedWorldSnapshot world,
        BotContentMap content) =>
        RankedStoneSpawnSites(world, content).Select(static site => site.Cell).ToArray();

    /// <summary>
    /// The same scan as <see cref="ChooseStoneSpawnPoints"/>, keeping the ore and log figures the
    /// ordering uses: a campaign report can then say why a site was picked instead of guessing.
    /// </summary>
    public static IReadOnlyList<(BotCoordinate Cell, int Ore, int Logs)> RankedStoneSpawnSites(
        GeneratedWorldSnapshot world,
        BotContentMap content)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(content);
        var stoneId = content.MaterialId("stone");
        var ironBlocks = CollectExposedIron(world, content.MaterialId("iron_ore"));
        var points = new List<(BotCoordinate Cell, int Ore, int Logs)>(world.Width * world.Height / 8);
        var trees = world.Trees.ToArray();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var column = world.SurfaceColumns[(y * world.Width) + x];
                if (column.MaterialId != stoneId)
                {
                    continue;
                }

                var hasTree = false;
                foreach (var tree in trees)
                {
                    if (tree.Origin.Z == column.SurfaceZ + 1 &&
                        Math.Abs(tree.Origin.X - x) + Math.Abs(tree.Origin.Y - y) <= IronTreeRadius)
                    {
                        hasTree = true;
                        break;
                    }
                }

                if (!hasTree)
                {
                    continue;
                }

                var cell = new BotCoordinate(x, y, column.SurfaceZ + 1);
                points.Add((cell, CountIronWithin(ironBlocks, cell), CountLogsWithin(trees, cell)));
            }
        }

        // A tools chain that starts where it cannot see exposed ore asks the world for chunks it
        // will never perceive (`source_not_found` for the rest of the run): the whole map holds
        // thousands of open veins, but a single site holds a handful of faces. Ranked rather than
        // filtered, because no cell of seed 1 both stands on stone, sees three open faces and has
        // two trees in the plain radius — a filter returned nothing and silently fell back to the
        // plain spawn, which is exactly the site that blocks the chain.
        //
        // Wood is selected before ore since the ladder needs six logs (planks and fuel): the most
        // ore-bearing site held two logs and the chain stalled on its second plank craft. The
        // tiers are explicit so the choice is readable in a report: a wood-sufficient site first,
        // then a four-log site, then a two-log one, then any site that at least sees ore.
        var ranked = new List<(BotCoordinate Cell, int Ore, int Logs)>(points.Count);
        AddWoodTier(LogsForToolLadder);
        AddWoodTier(4);
        AddWoodTier(2);
        AddWoodTier(0);
        return ranked;

        void AddWoodTier(int minimumLogs)
        {
            foreach (var item in points
                         .Where(item => item.Ore > 0 && item.Logs >= minimumLogs)
                         .OrderByDescending(static item => item.Ore)
                         .ThenByDescending(static item => item.Logs)
                         .ThenBy(static item => item.Cell.X)
                         .ThenBy(static item => item.Cell.Y))
            {
                if (ranked.Contains(item))
                {
                    continue;
                }

                ranked.Add(item);
            }
        }
    }

    /// <summary>
    /// Tools spawns that carry their own wood: a tree of the agent's terrace with the rest of the
    /// grove inside the perception radius (six reachable logs, the ladder's measured need), stone
    /// close enough to walk to and exposed ore inside the obtain hint. Seed 1 has **no** stone cell
    /// that both sees ore and stands near three trees — the 55 ore-facing stone cells hold two log
    /// each — so the ladder has to start in the grove beside the rock instead of on the rock.
    /// Sites keep their tree as anchor and are ordered by ore distance, then stone, then cell.
    /// </summary>
    public static IReadOnlyList<BotSpawnSite> ChooseWoodFirstToolsSites(
        GeneratedWorldSnapshot world,
        BotContentMap content)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(content);
        var stoneId = content.MaterialId("stone");
        var ironBlocks = CollectExposedIron(world, content.MaterialId("iron_ore"));
        // A face the agent can see is not necessarily a face it can stand next to. The whole
        // south cliff of seed 1 is coated in iron and every block of the upper face is out of the
        // 3x3x3 reach cube; ranking on the raw faces sent the ladder to a site whose ore was
        // visible and unmineable, and the obtain exhausted the frame with `source_not_found`.
        // Keep the faces that have an approach cell the action rule accepts.
        var reachableIron = ReachableIronBlocks(world, content, ironBlocks);
        // A pack that exposes no reachable face at all still gets sites: fall back to the raw
        // list rather than leaving the ladder with only the stone fallback.
        var siteOre = reachableIron.Count > 0 ? reachableIron : ironBlocks;
        var stoneColumns = new List<(int X, int Y, int Z)>();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var column = world.SurfaceColumns[(y * world.Width) + x];
                if (column.MaterialId == stoneId)
                {
                    stoneColumns.Add((x, y, column.SurfaceZ));
                }
            }
        }

        // Ore that the site can only *see* is not ore the ladder can mine: the whole south cliff of
        // seed 1 is an iron wall standing across a ravine, so a spawn a few cells away counted its
        // faces as sources and the obtain then walked at a face it could not reach, one `Move` per
        // tick, until the frame ran out (`source_not_found`, 0 chunk in hand at T+2414). A source
        // counts only when its approach cell is in the same walkable component as the spawn.
        var blocked = BlockedCells(world, content);
        var waterId = content.MaterialId("water");
        var sites = new List<(BotSpawnSite Site, int Ore, int Stone, bool OreReachable)>();
        foreach (var tree in world.Trees
                     .OrderBy(static tree => tree.Origin.X)
                     .ThenBy(static tree => tree.Origin.Y)
                     .ThenBy(static tree => tree.Origin.Z))
        {
            foreach (var (dx, dy) in TreeNeighbourOffsets)
            {
                var x = tree.Origin.X + dx;
                var y = tree.Origin.Y + dy;
                if (x < 0 || y < 0 || x >= world.Width || y >= world.Height ||
                    world.SurfaceColumns[(y * world.Width) + x].SurfaceZ != tree.Origin.Z - 1)
                {
                    continue;
                }

                var cell = new BotCoordinate(x, y, tree.Origin.Z);
                if (CountLogsWithin(world.Trees.ToArray(), cell) < LogsForToolLadder)
                {
                    continue;
                }

                var stone = NearestDistance(stoneColumns, cell);
                if (stone > ToolsStoneRadius)
                {
                    continue;
                }

                var ore = NearestDistance(siteOre, cell);
                if (ore > ToolsOreReach ||
                    CountIronWithin(siteOre, cell, ToolsOreReach) < IronChunksForPickaxe)
                {
                    continue;
                }

                var walkable = ReachableStands(world, waterId, blocked, cell);
                var reachableOre = CountReachableIron(siteOre, walkable);
                sites.Add((new BotSpawnSite(cell, tree.Origin), ore, stone,
                    reachableOre >= IronChunksForPickaxe));
            }
        }

        if (Environment.GetEnvironmentVariable("PRISM_DEBUG_BOT") == "1")
        {
            foreach (var site in sites.OrderByDescending(static site => site.OreReachable).Take(4))
            {
                Console.Error.WriteLine(
                    $"[plan-site] tools {site.Site.Cell} anchor={site.Site.Anchor} oreD={site.Ore} stoneD={site.Stone} oreReachable={site.OreReachable}");
            }
        }

        // Reachable sites win outright; a pack whose every grove is cut off from its rock keeps the
        // old ranking rather than leaving the ladder without a spawn.
        var ordered = sites
            .OrderByDescending(static site => site.OreReachable)
            .ThenBy(static site => site.Ore)
            .ThenBy(static site => site.Stone)
            .ThenBy(static site => site.Site.Cell.X)
            .ThenBy(static site => site.Site.Cell.Y)
            .Select(static site => site.Site)
            .ToArray();
        return ordered;
    }

    /// <summary>
    /// Logs a site can draw from, counting the trees of its own terrace inside the perception near
    /// radius. Two logs per tree are reachable from the standing cell (the base and the trunk
    /// block above it), so the count is a lower bound on what the macro-action can actually mine
    /// here.
    /// <para>
    /// The height band matters: a first version counted every tree in the radius and still picked
    /// the cliff site, whose wood grows two blocks above the agent. The macro then emitted a
    /// <c>Move</c> towards the log's own cell — never walkable — and the obtain died on
    /// <c>source_not_found</c> at T+252 exactly as before. A tree one block above the feet is a
    /// step, not a climb, so it still counts.
    /// </para>
    /// </summary>
    private static int CountLogsWithin(PlacedTree[] trees, BotCoordinate cell)
    {
        var logs = 0;
        foreach (var tree in trees)
        {
            if (Math.Abs(tree.Origin.Z - cell.Z) > 1)
            {
                continue;
            }

            if (Math.Abs(tree.Origin.X - cell.X) + Math.Abs(tree.Origin.Y - cell.Y) <= ToolsWoodRadius)
            {
                logs += LogsPerTree;
            }
        }

        return logs;
    }

    private static int CountIronWithin(List<(int X, int Y, int Z)> blocks, BotCoordinate cell)
    {
        return CountIronWithin(blocks, cell, IronSearchRadius);
    }

    private static int CountIronWithin(
        List<(int X, int Y, int Z)> blocks,
        BotCoordinate cell,
        int radius)
    {
        var count = 0;
        foreach (var (x, y, z) in blocks)
        {
            if (Math.Abs(x - cell.X) + Math.Abs(y - cell.Y) + Math.Abs(z - cell.Z) <= radius)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Ore blocks open to the air: at their column's surface, or on a cliff face whose neighbouring
    /// column stands lower. Exactly the blocks the perception frame now publishes.
    /// </summary>
    private static List<(int X, int Y, int Z)> CollectExposedIron(
        GeneratedWorldSnapshot world,
        ushort ironId)
    {
        var blocks = new List<(int X, int Y, int Z)>();
        foreach (var entry in world.Overrides)
        {
            if (entry.Value.IsAir || entry.Value.MaterialId != ironId)
            {
                continue;
            }

            var coordinate = entry.Key;
            var own = world.SurfaceColumns[(coordinate.Y * world.Width) + coordinate.X];
            var open = coordinate.Z > own.SurfaceZ;
            if (!open)
            {
                foreach (var (offsetX, offsetY) in TreeNeighbourOffsets)
                {
                    var neighbourX = coordinate.X + offsetX;
                    var neighbourY = coordinate.Y + offsetY;
                    if ((uint)neighbourX < (uint)world.Width &&
                        (uint)neighbourY < (uint)world.Height &&
                        world.SurfaceColumns[(neighbourY * world.Width) + neighbourX].SurfaceZ < coordinate.Z)
                    {
                        open = true;
                        break;
                    }
                }
            }

            if (open)
            {
                blocks.Add((coordinate.X, coordinate.Y, coordinate.Z));
            }
        }

        return blocks;
    }

    /// <summary>
    /// Exposed ore blocks an agent standing beside them could actually mine. The action's reach is
    /// a 3x3x3 cube centred on the feet (<c>BlockActionSystem.IsAdjacent</c>), so a neighbouring
    /// column works when its feet are at the ore's level, one below (ore at head level) or one
    /// above. The block above a tree base and the lower edge of a cliff face are exactly the
    /// head-level case: a site ranked on raw faces sent the ladder to a wall whose ore was visible
    /// and out of the cube, and the obtain exhausted the frame with <c>source_not_found</c>.
    /// </summary>
    private static List<(int X, int Y, int Z)> ReachableIronBlocks(
        GeneratedWorldSnapshot world,
        BotContentMap content,
        List<(int X, int Y, int Z)> blocks)
    {
        var waterId = content.MaterialId("water");
        var blocked = BlockedCells(world, content);

        var reachable = new List<(int X, int Y, int Z)>(blocks.Count);
        foreach (var block in blocks)
        {
            if (HasApproachCell(world, waterId, blocked, block))
            {
                reachable.Add(block);
            }
        }

        return reachable;
    }

    private static bool HasApproachCell(
        GeneratedWorldSnapshot world,
        ushort waterId,
        HashSet<(int X, int Y, int Z)> blocked,
        (int X, int Y, int Z) block)
    {
        foreach (var (dx, dy) in TreeNeighbourOffsets)
        {
            var x = block.X + dx;
            var y = block.Y + dy;
            if ((uint)x >= (uint)world.Width || (uint)y >= (uint)world.Height)
            {
                continue;
            }

            var column = world.SurfaceColumns[(y * world.Width) + x];
            if (column.MaterialId == waterId)
            {
                continue;
            }

            var feet = column.SurfaceZ + 1;
            if (feet < block.Z - 1 || feet > block.Z + 1)
            {
                continue;
            }

            if (blocked.Contains((x, y, feet)) || blocked.Contains((x, y, feet + 1)))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>Every non-air cell an agent cannot walk through: solids and liquids.</summary>
    private static HashSet<(int X, int Y, int Z)> BlockedCells(
        GeneratedWorldSnapshot world,
        BotContentMap content)
    {
        var waterId = content.MaterialId("water");
        var blocked = new HashSet<(int X, int Y, int Z)>();
        foreach (var entry in world.Overrides)
        {
            if (!entry.Value.IsAir &&
                (entry.Value.MaterialId == waterId || content.IsSolidMaterial(entry.Value.MaterialId)))
            {
                blocked.Add((entry.Key.X, entry.Key.Y, entry.Key.Z));
            }
        }

        return blocked;
    }

    /// <summary>
    /// Standing cells connected to <paramref name="start"/> by the movement rules the host uses:
    /// cardinal steps only (diagonals always come with one of the two cardinals), one level up,
    /// three down, never through a liquid or a solid. This is what "the agent can walk there"
    /// means, and what <see cref="CountReachableIron"/> asks before counting an ore face as a
    /// source of the ladder.
    /// </summary>
    private static HashSet<(int X, int Y, int FeetZ)> ReachableStands(
        GeneratedWorldSnapshot world,
        ushort waterId,
        HashSet<(int X, int Y, int Z)> blocked,
        BotCoordinate start)
    {
        var stands = new HashSet<(int X, int Y, int FeetZ)>();
        var startColumn = world.SurfaceColumns[(start.Y * world.Width) + start.X];
        var queue = new Queue<(int X, int Y, int FeetZ)>();
        queue.Enqueue((start.X, start.Y, startColumn.SurfaceZ + 1));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!stands.Add(current))
            {
                continue;
            }

            foreach (var (dx, dy) in TreeNeighbourOffsets)
            {
                var x = current.X + dx;
                var y = current.Y + dy;
                if ((uint)x >= (uint)world.Width || (uint)y >= (uint)world.Height)
                {
                    continue;
                }

                var column = world.SurfaceColumns[(y * world.Width) + x];
                if (column.MaterialId == waterId)
                {
                    continue;
                }

                var feet = column.SurfaceZ + 1;
                if (feet - current.FeetZ > 1 || current.FeetZ - feet > 3)
                {
                    continue;
                }

                if (blocked.Contains((x, y, feet)) || blocked.Contains((x, y, feet + 1)))
                {
                    continue;
                }

                queue.Enqueue((x, y, feet));
            }
        }

        return stands;
    }

    /// <summary>Ore faces whose approach cell is one the agent can actually walk to.</summary>
    private static int CountReachableIron(
        List<(int X, int Y, int Z)> blocks,
        HashSet<(int X, int Y, int FeetZ)> stands)
    {
        var count = 0;
        foreach (var (x, y, z) in blocks)
        {
            foreach (var (dx, dy) in TreeNeighbourOffsets)
            {
                var stand = (x + dx, y + dy);
                if (stands.Contains((stand.Item1, stand.Item2, z - 1)) ||
                    stands.Contains((stand.Item1, stand.Item2, z)) ||
                    stands.Contains((stand.Item1, stand.Item2, z + 1)))
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    private const int TreeSearchRadius = 10;

    /// <summary>How far a tools spawn may look for the exposed iron its pickaxe needs.</summary>
    private const int IronSearchRadius = 10;

    /// <summary>
    /// How far a tools spawn may look for its trees: the chain needs planks, a handle and three
    /// logs of fuel, and the iron-bearing rock of seed 1 is bare.
    /// </summary>
    private const int IronTreeRadius = 14;

    /// <summary>How far a farm spawn may look for the stone its hoe head needs.</summary>
    private const int FarmStoneRadius = 12;

    /// <summary>How far a farm spawn may look for a grass column the crop can drink beside.</summary>
    private const int FarmSoilRadius = 12;

    /// <summary>Logs a tree of this pack yields inside the 3×3×3 reach around its base.</summary>
    private const int LogsPerTree = 2;

    /// <summary>How far a tools spawn looks for wood, matching the pack's perception near radius.</summary>
    private const int ToolsWoodRadius = 16;

    /// <summary>How far a wood-first tools spawn may walk to reach the stone its hoe and furnace need.</summary>
    private const int ToolsStoneRadius = 16;

    /// <summary>How far a wood-first tools spawn may be from exposed ore (the obtain hint radius).</summary>
    private const int ToolsOreReach = 24;

    /// <summary>
    /// Exposed ore blocks a site must carry inside the obtain hint: the iron pickaxe consumes
    /// three ingots, one chunk each, and a source is used once.
    /// </summary>
    private const int IronChunksForPickaxe = 3;

    /// <summary>
    /// Logs the tool ladder consumes: two logs of planks (the workbench and the two handles) and
    /// three logs burned as fuel by the three smelts, plus one spare. This is the measured need,
    /// not a guess: a site holding three logs spent them on the workbench and the plan then asked
    /// the world for a log it could not see (`source_not_found` at T+252), so the chain stopped
    /// halfway up the ladder whatever the planner did.
    /// </summary>
    private const int LogsForToolLadder = 6;

    /// <summary>
    /// Minimum Manhattan distance between two campaign spawns. Separate cells are not enough:
    /// two chains that start around one cluster contest the same tree, the same stone and the
    /// same water.
    /// </summary>
    public const int MinimumSpawnSeparation = 8;

    /// <summary>
    /// Standing cells next to a tree that also have stone and irrigable soil within reach. A farm
    /// chain needs all three in one perception frame: wood for the tool handle and the workbench,
    /// stone for the hoe head (no block produces one), and a grass column with water inside the
    /// crop's four-block irrigation radius. Started on an inland plain, the bot cannot obtain a
    /// single stone and every stone-head craft is refused; that measures the site, not the chain.
    /// Each site carries the tree it stands beside as its anchor: agriculture and cooking are two
    /// agents of the same campaign, and giving them two cells of one tree made the second one
    /// answer <c>block_already_mined</c> at tick 36 instead of playing its chain.
    /// Points are ordered deterministically by coordinate.
    /// </summary>
    public static IReadOnlyList<BotSpawnSite> ChooseFarmSpawnSites(
        GeneratedWorldSnapshot world,
        BotContentMap content)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(content);
        var grassId = content.MaterialId("grass");
        var stoneId = content.MaterialId("stone");
        var waterId = content.MaterialId("water");

        // The crop rule measures irrigation horizontally, any height (CropGrowthSystem), so the
        // water is indexed by column rather than by block.
        var waterColumns = new HashSet<(int X, int Y)>();
        foreach (var entry in world.Overrides)
        {
            if (!entry.Value.IsAir && entry.Value.MaterialId == waterId)
            {
                waterColumns.Add((entry.Key.X, entry.Key.Y));
            }
        }

        var stoneColumns = new List<(int X, int Y, int Z)>();
        var irrigatedParcels = new List<(int X, int Y, int Z)>();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var column = world.SurfaceColumns[(y * world.Width) + x];
                if (column.MaterialId == stoneId)
                {
                    stoneColumns.Add((x, y, column.SurfaceZ));
                }
                else if (column.MaterialId == grassId &&
                         HasWaterWithin(waterColumns, x, y, WaterWithinBlocks))
                {
                    // The air cell above the soil is what Cultiver sows into.
                    irrigatedParcels.Add((x, y, column.SurfaceZ + 1));
                }
            }
        }

        var sites = new List<BotSpawnSite>();
        foreach (var tree in world.Trees
                     .OrderBy(static tree => tree.Origin.X)
                     .ThenBy(static tree => tree.Origin.Y)
                     .ThenBy(static tree => tree.Origin.Z))
        {
            foreach (var (dx, dy) in TreeNeighbourOffsets)
            {
                var x = tree.Origin.X + dx;
                var y = tree.Origin.Y + dy;
                if (x < 0 || y < 0 || x >= world.Width || y >= world.Height ||
                    world.SurfaceColumns[(y * world.Width) + x].SurfaceZ != tree.Origin.Z - 1)
                {
                    continue;
                }

                var cell = new BotCoordinate(x, y, tree.Origin.Z);
                if (NearestDistance(stoneColumns, cell) > FarmStoneRadius ||
                    NearestDistance(irrigatedParcels, cell) > FarmSoilRadius)
                {
                    continue;
                }

                sites.Add(new BotSpawnSite(cell, tree.Origin));
            }
        }

        return sites;
    }

    /// <summary>Farm spawn cells only, in the same deterministic order.</summary>
    public static IReadOnlyList<BotCoordinate> ChooseFarmSpawnPoints(
        GeneratedWorldSnapshot world,
        BotContentMap content) =>
        ChooseFarmSpawnSites(world, content).Select(static site => site.Cell).ToArray();

    /// <summary>Standing cells next to each tree, one per tree, anchored on that tree.</summary>
    public static IReadOnlyList<BotSpawnSite> ChooseTreeSpawnSites(GeneratedWorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var sites = new List<BotSpawnSite>();
        foreach (var tree in world.Trees
                     .OrderBy(static tree => tree.Origin.X)
                     .ThenBy(static tree => tree.Origin.Y)
                     .ThenBy(static tree => tree.Origin.Z))
        {
            if (FindStandingCell(world, tree.Origin) is { } cell)
            {
                sites.Add(new BotSpawnSite(cell, tree.Origin));
            }
        }

        return sites;
    }

    /// <summary>
    /// Spawn candidates of a chain, in the order the campaign tries them: the tool ladder on
    /// exposed stone ranked by wood then ore, the farm chains beside a tree with stone and water
    /// in reach, everything else beside a tree.
    /// </summary>
    public static IReadOnlyList<BotSpawnSite> CandidateSpawnPointsFor(
        GeneratedWorldSnapshot world,
        string chain,
        BotContentMap content)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(content);
        if (StringComparer.Ordinal.Equals(chain, "tools"))
        {
            // Wood first: the ladder needs six logs and no ore-facing stone cell of seed 1 offers
            // them. The plain stone sites stay as a fallback for a pack where the grove is absent.
            return ChooseWoodFirstToolsSites(world, content)
                .Concat(ChooseStoneSpawnPoints(world, content)
                    .Select(static cell => new BotSpawnSite(cell, null)))
                .ToArray();
        }

        if (StringComparer.Ordinal.Equals(chain, "agriculture") ||
            StringComparer.Ordinal.Equals(chain, "cooking"))
        {
            return ChooseFarmSpawnSites(world, content);
        }

        return ChooseTreeSpawnSites(world);
    }

    /// <summary>
    /// First candidate that shares neither a cell nor a tree with a spawn already taken, and that
    /// stands <see cref="MinimumSpawnSeparation"/> blocks away from all of them. Falls back to the
    /// first tree-distinct candidate when the world has no room for that spacing, and to nothing
    /// when every tree is taken; the caller then uses the chain's plain spawn.
    /// </summary>
    private static BotSpawnSite? ChooseDistinctSpawn(
        IReadOnlyList<BotSpawnSite> candidates,
        IReadOnlyList<BotSpawnSite> taken)
    {
        BotSpawnSite? fallback = null;
        foreach (var candidate in candidates)
        {
            if (SharesTreeOrCell(candidate, taken))
            {
                continue;
            }

            fallback ??= candidate;
            var spaced = true;
            foreach (var used in taken)
            {
                if (Math.Abs(used.Cell.X - candidate.Cell.X) +
                    Math.Abs(used.Cell.Y - candidate.Cell.Y) < MinimumSpawnSeparation)
                {
                    spaced = false;
                    break;
                }
            }

            if (spaced)
            {
                return candidate;
            }
        }

        return fallback;
    }

    private static bool SharesTreeOrCell(BotSpawnSite candidate, IReadOnlyList<BotSpawnSite> taken)
    {
        foreach (var used in taken)
        {
            if (used.Cell.X == candidate.Cell.X &&
                used.Cell.Y == candidate.Cell.Y &&
                used.Cell.Z == candidate.Cell.Z)
            {
                return true;
            }

            if (candidate.Anchor is { } anchor &&
                used.Anchor is { } usedAnchor &&
                anchor.X == usedAnchor.X &&
                anchor.Y == usedAnchor.Y &&
                anchor.Z == usedAnchor.Z)
            {
                return true;
            }
        }

        return false;
    }

    private const int WaterWithinBlocks = 4;

    private static bool HasWaterWithin(
        HashSet<(int X, int Y)> waterColumns,
        int x,
        int y,
        int radius)
    {
        for (var dx = -radius; dx <= radius; dx++)
        {
            var span = radius - Math.Abs(dx);
            for (var dy = -span; dy <= span; dy++)
            {
                if (waterColumns.Contains((x + dx, y + dy)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int NearestDistance(List<(int X, int Y, int Z)> columns, BotCoordinate cell)
    {
        var best = int.MaxValue;
        foreach (var column in columns)
        {
            var distance = Math.Abs(column.X - cell.X) +
                           Math.Abs(column.Y - cell.Y) +
                           Math.Abs(column.Z - cell.Z);
            if (distance < best)
            {
                best = distance;
                if (best == 0)
                {
                    break;
                }
            }
        }

        return best;
    }

    private static readonly (int Dx, int Dy)[] TreeNeighbourOffsets =
    [
        (-1, 0), (1, 0), (0, -1), (0, 1),
    ];

    public static R1RunResult Run(PrismHostRuntime runtime, BotContentMap content, int agents, long ticks)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(agents);
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);

        var world = runtime.WorldSnapshot
                    ?? throw new InvalidOperationException("The host has no generated world; prepare it first.");
        if (world.SpawnSites.Length == 0)
        {
            throw new InvalidOperationException("The generated world exposes no spawn site.");
        }

        var spawns = new List<BotSpawn>(agents);
        var bots = new Dictionary<ulong, IPrismBot>(agents);
        var planners = new List<(ulong AgentId, BotCoordinate Spawn, string Chain, string GoalItem, int Quantity, PlannerBot Bot)>(agents);
        var taken = new List<BotSpawnSite>(agents);
        for (var index = 0; index < agents; index++)
        {
            var agentId = (ulong)(index + 1);
            var (chain, goalItem, quantity) = ChainFor(index);
            // Each chain starts where it can actually be played: the tool ladder needs exposed
            // stone under its feet (a beach spawn cannot reach the plateau), the others start next
            // to a tree. Placing every bot on the first five trees measured a world, not a bot.
            // Spawns are also taken one at a time and kept apart: two chains that start around the
            // same tree contest it, and one of them loses the run on `block_already_mined`.
            var chosen = ChooseDistinctSpawn(CandidateSpawnPointsFor(world, chain, content), taken);
            BotCoordinate point;
            if (chosen is null)
            {
                point = ChooseSpawnPointFor(world, chain, index, content);
            }
            else
            {
                taken.Add(chosen);
                point = chosen.Cell;
            }

            if (Environment.GetEnvironmentVariable("PRISM_DEBUG_BOT") == "1")
            {
                Console.Error.WriteLine($"[plan-spawn] agent {agentId} {chain} at {point}");
            }

            var planner = new PlannerBot(content, GoalsFor(chain));
            spawns.Add(new BotSpawn(agentId, point));
            bots[agentId] = planner;
            planners.Add((agentId, point, chain, goalItem, quantity, planner));
        }

        var runner = new BotRunner(runtime, bots);
        BotRunResult run = runner.Run(0, spawns, ticks);

        var outcomes = planners
            .Select(entry => new R1AgentOutcome(
                entry.AgentId,
                entry.Spawn,
                entry.Chain,
                entry.GoalItem,
                entry.Quantity,
                entry.Bot.Plan.Count,
                entry.Bot.RejectedSteps.Select(static step => step.GetType().Name).ToArray(),
                entry.Bot.IsPlanCompleted))
            .ToArray();

        return new R1RunResult(world.Seed, ticks, run, outcomes, runtime.AuditConservation());
    }

    /// <summary>
    /// Places the campaign agents where the minimal pack gives them something to work with. The
    /// bare spawn site can be a beach (sand and water, no mineable drop), which is a fine start
    /// for a walker but a dead end for a macro-action planner. The harness therefore uses the
    /// world snapshot it is allowed to read to stand each agent next to a tree, falling back to
    /// the published spawn sites. Positions stay deterministic (trees are ordered by coordinate).
    /// </summary>
    public static IReadOnlyList<BotCoordinate> ChooseSpawnPoints(GeneratedWorldSnapshot world, int agents)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentOutOfRangeException.ThrowIfNegative(agents);

        var points = new List<BotCoordinate>(agents);
        var trees = world.Trees
            .OrderBy(static tree => tree.Origin.X)
            .ThenBy(static tree => tree.Origin.Y)
            .ThenBy(static tree => tree.Origin.Z)
            .ToArray();
        var index = 0;
        while (points.Count < agents)
        {
            if (trees.Length > 0)
            {
                var tree = trees[index % trees.Length];
                if (FindStandingCell(world, tree.Origin) is { } cell)
                {
                    points.Add(cell);
                    index++;
                    continue;
                }
            }

            var site = world.SpawnSites[index % world.SpawnSites.Length];
            points.Add(new BotCoordinate(site.X, site.Y, site.Z));
            index++;
        }

        return points;
    }

    private static BotCoordinate? FindStandingCell(GeneratedWorldSnapshot world, BlockCoordinate origin)
    {
        (int Dx, int Dy)[] neighbours = [(-1, 0), (1, 0), (0, -1), (0, 1)];
        foreach (var (dx, dy) in neighbours)
        {
            var x = origin.X + dx;
            var y = origin.Y + dy;
            if (x < 0 || y < 0 || x >= world.Width || y >= world.Height)
            {
                continue;
            }

            if (world.SurfaceColumns[x + world.Width * y].SurfaceZ == origin.Z - 1)
            {
                return new BotCoordinate(x, y, origin.Z);
            }
        }

        return null;
    }
}
