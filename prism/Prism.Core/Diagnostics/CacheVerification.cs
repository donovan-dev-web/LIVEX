namespace Prism.Core.Diagnostics;

/// <summary>
/// Opt-in verification of the caches the 10 November 2026 optimisations rely on (PRISM-108).
/// <para>
/// Every one of those caches assumes the same thing: <b>any effect on the world is applied
/// through a grid write, therefore a <see cref="World.WorldGrid.Revision"/> bump</b>. If a
/// mutation ever slips past the revision counter, the cache keeps answering from the old state —
/// no exception, no red test, just a wrong trajectory and a different fingerprint later. Turning
/// <c>PRISM_VERIFY_CACHES=1</c> on makes the caches recompute their answer without the memo and
/// compare: the first disagreement throws a <see cref="CacheVerificationException"/> that names
/// the cache, the tick, the agent and the cell.
/// </para>
/// <para>
/// The mode is read once at start-up, never writes to the world, and is free when off.
/// </para>
/// </summary>
public static class CacheVerification
{
    /// <summary>Environment variable that turns the mode on (<c>=1</c>).</summary>
    public const string EnvironmentVariable = "PRISM_VERIFY_CACHES";

    public static bool FromEnvironment() =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) == "1";
}

/// <summary>
/// A cache contradicted the world it is supposed to mirror. Raised only when
/// <c>PRISM_VERIFY_CACHES=1</c>: the run is meant to fail loudly rather than drift silently.
/// </summary>
public sealed class CacheVerificationException : InvalidOperationException
{
    public CacheVerificationException(
        string cache,
        long tick,
        ulong? agentId,
        string location,
        string detail)
        : base(BuildMessage(cache, tick, agentId, location, detail))
    {
        Cache = cache;
        Tick = tick;
        AgentId = agentId;
        Location = location;
        Detail = detail;
    }

    /// <summary>Name of the cache that lied, e.g. <c>perception.window</c>.</summary>
    public string Cache { get; }

    /// <summary>Tick the divergence was found on, or -1 when the caller had no tick to give.</summary>
    public long Tick { get; }

    /// <summary>Agent whose cache diverged, when the cache is per agent.</summary>
    public ulong? AgentId { get; }

    /// <summary>Cell or region the divergence was found at.</summary>
    public string Location { get; }

    /// <summary>What the cache said against what a fresh evaluation says.</summary>
    public string Detail { get; }

    private static string BuildMessage(
        string cache,
        long tick,
        ulong? agentId,
        string location,
        string detail)
    {
        var tickText = tick >= 0
            ? tick.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "unknown";
        var agentText = agentId is { } id
            ? id.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "none";
        return $"Cache verification failed [{cache}]: tick {tickText}, agent {agentText}, " +
               $"at {location}: {detail}. A world mutation happened without a grid revision bump, " +
               "or the cache is invalidated on the wrong key.";
    }
}
