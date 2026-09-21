using Simulation.Core.Prng;
using Xunit;

namespace Simulation.Core.Tests.Prng;

public class Xoshiro256StarStarTests
{
    // Vecteurs de référence calculés via une implémentation indépendante
    // (script Python xoshiro_ref.py, algorithme Blackman & Vigna) — pin de la
    // séquence : toute dérive algorithmique silencieuse casse la reproductibilité.
    [Theory]
    [InlineData(0UL, "99ec5f36cb75f2b4", "bf6e1f784956452a", "1a5f849d4933e6e0", "6aa594f1262d2d2c")]
    [InlineData(1UL, "b3f2af6d0fc710c5", "853b559647364cea", "92f89756082a4514", "642e1c7bc266a3a7")]
    [InlineData(42UL, "15780b2e0c2ec716", "6104d9866d113a7e", "ae17533239e499a1", "ecb8ad4703b360a1")]
    [InlineData(3735928559UL, "c5555444a74d7e83", "65c30d37b4b16e38", "54f773200a4efa23", "429aed75fb958af7")]
    public void Sequence_Pinned_ByIndependentReference(
        ulong seed, string expected0, string expected1, string expected2, string expected3)
    {
        var rng = Xoshiro256StarStar.Create(seed);

        for (int i = 0; i < 4; i++)
        {
            rng = rng.NextUInt64(out ulong value);
            string expected = i switch
            {
                0 => expected0,
                1 => expected1,
                2 => expected2,
                _ => expected3,
            };
            Assert.Equal(expected, value.ToString("x16"));
        }
    }

    [Fact]
    public void SameSeed_ProducesIdenticalStream()
    {
        var first = Xoshiro256StarStar.Create(2026);
        var second = Xoshiro256StarStar.Create(2026);

        for (int i = 0; i < 1000; i++)
        {
            first = first.NextUInt64(out ulong a);
            second = second.NextUInt64(out ulong b);
            Assert.Equal(a, b);
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentStreams()
    {
        var first = Xoshiro256StarStar.Create(1);
        var second = Xoshiro256StarStar.Create(2);

        bool anyDifference = false;
        for (int i = 0; i < 100; i++)
        {
            first = first.NextUInt64(out ulong a);
            second = second.NextUInt64(out ulong b);
            anyDifference |= a != b;
        }

        Assert.True(anyDifference);
    }

    [Fact]
    public void NextDouble_StaysWithinUnitRange()
    {
        var rng = Xoshiro256StarStar.Create(7);
        for (int i = 0; i < 10000; i++)
        {
            rng = rng.NextDouble(out double value);
            Assert.InRange(value, 0.0, 1.0);
        }
    }
}