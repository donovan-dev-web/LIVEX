namespace Simulation.Core.Prng;

/// <summary>
/// Mélangeur 64 bits SplitMix64 (faux-aléatoire déterministe) utilisé pour
/// initialiser l'état d'un <see cref="Xoshiro256StarStar"/> à partir d'une graine.
/// Purement déterministe : aucune dépendance à <see cref="System.Random"/>.
/// </summary>
public static class SplitMix64
{
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
    private const ulong M1 = 0xBF58476D1CE4E5B9UL;
    private const ulong M2 = 0x94D049BB133111EBUL;

    public static ulong Next(ref ulong state)
    {
        state += GoldenGamma;
        ulong z = state;
        z = (z ^ (z >> 30)) * M1;
        z = (z ^ (z >> 27)) * M2;
        return z ^ (z >> 31);
    }
}