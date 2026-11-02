using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class TransferActionSystemTests
{
    [Fact]
    public void TransferMovesExactQuantityAndPreservesToolDurability()
    {
        var miningRules = new BlockActionRules(
            [new MiningMaterialRule(1, true, 1, 0, [new ActionDropRule(10, 1, 1, 1000, null)])],
            [
                new ActionItemRule(10, 10, 0),
                new ActionItemRule(20, 1, 1, Durability: 5),
            ]);
        var world = new WorldGrid(2, 1, verticalSize: 16);
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(4, 1, 0, 0));
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(4, 1, 0, 0));
        var movement = new MovementGraph(world, new MovementRules([1], []));
        var agentRegistry = new AgentRegistry();
        var sender = agentRegistry.Incarnate(1, AgentPosition.AtCell(0, 0, 5), new(), movement);
        var recipient = agentRegistry.Incarnate(2, AgentPosition.AtCell(1, 0, 5), new(CarrySlots: 2), movement);
        Assert.True(sender.Inventory.TryAdd(10, 2, 10, out _));
        Assert.True(sender.Inventory.TryAdd(20, 1, 1, out _));
        var damaged = BlockActionSystem.Mine(
            world,
            sender,
            new GroundItemStore(),
            miningRules,
            new Prism.Core.Accounting.MatterLedger(),
            1,
            0,
            new BlockCoordinate(0, 0, 4),
            targetPerceived: true);
        Assert.True(damaged.Succeeded);
        Assert.Equal(4, Assert.Single(sender.Inventory.Snapshot(), stack => stack.ItemTypeId == 20).DurabilityRemaining);

        var movedTool = TransferActionSystem.Transfer(sender, recipient, miningRules, 20, 1);
        var movedItems = TransferActionSystem.Transfer(sender, recipient, miningRules, 10, 2);

        Assert.True(movedTool.Succeeded);
        Assert.True(movedItems.Succeeded, movedItems.ErrorCode);
        Assert.Equal(1, sender.Inventory.Count(10));
        Assert.Equal(2, recipient.Inventory.Count(10));
        Assert.Equal(4, Assert.Single(recipient.Inventory.Snapshot(), stack => stack.ItemTypeId == 20).DurabilityRemaining);
    }

    [Fact]
    public void TransferFailuresLeaveBothInventoriesUnchanged()
    {
        var (sender, recipient) = CreateAgents(recipientSlots: 1, recipientX: 5);
        Assert.True(sender.Inventory.TryAdd(10, 3, 10, out _));
        Assert.True(recipient.Inventory.TryAdd(11, 1, 10, out _));
        var (nearSender, fullRecipient) = CreateAgents(recipientSlots: 1, recipientX: 1);
        Assert.True(nearSender.Inventory.TryAdd(10, 1, 10, out _));
        Assert.True(fullRecipient.Inventory.TryAdd(11, 1, 10, out _));
        var rules = CreateRules();

        var tooFar = TransferActionSystem.Transfer(sender, recipient, rules, 10, 1);
        var full = TransferActionSystem.Transfer(nearSender, fullRecipient, rules, 10, 1);
        var missing = TransferActionSystem.Transfer(nearSender, fullRecipient, rules, 12, 1);

        Assert.Equal("too_far", tooFar.ErrorCode);
        Assert.Equal("capacity_reached", full.ErrorCode);
        Assert.Equal("item_missing", missing.ErrorCode);
        Assert.Equal(3, sender.Inventory.Count(10));
        Assert.Equal(1, recipient.Inventory.Count(11));
        Assert.Equal(1, nearSender.Inventory.Count(10));
        Assert.Equal(1, fullRecipient.Inventory.Count(11));
    }

    private static (AgentBody Sender, AgentBody Recipient) CreateAgents(
        int recipientSlots,
        int recipientX)
    {
        var world = new WorldGrid(6, 1, verticalSize: 16);
        for (var x = 0; x < world.Width; x++)
        {
            world.SetSurfaceColumn(x, 0, new SurfaceColumn(4, 1, 0, 0));
        }

        var movement = new MovementGraph(world, new MovementRules([1], []));
        var agents = new AgentRegistry();
        return (
            agents.Incarnate(1, AgentPosition.AtCell(0, 0, 5), new(), movement),
            agents.Incarnate(2, AgentPosition.AtCell(recipientX, 0, 5), new(CarrySlots: recipientSlots), movement));
    }

    private static BlockActionRules CreateRules() =>
        new(
            [],
            [
                new ActionItemRule(10, 10, 0),
                new ActionItemRule(11, 10, 0),
                new ActionItemRule(12, 10, 0),
            ]);
}
