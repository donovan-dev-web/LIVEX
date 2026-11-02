using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.Determinism;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class CommandFuzzTests
{
    [Fact]
    public void OneMillionDeterministicTransferAndConsumptionCommandsPreserveGlobalLedger()
    {
        const ushort breadId = 10;
        const ushort stoneId = 11;
        const ushort fruitId = 12;
        const ushort unknownId = 13;
        var (first, second) = CreateAgents();
        foreach (var agent in new[] { first, second })
        {
            Assert.True(agent.Inventory.TryAdd(breadId, 1000, 1000, out var remaining));
            Assert.Equal(0, remaining);
            Assert.True(agent.Inventory.TryAdd(stoneId, 1000, 1000, out remaining));
            Assert.Equal(0, remaining);
            Assert.True(agent.Inventory.TryAdd(fruitId, 1000, 1000, out remaining));
            Assert.Equal(0, remaining);
        }

        var itemRules = new BlockActionRules(
            [],
            [
                new ActionItemRule(breadId, 1000, 0),
                new ActionItemRule(stoneId, 1000, 0),
                new ActionItemRule(fruitId, 1000, 0),
            ]);
        var consumptionRules = new ConsumptionRules(
            [
                new ConsumableItemRule(breadId, new Dictionary<string, int> { ["hunger"] = -1 }),
                new ConsumableItemRule(fruitId, new Dictionary<string, int> { ["hunger"] = -1 }),
            ],
            []);
        var ledger = new MatterLedger();
        ledger.SetInitialCount(breadId, 2000);
        ledger.SetInitialCount(stoneId, 2000);
        ledger.SetInitialCount(fruitId, 2000);
        var random = new Xoshiro256StarStar(0x7A11CE55UL);

        for (var index = 0; index < 1_000_000; index++)
        {
            var action = random.NextInt(0, 6);
            var itemTypeId = action == 5
                ? unknownId
                : (ushort)(breadId + random.NextInt(0, 3));
            var agent = random.NextInt(0, 2) == 0 ? first : second;
            var other = ReferenceEquals(agent, first) ? second : first;

            if (action < 4)
            {
                var quantity = action == 3
                    ? 1001
                    : random.NextInt(1, 8);
                _ = TransferActionSystem.Transfer(
                    agent,
                    other,
                    itemRules,
                    itemTypeId,
                    quantity);
            }
            else
            {
                _ = ConsumptionSystem.ConsumeItem(agent, consumptionRules, ledger, itemTypeId);
            }

            if (index % 10_000 == 9_999)
            {
                Assert.Empty(ledger.Audit(CountItems(first, second, breadId, stoneId, fruitId)));
            }
        }

        Assert.Empty(ledger.Audit(CountItems(first, second, breadId, stoneId, fruitId)));
    }

    private static IReadOnlyDictionary<ushort, long> CountItems(
        AgentBody first,
        AgentBody second,
        ushort breadId,
        ushort stoneId,
        ushort fruitId)
    {
        var counts = new Dictionary<ushort, long>
        {
            [breadId] = first.Inventory.Count(breadId) + second.Inventory.Count(breadId),
            [stoneId] = first.Inventory.Count(stoneId) + second.Inventory.Count(stoneId),
            [fruitId] = first.Inventory.Count(fruitId) + second.Inventory.Count(fruitId),
        };
        return counts;
    }

    private static (AgentBody First, AgentBody Second) CreateAgents()
    {
        var world = new WorldGrid(2, 1, verticalSize: 16);
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(4, 1, 0, 0));
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(4, 1, 0, 0));
        var movement = new MovementGraph(world, new MovementRules([1], []));
        var agents = new AgentRegistry();
        return (
            agents.Incarnate(1, AgentPosition.AtCell(0, 0, 5), new AgentBodyProfile(CarrySlots: 8), movement),
            agents.Incarnate(2, AgentPosition.AtCell(1, 0, 5), new AgentBodyProfile(CarrySlots: 8), movement));
    }
}
