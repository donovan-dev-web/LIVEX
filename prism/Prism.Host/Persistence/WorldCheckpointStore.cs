using Microsoft.Data.Sqlite;

namespace Prism.Host.Persistence;

public sealed record WorldCheckpoint(
    string CheckpointId,
    long Tick,
    ulong Seed,
    string ContentHash,
    byte[] HostState,
    byte[]? WorldState = null);

public sealed class CheckpointContentMismatchException()
    : Exception("Checkpoint contentHash does not match the requested content pack.");

public sealed class WorldCheckpointStore
{
    private const int SchemaVersion = 2;
    private readonly string _connectionString;

    public WorldCheckpointStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // Chaque opération ouvre puis referme sa propre connexion : le cache partagé et
            // le pooling ne servent à rien ici et gardent le fichier verrouillé après Dispose
            // (suppression du répertoire impossible sous Windows).
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        }.ToString();
        Initialize();
    }

    public void Save(WorldCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ValidateCheckpoint(checkpoint);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO checkpoints (checkpoint_id, tick, seed, content_hash, host_state, world_state)
            VALUES ($checkpointId, $tick, $seed, $contentHash, $hostState, $worldState)
            ON CONFLICT(checkpoint_id) DO UPDATE SET
                tick = excluded.tick,
                seed = excluded.seed,
                content_hash = excluded.content_hash,
                host_state = excluded.host_state,
                world_state = excluded.world_state;
            """;
        command.Parameters.AddWithValue("$checkpointId", checkpoint.CheckpointId);
        command.Parameters.AddWithValue("$tick", checkpoint.Tick);
        command.Parameters.AddWithValue("$seed", checkpoint.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$contentHash", checkpoint.ContentHash);
        command.Parameters.Add("$hostState", SqliteType.Blob).Value = checkpoint.HostState;
        command.Parameters.Add("$worldState", SqliteType.Blob).Value =
            checkpoint.WorldState is null ? DBNull.Value : checkpoint.WorldState;
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public WorldCheckpoint Load(string checkpointId, string expectedContentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedContentHash);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT tick, seed, content_hash, host_state, world_state
            FROM checkpoints
            WHERE checkpoint_id = $checkpointId;
            """;
        command.Parameters.AddWithValue("$checkpointId", checkpointId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"Checkpoint '{checkpointId}' does not exist.");
        }

        var contentHash = reader.GetString(2);
        if (!StringComparer.Ordinal.Equals(contentHash, expectedContentHash))
        {
            throw new CheckpointContentMismatchException();
        }

        if (!ulong.TryParse(
                reader.GetString(1),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var seed))
        {
            throw new InvalidDataException($"Checkpoint '{checkpointId}' contains an invalid seed.");
        }

        return new WorldCheckpoint(
            checkpointId,
            reader.GetInt64(0),
            seed,
            contentHash,
            (byte[])reader[3],
            reader.IsDBNull(4) ? null : (byte[])reader[4]);
    }

    public void PruneAutomaticCheckpoints(string checkpointIdPrefix, int keepCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointIdPrefix);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(keepCount);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = """
            SELECT checkpoint_id
            FROM checkpoints
            WHERE substr(checkpoint_id, 1, length($prefix)) = $prefix
            ORDER BY tick DESC, checkpoint_id DESC;
            """;
        query.Parameters.AddWithValue("$prefix", checkpointIdPrefix);
        var checkpointIds = new List<string>();
        using (var reader = query.ExecuteReader())
        {
            while (reader.Read())
            {
                checkpointIds.Add(reader.GetString(0));
            }
        }

        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM checkpoints WHERE checkpoint_id = $checkpointId;";
        var checkpointIdParameter = delete.Parameters.Add("$checkpointId", SqliteType.Text);
        foreach (var checkpointId in checkpointIds.Skip(keepCount))
        {
            checkpointIdParameter.Value = checkpointId;
            delete.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var schemaVersion = Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        if (schemaVersion is not (0 or 1 or SchemaVersion))
        {
            throw new InvalidDataException(
                $"Checkpoint schema version {schemaVersion} is not supported (expected {SchemaVersion}).");
        }

        command.CommandText = "PRAGMA journal_mode = WAL;";
        command.ExecuteNonQuery();
        if (schemaVersion == 0)
        {
            command.CommandText = """
                CREATE TABLE checkpoints (
                    checkpoint_id TEXT PRIMARY KEY NOT NULL,
                    tick INTEGER NOT NULL CHECK (tick >= -1),
                    seed TEXT NOT NULL,
                    content_hash TEXT NOT NULL,
                    host_state BLOB NOT NULL,
                    world_state BLOB NULL
                );
                PRAGMA user_version = 2;
                """;
            command.ExecuteNonQuery();
        }
        else if (schemaVersion == 1)
        {
            command.CommandText = """
                ALTER TABLE checkpoints ADD COLUMN world_state BLOB NULL;
                PRAGMA user_version = 2;
                """;
            command.ExecuteNonQuery();
        }
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void ValidateCheckpoint(WorldCheckpoint checkpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpoint.CheckpointId);
        ArgumentOutOfRangeException.ThrowIfLessThan(checkpoint.Tick, -1);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpoint.ContentHash);
        ArgumentNullException.ThrowIfNull(checkpoint.HostState);
        if (checkpoint.HostState.Length == 0)
        {
            throw new ArgumentException("Checkpoint host state must not be empty.", nameof(checkpoint));
        }
    }
}
