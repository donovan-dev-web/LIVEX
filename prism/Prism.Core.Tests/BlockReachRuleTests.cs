using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Core.Tests;

/// <summary>
/// The reach rule decides everything a bot can gather. It has to cover the block a surface
/// occupies next to the agent (one level below its feet), otherwise every tree base, stone and
/// ore block of a generated world is out of reach and no production chain can start. It also has
/// to cover the block beside the agent's head (A2): a two-block tree only gave one log, so the
/// eight planks of a chest — two logs — could never be gathered.
/// </summary>
public sealed class BlockReachRuleTests
{
    [Fact]
    public void OwnColumn_IsReachableBelowAtFeetAndAtHeadHeight()
    {
        var position = AgentPosition.AtCell(10, 10, 20);

        Assert.True(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(10, 10, 19)));
        Assert.True(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(10, 10, 20)));
        Assert.True(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(10, 10, 21)));
        Assert.False(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(10, 10, 22)));
        Assert.False(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(10, 10, 18)));
    }

    [Fact]
    public void NeighbouringColumn_IsReachableAtFeetLevelAndOneBlockBelow()
    {
        var position = AgentPosition.AtCell(10, 10, 20);

        // The surface block standing in the column next to the agent.
        Assert.True(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(11, 10, 19)));
        Assert.True(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(10, 11, 19)));
        // Diagonals are within reach too (a 3x3 footprint).
        Assert.True(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(11, 11, 19)));
        // A block at the agent's own feet level beside it can also be touched.
        Assert.True(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(11, 10, 20)));
        // The trunk above the base shares a face with the agent's head: without it a tree only
        // ever gives one log and the storage chain cannot reach eight planks (A2).
        Assert.True(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(11, 10, 21)));
        Assert.True(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(11, 11, 21)));
        // Two blocks above the feet is out of the body's reach, in its own column or beside it.
        Assert.False(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(11, 10, 22)));
        Assert.False(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(10, 10, 22)));
        // More than one column away is never reachable.
        Assert.False(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(12, 10, 19)));
        Assert.False(BlockActionSystem.IsAdjacent(position, new BlockCoordinate(10, 12, 19)));
    }
}
