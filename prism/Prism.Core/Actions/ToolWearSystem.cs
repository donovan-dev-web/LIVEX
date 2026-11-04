using Prism.Core.Accounting;
using Prism.Core.World;

namespace Prism.Core.Actions;

internal static class ToolWearSystem
{
    public static bool Apply(
        AgentBody agent,
        ActionItemRule item,
        MatterLedger ledger)
    {
        if (item.Durability == 0)
        {
            return false;
        }

        if (!agent.Inventory.TryApplyWear(
                item.ItemTypeId,
                item.Durability,
                item.WearPerAction,
                out var broken))
        {
            throw new InvalidOperationException(
                $"Tool {item.ItemTypeId} disappeared after its action preflight.");
        }

        if (broken)
        {
            ledger.RecordDestroyed(item.ItemTypeId, 1, "wear");
        }

        return broken;
    }
}
