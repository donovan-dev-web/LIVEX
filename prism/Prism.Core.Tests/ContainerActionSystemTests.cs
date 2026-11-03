using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class ContainerActionSystemTests
{
    [Fact]
    public void DepositAndTakeMoveItemsWithoutChangingTheirQuantity()
    {
        var (movement, agents) = CreateAgents(2);
        var agent = Incarnate(agents, movement, 7, 0);
        var container = new ContainerInventory(
            capacitySlots: 2,
            decayPermillePerTick: 800,
            new BlockCoordinate(1, 0, 5),
            AccessPrincipal.ForAgent(agent.AgentId));
        var rules = CreateRules();
        Assert.True(agent.Inventory.TryAdd(10, 4, 5, out var remaining));
        Assert.Equal(0, remaining);

        var deposited = ContainerActionSystem.Deposit(agent, container, rules, 10, 4);
        Assert.True(deposited.Succeeded);
        Assert.Equal(4, deposited.Quantity);
        Assert.Equal(0, agent.Inventory.Count(10));
        Assert.Equal(4, container.Snapshot().Single().Quantity);

        var taken = ContainerActionSystem.Take(agent, container, rules, 10, 3);
        Assert.True(taken.Succeeded);
        Assert.Equal(3, taken.Quantity);
        Assert.Equal(3, agent.Inventory.Count(10));
        Assert.Equal(1, container.Snapshot().Single().Quantity);
    }

    [Fact]
    public void AccessListSupportsOpaqueGroupsAndDeniesUnauthorizedTransfers()
    {
        var (movement, agents) = CreateAgents(3);
        var owner = Incarnate(agents, movement, 8, 0);
        var agent = Incarnate(agents, movement, 7, 1);
        var container = new ContainerInventory(
            capacitySlots: 2,
            decayPermillePerTick: 800,
            new BlockCoordinate(2, 0, 5),
            AccessPrincipal.ForAgent(owner.AgentId));
        var group = AccessPrincipal.ForGroup(55);
        Assert.True(container.SetAccessList(
            AccessPrincipal.ForAgent(owner.AgentId),
            [new ContainerAccessRule(group, ContainerAccessRights.Put)]).Succeeded);
        Assert.True(agent.Inventory.TryAdd(10, 2, 5, out var remaining));
        Assert.Equal(0, remaining);
        var rules = CreateRules();

        var denied = ContainerActionSystem.Deposit(agent, container, rules, 10, 1);
        Assert.False(denied.Succeeded);
        Assert.Equal("access_denied", denied.ErrorCode);
        Assert.Equal(2, agent.Inventory.Count(10));

        var allowed = ContainerActionSystem.Deposit(
            agent,
            container,
            rules,
            10,
            1,
            [group]);
        Assert.True(allowed.Succeeded);

        var takeDenied = ContainerActionSystem.Take(
            agent,
            container,
            rules,
            10,
            1,
            [group]);
        Assert.False(takeDenied.Succeeded);
        Assert.Equal("access_denied", takeDenied.ErrorCode);
        Assert.Equal(1, container.Snapshot().Single().Quantity);

        Assert.Equal(
            "not_owner",
            container.SetAccessList(
                AccessPrincipal.ForAgent(agent.AgentId),
                []).ErrorCode);
    }

    [Fact]
    public void FullContainerRejectsDepositWithoutRemovingInventoryItems()
    {
        var (movement, agents) = CreateAgents(2);
        var agent = Incarnate(agents, movement, 7, 0);
        var container = new ContainerInventory(
            capacitySlots: 1,
            decayPermillePerTick: 800,
            new BlockCoordinate(1, 0, 5),
            AccessPrincipal.ForAgent(agent.AgentId));
        Assert.True(agent.Inventory.TryAdd(10, 1, 1, out var remaining));
        Assert.Equal(0, remaining);
        Assert.True(agent.Inventory.TryAdd(11, 1, 1, out remaining));
        Assert.Equal(0, remaining);
        var rules = CreateRules();

        Assert.True(ContainerActionSystem.Deposit(agent, container, rules, 10, 1).Succeeded);
        var rejected = ContainerActionSystem.Deposit(agent, container, rules, 11, 1);

        Assert.False(rejected.Succeeded);
        Assert.Equal("capacity_reached", rejected.ErrorCode);
        Assert.Equal(1, agent.Inventory.Count(11));
        Assert.Equal((ushort)10, container.Snapshot().Single().ItemTypeId);
    }

    [Fact]
    public void DepositAndTakePreserveToolDurability()
    {
        var (movement, agents) = CreateAgents(2);
        var world = movement.World;
        var agent = Incarnate(agents, movement, 7, 0);
        Assert.True(agent.Inventory.TryAdd(20, 1, 1, out _));
        var rules = new BlockActionRules(
            [new MiningMaterialRule(1, true, 1, 1, [])],
            [new ActionItemRule(20, 1, 1, Durability: 5)]);
        var ledger = new MatterLedger();
        var mined = BlockActionSystem.Mine(
            world,
            agent,
            new GroundItemStore(),
            rules,
            ledger,
            runSeed: 1,
            tick: 0,
            new BlockCoordinate(0, 0, 4),
            targetPerceived: true);
        Assert.True(mined.Succeeded);
        Assert.Equal(4, Assert.Single(agent.Inventory.Snapshot()).DurabilityRemaining);

        var container = new ContainerInventory(
            capacitySlots: 1,
            decayPermillePerTick: 800,
            new BlockCoordinate(1, 0, 5),
            AccessPrincipal.ForAgent(agent.AgentId));
        Assert.True(ContainerActionSystem.Deposit(agent, container, rules, 20, 1).Succeeded);
        Assert.Equal(4, Assert.Single(container.Snapshot()).DurabilityRemaining);
        Assert.True(ContainerActionSystem.Take(agent, container, rules, 20, 1).Succeeded);
        Assert.Equal(4, Assert.Single(agent.Inventory.Snapshot()).DurabilityRemaining);
    }

    [Fact]
    public void ContainerStoreCheckpointPreservesContentsOwnershipAndAccessRules()
    {
        var (movement, agents) = CreateAgents(2);
        var owner = Incarnate(agents, movement, 8, 0);
        var other = Incarnate(agents, movement, 7, 1);
        Assert.True(owner.Inventory.TryAdd(10, 2, 5, out var remaining));
        Assert.Equal(0, remaining);
        var position = new BlockCoordinate(1, 0, 5);
        var container = new ContainerInventory(
            capacitySlots: 2,
            decayPermillePerTick: 800,
            position,
            AccessPrincipal.ForAgent(owner.AgentId));
        Assert.True(container.SetAccessList(
            AccessPrincipal.ForAgent(owner.AgentId),
            [new ContainerAccessRule(AccessPrincipal.ForGroup(55), ContainerAccessRights.Put)]).Succeeded);
        Assert.True(ContainerActionSystem.Deposit(owner, container, CreateRules(), 10, 2).Succeeded);
        var store = new WorldContainerStore();
        Assert.True(store.TryAdd(container));

        var restoredStore = new WorldContainerStore();
        restoredStore.RestoreState(store.CaptureState());

        Assert.True(restoredStore.TryGet(position, out var restored));
        Assert.Equal(container.Snapshot(), restored!.Snapshot());
        Assert.Equal(AccessPrincipal.ForAgent(owner.AgentId), restored.Owner);
        Assert.Equal(
            "access_denied",
            ContainerActionSystem.Take(other, restored, CreateRules(), 10, 1).ErrorCode);
    }

    private static BlockActionRules CreateRules() =>
        new(
            [],
            [
                new ActionItemRule(10, MaxStackSize: 5, ToolTier: 0),
                new ActionItemRule(11, MaxStackSize: 5, ToolTier: 0),
            ]);

    private static AgentBody Incarnate(
        AgentRegistry agents,
        MovementGraph movement,
        ulong agentId,
        int x) =>
        agents.Incarnate(agentId, AgentPosition.AtCell(x, 0, 5), new(), movement);

    private static (MovementGraph Movement, AgentRegistry Agents) CreateAgents(int width)
    {
        var world = new WorldGrid(width, 1, verticalSize: 16);
        for (var x = 0; x < width; x++)
        {
            world.SetSurfaceColumn(x, 0, new SurfaceColumn(4, 1, 0, 0));
        }

        var movement = new MovementGraph(
            world,
            new MovementRules(new HashSet<ushort> { 1 }, new HashSet<ushort>()));
        return (movement, new AgentRegistry());
    }
}
