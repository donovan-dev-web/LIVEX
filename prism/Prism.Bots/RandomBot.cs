using Prism.Core.Determinism;
using Prism.Core.Perception;

namespace Prism.Bots;

/// <summary>
/// Baseline bot: uniform random walk / mine / eat. Its only purpose is to produce an
/// unpredictable command mix that regenerressions the whole dispatch surface, deterministically.
/// </summary>
public sealed class RandomBot : IPrismBot
{
    private readonly Xoshiro256StarStar _random;
    private readonly BotContentMap _content;

    public RandomBot(ulong runSeed, ulong agentId, BotContentMap content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _content = content;
        _random = new Xoshiro256StarStar(
            DeterministicRandom.DeriveStreamSeed(runSeed ^ DeterministicRandom.Mix64(agentId), "bot.random"));
    }

    public string Kind => "random";

    public IReadOnlyList<BotCommand> Decide(BotSenses senses)
    {
        ArgumentNullException.ThrowIfNull(senses);
        var blocks = senses.Perceived(PerceptionEntityKind.Block);
        if (blocks.Count == 0)
        {
            return [];
        }

        var roll = _random.NextInt(0, 8);
        if (roll == 7)
        {
            return [BotCommand.Cancel()];
        }

        if (roll == 6)
        {
            foreach (var stack in senses.Inventory)
            {
                if (_content.IsEdibleItem(stack.ItemTypeId))
                {
                    return [BotCommand.Consume(stack.ItemTypeId)];
                }
            }
        }

        var candidate = blocks[_random.NextInt(0, blocks.Count)];
        var coordinate = new BotCoordinate(candidate.X, candidate.Y, candidate.Z);
        if (roll < 3 && _content.IsMineableMaterial(candidate.MaterialId))
        {
            return [BotCommand.Mine(coordinate)];
        }

        if (roll is 3 or 4)
        {
            return [BotCommand.MoveTo(new BotCoordinate(coordinate.X, coordinate.Y, coordinate.Z + 1))];
        }

        return [BotCommand.Till(coordinate)];
    }
}

/// <summary>
/// Greedy bot: walks to the nearest mineable perceived block and mines it, forever. It is the
/// "does the loop close" bot: movement, adjacency, duration and drops in one continuous run.
/// </summary>
public sealed class GreedyBot : IPrismBot
{
    private readonly BotContentMap _content;
    private BotCoordinate? _target;

    public GreedyBot(BotContentMap content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _content = content;
    }

    public string Kind => "greedy";

    public IReadOnlyList<BotCommand> Decide(BotSenses senses)
    {
        ArgumentNullException.ThrowIfNull(senses);
        var cell = senses.Cell;
        if (_target is { } current)
        {
            var stillThere = false;
            foreach (var entry in senses.Entries)
            {
                if (entry.Kind == PerceptionEntityKind.Block &&
                    entry.X == current.X && entry.Y == current.Y && entry.Z == current.Z)
                {
                    stillThere = _content.IsMineableMaterial(entry.MaterialId);
                    break;
                }
            }

            if (!stillThere || HasFailed(senses, "Miner"))
            {
                _target = null;
            }
        }

        _target ??= FindNearestTarget(senses);
        if (_target is not { } target)
        {
            return [];
        }

        if (cell.IsAdjacentTo(target))
        {
            return [BotCommand.Mine(target)];
        }

        return [BotCommand.MoveTo(new BotCoordinate(target.X, target.Y, target.Z + 1))];
    }

    private BotCoordinate? FindNearestTarget(BotSenses senses)
    {
        var cell = senses.Cell;
        BotCoordinate? best = null;
        var bestDistance = int.MaxValue;
        foreach (var entry in senses.Entries)
        {
            if (entry.Kind != PerceptionEntityKind.Block ||
                !_content.IsMineableMaterial(entry.MaterialId))
            {
                continue;
            }

            var candidate = new BotCoordinate(entry.X, entry.Y, entry.Z);
            var distance = cell.ManhattanDistanceTo(candidate);
            if (distance >= bestDistance)
            {
                continue;
            }

            if (candidate == cell)
            {
                continue;
            }

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    private static bool HasFailed(BotSenses senses, string kind) =>
        senses.Failures.Any(failure => StringComparer.Ordinal.Equals(failure.Kind, kind));
}
