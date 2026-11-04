using Prism.Viewer;
using Prism.Core.World;

namespace Prism.Viewer.Tests;

public sealed class ViewerHudMeshBuilderTests
{
    [Fact]
    public void BuildsDeterministicHudWithinClipSpaceAndReflectsViewerState()
    {
        var state = new ViewerHudState(12345, 96, 96, 3, ViewerCameraMode.Orbit, false, null, null);

        var vertices = ViewerHudMeshBuilder.Build(state, 1280, 800);
        var repeated = ViewerHudMeshBuilder.Build(state, 1280, 800);
        var freeCamera = ViewerHudMeshBuilder.Build(
            state with { CameraMode = ViewerCameraMode.Free },
            1280,
            800);

        Assert.NotEmpty(vertices);
        Assert.Equal(vertices, repeated);
        Assert.NotEqual(vertices, freeCamera);
        Assert.All(
            vertices.Chunk(6),
            vertex => Assert.InRange(vertex[0], -1f, 1f));
        Assert.All(
            vertices.Chunk(6),
            vertex => Assert.InRange(vertex[1], -1f, 1f));
    }

    [Fact]
    public void DoesNotBuildHudForZeroSizedViewport()
    {
        var state = new ViewerHudState(1, 16, 16, 0, ViewerCameraMode.Free, false, null, null);

        Assert.Empty(ViewerHudMeshBuilder.Build(state, 0, 800));
        Assert.Empty(ViewerHudMeshBuilder.Build(state, 1280, 0));
    }

    [Fact]
    public void ScalesHudToSmallViewportWithoutLeavingClipSpace()
    {
        var state = new ViewerHudState(9, 32, 32, 1, ViewerCameraMode.Orbit, true, null, null);

        var vertices = ViewerHudMeshBuilder.Build(state, 240, 160);

        Assert.NotEmpty(vertices);
        Assert.All(vertices.Chunk(6), vertex => Assert.InRange(vertex[0], -1f, 1f));
        Assert.All(vertices.Chunk(6), vertex => Assert.InRange(vertex[1], -1f, 1f));
    }

    [Fact]
    public void InspectorHudChangesWithSelectedAgentSnapshot()
    {
        var withoutSelection = new ViewerHudState(
            1, 16, 16, 1, ViewerCameraMode.Orbit, false, null, null);
        var selected = withoutSelection with
        {
            SelectedAgent = new AgentEntitySnapshot(
                7,
                AgentPosition.AtCell(3, 4, 5),
                new AgentBodyProfile(),
                [new AgentItemStack(2, 3, 10)]),
        };

        var emptyHud = ViewerHudMeshBuilder.Build(withoutSelection, 1280, 800);
        var selectedHud = ViewerHudMeshBuilder.Build(selected, 1280, 800);

        Assert.NotEqual(emptyHud, selectedHud);
    }

    [Fact]
    public void HudReflectsCalendarTickAndLiveState()
    {
        var state = new ViewerHudState(
            1, 16, 16, 1, ViewerCameraMode.Orbit, false, null, null);
        var withCalendar = state with
        {
            Tick = 240,
            Time = ViewerDayNightCycle.Evaluate(240, new ViewerCalendarConfig()),
        };
        var live = state with { Tick = 240, Live = true };

        var baseline = ViewerHudMeshBuilder.Build(state, 1280, 800);

        Assert.NotEqual(baseline, ViewerHudMeshBuilder.Build(withCalendar, 1280, 800));
        Assert.NotEqual(baseline, ViewerHudMeshBuilder.Build(live, 1280, 800));
    }

    [Fact]
    public void InspectorHudReportsFollowedAgent()
    {
        var state = new ViewerHudState(
            1, 16, 16, 1, ViewerCameraMode.Orbit, false, null, null);
        var following = ViewerHudMeshBuilder.Build(
            state with { FollowedAgentId = 7 },
            1280,
            800);

        Assert.NotEqual(
            ViewerHudMeshBuilder.Build(state, 1280, 800),
            following);
    }
}
