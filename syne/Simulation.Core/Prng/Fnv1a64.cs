namespace Simulation.Core.Prng;

/// <summary>
/// Hachage FNV-1a 64 bits canonique (DETERMINISM.md §6) : hachage de contrôle de
/// l'état du monde et de la description de monde, reproductible bit-à-bit à seed
/// égale. Centralisé car la même fonction (offset <c>14695981039346656037</c>,
/// premier <c>1099511628211</c>) était recopiée dans la persistance, la perception
/// et le lanceur CLI — trois sites à garder synchronisés à la main.
/// </summary>
public static class Fnv1a64
{
    /// <summary>Offset basis FNV-1a 64 bits.</summary>
    public const ulong OffsetBasis = 14695981039346656037UL;

    /// <summary>Premier multiplicateur premier FNV (64 bits).</summary>
    public const ulong Prime = 1099511628211UL;

    /// <summary>État initial d'un hachage FNV-1a.</summary>
    public static ulong Seed => OffsetBasis;

    /// <summary>Ajoute un octet à l'état FNV-1a.</summary>
    public static ulong Append(ulong hash, byte value) => (hash ^ value) * Prime;

    /// <summary>Ajoute une suite d'octets à l'état FNV-1a.</summary>
    public static ulong Append(ulong hash, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            hash = (hash ^ value) * Prime;
        }

        return hash;
    }

    /// <summary>Hachage FNV-1a 64 bits d'une chaîne UTF-8 (forme canonique).</summary>
    public static ulong HashUtf8(string text) => Append(OffsetBasis, System.Text.Encoding.UTF8.GetBytes(text));
}
