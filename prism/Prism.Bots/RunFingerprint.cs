using System.Text;

namespace Prism.Bots;

/// <summary>
/// Canonical 64-bit FNV-1a hash of a run's observable output. Two runs of the same seed, content
/// and bot decisions must produce the same value on every platform; that is the determinism pin
/// PRISM-077/079 locks.
/// </summary>
public static class RunFingerprint
{
    public const ulong Empty = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

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
