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

/// <summary>
/// Planner bot: expands a goal item through the recipe graph into obtain/craft steps, then
/// executes them one at a time through macro-actions. It never searches the world globally: it
/// only uses the perception frame and fails over when a step is refused.
/// </summary>
public sealed class PlannerBot : IPrismBot
{
    private const int StepDeadlineTicks = 600;
    private const int MaximumExpansionDepth = 4;

    private readonly BotRecipeBook _recipes;
    private readonly List<BotPlanStep> _plan;
    private int _stepIndex;
    private long _deadlineTick = -1;
    private bool _stepIssued;

    public PlannerBot(BotContentMap content, IReadOnlyList<(string ItemKey, int Quantity)> goals)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(goals);
        _recipes = BotRecipeBook.FromContent(content);
        _plan = [];
        foreach (var (itemKey, quantity) in goals)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(goals), "Goal quantities must be positive.");
            }

            Expand(content.ItemId(itemKey), quantity, 0);
        }
    }

    public string Kind => "planner";

    /// <summary>The expanded plan, in execution order. Exposed for tests and run reports.</summary>
    public IReadOnlyList<BotPlanStep> Plan => _plan;

    /// <summary>Steps the world refused; a planner that reports them keeps the run auditable.</summary>
    public IReadOnlyList<BotPlanStep> RejectedSteps => _rejectedSteps;

    private readonly List<BotPlanStep> _rejectedSteps = [];

    public IReadOnlyList<BotCommand> Decide(BotSenses senses)
    {
        ArgumentNullException.ThrowIfNull(senses);
        if (_stepIndex >= _plan.Count)
        {
            return [];
        }

        var step = _plan[_stepIndex];
        if (_stepIssued)
        {
            if (IsRefused(senses, step))
            {
                _rejectedSteps.Add(step);
                Advance();
            }
            else if (IsSatisfied(senses, step))
            {
                Advance();
            }
            else if (senses.Tick > _deadlineTick)
            {
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

        _stepIssued = true;
        _deadlineTick = senses.Tick + StepDeadlineTicks;
        return [BuildCommand(_plan[_stepIndex], senses)];
    }

    private void Advance()
    {
        _stepIndex++;
        _stepIssued = false;
        _deadlineTick = -1;
    }

    private BotCommand BuildCommand(BotPlanStep step, BotSenses senses) => step switch
    {
        ObtainItemStep obtain => BotCommand.ObtainObject(obtain.ItemId, obtain.Quantity, senses.Cell, 24),
        ObtainTagStep obtain => BotCommand.ObtainTag(obtain.TagId, obtain.Quantity, senses.Cell, 24),
        CraftItemStep craft => BotCommand.CraftMacro(craft.RecipeId, FindStation(senses, craft.StationTag)),
        _ => throw new InvalidOperationException($"Unsupported plan step '{step.GetType().Name}'."),
    };

    private bool IsSatisfied(BotSenses senses, BotPlanStep step) => step switch
    {
        ObtainItemStep obtain => CountOwned(senses, obtain.ItemId) >= obtain.Quantity,
        ObtainTagStep => senses.Inventory.Count > 0,
        CraftItemStep craft => CountOwned(senses, craft.OutputItemId) >= craft.OutputQuantity,
        _ => throw new InvalidOperationException($"Unsupported plan step '{step.GetType().Name}'."),
    };

    private static bool IsRefused(BotSenses senses, BotPlanStep step)
    {
        var expectedKind = step switch
        {
            ObtainItemStep or ObtainTagStep => "ObtenirObjet",
            CraftItemStep => "FabriquerMacro",
            _ => null,
        };
        return expectedKind is not null &&
               senses.Failures.Any(failure => StringComparer.Ordinal.Equals(failure.Kind, expectedKind));
    }

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
                !_recipes.Content.MaterialHasTag(entry.MaterialId, stationTag))
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

        return best;
    }

    private void Expand(ushort itemTypeId, int quantity, int depth)
    {
        if (quantity <= 0)
        {
            return;
        }

        if (depth >= MaximumExpansionDepth ||
            !_recipes.TryFindRecipeFor(itemTypeId, out var recipe))
        {
            _plan.Add(new ObtainItemStep(itemTypeId, quantity));
            return;
        }

        var crafts = OutputQuantityFor(recipe, itemTypeId);
        if (crafts <= 0)
        {
            _plan.Add(new ObtainItemStep(itemTypeId, quantity));
            return;
        }

        var runs = (quantity + crafts - 1) / crafts;
        foreach (var input in recipe.Inputs)
        {
            var required = checked(input.Quantity * runs);
            if (input.Item is { Length: > 0 } inputItemKey &&
                _recipes.Content.TryItemId(inputItemKey, out var inputItemId))
            {
                Expand(inputItemId, required, depth + 1);
            }
            else if (input.Tag is { Length: > 0 } tag)
            {
                _plan.Add(new ObtainTagStep(tag, required));
            }
        }

        _plan.Add(new CraftItemStep(
            _recipes.Content.RecipeId(recipe.Id),
            itemTypeId,
            checked(crafts * runs),
            recipe.Station));
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
