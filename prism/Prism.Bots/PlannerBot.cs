using Prism.Content;
using Prism.Core.Perception;

namespace Prism.Bots;

/// <summary>One step of a planned production chain.</summary>
public abstract record BotPlanStep;

/// <summary>Acquire an item through the <c>ObtenirObjet</c> macro-action.</summary>
public sealed record ObtainItemStep(ushort ItemId, int Quantity) : BotPlanStep;

/// <summary>Acquire anything carrying a tag through the <c>ObtenirObjet</c> macro-action.</summary>
public sealed record ObtainTagStep(string TagId, int Quantity) : BotPlanStep;

/// <summary>Craft a recipe, optionally at a perceived station.</summary>
public sealed record CraftItemStep(ushort RecipeId, ushort OutputItemId, int OutputQuantity, string? StationTag)
    : BotPlanStep;

/// <summary>Place a crafted station (workbench, furnace) next to the agent.</summary>
public sealed record PlaceStationStep(ushort ItemId, ushort MaterialId, string StationTag) : BotPlanStep;

/// <summary>Sow, wait for ripeness and harvest a crop, through the <c>Cultiver</c> macro-action.</summary>
public sealed record CultivateStep(ushort SeedItemId, ushort HarvestItemId, int Quantity) : BotPlanStep;

/// <summary>
/// Planner bot: expands a goal item through the recipe graph into obtain/craft steps, then
/// executes them one at a time through macro-actions. It never searches the world globally: it
/// only uses the perception frame and fails over when a step is refused.
/// </summary>
public sealed class PlannerBot : IPrismBot
{
    private const int StepDeadlineTicks = 600;

    /// <summary>
    /// A cultivation step covers a whole crop cycle (three 150-tick stages plus the walk to the
    /// parcel and the harvest), so the generic 600-tick deadline cut it before the harvest.
    /// </summary>
    private const int CultivateDeadlineTicks = 1500;

    /// <summary>Verbose per-tick plan tracing, opt-in with <c>PRISM_DEBUG_BOT=1</c>.</summary>
    private static readonly bool DebugPlan =
        Environment.GetEnvironmentVariable("PRISM_DEBUG_BOT") is { Length: > 0 };

    /// <summary>
    /// How many times a step may walk back to a station it placed before the step is given up.
    /// One walk is enough when the station is still there; the extra attempts cover a path that
    /// ends a step short of the block.
    /// </summary>
    private const int MaximumStationApproachAttempts = 4;

    /// <summary>Manhattan radius of the source hint sent with an obtain request.</summary>
    private const int HintRadius = 24;

    /// <summary>
    /// How many times a step whose target turned out unusable (a parcel behind water, a block
    /// that moved out of reach) may be retried before it is reported as refused.
    /// </summary>
    private const int MaximumStepRetries = 3;

    /// <summary>
    /// How many blocks a bot may dig around the last ore it saw before it gives up on an ore
    /// obtain. Perception only shows an exposed vein: the rest of a deposit is buried in the rock,
    /// so the way to the next chunk is to open the blocks next to where ore was found.
    /// </summary>
    private const int MaximumProspectAttempts = 8;

    private readonly BotContentMap _content;
    private readonly BotRecipeBook _recipes;
    private readonly List<BotPlanStep> _plan;
    private readonly HashSet<string> _provisionedStations = new(StringComparer.Ordinal);
    private readonly HashSet<BotCoordinate> _usedParcels = [];
    private readonly Dictionary<string, BotCoordinate> _knownStations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BotCoordinate> _stationApproaches = new(StringComparer.Ordinal);
    private int _stepIndex;
    private int _stationApproachAttempts;
    private int _stepRetries;
    private int _prospectAttempts;
    private bool _waitingForBusy;
    private BotCoordinate? _lastOreSite;
    private BotCoordinate? _lastProspect;
    private int _lastOreCount = -1;
    private int _lastFrameSize = -1;
    private bool _stationApproachIssued;
    private bool _perceptionWarm;
    private long _deadlineTick = -1;
    private bool _stepIssued;

