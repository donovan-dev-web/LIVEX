using Prism.Core.World;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class AgentVisualMeshBuilderTests
{
    [Fact]
    public void BuildsDeterministicVoxelSilhouetteAtFixedPointAgentPosition()
    {
        var agent = CreateAgent(17, AgentPosition.AtCell(4, 5, 6));

        var mesh = Assert.Single(AgentVisualMeshBuilder.Build([agent]));
        var repeated = Assert.Single(AgentVisualMeshBuilder.Build([agent]));

        Assert.Equal(17UL, mesh.AgentId);
        Assert.Equal(216 * 6, mesh.Vertices.Length);
        Assert.Equal(mesh.Vertices, repeated.Vertices);
        Assert.Equal(
            4.02f,
            mesh.Vertices.Where((_, index) => index % 6 == 0).Min(),
            2);
        Assert.Equal(
            4.98f,
            mesh.Vertices.Where((_, index) => index % 6 == 0).Max(),
            2);
        Assert.Equal(
            5.27f,
            mesh.Vertices.Where((_, index) => index % 6 == 1).Min(),
            2);
        Assert.Equal(
            5.73f,
            mesh.Vertices.Where((_, index) => index % 6 == 1).Max(),
            2);
        Assert.Equal(
            6f,
            mesh.Vertices.Where((_, index) => index % 6 == 2).Min(),
            2);
        Assert.Equal(
            7.88f,
            mesh.Vertices.Where((_, index) => index % 6 == 2).Max(),
            2);
    }

    [Fact]
    public void CarryingInventoryAddsVisibleCargoAndChangesBodyColor()
    {
        var empty = Assert.Single(AgentVisualMeshBuilder.Build([
            CreateAgent(1, AgentPosition.AtCell(0, 0, 1)),
        ]));
        var carrying = Assert.Single(AgentVisualMeshBuilder.Build([
            CreateAgent(
                2,
                AgentPosition.AtCell(0, 0, 1),
                [new AgentItemStack(3, 1, 10)]),
        ]));

        Assert.Equal(216 * 6, empty.Vertices.Length);
        Assert.Equal(252 * 6, carrying.Vertices.Length);
        Assert.Contains(0.94f, carrying.Vertices);
        Assert.NotEqual(empty.Vertices, carrying.Vertices);
    }

    private static AgentEntitySnapshot CreateAgent(
        ulong id,
        AgentPosition position,
        AgentItemStack[]? inventory = null) =>
        new(id, position, new AgentBodyProfile(), inventory ?? []);
}
