// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : SqlitePersistenceStore - partie Maintenance.

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
    /// Rotation des backups (<c>maxBackups</c>) : supprime les
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
