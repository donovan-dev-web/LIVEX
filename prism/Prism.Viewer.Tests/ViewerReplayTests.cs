using System.Text.Json;
using Prism.Contracts;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerReplayTests
{
    [Fact]
    public void RecordedStreamReloadsIdenticalEnvelopes()
    {
        var directory = Directory.CreateTempSubdirectory("prism-viewer-replay");
        var path = Path.Combine(directory.FullName, "run.prisml");
        try
        {
            var envelopes = new[] { Envelope(0, 0, "checksum"), Envelope(1, 1, "entity_snapshot") };

            ViewerReplayFile.Write(path, envelopes);
            var loaded = ViewerReplayFile.Load(path);

            Assert.Equal(2, loaded.Count);
            Assert.Equal(envelopes[0].Kind, loaded[0].Kind);
            Assert.Equal(envelopes[1].Tick, loaded[1].Tick);
            Assert.Equal(envelopes[1].Sequence, loaded[1].Sequence);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void RecorderAppendsOneEnvelopePerLine()
    {
        var directory = Directory.CreateTempSubdirectory("prism-viewer-record");
        var path = Path.Combine(directory.FullName, "run.prisml");
        try
        {
            using (var recorder = new ViewerStreamRecorder(path))
            {
                recorder.Append(Envelope(0, 0, "checksum"));
                recorder.Append(Envelope(1, 1, "checksum"));
                recorder.Flush();
            }

            var lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.All(lines, line => Assert.True(ViewerStreamEnvelope.Parse(line).Sequence >= 0));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ReplayCursorHonoursTheRequestedTickRate()
    {
        var envelopes = new[]
        {
            Envelope(0, 0, "checksum"),
            Envelope(1, 1, "tick_result"),
            Envelope(2, 1, "tick_result"),
            Envelope(3, 2, "tick_result"),
        };
        var cursor = new ViewerReplayCursor(envelopes, ticksPerSecond: 2d);

        // Horloge 0,2 tick : seul le premier tick est déverrouillé.
        var first = cursor.Next(0.1d);
        Assert.Single(first);
        Assert.Equal(envelopes[0].Sequence, first[0].Sequence);

        // Horloge 1,4 tick : le tick 1 passe, le tick 2 reste verrouillé.
        var second = cursor.Next(0.6d);
        Assert.Equal(2, second.Count);
        Assert.False(cursor.Complete);

        // Ticks par seconde 0 : aucune contrainte de rythme, tout le reste d'un coup.
        var fast = new ViewerReplayCursor(envelopes, ticksPerSecond: 0d);
        Assert.Equal(envelopes.Length, fast.Next(0d).Count);
        Assert.True(fast.Complete);
        Assert.Empty(fast.Next(1d));
    }

    [Fact]
    public async Task LiveConnectionKeepsABoundedQueueAndCountsDrops()
    {
        var connection = new ViewerLiveConnection();
        for (var index = 0; index < ViewerLiveConnection.MaximumPending + 10; index++)
        {
            connection.Enqueue(Envelope(index, index, "checksum"));
        }

        Assert.Equal(ViewerLiveConnection.MaximumPending, connection.PendingCount);
        Assert.Equal(10, connection.Dropped);
        Assert.True(connection.TryDequeue(out var first));
        Assert.Equal(10, first.Sequence);
        await connection.DisposeAsync();
    }

    private static ViewerStreamEnvelope Envelope(long sequence, long tick, string kind) =>
        new(
            ProtocolVersion.Current.ToString(),
            sequence,
            tick,
            kind,
            JsonSerializer.SerializeToElement(new { tick }, ProtocolJson.Options));
}
