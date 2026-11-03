using System.Text.Json;
using Microsoft.Data.Sqlite;
using Prism.Contracts;
using Prism.Host.Hosting;
using Prism.Host.Persistence;
using Prism.Host.Ticking;

namespace Prism.Host.Tests;

public sealed class WorldCheckpointStoreTests
{
    [Fact]
    public void RuntimeAutomaticallyCheckpointsAndPrunesOnlyTheCurrentRunPrefix()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"prism-auto-checkpoint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new WorldCheckpointStore(Path.Combine(directory, "world.sqlite"));
            var runtime = new PrismHostRuntime(
                checkpointStore: store,
                automaticCheckpointIntervalTicks: 2,
                automaticCheckpointsToKeep: 2);
            runtime.Prepare(new PrepareWorldRequest(123, "content-hash"));
            runtime.Start(new StartWorldRequest());
            for (var tick = 0; tick <= 4; tick++)
            {
                _ = runtime.Step(new TickRequest(ProtocolVersion.Current.ToString(), tick, []));
            }

            runtime.Stop();

            Assert.Throws<KeyNotFoundException>(
                () => store.Load("auto:content-hash:123:1", "content-hash"));
            Assert.Equal(3, store.Load("auto:content-hash:123:3", "content-hash").Tick);
            Assert.Equal(4, store.Load("auto:content-hash:123:4", "content-hash").Tick);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RestoredRunReplaysPreviouslyAppliedCommandWithoutRepeatingHandler()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"prism-checkpoint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new WorldCheckpointStore(Path.Combine(directory, "world.sqlite"));
            var handler = new CountingCommandHandler();
            var firstRuntime = new PrismHostRuntime(
                () => CreateWorldHost(handler),
                checkpointStore: store);
            firstRuntime.Prepare(new PrepareWorldRequest(123, "content-hash"));
            firstRuntime.Start(new StartWorldRequest());
            using var payload = JsonDocument.Parse("""{"amount":3}""");
            var command = new CommandEnvelope(
                ProtocolVersion.Current.ToString(),
                0,
                "transfer-1",
                42,
                "Transferer",
                payload.RootElement.Clone());
            _ = firstRuntime.Step(new TickRequest(ProtocolVersion.Current.ToString(), 0, [command]));
            firstRuntime.SaveCheckpoint("before-retry");

            var restoredRuntime = new PrismHostRuntime(
                () => CreateWorldHost(handler),
                checkpointStore: store);
            restoredRuntime.RestoreCheckpoint("before-retry", "content-hash");
            restoredRuntime.Start(new StartWorldRequest());
            var replay = command with { Tick = 1 };
            var result = restoredRuntime.Step(new TickRequest(ProtocolVersion.Current.ToString(), 1, [replay]));

            Assert.Equal(1, handler.ExecutionCount);
            Assert.Equal(1, result.Tick);
            Assert.Equal("action_completed", Assert.Single(result.Events).Kind);
            Assert.Equal(0, firstRuntime.Status.LastCompletedTick);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RestoreRejectsContentHashMismatchWithoutChangingRuntimeState()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"prism-checkpoint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new WorldCheckpointStore(Path.Combine(directory, "world.sqlite"));
            store.Save(new WorldCheckpoint("saved", -1, 1, "original", [1]));
            var runtime = new PrismHostRuntime(checkpointStore: store);

            var exception = Assert.Throws<HostLifecycleException>(
                () => runtime.RestoreCheckpoint("saved", "different"));

            Assert.Equal("content_hash_mismatch", exception.ErrorCode);
            Assert.Equal("idle", runtime.Status.State);
            Assert.False(runtime.IsReady);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void StoreMigratesVersionOneSchemaAndPreservesLegacyCheckpoints()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"prism-checkpoint-migration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var databasePath = Path.Combine(directory, "world.sqlite");
            using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE checkpoints (
                        checkpoint_id TEXT PRIMARY KEY NOT NULL,
                        tick INTEGER NOT NULL CHECK (tick >= -1),
                        seed TEXT NOT NULL,
                        content_hash TEXT NOT NULL,
                        host_state BLOB NOT NULL
                    );
                    INSERT INTO checkpoints VALUES ('legacy', -1, '1', 'content', X'01');
                    PRAGMA user_version = 1;
                    """;
                command.ExecuteNonQuery();
            }

            var store = new WorldCheckpointStore(databasePath);
            var legacy = store.Load("legacy", "content");
            Assert.Null(legacy.WorldState);
            Assert.Equal([1], legacy.HostState);

            store.Save(new WorldCheckpoint("new", 0, 2, "content", [2], [3]));
            var saved = store.Load("new", "content");
            Assert.Equal([2], saved.HostState);
            Assert.Equal([3], saved.WorldState);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static OrderedWorldHost CreateWorldHost(CountingCommandHandler handler)
    {
        var dispatch = new CommandDispatchPhase([handler]);
        var phases = Enum.GetValues<WorldTickPhase>()
            .Select(phase => phase switch
            {
                WorldTickPhase.Receive => (IWorldTickPhaseHandler)dispatch.ReceivePhase,
                WorldTickPhase.StartOrCancelActions => dispatch,
                _ => new NoOpPhase(phase),
            });
        return new OrderedWorldHost(phases);
    }

    private sealed class CountingCommandHandler : IWorldCommandHandler
    {
        public string Kind => "Transferer";
        public int ExecutionCount { get; private set; }

        public CommandExecutionResult Execute(CommandEnvelope command, WorldTickContext context)
        {
            ExecutionCount++;
            return CommandExecutionResult.Success(command.Payload);
        }
    }

    private sealed class NoOpPhase(WorldTickPhase phase) : IWorldTickPhaseHandler
    {
        public WorldTickPhase Phase => phase;
        public void Execute(WorldTickContext context) => ArgumentNullException.ThrowIfNull(context);
    }
}
