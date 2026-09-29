namespace Simulation.Core.Prng;

/// <summary>
/// Mélangeur 64 bits SplitMix64 (faux-aléatoire déterministe) utilisé pour
/// initialiser l'état d'un <see cref="Xoshiro256StarStar"/> à partir d'une graine.
/// Purement déterministe : aucune dépendance à <see cref="System.Random"/>.
///
/// <see cref="Finalize"/> expose l'avalanche (multiplication + xor-shift) séparément
/// du générateur séquentiel : la même fonction d'avalanche servait réimplémentée à
/// l'identique dans la communication, les conflits de priorité, l'héritage, le
/// pathfinding et l'observabilité. Divergence future d'une constante = trajectoires
/// non reproductibles ; un seul site fait désormais autorité.
/// </summary>
public static class SplitMix64
{
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
    private const ulong M1 = 0xBF58476D1CE4E5B9UL;
    private const ulong M2 = 0x94D049BB133111EBUL;

    /// <summary>Constante de mélange « γ » (multiplicateur impair canonique de SplitMix64).</summary>
    public const ulong Gamma = GoldenGamma;

    public static ulong Next(ref ulong state)
    {
        state += GoldenGamma;
        return Avalanche(state);
    }

    /// <summary>
    /// Avalanche SplitMix64 (« finalizer ») d'une valeur déjà mélangée : fonction
    /// pure et stable, sans état. Utilisée partout où un hachage déterministe est
    /// nécessaire sans consommer le PRNG global (DETERMINISM.md §3).
    /// </summary>
    public static ulong Avalanche(ulong z)
    {
        z = (z ^ (z >> 30)) * M1;
        z = (z ^ (z >> 27)) * M2;
        return z ^ (z >> 31);
    }

    /// <summary>Alias historique de <see cref="Avalanche"/> (finaliseur d'avalanche stable).</summary>
    public static ulong Finalize(ulong z) => Avalanche(z);
}