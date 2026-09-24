using System.Text.Json;
using Microsoft.Data.Sqlite;
using Simulation.Core.Configuration;
using Simulation.Core.Loop;

namespace Simulation.Core.Persistence;

/// <summary>
/// Store SQLite V2.0 (SYNE-110, PERSISTENCE.md §3 — Annexe G) : schéma relationnel
/// de 11 tables, transactions atomiques (PERSISTENCE.md §5) et rotation de
/// <c>maxBackups</c> runs. Le snapshot bit-à-bit complet (monde + cognition + RNG
/// 4×64) est conservé dans <c>tick_states.state</c>/<c>rng_state</c> pour la reprise
/// exacte (PERSISTENCE.md §4). Les tables agents/ressources/groupes/événements/
/// messages/metrics restent le pendant relationnel requis par le schéma.
/// </summary>
public sealed class SqlitePersistenceStore : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly object _gate = new();

    /// <summary>Chaîne de connexion partagée + point de connexion fixe (<c>Foreign Keys</c>).</summary>
    public SqlitePersistenceStore(string dbPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);
        _dbPath = dbPath;
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath, ForeignKeys = true }.ToString();
        var directory = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        EnsureSchema();
    }

    public string DbPath => _dbPath;

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA user_version = 2;

            CREATE TABLE IF NOT EXISTS runs (
                id            TEXT PRIMARY KEY,
                name          TEXT NOT NULL,
                started_at    TEXT NOT NULL,
                ended_at      TEXT,
                seed          INTEGER NOT NULL,
                config        TEXT NOT NULL,
                total_ticks   INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS tick_states (
                run_id      TEXT NOT NULL REFERENCES runs(id),
                tick_number INTEGER NOT NULL,
                state       TEXT NOT NULL,
                rng_state   TEXT NOT NULL,
                timestamp   TEXT NOT NULL,
                PRIMARY KEY (run_id, tick_number)
            );

            CREATE TABLE IF NOT EXISTS agents (
                id            INTEGER PRIMARY KEY,
                run_id        TEXT NOT NULL REFERENCES runs(id),
                species       TEXT NOT NULL,
                name          TEXT,
                birth_tick    INTEGER NOT NULL,
                death_tick    INTEGER,
                current_state TEXT NOT NULL,
                initial_traits TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS agent_snapshots (
                run_id   TEXT NOT NULL REFERENCES runs(id),
                tick_number INTEGER NOT NULL,
                agent_id INTEGER NOT NULL,
                x        REAL NOT NULL,
                y        REAL NOT NULL,
                health   REAL NOT NULL,
                energy   REAL NOT NULL,
                hunger   REAL NOT NULL,
                thirst   REAL NOT NULL,
                fatigue  REAL NOT NULL,
                current_action TEXT,
                action_progress REAL,
                beliefs  TEXT,
                goals    TEXT,
                relationships TEXT,
                memory   TEXT,
                PRIMARY KEY (run_id, tick_number, agent_id)
            );

            CREATE TABLE IF NOT EXISTS resources (
                id       TEXT PRIMARY KEY,
                run_id   TEXT NOT NULL REFERENCES runs(id),
                type     TEXT NOT NULL,
                position TEXT NOT NULL,
                quantity REAL NOT NULL,
                capacity REAL NOT NULL
            );

            CREATE TABLE IF NOT EXISTS resource_snapshots (
                run_id   TEXT NOT NULL REFERENCES runs(id),
                tick_number INTEGER NOT NULL,
                resource_id TEXT NOT NULL,
                quantity REAL NOT NULL,
                PRIMARY KEY (run_id, tick_number, resource_id)
            );

            CREATE TABLE IF NOT EXISTS groups (
                id               INTEGER PRIMARY KEY,
                run_id           TEXT NOT NULL REFERENCES runs(id),
                name             TEXT NOT NULL,
                formation_tick   INTEGER NOT NULL,
                dissolution_tick INTEGER,
                leader_id        INTEGER,
                avg_cohesion     REAL
            );

            CREATE TABLE IF NOT EXISTS group_memberships (
                group_id    INTEGER NOT NULL,
                agent_id    INTEGER NOT NULL,
                run_id      TEXT NOT NULL REFERENCES runs(id),
                role        TEXT NOT NULL,
                joined_tick INTEGER NOT NULL,
                left_tick   INTEGER,
                PRIMARY KEY (group_id, agent_id)
            );

            CREATE TABLE IF NOT EXISTS events (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                run_id     TEXT NOT NULL REFERENCES runs(id),
                tick_number INTEGER NOT NULL,
                type       TEXT NOT NULL,
                entity_id  INTEGER,
                target_id  INTEGER,
                details    TEXT
            );

            CREATE TABLE IF NOT EXISTS messages (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                run_id     TEXT NOT NULL REFERENCES runs(id),
                tick_number INTEGER NOT NULL,
                type       TEXT NOT NULL,
                sender_id  INTEGER NOT NULL,
                receiver_id INTEGER,
                content    TEXT NOT NULL,
                confidence REAL NOT NULL,
                hops       INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS metrics (
                run_id      TEXT NOT NULL REFERENCES runs(id),
                tick_number INTEGER NOT NULL,
                alive_count INTEGER NOT NULL,
                total_deaths INTEGER NOT NULL,
                total_births INTEGER NOT NULL,
                total_conflicts INTEGER NOT NULL,
                total_cooperations INTEGER NOT NULL,
                groups_count INTEGER NOT NULL,
                messages_count INTEGER NOT NULL,
                PRIMARY KEY (run_id, tick_number)
            );
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Démarre un run : inscrit la ligne <c>runs</c> avec la config complète
    /// (sérialisée JSON) et applique la rotation des backups.
    /// </summary>
    public void BeginRun(string runId, string name, ulong seed, SimulationOptions config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(config);

        lock (_gate)
        {
            string configJson = JsonSerializer.Serialize(config);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO runs (id, name, started_at, seed, config, total_ticks)
                    VALUES ($id, $name, $started, $seed, $config, 0)
                    ON CONFLICT(id) DO NOTHING;
                    """;
                command.Parameters.AddWithValue("$id", runId);
                command.Parameters.AddWithValue("$name", name);
                command.Parameters.AddWithValue("$started", DateTimeOffset.UtcNow.ToString("O"));
                command.Parameters.AddWithValue("$seed", checked((long)seed));
                command.Parameters.AddWithValue("$config", configJson);
                command.ExecuteNonQuery();
            }

            PruneRuns(connection, transaction, config.Simulation.MaxBackups);
            transaction.Commit();
        }
    }

    /// <summary>
    /// Sauvegarde atomique d'un tick : stocke le snapshot bit-à-bit dans
    /// <c>tick_states</c> + écrit les sous-ensembles relationnels (agents,
    /// ressources, groupes, métriques) puis met à jour <c>runs.total_ticks</c>.
    /// </summary>
    public void SaveTick(string runId, SimulationLoop loop)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(loop);

        lock (_gate)
        {
            SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);
            string stateJson = SimulationSnapshotCodec.ToJson(snapshot);
            string rngJson = $"[{snapshot.Rng.S0},{snapshot.Rng.S1},{snapshot.Rng.S2},{snapshot.Rng.S3}]";

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO tick_states (run_id, tick_number, state, rng_state, timestamp)
                    VALUES ($run, $tick, $state, $rng, $ts)
                    ON CONFLICT(run_id, tick_number) DO UPDATE SET
                        state = excluded.state,
                        rng_state = excluded.rng_state,
                        timestamp = excluded.timestamp;
                    """;
                command.Parameters.AddWithValue("$run", runId);
                command.Parameters.AddWithValue("$tick", checked((long)snapshot.Tick));
                command.Parameters.AddWithValue("$state", stateJson);
                command.Parameters.AddWithValue("$rng", rngJson);
                command.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToString("O"));
                command.ExecuteNonQuery();
            }

            WriteRelationals(connection, transaction, runId, snapshot);
            UpdateRunTicks(connection, transaction, runId, snapshot.Tick);

            transaction.Commit();
        }
    }

    /// <summary>Termine proprement un run (renseigne <c>ended_at</c>).</summary>
    public void EndRun(string runId)
    {
        lock (_gate)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE runs SET ended_at = $ended WHERE id = $id;";
            command.Parameters.AddWithValue("$ended", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$id", runId);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Charge le dernier snapshot sauvegardé du run (reprise après crash,
    /// SYNE-112) et reconstruit une boucle exécutable bit-à-bit.
    /// </summary>
    public SimulationLoop LoadLatestRun()
    {
        lock (_gate)
        {
            SqliteConnection? connection = null;
            SqliteTransaction? transaction = null;
            try
            {
                connection = new SqliteConnection(_connectionString);
                connection.Open();
                transaction = connection.BeginTransaction();

                string configJson;
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "SELECT config, id FROM runs ORDER BY started_at DESC LIMIT 1;";
                    using var reader = command.ExecuteReader();
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException("Aucun run sauvegardé dans la base.");
                    }

                    configJson = reader.GetString(0);
                }

                SimulationOptions options = JsonSerializer.Deserialize<SimulationOptions>(configJson)
                    ?? throw new InvalidOperationException("Config de run invalide.");

                string stateJson;
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = """
                        SELECT state FROM tick_states
                        ORDER BY tick_number DESC LIMIT 1;
                        """;
                    using var reader = command.ExecuteReader();
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException("Aucun snapshot (tick_states) pour ce run.");
                    }

                    stateJson = reader.GetString(0);
                }

                SimulationSnapshot snapshot = SimulationSnapshotCodec.FromJson(stateJson);
                transaction.Commit();
                return SimulationSnapshotRestorer.Restore(snapshot, options);
            }
            catch
            {
                transaction?.Rollback();
                throw;
            }
            finally
            {
                transaction?.Dispose();
                connection?.Dispose();
            }
        }
    }

    /// <summary>Nombre de runs catalogués.</summary>
    public int CountRuns()
    {
        lock (_gate)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM runs;";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    /// <summary>Nombre de snapshots (tick_states) d'un run.</summary>
    public long CountSnapshots(string runId)
    {
        lock (_gate)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM tick_states WHERE run_id = $id;";
            command.Parameters.AddWithValue("$id", runId);
            return Convert.ToInt64(command.ExecuteScalar());
        }
    }

    private static void WriteRelationals(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string runId,
        SimulationSnapshot snapshot)
    {
        WriteAgents(connection, transaction, runId, snapshot);
        WriteResources(connection, transaction, runId, snapshot);
        WriteGroups(connection, transaction, runId, snapshot);
        WriteMetrics(connection, transaction, runId, snapshot);
    }

    private static void WriteAgents(SqliteConnection connection, SqliteTransaction transaction, string runId, SimulationSnapshot snapshot)
    {
        foreach (EntitySnapshotDto entity in snapshot.World.Entities)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO agents (id, run_id, species, name, birth_tick, current_state, initial_traits)
                VALUES ($id, $run, $species, $name, $birth, 'alive', $traits)
                ON CONFLICT(id) DO UPDATE SET
                    species = excluded.species,
                    name = excluded.name,
                    current_state = 'alive';
                """;
            command.Parameters.AddWithValue("$id", checked((long)entity.Id));
            command.Parameters.AddWithValue("$run", runId);
            command.Parameters.AddWithValue("$species", entity.Species);
            command.Parameters.AddWithValue("$name", (object?)entity.Name ?? DBNull.Value);
            command.Parameters.AddWithValue("$birth", checked((long)entity.BornAt));
            command.Parameters.AddWithValue("$traits", JsonSerializer.Serialize(entity.Traits));
            command.ExecuteNonQuery();
        }
    }

    private static void WriteResources(SqliteConnection connection, SqliteTransaction transaction, string runId, SimulationSnapshot snapshot)
    {
        string[] kinds = { "food", "water", "wood" };
        double[] quantities = { snapshot.World.FoodStock, snapshot.World.WaterStock, snapshot.World.WoodStock };
        for (int i = 0; i < kinds.Length; i++)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO resources (id, run_id, type, position, quantity, capacity)
                VALUES ($id, $run, $type, '{"x":0,"y":0}', $qty, $cap)
                ON CONFLICT(id) DO UPDATE SET quantity = excluded.quantity;
                """;
            command.Parameters.AddWithValue("$id", $"global-{kinds[i]}");
            command.Parameters.AddWithValue("$run", runId);
            command.Parameters.AddWithValue("$type", kinds[i]);
            command.Parameters.AddWithValue("$qty", quantities[i]);
            command.Parameters.AddWithValue("$cap", quantities[i]);
            command.ExecuteNonQuery();

            using var snapCommand = connection.CreateCommand();
            snapCommand.Transaction = transaction;
            snapCommand.CommandText = """
                INSERT INTO resource_snapshots (run_id, tick_number, resource_id, quantity)
                VALUES ($run, $tick, $id, $qty)
                ON CONFLICT(run_id, tick_number, resource_id) DO UPDATE SET quantity = excluded.quantity;
                """;
            snapCommand.Parameters.AddWithValue("$run", runId);
            snapCommand.Parameters.AddWithValue("$tick", checked((long)snapshot.Tick));
            snapCommand.Parameters.AddWithValue("$id", $"global-{kinds[i]}");
            snapCommand.Parameters.AddWithValue("$qty", quantities[i]);
            snapCommand.ExecuteNonQuery();
        }
    }

    private static void WriteGroups(SqliteConnection connection, SqliteTransaction transaction, string runId, SimulationSnapshot snapshot)
    {
        foreach (GroupSnapshotDto group in snapshot.Groups)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO groups (id, run_id, name, formation_tick, leader_id, avg_cohesion)
                VALUES ($id, $run, $name, $formation, $leader, $cohesion)
                ON CONFLICT(id) DO UPDATE SET
                    leader_id = excluded.leader_id,
                    avg_cohesion = excluded.avg_cohesion;
                """;
            command.Parameters.AddWithValue("$id", checked((long)group.Id));
            command.Parameters.AddWithValue("$run", runId);
            command.Parameters.AddWithValue("$name", $"g{group.Id}");
            command.Parameters.AddWithValue("$formation", checked((long)group.BornTick));
            command.Parameters.AddWithValue("$leader", group.LeaderId is { } leader ? (object)checked((long)leader) : DBNull.Value);
            command.Parameters.AddWithValue("$cohesion", group.MeanCohesion);
            command.ExecuteNonQuery();

            foreach (ulong member in group.Members)
            {
                using var membership = connection.CreateCommand();
                membership.Transaction = transaction;
                membership.CommandText = """
                    INSERT INTO group_memberships (group_id, agent_id, run_id, role, joined_tick)
                    VALUES ($gid, $aid, $run, 'member', $joined)
                    ON CONFLICT(group_id, agent_id) DO NOTHING;
                    """;
                membership.Parameters.AddWithValue("$gid", checked((long)group.Id));
                membership.Parameters.AddWithValue("$aid", checked((long)member));
                membership.Parameters.AddWithValue("$run", runId);
                membership.Parameters.AddWithValue("$joined", checked((long)group.BornTick));
                membership.ExecuteNonQuery();
            }
        }
    }

    private static void WriteMetrics(SqliteConnection connection, SqliteTransaction transaction, string runId, SimulationSnapshot snapshot)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO metrics (run_id, tick_number, alive_count, total_deaths, total_births, total_conflicts, total_cooperations, groups_count, messages_count)
            VALUES ($run, $tick, $alive, 0, 0, 0, 0, $groups, 0)
            ON CONFLICT(run_id, tick_number) DO UPDATE SET
                alive_count = excluded.alive_count,
                groups_count = excluded.groups_count;
            """;
        command.Parameters.AddWithValue("$run", runId);
        command.Parameters.AddWithValue("$tick", checked((long)snapshot.Tick));
        command.Parameters.AddWithValue("$alive", snapshot.World.Entities.Count);
        command.Parameters.AddWithValue("$groups", snapshot.Groups.Count);
        command.ExecuteNonQuery();
    }

    private static void UpdateRunTicks(SqliteConnection connection, SqliteTransaction transaction, string runId, ulong tick)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE runs SET total_ticks = $tick WHERE id = $run;";
        command.Parameters.AddWithValue("$tick", checked((long)tick));
        command.Parameters.AddWithValue("$run", runId);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Rotation des backups (PERSISTENCE.md §5, <c>maxBackups</c>) : supprime les
    /// runs les plus anciens dès que le nombre dépasse le plafond. V0.1 : politique
    /// simple « garder les N plus récents », la compression est hors périmètre.
    /// </summary>
    private static void PruneRuns(SqliteConnection connection, SqliteTransaction transaction, int maxBackups)
    {
        if (maxBackups <= 0)
        {
            maxBackups = 5;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM runs
            WHERE id IN (
                SELECT id FROM runs
                ORDER BY started_at DESC
                LIMIT -1 OFFSET $keep
            );
            """;
        command.Parameters.AddWithValue("$keep", Math.Max(1, maxBackups));
        command.ExecuteNonQuery();

        using var cleanup = connection.CreateCommand();
        cleanup.Transaction = transaction;
        cleanup.CommandText = "DELETE FROM tick_states WHERE run_id NOT IN (SELECT id FROM runs);";
        cleanup.ExecuteNonQuery();
    }

    public void Dispose()
    {
    }
}