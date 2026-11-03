using System.Text.Json;
using Prism.Contracts;
using Prism.Core.World;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerReadStreamClientTests
{
    private const ulong AgentId = 42;

    [Fact]
    public void ParseRejectsAnUnsupportedMajorVersion()
    {
        var json = EnvelopeJson("99.0.0", 0, 0, "entity_snapshot", Payload(new { tick = 0, agents = Array.Empty<object>() }));

        var exception = Assert.Throws<UnsupportedProtocolVersionException>(() => ViewerStreamEnvelope.Parse(json));
        Assert.Equal("protocol_unsupported", exception.ErrorCode);
    }

    [Fact]
    public void ParseRejectsAnInvalidEnvelope()
    {
        Assert.Throws<JsonException>(() =>
            ViewerStreamEnvelope.Parse(EnvelopeJson("1.0.0", -1, 0, "checksum", Payload(new { tick = 0 }))));
        Assert.Throws<JsonException>(() =>
            ViewerStreamEnvelope.Parse(EnvelopeJson("1.0.0", 0, 0, "", Payload(new { tick = 0 }))));
    }

    [Fact]
    public void EntitySnapshotIsAppliedToTheStore()
    {
        var session = new ViewerLiveSession();
        var status = session.Apply(ViewerStreamEnvelope.Parse(EntitySnapshot(0, tick: 5, x: 3)));

        Assert.Equal(ViewerApplyStatus.Applied, status);
        Assert.Equal(5, session.Tick);
        Assert.Equal(1, session.Store.AgentCount);
        Assert.True(session.Store.TryGetAgent(AgentId, out var agent));
        Assert.Equal(3.5f, agent!.X);
    }

    [Fact]
    public void SequenceGapDropsDeltasUntilTheNextFullSnapshot()
    {
        var session = new ViewerLiveSession();
        session.Apply(ViewerStreamEnvelope.Parse(EntitySnapshot(0, tick: 1, x: 1)));

        // Sequence 1 is lost: the next delta must not be applied on top of a stale view.
        var gap = session.Apply(ViewerStreamEnvelope.Parse(EntitySnapshot(2, tick: 3, x: 9)));
        Assert.Equal(ViewerApplyStatus.Resynchronised, gap);
        Assert.True(session.AwaitingSnapshot);
        Assert.Equal(1, session.Resynchronisations);

        var stillWaiting = session.Apply(ViewerStreamEnvelope.Parse(EntitySnapshot(3, tick: 4, x: 9)));
        Assert.Equal(ViewerApplyStatus.Resynchronised, stillWaiting);
        Assert.True(session.Store.TryGetAgent(AgentId, out var stale));
        Assert.Equal(1.5f, stale!.X);

        var snapshot = session.Apply(ViewerStreamEnvelope.Parse(WorldSnapshot(4, tick: 6)));
        Assert.Equal(ViewerApplyStatus.Applied, snapshot);
        Assert.False(session.AwaitingSnapshot);

        var resumed = session.Apply(ViewerStreamEnvelope.Parse(EntitySnapshot(5, tick: 7, x: 7)));
        Assert.Equal(ViewerApplyStatus.Applied, resumed);
        Assert.True(session.Store.TryGetAgent(AgentId, out var live));
        Assert.Equal(7.5f, live!.X);
    }

    [Fact]
    public void OlderEnvelopeIsIgnoredWithoutTouchingTheState()
    {
        var session = new ViewerLiveSession();
        session.Apply(ViewerStreamEnvelope.Parse(EntitySnapshot(0, tick: 10, x: 4)));

        var status = session.Apply(ViewerStreamEnvelope.Parse(EntitySnapshot(1, tick: 9, x: 8)));

        Assert.Equal(ViewerApplyStatus.Stale, status);
        Assert.True(session.Store.TryGetAgent(AgentId, out var agent));
        Assert.Equal(4.5f, agent!.X);
    }

    [Fact]
    public void ChecksumUpdatesTheWorldHash()
    {
        var session = new ViewerLiveSession();
        var status = session.Apply(ViewerStreamEnvelope.Parse(EnvelopeJson(
            ProtocolVersion.Current.ToString(),
            0,
            12,
            "checksum",
            Payload(new { tick = 12, worldHash = 0xDEADBEEFUL }))));

        Assert.Equal(ViewerApplyStatus.Applied, status);
        Assert.Equal(0xDEADBEEFUL, session.WorldHash);
        Assert.Equal(12, session.Tick);
    }

    private static string EntitySnapshot(long sequence, long tick, int x) =>
        EnvelopeJson(
            ProtocolVersion.Current.ToString(),
            sequence,
            tick,
            "entity_snapshot",
            Payload(new
            {
                tick,
                agents = new[]
                {
                    new AgentEntitySnapshot(
                        AgentId,
                        AgentPosition.AtCell(x, 2, 4),
                        new AgentBodyProfile(),
                        []),
                },
                groundItems = Array.Empty<GroundItem>(),
            }));

    private static string WorldSnapshot(long sequence, long tick) =>
        EnvelopeJson(
            ProtocolVersion.Current.ToString(),
            sequence,
            tick,
            "world_snapshot",
            Payload(new { tick, worldHash = 123UL, agents = Array.Empty<AgentEntitySnapshot>() }));

    private static JsonElement Payload(object value) =>
        JsonSerializer.SerializeToElement(value, ProtocolJson.Options);

    private static string EnvelopeJson(string version, long sequence, long tick, string kind, JsonElement payload) =>
        JsonSerializer.Serialize(
            new { protocolVersion = version, sequence, tick, kind, payload },
            ProtocolJson.Options);
}
