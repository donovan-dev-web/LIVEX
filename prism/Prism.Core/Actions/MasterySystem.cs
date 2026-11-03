using System.Text.Json;

namespace Prism.Core.Actions;

public static class MasteryCategories
{
    public const string Mining = "mine";
    public const string Crafting = "craft";
    public const string Farming = "farm";
    public const string Building = "build";
    public const string Smelting = "smelt";
    public const string Cooking = "cook";
}

public sealed record MasteryRules
{
    public MasteryRules(int gainPerActionPermille, int maximumSpeedupPermille)
    {
        if (gainPerActionPermille is < 0 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(gainPerActionPermille));
        }

        if (maximumSpeedupPermille is < 0 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSpeedupPermille));
        }

        GainPerActionPermille = gainPerActionPermille;
        MaximumSpeedupPermille = maximumSpeedupPermille;
    }

    public int GainPerActionPermille { get; }
    public int MaximumSpeedupPermille { get; }

    public static MasteryRules Default { get; } = new(5, 500);
}

public sealed record MasteryLevelSnapshot(ulong AgentId, string Category, int Permille);

public sealed class MasterySystem
{
    private const int CheckpointVersion = 1;
    private static readonly JsonSerializerOptions CheckpointJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SortedDictionary<ulong, SortedDictionary<string, int>> _levels = [];
    private MasteryRules _rules;

    public MasterySystem(MasteryRules? rules = null) => _rules = rules ?? MasteryRules.Default;

    public MasteryRules Rules => _rules;
    public bool IsEmpty => _levels.Count == 0;
    public int AgentCount => _levels.Count;

    public int GetPermille(ulong agentId, string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        return _levels.TryGetValue(agentId, out var categories) &&
               categories.TryGetValue(category, out var permille)
            ? permille
            : 0;
    }

    public int Award(ulong agentId, string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        var current = GetPermille(agentId, category);
        var next = Math.Min(
            _rules.MaximumSpeedupPermille,
            checked(current + _rules.GainPerActionPermille));
        if (next == current)
        {
            return current;
        }

        if (!_levels.TryGetValue(agentId, out var categories))
        {
            categories = new SortedDictionary<string, int>(StringComparer.Ordinal);
            _levels.Add(agentId, categories);
        }

        categories[category] = next;
        return next;
    }

    public bool Forget(ulong agentId) => _levels.Remove(agentId);

    public IReadOnlyList<MasteryLevelSnapshot> Snapshot() =>
        _levels
            .SelectMany(agent => agent.Value.Select(category => new MasteryLevelSnapshot(
                agent.Key,
                category.Key,
                category.Value)))
            .ToArray();

    public byte[] CaptureState()
    {
        var checkpoint = new MasteryCheckpoint(
            CheckpointVersion,
            _rules.GainPerActionPermille,
            _rules.MaximumSpeedupPermille,
            Snapshot()
                .Select(static level => new MasteryLevelCheckpoint(
                    level.AgentId,
                    level.Category,
                    level.Permille))
                .ToArray());
        return JsonSerializer.SerializeToUtf8Bytes(checkpoint, CheckpointJsonOptions);
    }

    public void RestoreEmptyState()
    {
        if (!IsEmpty)
        {
            throw new JsonException("Mastery state can only be initialized as empty in a new runtime.");
        }
    }

    public void RestoreState(ReadOnlySpan<byte> checkpointBytes)
    {
        if (!IsEmpty)
        {
            throw new InvalidOperationException("Mastery state can only be restored into a new system.");
        }

        var checkpoint = JsonSerializer.Deserialize<MasteryCheckpoint>(
                             checkpointBytes,
                             CheckpointJsonOptions)
                         ?? throw new InvalidDataException("Mastery checkpoint is empty.");
        if (checkpoint.Version != CheckpointVersion ||
            checkpoint.Levels is null ||
            checkpoint.GainPerActionPermille is < 0 or > 1000 ||
            checkpoint.MaximumSpeedupPermille is < 0 or > 1000)
        {
            throw new InvalidDataException("Mastery checkpoint version or rules are invalid.");
        }

        var restored = new SortedDictionary<ulong, SortedDictionary<string, int>>();
        foreach (var level in checkpoint.Levels)
        {
            if (level is null ||
                string.IsNullOrWhiteSpace(level.Category) ||
                level.Permille < 0 ||
                level.Permille > checkpoint.MaximumSpeedupPermille)
            {
                throw new InvalidDataException("Mastery checkpoint contains an invalid level.");
            }

            if (!restored.TryGetValue(level.AgentId, out var categories))
            {
                categories = new SortedDictionary<string, int>(StringComparer.Ordinal);
                restored.Add(level.AgentId, categories);
            }

            if (!categories.TryAdd(level.Category, level.Permille))
            {
                throw new InvalidDataException("Mastery checkpoint contains duplicate levels.");
            }
        }

        _rules = new MasteryRules(
            checkpoint.GainPerActionPermille,
            checkpoint.MaximumSpeedupPermille);
        _levels.Clear();
        foreach (var (agentId, categories) in restored)
        {
            _levels.Add(agentId, categories);
        }
    }

    private sealed record MasteryCheckpoint(
        int Version,
        int GainPerActionPermille,
        int MaximumSpeedupPermille,
        MasteryLevelCheckpoint[] Levels);

    private sealed record MasteryLevelCheckpoint(
        ulong AgentId,
        string Category,
        int Permille);
}
