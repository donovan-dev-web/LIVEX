// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : SqlitePersistenceStore - partie Write.

using System.Text.Json;
using Microsoft.Data.Sqlite;
using Simulation.Core.Configuration;
using Simulation.Core.Loop;

namespace Simulation.Core.Persistence;

using System.Text.Json;
using Microsoft.Data.Sqlite;
using Simulation.Core.Configuration;
using Simulation.Core.Loop;

public sealed partial class SqlitePersistenceStore : IDisposable
{

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
        (string Kind, double Quantity)[] resources =
        {
            ("food", snapshot.World.FoodStock),
            ("water", snapshot.World.WaterStock),
            ("wood", snapshot.World.WoodStock),
            ("mineral", snapshot.World.MineralStock),
        };

        foreach ((string kind, double quantity) in resources)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO resources (id, run_id, type, position, quantity, capacity)
                VALUES ($id, $run, $type, '{"x":0,"y":0}', $qty, $cap)
                ON CONFLICT(id) DO UPDATE SET quantity = excluded.quantity;
                """;
            command.Parameters.AddWithValue("$id", $"global-{kind}");
            command.Parameters.AddWithValue("$run", runId);
            command.Parameters.AddWithValue("$type", kind);
            command.Parameters.AddWithValue("$qty", quantity);
            command.Parameters.AddWithValue("$cap", quantity);
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
            snapCommand.Parameters.AddWithValue("$id", $"global-{kind}");
            snapCommand.Parameters.AddWithValue("$qty", quantity);
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
}