    public PlannerBot(BotContentMap content, IReadOnlyList<(string ItemKey, int Quantity)> goals)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(goals);
        _content = content;
        _recipes = BotRecipeBook.FromContent(content);
        _plan = [];
        foreach (var (itemKey, quantity) in goals)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(goals), "Goal quantities must be positive.");
            }

            Expand(content.ItemId(itemKey), quantity);
        }
    }

    public string Kind => "planner";

    /// <summary>The expanded plan, in execution order. Exposed for tests and run reports.</summary>
    public IReadOnlyList<BotPlanStep> Plan => _plan;

    /// <summary>Steps the world refused; a planner that reports them keeps the run auditable.</summary>
    public IReadOnlyList<BotPlanStep> RejectedSteps => _rejectedSteps;

    /// <summary>
    /// True once every planned step has been played. The campaign reports it so a chain is
    /// counted as done from the plan itself, not inferred from a shorter list of refusals.
    /// </summary>
    public bool IsPlanCompleted => _stepIndex >= _plan.Count;

    private readonly List<BotPlanStep> _rejectedSteps = [];

    public IReadOnlyList<BotCommand> Decide(BotSenses senses)
    {
        ArgumentNullException.ThrowIfNull(senses);
        if (_stepIndex >= _plan.Count)
        {
            return [];
        }

        if (!_perceptionWarm)
        {
            // The observation frame is capped (`maxObservedPerAgent`), and a coast spawn fills
            // the first two frames with the blocks of the lower rows: the opening obtain saw 256
            // blocks and not one log, failed `source_not_found`, and the whole chain then played
            // its steps out of order. A bot must not act on a frame that is still arriving.
            if (senses.Entries.Count == _lastFrameSize)
            {
                _perceptionWarm = true;
            }
            else
            {
                _lastFrameSize = senses.Entries.Count;
                return [];
            }
        }

        var step = _plan[_stepIndex];
        RememberOreSightings(senses);
        if (_stepIssued)
        {
            if (IsRefused(senses, step))
            {
                if (step is ObtainItemStep oreStep &&
                    IsOreGathering(oreStep) &&
                    HasFailureReason(senses, "ObtenirObjet", "source_not_found") &&
                    _prospectAttempts < MaximumProspectAttempts &&
                    ProspectTarget(senses) is { } prospect)
                {
                    // Nothing that drops the ore is in sight, so the deposit is buried. Open the
                    // rock next to the last ore seen and ask again next tick: without this the
                    // tools chain stopped at its second chunk (source_not_found) and never made
                    // the three ingots an iron pickaxe needs.
                    // Progress resets the budget: eight blind blocks are allowed per chunk found,
                    // not per step, or a step needing three chunks would stop after the first.
                    var owned = CountOwned(senses, oreStep.ItemId);
                    if (owned != _lastOreCount)
                    {
                        _lastOreCount = owned;
                        _prospectAttempts = 0;
                    }

                    _prospectAttempts++;
                    _stepIssued = false;
                    return [BotCommand.Mine(prospect)];
                }

                if (step is CultivateStep &&
                    IsRecoverableCultivateFailure(senses, step) &&
                    _stepRetries < MaximumStepRetries)
                {
                    // The parcel the bot picked is behind water or out of reach. `FindParcel`
                    // already marked it used, so asking again on the next tick sows another one
                    // instead of losing the whole harvest step.
                    _stepRetries++;
                    _stepIssued = false;
                    _deadlineTick = -1;
                    if (DebugPlan)
                    {
                        Console.Error.WriteLine($"[plan-retry] at {senses.Cell} tick {senses.Tick} cultivate because {DescribeFailures(senses)}");
                    }
                }
                else
                {
                    // A refused step is rare and always actionable for the gate report: keep it
                    // visible without the per-tick tracing.
                    Console.Error.WriteLine($"[plan] at {senses.Cell} tick {senses.Tick} refused {step.GetType().Name} {Describe(step)}{DescribeState(step, senses)} because {DescribeFailures(senses)}");
                    _rejectedSteps.Add(step);
                    Advance();
                }
            }
            else if (IsSatisfied(senses, step))
            {
                Advance();
            }
            else if (ExpectedKind(step) is { } kind && senses.CompletedKind(kind))
            {
                // The command finished but the step is not satisfied: it produced less than
                // asked (one craft run of four planks for a step that needs eight). Issue it
                // again on this tick instead of waiting for the step deadline.
                _stepIssued = false;
                _deadlineTick = -1;
            }
            else if (ExpectedKind(step) is { } busyKind && IsBusy(senses, busyKind))
            {
                // The command was dropped because an action is still running. Ask again once
                // the world reports an event (the still-running action completed or failed)
                // instead of on a timer: re-sending every few ticks burned a fresh parcel on
                // every attempt and flooded the campaign with `action_already_active`.
                // The macro that refused us may end on this very tick: the runner then reports
                // its `action_completed` alongside the busy failure. Ignoring it here put the
                // bot to sleep for the rest of the run (4 commands sent, 0 crop).
                if (!SawMacroEnd(senses))
                {
                    if (DebugPlan)
                    {
                        Console.Error.WriteLine($"[plan-busy] at {senses.Cell} tick {senses.Tick} wait after {DescribeFailures(senses)} on {step.GetType().Name}");
                    }

                    _waitingForBusy = true;
                    return [];
                }

                _stepIssued = false;
                _deadlineTick = -1;
            }
            else if (_waitingForBusy)
            {
                if (!SawMacroEnd(senses))
                {
                    return [];
                }

                if (DebugPlan)
                {
                    Console.Error.WriteLine($"[plan-busy] at {senses.Cell} tick {senses.Tick} resume {step.GetType().Name} completed={string.Join(",", senses.Completed ?? [])} failures={DescribeFailures(senses)}");
                }

                _waitingForBusy = false;
                _stepIssued = false;
                _deadlineTick = -1;
            }
            else if (senses.Tick > _deadlineTick)
            {
                if (DebugPlan)
                {
                    Console.Error.WriteLine($"[plan] agent tick {senses.Tick} deadline {step.GetType().Name} {Describe(step)}");
                }
                _rejectedSteps.Add(step);
                Advance();
            }
            else
            {
                return [];
            }
        }

        if (_stepIndex >= _plan.Count)
        {
            return [];
        }

        var next = _plan[_stepIndex];
        if (next is CraftItemStep { StationTag: { Length: > 0 } stationTag } &&
            FindStation(senses, stationTag) is null &&
            _knownStations.TryGetValue(stationTag, out var remembered))
        {
            // The plan placed its workbench or furnace once, then walked away to gather the rest
            // of the materials. A craft at a station the agent can no longer perceive is refused
            // (`station_absent` once the position is not even sent), so the bot walks back to the
            // station it remembers and only then issues the craft.
            if (!_stationApproachIssued)
            {
                if (_stationApproachAttempts >= MaximumStationApproachAttempts)
                {
                    if (DebugPlan)
                    {
                        Console.Error.WriteLine(
                            $"[plan] agent tick {senses.Tick} unreachable {Describe(next)}");
                    }
                    _rejectedSteps.Add(next);
                    Advance();
                    return [];
                }

                _stationApproachIssued = true;
                _stationApproachAttempts++;
                // Not the station block itself: its column is solid, so a move order onto it has
                // no path and the bot reported the station unreachable after four attempts.
                // The cell the agent stood on when it placed the station is next to it by
                // construction, and the agent has already walked there once.
                var destination = _stationApproaches.TryGetValue(stationTag, out var approach)
                    ? approach
                    : remembered;
                if (DebugPlan)
                {
                    Console.Error.WriteLine(
                        $"[plan-station] at {senses.Cell} tick {senses.Tick} walk to {stationTag} {destination} (station {remembered}, attempt {_stationApproachAttempts})");
                }

                return [BotCommand.MoveTo(destination)];
            }

            if (senses.CompletedKind("Aller") || HasFailure(senses, "Aller"))
            {
                _stationApproachIssued = false;
            }

            return [];
        }

        _stationApproachIssued = false;
        _stationApproachAttempts = 0;
        _stepIssued = true;
        _waitingForBusy = false;
        _deadlineTick = senses.Tick + DeadlineFor(next);
        if (next is PlaceStationStep placement)
        {
            _stationApproaches[placement.StationTag] = senses.Cell;
        }

        return [BuildCommand(next, senses)];
    }

    private static string DescribeFailures(BotSenses senses)
    {
        var parts = new List<string>(senses.Failures.Count);
        foreach (var failure in senses.Failures)
        {
            parts.Add(failure.Kind + ":" + failure.Reason);
        }

        return string.Join(",", parts);
    }

    /// <summary>What the bot already holds (and where the station is) when a step is refused.</summary>
    private string DescribeState(BotPlanStep step, BotSenses senses) => step switch
    {
        ObtainItemStep obtain => $" (owned {CountOwned(senses, obtain.ItemId)}/{obtain.Quantity})",
        ObtainTagStep obtain => $" (owned {CountOwnedWithTag(senses, obtain.TagId)}/{obtain.Quantity})",
        CraftItemStep craft => $" (station {(FindStation(senses, craft.StationTag)?.ToString() ?? "remembered" +
            (_knownStations.TryGetValue(craft.StationTag ?? string.Empty, out var known) ? " " + known : " none"))})",
        CultivateStep cultivate => $" (owned {CountOwned(senses, cultivate.HarvestItemId)}/{cultivate.Quantity})",
        _ => string.Empty,
    };

    private string Describe(BotPlanStep step) => step switch
    {
        ObtainItemStep obtain => _content.ItemKey(obtain.ItemId) + " x" + obtain.Quantity,
        ObtainTagStep obtain => "tag:" + obtain.TagId + " x" + obtain.Quantity,
        CraftItemStep craft => _content.ItemKey(craft.OutputItemId) + " station=" + craft.StationTag,
        PlaceStationStep place => _content.ItemKey(place.ItemId),
        CultivateStep cultivate => _content.ItemKey(cultivate.HarvestItemId),
        _ => step.ToString() ?? string.Empty,
    };

    private static int DeadlineFor(BotPlanStep step) =>
        step is CultivateStep ? CultivateDeadlineTicks : StepDeadlineTicks;

    private void Advance()
    {
        _stepIndex++;
        _stepIssued = false;
        _waitingForBusy = false;
        _deadlineTick = -1;
        _stationApproachIssued = false;
        _stationApproachAttempts = 0;
        _stepRetries = 0;
        _prospectAttempts = 0;
        _lastProspect = null;
    }

    /// <summary>
    /// True for an obtain of something the world holds as a block (an ore chunk), as opposed to
    /// an item that merely carries the `ore` tag (`iron_ingot`): only the former can be reached by
    /// digging.
    /// </summary>
    private bool IsOreGathering(ObtainItemStep step) =>
        _content.ItemHasTag(step.ItemId, "ore") && _content.IsDroppedByAnyBlock(step.ItemId);

    /// <summary>Keeps the place of the last ore block seen, even once it has been mined away.</summary>
    private void RememberOreSightings(BotSenses senses)
    {
        // The nearest find, not the last one the frame happens to hold: taking the last entry sent
        // the bot to a deposit twenty blocks away, where it dug the surface it was standing on.
        var bestDistance = int.MaxValue;
        foreach (var entry in senses.Entries)
        {
            if (entry.Kind != PerceptionEntityKind.Block ||
                !_content.MaterialHasTag(entry.MaterialId, "ore"))
            {
                continue;
            }

            var candidate = new BotCoordinate(entry.X, entry.Y, entry.Z);
            var distance = senses.Cell.ManhattanDistanceTo(candidate);
            if (distance <= bestDistance)
            {
                _lastOreSite = candidate;
                bestDistance = distance;
            }
        }
    }

    /// <summary>
    /// Block to open to reach a buried deposit: the nearest perceived mineable block to the last
    /// ore seen (or to the agent when it never saw one), skipping the block it stands on.
    /// </summary>
    private BotCoordinate? ProspectTarget(BotSenses senses)
    {
        // The ore of this world sits under the surface: a find only marks where the vein reaches
        // the open air, and the deposit itself is in the rock below it. Digging a pocket around
        // the find (nearest block first) opened eight blocks of grass at surface height and never
        // showed ore; the way into a deposit is the shaft under the find.
        var anchor = _lastProspect ?? _lastOreSite ?? senses.Cell;
        if (TryStoneAt(senses, out var dug, (anchor.X, anchor.Y, anchor.Z - 1)))
        {
            _lastProspect = dug;
            return dug;
        }

        foreach (var (dx, dy) in ShaftSideOffsets)
        {
            if (TryStoneAt(senses, out dug, (anchor.X + dx, anchor.Y + dy, anchor.Z)))
            {
                _lastProspect = dug;
                return dug;
            }
        }

        // The shaft left the rock: fall back to rock the agent can still see, close enough for the
        // shaft to be about the find rather than about wandering.
        if (NearestStone(senses, anchor) is { } fallback)
        {
            _lastProspect = fallback;
            return fallback;
        }

        return null;
    }

    private static readonly (int Dx, int Dy)[] ShaftSideOffsets =
    [
        (0, -1), (-1, 0), (1, 0), (0, 1),
    ];

    private bool TryStoneAt(BotSenses senses, out BotCoordinate coordinate, (int X, int Y, int Z) at)
    {
        coordinate = new BotCoordinate(at.X, at.Y, at.Z);
        foreach (var entry in senses.Entries)
        {
            if (entry.Kind == PerceptionEntityKind.Block &&
                entry.X == at.X &&
                entry.Y == at.Y &&
                entry.Z == at.Z)
            {
                return _content.IsMineableMaterial(entry.MaterialId) &&
                       _content.MaterialHasTag(entry.MaterialId, "stone");
            }
        }

        return false;
    }

    private BotCoordinate? NearestStone(BotSenses senses, BotCoordinate origin)
    {
        BotCoordinate? best = null;
        var bestDistance = int.MaxValue;
        foreach (var entry in senses.Entries)
        {
            if (entry.Kind != PerceptionEntityKind.Block ||
                !_content.IsMineableMaterial(entry.MaterialId) ||
                !_content.MaterialHasTag(entry.MaterialId, "stone"))
            {
                continue;
            }

            if (entry.X == senses.Cell.X &&
                entry.Y == senses.Cell.Y &&
                entry.Z < senses.Cell.Z)
            {
                continue;
            }

            var candidate = new BotCoordinate(entry.X, entry.Y, entry.Z);
            var distance = origin.ManhattanDistanceTo(candidate);
            if (distance <= MaximumProspectReach && distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    private const int MaximumProspectReach = 4;

    private static bool HasFailureReason(BotSenses senses, string kind, string reason)
    {
        foreach (var failure in senses.Failures)
        {
            if (StringComparer.Ordinal.Equals(failure.Kind, kind) &&
                StringComparer.Ordinal.Equals(failure.Reason, reason))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRecoverableCultivateFailure(BotSenses senses, BotPlanStep step)
    {
        if (ExpectedKind(step) is not { } kind)
        {
            return false;
        }

        foreach (var failure in senses.Failures)
        {
            if (!StringComparer.Ordinal.Equals(failure.Kind, kind))
            {
                continue;
            }

            if (failure.Reason is "path_not_found" or "not_perceived" or "crop_not_found" or
                "cell_occupied" or "soil_unsuitable" or "no_water")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the world reported the end of a macro-action (its final <c>action_completed</c>
    /// or an <c>action_failed</c>) rather than the completion of one of its internal steps. The
    /// runner records the latter under the synthetic command id and names it <c>unknown</c>;
    /// waking on those only produced another <c>action_already_active</c>.
    /// </summary>
    private static bool SawMacroEnd(BotSenses senses)
    {
        foreach (var failure in senses.Failures)
        {
            // The busy refusal itself says the opposite: the macro is still running.
            if (!StringComparer.Ordinal.Equals(failure.Reason, "action_already_active"))
            {
                return true;
            }
        }

        if (senses.Completed is null)
        {
            return false;
        }

        foreach (var completed in senses.Completed)
        {
            if (!StringComparer.Ordinal.Equals(completed, "unknown"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasFailure(BotSenses senses, string kind)
    {
        foreach (var failure in senses.Failures)
        {
            if (StringComparer.Ordinal.Equals(failure.Kind, kind))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsBusy(BotSenses senses, string kind)
    {
        var busy = false;
        foreach (var failure in senses.Failures)
        {
            if (StringComparer.Ordinal.Equals(failure.Kind, kind) &&
                StringComparer.Ordinal.Equals(failure.Reason, "action_already_active"))
            {
                busy = true;
            }
        }

        return busy;
    }

    private BotCommand BuildCommand(BotPlanStep step, BotSenses senses) => step switch
    {
        // Only the remainder is asked for: a bot that already holds one log of the two a step
        // needs used to ask for two more and over-mine the source it stands on.
        ObtainItemStep obtain => BotCommand.ObtainObject(
            obtain.ItemId,
            RemainingQuantity(senses, obtain.ItemId, obtain.Quantity),
            senses.Cell,
            HintRadius),
        ObtainTagStep obtain => BotCommand.ObtainTag(
            obtain.TagId,
            RemainingQuantity(senses, obtain.TagId, obtain.Quantity),
            senses.Cell,
            HintRadius),
        CraftItemStep craft => BotCommand.CraftMacro(craft.RecipeId, FindStation(senses, craft.StationTag)),
        PlaceStationStep place => BotCommand.Place(FindPlacementCell(senses), place.ItemId, place.MaterialId),
        CultivateStep cultivate => BotCommand.Cultivate(
            FindParcel(senses) ?? senses.Cell,
            cultivate.SeedItemId),
        _ => throw new InvalidOperationException($"Unsupported plan step '{step.GetType().Name}'."),
    };

    private bool IsSatisfied(BotSenses senses, BotPlanStep step) => step switch
    {
        ObtainItemStep obtain => CountOwned(senses, obtain.ItemId) >= obtain.Quantity,
        // An inventory that merely holds something is not a fuel supply: "any item" satisfied
        // the step, so the bot smelted iron with no coal and reported `input_missing` at the
        // furnace. The tag has to be carried by what is actually in the bag.
        ObtainTagStep obtain => CountOwnedWithTag(senses, obtain.TagId) >= obtain.Quantity,
        CraftItemStep craft => CountOwned(senses, craft.OutputItemId) >= craft.OutputQuantity,
        PlaceStationStep place => FindStation(senses, place.StationTag) is not null,
        CultivateStep cultivate => CountOwned(senses, cultivate.HarvestItemId) >= cultivate.Quantity,
        _ => throw new InvalidOperationException($"Unsupported plan step '{step.GetType().Name}'."),
    };

    private static bool IsRefused(BotSenses senses, BotPlanStep step)
    {
        // `action_already_active` says the previous macro-action of this same step is still
        // running: that is not a refusal, it is the answer to a command sent too early. It
        // used to advance the plan to the next step, so the bot crafted before it held the
        // materials and reported `input_missing`.
        return ExpectedKind(step) is { } expectedKind &&
               senses.Failures.Any(failure =>
                   StringComparer.Ordinal.Equals(failure.Kind, expectedKind) &&
                   !StringComparer.Ordinal.Equals(failure.Reason, "action_already_active"));
    }

    private static string? ExpectedKind(BotPlanStep step) => step switch
    {
        ObtainItemStep or ObtainTagStep => "ObtenirObjet",
        CraftItemStep => "FabriquerMacro",
        PlaceStationStep => "Poser",
        CultivateStep => "Cultiver",
        _ => null,
    };

    private static int CountOwned(BotSenses senses, ushort itemTypeId)
    {
        var total = 0;
        foreach (var stack in senses.Inventory)
        {
            if (stack.ItemTypeId == itemTypeId)
            {
                total += stack.Quantity;
            }
        }

        return total;
    }

    private int CountOwnedWithTag(BotSenses senses, string tagId)
    {
        var total = 0;
        foreach (var stack in senses.Inventory)
        {
            if (_content.ItemHasTag(stack.ItemTypeId, tagId))
            {
                total += stack.Quantity;
            }
        }

        return total;
    }

    private int RemainingQuantity(BotSenses senses, ushort itemTypeId, int quantity) =>
        Math.Max(1, quantity - CountOwned(senses, itemTypeId));

    private int RemainingQuantity(BotSenses senses, string tagId, int quantity) =>
        Math.Max(1, quantity - CountOwnedWithTag(senses, tagId));

    /// <summary>
    /// Nearest perceived station for <paramref name="stationTag"/>. A recipe names the station by
    /// its material ("workbench"), while the material itself only carries the generic tag
    /// "station": both spellings are accepted so a placed workbench is really found.
    /// </summary>
    private BotCoordinate? FindStation(BotSenses senses, string? stationTag)
    {
        if (stationTag is null)
        {
            return null;
        }

        BotCoordinate? best = null;
        var bestDistance = int.MaxValue;
        var cell = senses.Cell;
        foreach (var entry in senses.Entries)
        {
            if (entry.Kind != PerceptionEntityKind.Block ||
                !IsStationMaterial(entry.MaterialId, stationTag))
            {
                continue;
            }

            var candidate = new BotCoordinate(entry.X, entry.Y, entry.Z);
            var distance = cell.ManhattanDistanceTo(candidate);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        if (best is { } located)
        {
            // Remembered even after walking out of perception: the plan has to be able to come
            // back to the station it placed instead of crafting into the void.
            _knownStations[stationTag] = located;
        }

        return best;
    }

    private bool IsStationMaterial(ushort materialId, string stationTag) =>
        _content.MaterialHasTag(materialId, stationTag) ||
        StringComparer.Ordinal.Equals(_content.MaterialKey(materialId), stationTag);

    /// <summary>
    /// Neighbour cell where a crafted station can be placed: on the solid ground of that column,
    /// within one level of the agent's feet, and not already perceived as occupied. Deterministic
    /// order (north, west, east, south).
    /// <para>
    /// The cell is derived from the column's ground, not from the agent's own feet: a ladder that
    /// dug the stone of its furnace stood in a one-deep hole, every neighbour at its feet level
    /// was the solid it had not mined yet, and the world answered <c>cell_occupied</c> — the
    /// furnace was never placed and the iron ingot of the pickaxe was never smelted.
    /// </para>
    /// </summary>
    private BotCoordinate FindPlacementCell(BotSenses senses)
    {
        var cell = senses.Cell;
        foreach (var (dx, dy) in PlacementOffsets)
        {
            var x = cell.X + dx;
            var y = cell.Y + dy;
            var groundZ = int.MinValue;
            foreach (var entry in senses.Entries)
            {
                if (entry.Kind == PerceptionEntityKind.Block &&
                    entry.X == x && entry.Y == y &&
                    Math.Abs(entry.Z - cell.Z) <= 2 &&
                    _content.IsSolidMaterial(entry.MaterialId) &&
                    entry.Z > groundZ)
                {
                    groundZ = entry.Z;
                }
            }

            if (groundZ == int.MinValue)
            {
                continue;
            }

            var candidate = new BotCoordinate(x, y, groundZ + 1);
            if (Math.Abs(candidate.Z - cell.Z) > 1 || IsPerceivedBlock(senses, candidate))
            {
                continue;
            }

            return candidate;
        }

        // Ground of the neighbours unknown: fall back to the agent's own level rather than
        // refusing the placement outright.
        BotCoordinate? fallback = null;
        foreach (var (dx, dy) in PlacementOffsets)
        {
            var candidate = new BotCoordinate(cell.X + dx, cell.Y + dy, cell.Z);
            if (!IsPerceivedBlock(senses, candidate))
            {
                fallback ??= candidate;
            }
        }

        return fallback ?? cell;
    }

    private static bool IsPerceivedBlock(BotSenses senses, BotCoordinate coordinate)
    {
        foreach (var entry in senses.Entries)
        {
            if (entry.Kind == PerceptionEntityKind.Block &&
                entry.X == coordinate.X &&
                entry.Y == coordinate.Y &&
                entry.Z == coordinate.Z)
            {
                return true;
            }
        }

        return false;
    }

    private static readonly (int Dx, int Dy)[] PlacementOffsets =
    [
        (0, -1), (-1, 0), (1, 0), (0, 1),
    ];

    /// <summary>
    /// Nearest tillable column that has water within four blocks — the pack only ripens a crop
    /// inside that distance, so sowing elsewhere would wait for a harvest that never comes.
    /// Returns the air cell above the soil, which is what <c>Cultiver</c> expects.
    /// </summary>
    private BotCoordinate? FindParcel(BotSenses senses)
    {
        BotCoordinate? best = null;
        var bestDistance = int.MaxValue;
        foreach (var entry in senses.Entries)
        {
            if (entry.Kind != PerceptionEntityKind.Block ||
                !_content.IsTillableMaterial(entry.MaterialId))
            {
                continue;
            }

            var parcel = new BotCoordinate(entry.X, entry.Y, entry.Z + 1);
            if (_usedParcels.Contains(parcel) || !HasWaterNear(senses, parcel))
            {
                continue;
            }

            var distance = senses.Cell.ManhattanDistanceTo(parcel);
            if (distance < bestDistance)
            {
                best = parcel;
                bestDistance = distance;
            }
        }

        if (best is { } chosen)
        {
            _usedParcels.Add(chosen);
            if (DebugPlan)
            {
                Console.Error.WriteLine($"[plan-parcel] at {senses.Cell} tick {senses.Tick} chose parcel {chosen} (soil z={chosen.Z - 1})");
            }
        }

        return best;
    }

    private bool HasWaterNear(BotSenses senses, BotCoordinate parcel)
    {
        foreach (var entry in senses.Entries)
        {
            if (entry.Kind != PerceptionEntityKind.Block ||
                !_content.MaterialHasTag(entry.MaterialId, "drinkable"))
            {
                continue;
            }

            var distance = Math.Abs(entry.X - parcel.X) +
                           Math.Abs(entry.Y - parcel.Y) +
                           Math.Abs(entry.Z - parcel.Z);
            if (distance <= WaterWithinBlocks)
            {
                return true;
            }
        }

        return false;
    }

    private const int WaterWithinBlocks = 4;

    private void Expand(ushort itemTypeId, int quantity) => Expand(itemTypeId, quantity, []);

    /// <summary>
    /// Expands one goal into the steps that produce it. <paramref name="path"/> holds the items of
    /// the branch being expanded, so a recipe graph that loops (a handle made of handles) stops
    /// instead of recursing for ever. The guard is a cycle test, not a depth limit: a fixed depth
    /// of four cut the cooking plan at "obtain a plank" — an item no block of the world drops — so
    /// that chain could not start at all and every later step was played out of order.
    /// </summary>
    private void Expand(ushort itemTypeId, int quantity, HashSet<ushort> path)
    {
        if (quantity <= 0)
        {
            return;
        }

        if (!_recipes.TryFindRecipeFor(itemTypeId, out var recipe))
        {
            // A harvest is not something a bot can walk up to and pick: it has to sow the crop
            // that produces it. Without this, "obtain three wheat" stayed a source_not_found
            // forever, because no block in the world answers it.
            if (_content.TryCropSeedFor(itemTypeId, out var seedItemId))
            {
                if (_content.TryHoeItemId(out var hoeItemId))
                {
                    Expand(hoeItemId, 1, path);
                }
                _plan.Add(new ObtainItemStep(seedItemId, 1));
                _plan.Add(new CultivateStep(seedItemId, itemTypeId, quantity));
                return;
            }

            _plan.Add(new ObtainItemStep(itemTypeId, quantity));
            return;
        }

        if (!path.Add(itemTypeId))
        {
            // A pack whose recipes form a cycle: the item cannot be produced from itself, so the
            // plan falls back to taking it from the world rather than expanding for ever.
            _plan.Add(new ObtainItemStep(itemTypeId, quantity));
            return;
        }

        try
        {
            var crafts = OutputQuantityFor(recipe, itemTypeId);
            if (crafts <= 0)
            {
                _plan.Add(new ObtainItemStep(itemTypeId, quantity));
                return;
            }

            var runs = (quantity + crafts - 1) / crafts;
            // Set the workshop up first, then gather the inputs. A station placed after the
            // inputs is not wrong by itself, but the cooking chain harvested its wheat before
            // spending hundreds of ticks on stone, furnace and fuel, and the wheat expired in
            // the bag (600-tick shelf life) before the bake: `input_missing` at the furnace.
            if (recipe.Station is { Length: > 0 } stationTag)
            {
                ProvisionStation(stationTag, path);
            }

            // Perishable inputs go last so they are still fresh when the recipe runs; the
            // durable ones can wait in the bag for as long as the chain needs.
            ExpandInputs(perishable: false);
            ExpandInputs(perishable: true);

            void ExpandInputs(bool perishable)
            {
                foreach (var input in recipe.Inputs)
                {
                    var required = checked(input.Quantity * runs);
                    if (input.Item is { Length: > 0 } inputItemKey &&
                        _recipes.Content.TryItemId(inputItemKey, out var inputItemId))
                    {
                        if (_content.HasShelfLife(inputItemId) != perishable)
                        {
                            continue;
                        }

                        Expand(inputItemId, required, path);
                    }
                    else if (input.Tag is { Length: > 0 } tag && !perishable)
                    {
                        // A tag names a class of items. When the world cannot hand any of them
                        // over ("handle", "stone_head"), the tag has to be satisfied by crafting
                        // one of its members; otherwise the plan asks for something no block will
                        // ever drop and the whole chain stops on source_not_found.
                        if (!TagIsDroppedByWorld(tag) && TryCraftableItemWithTag(tag, out var tagItemId))
                        {
                            Expand(tagItemId, required, path);
                        }
                        else
                        {
                            _plan.Add(new ObtainTagStep(tag, required));
                        }
                    }
                }
            }

            _plan.Add(new CraftItemStep(
                _recipes.Content.RecipeId(recipe.Id),
                itemTypeId,
                checked(crafts * runs),
                recipe.Station));

            // A station is only a station once it stands in the world: craft the item, then place
            // it ("station_absent" was the last blocker of the tools and cooking chains).
            if (_content.TryPlacedMaterialId(itemTypeId, out var placedMaterialId) &&
                _content.MaterialHasTag(placedMaterialId, "station"))
            {
                _plan.Add(new PlaceStationStep(
                    itemTypeId,
                    placedMaterialId,
                    _content.MaterialKey(placedMaterialId)));
            }
        }
        finally
        {
            path.Remove(itemTypeId);
        }
    }

    /// <summary>
    /// Adds the craft-and-place steps of a station the plan has not provisioned yet. Idempotent:
    /// two recipes needing the same workbench share the one the plan already places.
    /// </summary>
    private void ProvisionStation(string stationTag, HashSet<ushort> path)
    {
        if (!_provisionedStations.Add(stationTag))
        {
            return;
        }

        if (_content.TryStationItemId(stationTag, out var stationItemId))
        {
            Expand(stationItemId, 1, path);
        }
    }

    private bool TagIsDroppedByWorld(string tag)
    {
        foreach (var itemTypeId in _content.ItemsWithTag(tag))
        {
            if (_content.IsDroppedByAnyBlock(itemTypeId))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryCraftableItemWithTag(string tag, out ushort itemTypeId)
    {
        foreach (var candidate in _content.ItemsWithTag(tag))
        {
            if (_recipes.TryFindRecipeFor(candidate, out _))
            {
                itemTypeId = candidate;
                return true;
            }
        }

        itemTypeId = 0;
        return false;
    }

    private int OutputQuantityFor(RecipeDefinition recipe, ushort itemTypeId)
    {
        var total = 0;
        foreach (var output in recipe.Outputs)
        {
            if (_recipes.Content.TryItemId(output.Item, out var outputItemId) &&
                outputItemId == itemTypeId)
            {
                total += output.Quantity;
            }
        }

        return total > 0 ? total : 1;
    }
}
