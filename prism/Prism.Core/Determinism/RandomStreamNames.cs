namespace Prism.Core.Determinism;

public static class RandomStreamNames
{
    public const string Terrain = "terrain";
    public const string Ores = "ores";
    public const string Forest = "forest";
    public const string Spawn = "spawn";
    public const string RandomTicks = "randomTicks";
    public const string Conflicts = "conflicts";
    public const string GrowthJitter = "growthJitter";
    public const string Drops = "drops";
    public const string GrassSpread = "grassSpread";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
    [
        Terrain,
        Ores,
        Forest,
        Spawn,
        RandomTicks,
        Conflicts,
        GrowthJitter,
        Drops,
        GrassSpread,
    ]);
}
