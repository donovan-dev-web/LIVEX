// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : SqlitePersistenceStore - partie Schema.

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
}
