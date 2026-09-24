namespace Simulation.Core.Prng;

/// <summary>
/// Générateur pseudo-aléatoire déterministe xoshiro256** (Blackman &amp; Vigna).
///
/// <para>
/// Type valeur immuable : chaque tirage renvoie le résultat ET l'état suivant.
/// Pour une même graine et un même nombre de tirages, la séquence est identique
/// bit-à-bit quelle que soit la plateforme — les trajectoires de simulation
/// restent reproductibles (cf. DETERMINISM.md, arrêt d'utilisation de System.Random).
/// </para>
/// </summary>
public readonly struct Xoshiro256StarStar
{
    private readonly ulong _s0;
    private readonly ulong _s1;
    private readonly ulong _s2;
    private readonly ulong _s3;

    public Xoshiro256StarStar(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        _s0 = s0;
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;
    }

    /// <summary>Initialise le générateur depuis une graine 64 bits via SplitMix64.</summary>
    public static Xoshiro256StarStar Create(ulong seed)
    {
        ulong state = seed;
        ulong s0 = SplitMix64.Next(ref state);
        ulong s1 = SplitMix64.Next(ref state);
        ulong s2 = SplitMix64.Next(ref state);
        ulong s3 = SplitMix64.Next(ref state);
        return new Xoshiro256StarStar(s0, s1, s2, s3);
    }

    /// <summary>Entier pseudo-aléatoire uniformément distribué sur 64 bits, état suivant renvoyé.</summary>
    public Xoshiro256StarStar NextUInt64(out ulong value)
    {
        value = Rotl(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        // Corps xoshiro256** : s2^=s0 ; s3^=s1 ; s1^=s2' ; s0^=s3' ; s2'^=t ; s3=rotl(s3',45)
        ulong s0 = _s0 ^ _s1 ^ _s3;
        ulong s1 = _s0 ^ _s1 ^ _s2;
        ulong s2 = _s0 ^ _s2 ^ t;
        ulong s3 = Rotl(_s3 ^ _s1, 45);

        return new Xoshiro256StarStar(s0, s1, s2, s3);
    }

    /// <summary>Float dans [0, 1) sur 53 bits (définition classique xoshiro), état suivant renvoyé.</summary>
    public Xoshiro256StarStar NextDouble(out double value)
    {
        var next = NextUInt64(out ulong bits);
        value = (bits >> 11) * (1.0 / 9007199254740992.0);
        return next;
    }

    /// <summary>État complet du générateur (4 × ulong) — persistance bit-à-bit (PERSISTENCE.md §4).</summary>
    internal (ulong S0, ulong S1, ulong S2, ulong S3) State => (_s0, _s1, _s2, _s3);

    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));
}