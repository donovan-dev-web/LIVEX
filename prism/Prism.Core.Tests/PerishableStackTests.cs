using Prism.Core.Accounting;

namespace Prism.Core.Tests;

public sealed class PerishableStackTests
{
    [Fact]
    public void FreshnessDecaysLazilyAccordingToCurrentContainerRate()
    {
        var stack = PerishableStack.Create(itemTypeId: 7, quantity: 3, shelfLifeTicks: 10, currentTick: 5);
        var ledger = new MatterLedger();

        var accessed = PerishableStackService.Access(stack, 9, containerDecayPermille: 500, ledger);

        Assert.NotNull(accessed.Stack);
        Assert.Equal(8_000, accessed.Stack!.Value.RemainingFreshnessPermilleTicks);
        Assert.Equal(9, accessed.Stack.Value.LastUpdatedTick);
        Assert.Null(accessed.Spoilage);
        Assert.Empty(ledger.EnumerateEntries());
    }

    [Fact]
    public void StoredStackDecaysMoreSlowlyAndMovementMaterializesSourceFreshness()
    {
        var stack = PerishableStack.Create(itemTypeId: 8, quantity: 2, shelfLifeTicks: 10, currentTick: 0);

        var movedFromGranary = PerishableStackService.Move(
            stack, currentTick: 4, sourceDecayPermille: 500, new MatterLedger());
        var accessedInInventory = movedFromGranary.Advance(currentTick: 6, decayPermillePerTick: 1000);

        Assert.Equal(8_000, movedFromGranary.RemainingFreshnessPermilleTicks);
        Assert.Equal(6_000, accessedInInventory.Stack!.Value.RemainingFreshnessPermilleTicks);
        Assert.Equal(6, accessedInInventory.Stack.Value.LastUpdatedTick);
    }

    [Fact]
    public void SpoilageDestroysTheWholeStackOnceAndReportsAnEvent()
    {
        var stack = PerishableStack.Create(itemTypeId: 9, quantity: 4, shelfLifeTicks: 3, currentTick: 10);
        var ledger = new MatterLedger();

        var result = PerishableStackService.Access(stack, 13, containerDecayPermille: 1000, ledger);

        Assert.Null(result.Stack);
        Assert.Equal(new SpoilageEvent(13, 9, 4), result.Spoilage);
        Assert.Equal(
            new MatterLedgerEntry(9, "spoil", Created: 0, Destroyed: 4),
            Assert.Single(ledger.EnumerateEntries()));
    }

    [Fact]
    public void SpoilageOverflowSaturatesAndMoveRejectsAnExpiredStack()
    {
        var tinyStack = PerishableStack.Create(10, 1, 1, 0) with
        {
            RemainingFreshnessPermilleTicks = long.MaxValue,
        };
        var advanced = tinyStack.Advance(long.MaxValue, int.MaxValue);

        Assert.Null(advanced.Stack);
        Assert.Equal(long.MaxValue, advanced.Spoilage!.Value.Tick);
        var ledger = new MatterLedger();
        Assert.Throws<PerishableStackSpoiledException>(() =>
            PerishableStackService.Move(
                PerishableStack.Create(10, 1, 1, 0),
                currentTick: 1,
                sourceDecayPermille: 1000,
                ledger));
        Assert.Equal(1, Assert.Single(ledger.EnumerateEntries()).Destroyed);
    }

    [Fact]
    public void RejectsInvalidFreshnessRateAndTimeReversal()
    {
        var stack = PerishableStack.Create(11, 1, 10, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => stack.Advance(1, 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => PerishableStackService.Access(
            stack, 3, containerDecayPermille: 10_001, new MatterLedger()));
        Assert.Throws<ArgumentOutOfRangeException>(() => PerishableStack.Create(11, 0, 10, 0));
    }
}
