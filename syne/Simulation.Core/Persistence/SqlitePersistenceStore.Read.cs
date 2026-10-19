// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : SqlitePersistenceStore - partie Read.

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
}
