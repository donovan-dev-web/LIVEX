using Prism.Core.Determinism;

namespace Prism.Core.Simulation;

public readonly record struct ConflictTarget(
    byte Kind,
    int X,
    int Y,
    int Z,
    ulong EntityId) : IComparable<ConflictTarget>
{
    public int CompareTo(ConflictTarget other)
    {
        var comparison = Kind.CompareTo(other.Kind);
        if (comparison != 0) return comparison;
        comparison = Z.CompareTo(other.Z);
        if (comparison != 0) return comparison;
        comparison = Y.CompareTo(other.Y);
        if (comparison != 0) return comparison;
        comparison = X.CompareTo(other.X);
        return comparison != 0 ? comparison : EntityId.CompareTo(other.EntityId);
    }
}

public readonly record struct ConflictClaim(ulong AgentId, ConflictTarget Target, ulong IntentId = 0);

public readonly record struct ConflictDecision(
    ConflictClaim Claim,
    ulong Priority,
    bool IsWinner);

public static class ConflictArbitrator
{
    public static IReadOnlyList<ConflictDecision> Resolve(
        IEnumerable<ConflictClaim> claims,
        ulong runSeed,
        ulong tick)
    {
        ArgumentNullException.ThrowIfNull(claims);
        var grouped = new SortedDictionary<ConflictTarget, List<(ConflictClaim Claim, ulong Priority)>>();
        var seen = new HashSet<(ConflictTarget Target, ulong AgentId)>();
        foreach (var claim in claims)
        {
            if (!seen.Add((claim.Target, claim.AgentId)))
            {
                throw new ArgumentException(
                    $"Agent '{claim.AgentId}' submitted more than one claim for the same conflict target.",
                    nameof(claims));
            }

            if (!grouped.TryGetValue(claim.Target, out var group))
            {
                group = [];
                grouped.Add(claim.Target, group);
            }

            var priority = DeterministicRandom.CounterValue(
                runSeed,
                RandomStreamNames.Conflicts,
                tick,
                claim.AgentId);
            group.Add((claim, priority));
        }

        var decisions = new List<ConflictDecision>();
        foreach (var group in grouped)
        {
            group.Value.Sort(static (left, right) =>
            {
                var comparison = left.Priority.CompareTo(right.Priority);
                return comparison != 0
                    ? comparison
                    : left.Claim.AgentId.CompareTo(right.Claim.AgentId);
            });

            for (var index = 0; index < group.Value.Count; index++)
            {
                var (claim, priority) = group.Value[index];
                decisions.Add(new ConflictDecision(claim, priority, index == 0));
            }
        }

        decisions.Sort(static (left, right) =>
        {
            var comparison = left.Priority.CompareTo(right.Priority);
            if (comparison != 0) return comparison;
            comparison = left.Claim.AgentId.CompareTo(right.Claim.AgentId);
            if (comparison != 0) return comparison;
            comparison = left.Claim.Target.CompareTo(right.Claim.Target);
            return comparison != 0 ? comparison : left.Claim.IntentId.CompareTo(right.Claim.IntentId);
        });

        return decisions.AsReadOnly();
    }
}
