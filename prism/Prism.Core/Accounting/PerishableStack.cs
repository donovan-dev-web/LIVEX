namespace Prism.Core.Accounting;

public readonly record struct PerishableStack(
    ushort ItemTypeId,
    long Quantity,
    long RemainingFreshnessPermilleTicks,
    long LastUpdatedTick)
{
    public static PerishableStack Create(
        ushort itemTypeId,
        long quantity,
        int shelfLifeTicks,
        long currentTick)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (shelfLifeTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shelfLifeTicks));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(currentTick);
        return new PerishableStack(
            itemTypeId,
            quantity,
            checked((long)shelfLifeTicks * 1000),
            currentTick);
    }

    public PerishableStackAdvance Advance(long currentTick, int decayPermillePerTick)
    {
        if (Quantity <= 0)
        {
            throw new InvalidOperationException("A perishable stack must contain at least one item.");
        }

        if (RemainingFreshnessPermilleTicks < 0)
        {
            throw new InvalidOperationException("Remaining freshness cannot be negative.");
        }

        if (LastUpdatedTick < 0 || currentTick < LastUpdatedTick)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Freshness cannot move backwards.");
        }

        if (decayPermillePerTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decayPermillePerTick));
        }

        var elapsed = currentTick - LastUpdatedTick;
        var decay = (Int128)elapsed * decayPermillePerTick;
        var consumedFreshness = decay >= RemainingFreshnessPermilleTicks
            ? RemainingFreshnessPermilleTicks
            : (long)decay;
        var remaining = RemainingFreshnessPermilleTicks - consumedFreshness;
        if (remaining == 0)
        {
            return new PerishableStackAdvance(
                null,
                new SpoilageEvent(currentTick, ItemTypeId, Quantity));
        }

        return new PerishableStackAdvance(
            this with
            {
                RemainingFreshnessPermilleTicks = remaining,
                LastUpdatedTick = currentTick,
            },
            null);
    }
}

public readonly record struct SpoilageEvent(long Tick, ushort ItemTypeId, long Quantity);

public readonly record struct PerishableStackAdvance(
    PerishableStack? Stack,
    SpoilageEvent? Spoilage);

public static class PerishableStackService
{
    public static PerishableStackAdvance Access(
        PerishableStack stack,
        long currentTick,
        int containerDecayPermille,
        MatterLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        if (containerDecayPermille is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(containerDecayPermille));
        }

        var result = stack.Advance(currentTick, containerDecayPermille);
        if (result.Spoilage is { } spoilage)
        {
            ledger.RecordDestroyed(spoilage.ItemTypeId, spoilage.Quantity, "spoil");
        }

        return result;
    }

    public static PerishableStack Move(
        PerishableStack stack,
        long currentTick,
        int sourceDecayPermille,
        MatterLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        if (sourceDecayPermille is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceDecayPermille));
        }

        var advanced = stack.Advance(currentTick, sourceDecayPermille);
        if (advanced.Stack is not { } moved)
        {
            ledger.RecordDestroyed(
                advanced.Spoilage!.Value.ItemTypeId,
                advanced.Spoilage.Value.Quantity,
                "spoil");
            throw new PerishableStackSpoiledException(advanced.Spoilage!.Value);
        }

        return moved;
    }
}

public sealed class PerishableStackSpoiledException(SpoilageEvent spoilage)
    : InvalidOperationException("The item stack spoiled before it could be moved.")
{
    public SpoilageEvent Spoilage { get; } = spoilage;
}
