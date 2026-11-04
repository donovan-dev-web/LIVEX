using Prism.Core.Simulation;

namespace Prism.Core.Tests;

public sealed class ConflictArbitratorTests
{
    [Fact]
    public void ArbitrationIsIndependentOfClaimInputOrder()
    {
        ConflictClaim[] claims =
        [
            new(8, new ConflictTarget(1, 4, 5, 6, 0), 12),
            new(3, new ConflictTarget(1, 4, 5, 6, 0), 19),
            new(5, new ConflictTarget(2, 0, 0, 0, 55), 42),
            new(11, new ConflictTarget(2, 0, 0, 0, 55), 48),
        ];

        var first = ConflictArbitrator.Resolve(claims, 456, 77);
        var reversed = ConflictArbitrator.Resolve(claims.Reverse(), 456, 77);

        Assert.Equal(first, reversed);
        Assert.Equal(2, first.Count(decision => decision.IsWinner));
        Assert.Equal(2, first.Count(decision => !decision.IsWinner));
    }

    [Fact]
    public void ArbitrationIsRepeatableAndFairAcrossManyTicks()
    {
        var target = new ConflictTarget(1, 2, 3, 4, 0);
        var claims = new[] { new ConflictClaim(1, target), new ConflictClaim(2, target) };
        var firstWins = 0;
        for (ulong tick = 0; tick < 10_000; tick++)
        {
            var result = ConflictArbitrator.Resolve(claims, 12345, tick);
            Assert.Equal(result, ConflictArbitrator.Resolve(claims, 12345, tick));
            if (Assert.Single(result, decision => decision.IsWinner).Claim.AgentId == 1)
            {
                firstWins++;
            }
        }

        Assert.InRange(firstWins, 4_300, 5_700);
    }

    [Fact]
    public void ArbitrationRejectsDuplicateClaimsForOneTarget()
    {
        var target = new ConflictTarget(1, 2, 3, 4, 0);
        var claims = new[] { new ConflictClaim(1, target), new ConflictClaim(1, target, 8) };

        Assert.Throws<ArgumentException>(() => ConflictArbitrator.Resolve(claims, 3, 9));
    }

    [Fact]
    public void AgentMaySubmitDistinctClaimsForDifferentTargets()
    {
        var claims = new[]
        {
            new ConflictClaim(1, new ConflictTarget(1, 0, 0, 0, 0)),
            new ConflictClaim(1, new ConflictTarget(1, 1, 0, 0, 0)),
        };

        Assert.Equal(2, ConflictArbitrator.Resolve(claims, 3, 9).Count);
    }
}
