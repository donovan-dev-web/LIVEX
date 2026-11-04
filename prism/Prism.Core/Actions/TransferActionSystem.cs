using Prism.Core.World;

namespace Prism.Core.Actions;

public sealed record TransferActionResult(bool Succeeded, string? ErrorCode, int Quantity)
{
    public static TransferActionResult Failure(string errorCode) =>
        new(false, errorCode, 0);

    public static TransferActionResult Success(int quantity) =>
        new(true, null, quantity);
}

public static class TransferActionSystem
{
    public static TransferActionResult Transfer(
        AgentBody sender,
        AgentBody recipient,
        BlockActionRules rules,
        ushort itemTypeId,
        int quantity,
        int maximumDistance = 3,
        bool recipientPerceived = true)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDistance);

        if (sender.AgentId == recipient.AgentId)
        {
            return TransferActionResult.Failure("refused");
        }

        if (!recipientPerceived)
        {
            return TransferActionResult.Failure("not_perceived");
        }

        var dx = (long)sender.Position.CellX - recipient.Position.CellX;
        var dy = (long)sender.Position.CellY - recipient.Position.CellY;
        if (dx * dx + dy * dy > (long)maximumDistance * maximumDistance)
        {
            return TransferActionResult.Failure("too_far");
        }

        if (!rules.TryGetItem(itemTypeId, out var item))
        {
            return TransferActionResult.Failure("unknown_item");
        }

        if (sender.Inventory.Count(itemTypeId) < quantity)
        {
            return TransferActionResult.Failure("item_missing");
        }

        var sourceStacks = sender.Inventory.Snapshot()
            .Where(stack => stack.ItemTypeId == itemTypeId)
            .ToArray();
        if (sourceStacks.Any(stack => stack.MaxStackSize != item.MaxStackSize))
        {
            return TransferActionResult.Failure("inventory_stack_limit_mismatch");
        }

        if (!recipient.Inventory.CanAdd(itemTypeId, quantity, item.MaxStackSize))
        {
            return TransferActionResult.Failure("capacity_reached");
        }

        if (!sender.Inventory.TryTake(itemTypeId, quantity, out var removed))
        {
            return TransferActionResult.Failure("item_missing");
        }

        if (!recipient.Inventory.TryAddStacks(removed))
        {
            if (!sender.Inventory.TryAddStacks(removed))
            {
                throw new InvalidOperationException("Failed to roll back an unsuccessful agent transfer.");
            }

            return TransferActionResult.Failure("capacity_reached");
        }

        return TransferActionResult.Success(quantity);
    }
}
