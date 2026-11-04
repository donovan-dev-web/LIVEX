using System.Text.Json;
using Prism.Contracts;
using Prism.Core.World;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerEntityStoreTests
{
    private static readonly string Version = ProtocolVersion.Current.ToString();

    [Fact]
    public void InterpolatesBetweenSnapshots()
    {
        var store = new ViewerEntityStore();
        store.ReplaceAgents([Agent(7, 0, 0, 5)], tick: 0);
        store.ApplySnapshot([Agent(7, 2, 0, 5)], [], tick: 2);

        var start = Assert.Single(store.SampleAgents(0f));
        var middle = Assert.Single(store.SampleAgents(0.5f));
        var end = Assert.Single(store.SampleAgents(1f));
        var clamped = Assert.Single(store.SampleAgents(4f));

        Assert.Equal(0.5f, start.X, 3);
        Assert.Equal(1.5f, middle.X, 3);
        Assert.Equal(0.5f, middle.Y, 3);
        Assert.Equal(5f, middle.Z, 3);
        Assert.Equal(2.5f, end.X, 3);
        Assert.Equal(end.X, clamped.X, 3);
    }

    [Fact]
    public void SnapshotReplacesRemovedAgentsAndGroundItems()
    {
        var store = new ViewerEntityStore();
        store.ReplaceAgents([Agent(7, 0, 0, 1), Agent(8, 1, 0, 1)], tick: 0);
        var item = new GroundItem(
            42,
            new BlockCoordinate(3, 4, 5),
            new AgentItemStack(2, 6, 20));

        store.ApplySnapshot([Agent(8, 3, 0, 1)], [item], tick: 3);

        Assert.Equal(1, store.AgentCount);
        Assert.False(store.TryGetAgent(7, out _));
        Assert.True(store.TryGetAgent(8, out var agent));
        Assert.NotNull(agent);
        var groundItem = Assert.Single(store.GroundItems);
        Assert.Equal(42UL, groundItem.Id);
        Assert.Equal(3, groundItem.X);
        Assert.Equal(6, groundItem.Quantity);
    }

    [Fact]
    public void TracksMacroActionUntilCompletion()
    {
        var store = new ViewerEntityStore();
        store.ReplaceAgents([Agent(7, 0, 0, 1)], tick: 0);
        var stepEvent = new EventEnvelope(
            Version,
            1,
            "e1",
            "macro_step_completed",
            7,
            JsonSerializer.SerializeToElement(
                new { result = new { step = "Move" } },
                ProtocolJson.Options));

        store.ApplyTickResult(new TickResult(Version, 1, [], [stepEvent]));
        Assert.True(store.TryGetAgent(7, out var moving));
        Assert.Equal("Move", moving!.ActiveAction);

        var completion = new EventEnvelope(
            Version,
            2,
            "e2",
            "action_completed",
            7,
            JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options));
        store.ApplyTickResult(new TickResult(Version, 2, [], [completion]));

        Assert.True(store.TryGetAgent(7, out var done));
        Assert.Null(done!.ActiveAction);
    }

    [Fact]
    public void AppliesObservationWithoutEntitySnapshot()
    {
        var store = new ViewerEntityStore();
        store.ReplaceAgents([Agent(7, 0, 0, 1)], tick: 0);
        var self = JsonSerializer.SerializeToElement(
            new
            {
                position = new { x = 4_500, y = 500, z = 3_000 },
                inventory = new[]
                {
                    new { itemTypeId = (ushort)2, quantity = 3, maxStackSize = 10 },
                },
            },
            ProtocolJson.Options);
        var changes = JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options);

        Assert.True(store.ApplyObservation(
            new ObservationEnvelope(Version, 3, 7, changes, self)));

        Assert.True(store.TryGetAgent(7, out var agent));
        Assert.Equal(4.5f, agent!.X, 3);
        Assert.Equal(3f, agent.Z, 3);
        var stack = Assert.Single(agent.Inventory);
        Assert.Equal((ushort)2, stack.ItemTypeId);
        Assert.Equal(3, stack.Quantity);
    }

    [Fact]
    public void IgnoresStaleObservationsAndMalformedPayloads()
    {
        var store = new ViewerEntityStore();
        store.ReplaceAgents([Agent(7, 0, 0, 1)], tick: 5);
        var staleSelf = JsonSerializer.SerializeToElement(
            new { position = new { x = 9_000, y = 0, z = 0 }, inventory = Array.Empty<object>() },
            ProtocolJson.Options);
        var malformed = JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options);

        Assert.False(store.ApplyObservation(
            new ObservationEnvelope(Version, 1, 7, malformed, staleSelf)));
        Assert.False(store.ApplyObservation(
            new ObservationEnvelope(Version, 6, 7, malformed, malformed)));
        Assert.True(store.TryGetAgent(7, out var agent));
        Assert.Equal(0.5f, agent!.X, 3);
    }

    [Fact]
    public void TogglesAllSevenInspectionLayers()
    {
        var store = new ViewerEntityStore();
        var layers = Enum.GetValues<ViewerLayer>();

        Assert.Equal(7, layers.Length);
        Assert.All(layers, layer => Assert.False(store.IsLayerEnabled(layer)));
        Assert.All(layers, layer => Assert.True(store.ToggleLayer(layer)));
        Assert.All(layers, layer => Assert.True(store.IsLayerEnabled(layer)));
        Assert.False(store.ToggleLayer(ViewerLayer.Zones));
        Assert.False(store.IsLayerEnabled(ViewerLayer.Zones));
        store.SetLayer(ViewerLayer.Zones, true);
        Assert.True(store.IsLayerEnabled(ViewerLayer.Zones));
    }

    private static AgentEntitySnapshot Agent(ulong id, int x, int y, int feetZ) =>
        new(
            id,
            AgentPosition.AtCell(x, y, feetZ),
            new AgentBodyProfile(),
            []);
}
