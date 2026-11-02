namespace Prism.Core.Accounting;

public readonly record struct MatterLedgerEntry(
    ushort ItemTypeId,
    string RuleId,
    long Created,
    long Destroyed);

public readonly record struct MatterAuditViolation(
    ushort ItemTypeId,
    long Expected,
    long Actual)
{
    public long Difference => checked(Actual - Expected);
}

public sealed class MatterLedger
{
    private static readonly IComparer<(ushort ItemTypeId, string RuleId)> EntryComparer =
        Comparer<(ushort ItemTypeId, string RuleId)>.Create(static (left, right) =>
        {
            var itemComparison = left.ItemTypeId.CompareTo(right.ItemTypeId);
            return itemComparison != 0
                ? itemComparison
                : StringComparer.Ordinal.Compare(left.RuleId, right.RuleId);
        });

    private readonly SortedDictionary<ushort, long> _initialCounts = [];
    private readonly SortedDictionary<(ushort ItemTypeId, string RuleId), MutableEntry> _entries =
        new(EntryComparer);

    public void SetInitialCount(ushort itemTypeId, long quantity)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        _initialCounts[itemTypeId] = quantity;
    }

    public void RecordCreated(ushort itemTypeId, long quantity, string ruleId) =>
        Record(itemTypeId, quantity, ruleId, created: true);

    public void RecordDestroyed(ushort itemTypeId, long quantity, string ruleId) =>
        Record(itemTypeId, quantity, ruleId, created: false);

    public IEnumerable<MatterLedgerEntry> EnumerateEntries()
    {
        foreach (var (key, value) in _entries)
        {
            yield return new MatterLedgerEntry(key.ItemTypeId, key.RuleId, value.Created, value.Destroyed);
        }
    }

    public IReadOnlyList<MatterAuditViolation> Audit(IReadOnlyDictionary<ushort, long> currentCounts)
    {
        ArgumentNullException.ThrowIfNull(currentCounts);
        var violations = new List<MatterAuditViolation>();
        var itemIds = new SortedSet<ushort>(_initialCounts.Keys);
        itemIds.UnionWith(_entries.Keys.Select(key => key.ItemTypeId));
        itemIds.UnionWith(currentCounts.Keys);

        foreach (var itemTypeId in itemIds)
        {
            var current = currentCounts.TryGetValue(itemTypeId, out var quantity) ? quantity : 0;
            if (current < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentCounts), $"Current count for item {itemTypeId} cannot be negative.");
            }

            var created = 0L;
            var destroyed = 0L;
            foreach (var (key, entry) in _entries)
            {
                if (key.ItemTypeId == itemTypeId)
                {
                    created = checked(created + entry.Created);
                    destroyed = checked(destroyed + entry.Destroyed);
                }
            }

            var initial = _initialCounts.TryGetValue(itemTypeId, out var initialQuantity)
                ? initialQuantity
                : 0;
            var expected = checked(initial + created - destroyed);
            if (expected != current)
            {
                violations.Add(new MatterAuditViolation(itemTypeId, expected, current));
            }
        }

        return violations;
    }

    private void Record(ushort itemTypeId, long quantity, string ruleId, bool created)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Ledger quantities must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        var key = (itemTypeId, ruleId);
        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = new MutableEntry();
            _entries.Add(key, entry);
        }

        if (created)
        {
            entry.Created = checked(entry.Created + quantity);
        }
        else
        {
            entry.Destroyed = checked(entry.Destroyed + quantity);
        }
    }

    private sealed class MutableEntry
    {
        public long Created { get; set; }
        public long Destroyed { get; set; }
    }
}
