using Simulation.Core.Prng;
using Xunit;

namespace Simulation.Core.Tests.Prng;

/// <summary>
/// Vecteurs de contrôle des primitives de hachage canoniques centralisées au
/// jalon review/refactor (engineVersion 0.12.0).
///
/// <para>
/// Avant cette centralisation, la même avalanche SplitMix64 était recopiée à
/// l'identique dans <c>CommunicationSystem</c>, <c>PriorityConflictResolver</c>,
/// <c>Inheritance</c>, <c>ActionExecutor</c> et <c>AStarPathfinder</c>, et le
/// hachage FNV-1a dans <c>Perception</c>, <c>SimulationSnapshotCodec</c> et le
/// lanceur CLI. Ces copies étaient bit-à-bit équivalentes — vérifié ici par
/// vecteurs de contrôle — mais toute retouche ultérieure d'une seule copie
/// produirait une dérive silencieuse de la trajectoire.
/// </para>
///
/// <para>
/// Ces tests épinglent donc les <b>constantes</b> : une modification de
/// <see cref="SplitMix64.Avalanche"/> ou de <see cref="Fnv1a64"/> doit être un
/// acte délibéré, assorti d'un bump MINOR de la version moteur et d'un
/// ré-échelonnement des checksums dorés (DETERMINISM.md §7).
/// </para>
/// </summary>
public class HashPrimitiveTests
{
    /// <summary>
    /// Vecteurs de contrôle de l'avalanche SplitMix64, recalculés à partir des
    /// constantes canoniques (« finalizer » de Steele/Lea/Flood) et non recopiés
    /// depuis l'implémentation. <c>Avalanche(0) == 0</c> est attendu : la
    /// fonction est une bijection et ne dépend que de son entrée, sans incrément
    /// d'état — c'est précisément ce qui la rend réutilisable comme hachage.
    /// </summary>
    [Theory]
    [InlineData(0UL, 0x0UL)]
    [InlineData(1UL, 0x5692161D100B05E5UL)]
    [InlineData(2UL, 0xDBD238973A2B148AUL)]
    [InlineData(0xDEADBEEFUL, 0x4E062702EC929EEAUL)]
    public void Avalanche_MatchesCanonicalVectors(ulong input, ulong expected)
    {
        Assert.Equal(expected, SplitMix64.Avalanche(input));
    }

    [Fact]
    public void Avalanche_IsBitForBitEqualToTheFinalizeAlias()
    {
        // Les deux noms coexistent : Finalize est l'alias historique conservé pour
        // les appelants d'origine, Avalanche le nom canonique. Toute divergence
        // entre les deux serait un bug silencieux de reproductibilité.
        for (ulong z = 0; z < 512; z++)
        {
            Assert.Equal(SplitMix64.Avalanche(z), SplitMix64.Finalize(z));
        }
    }

    [Fact]
    public void Avalanche_IsAPureFunction_WithoutGlobalState()
    {
        // Deux évaluations successives, y compris après avoir consommé le PRNG
        // global, doivent rester identiques : aucun état caché ne doit s'infiltrer
        // dans le résultat (c'est tout l'intérêt d'un hachage déterministe).
        ulong reference = SplitMix64.Avalanche(0xDEADBEEFUL);

        ulong state = 12345UL;
        for (int i = 0; i < 64; i++)
        {
            _ = SplitMix64.Next(ref state);
            Assert.Equal(reference, SplitMix64.Avalanche(0xDEADBEEFUL));
        }

        // SplitMix64.Next reste, lui, un générateur séquentiel : Avalanche ne doit
        // surtout pas l'être (c'est ce qui permet de l'utiliser comme hachage).
        Assert.NotEqual(SplitMix64.Avalanche(1UL), SplitMix64.Avalanche(2UL));
    }

    [Fact]
    public void Gamma_IsTheCanonicalSplitMix64OddMultiplier()
    {
        Assert.Equal(0x9E3779B97F4A7C15UL, SplitMix64.Gamma);
    }

    /// <summary>
    /// FNV-1a doit rester conforme à ses constantes normatives (offset basis et
    /// premier). Le hachage sert de contrôle d'état pour la persistance et
    /// l'observabilité : une dérive de constante invaliderait silencieusement
    /// toutes les comparaisons de trajectoires.
    /// </summary>
    [Fact]
    public void Fnv1a_UsesCanonicalConstants()
    {
        Assert.Equal(14695981039346656037UL, Fnv1a64.OffsetBasis);
        Assert.Equal(1099511628211UL, Fnv1a64.Prime);
    }

    [Fact]
    public void Fnv1a_HashUtf8_MatchesTheInlineReferenceImplementation()
    {
        // Recalcul par la boucle littérale telle qu'elle figurait dans Perception,
        // SimulationSnapshotCodec et les suites de tests : la centralisation doit
        // être strictement neutre.
        string[] samples =
        [
            string.Empty,
            "a",
            "rocher-1",
            "syne-observability-snapshot",
            "tick=12345;entity=42;energy=0.87",
        ];

        foreach (string sample in samples)
        {
            Assert.Equal(ReferenceFnv1a(sample), Fnv1a64.HashUtf8(sample));
        }
    }

    [Fact]
    public void Fnv1a_AppendBytes_IsIncrementalAndEqualToWholeString()
    {
        const string text = "territories|spring|2|42,43";
        byte[] first = System.Text.Encoding.UTF8.GetBytes(text[..5]);
        byte[] rest = System.Text.Encoding.UTF8.GetBytes(text[5..]);

        ulong incremental = Fnv1a64.Append(Fnv1a64.Append(Fnv1a64.Seed, first), rest);

        Assert.Equal(Fnv1a64.HashUtf8(text), incremental);
    }

    [Fact]
    public void Fnv1a_AppendSingleByte_EqualsAppendSpan()
    {
        // Les deux surcharges doivent rester équivalentes : une divergence
        // produirait deux hachages différents pour la même entrée selon le site
        // appelant, ce qui est indétectable à l'exécution.
        byte[] payload = System.Text.Encoding.UTF8.GetBytes("perception");
        ulong byByte = Fnv1a64.Seed;
        foreach (byte value in payload)
        {
            byByte = Fnv1a64.Append(byByte, value);
        }

        Assert.Equal(Fnv1a64.Append(Fnv1a64.Seed, payload), byByte);
    }

    private static ulong ReferenceFnv1a(string text)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte value in System.Text.Encoding.UTF8.GetBytes(text))
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }

        return hash;
    }
}
