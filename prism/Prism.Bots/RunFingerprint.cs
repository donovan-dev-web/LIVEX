using System.Text;
using Prism.Contracts;

namespace Prism.Bots;

/// <summary>
/// Canonical 64-bit FNV-1a hash of a run's observable output. Two runs of the same seed, content
/// and bot decisions must produce the same value on every platform; that is the determinism pin
/// PRISM-077/079 locks.
/// <para>
/// This is the <b>byte level</b> of the two-level fingerprints of PRISM-107: it hashes the whole
/// serialized stream of a tick — every event kind, agent and raw payload, every observation's raw
/// changes and self JSON, in the order the host published them. It answers "is the stream
/// identical, bit for bit?", which is what the contract and a replay need. It is deliberately
/// strict: a different emission order moves it, so it must not be used to judge a performance
/// change — <see cref="RunTrajectory"/> is the level for that question.
/// </para>
/// </summary>
public static class RunFingerprint
{
    public const ulong Empty = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    /// <summary>
    /// Appends one tick of the serialized stream: the tick, then its events and its observations in
    /// the published order, each with its raw JSON payload. The bot run fingerprint is this call
    /// folded over every tick, so any change to this method re-pins every golden campaign.
    /// </summary>
    public static ulong AppendSerializedTick(ulong hash, TickResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        hash = Append(hash, result.Tick);
        foreach (var eventEnvelope in result.Events)
        {
            hash = Append(hash, eventEnvelope.Kind);
            hash = Append(hash, eventEnvelope.AgentId?.ToString() ?? "-");
            hash = Append(hash, eventEnvelope.Payload.GetRawText());
        }

        foreach (var observation in result.Observations)
        {
            hash = Append(hash, "obs");
            hash = Append(hash, observation.AgentId);
            hash = Append(hash, observation.Changes.GetRawText());
            hash = Append(hash, observation.Self.GetRawText());
        }

        return hash;
    }

    public static ulong Append(ulong hash, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            hash = (hash ^ value) * Prime;
        }

        return hash;
    }

    public static ulong Append(ulong hash, long value) =>
        Append(hash, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static ulong Append(ulong hash, ulong value) =>
        Append(hash, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static string Format(ulong hash) => hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
}
