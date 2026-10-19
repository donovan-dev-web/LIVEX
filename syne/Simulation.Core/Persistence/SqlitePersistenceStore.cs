// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Store SQLite V2.0 (SYNE-110) : schéma relationnel de 11 tables, transactions atomiques et rotation de
//        maxBackups runs.

using System.Text.Json;
using Microsoft.Data.Sqlite;
using Simulation.Core.Configuration;
using Simulation.Core.Loop;

namespace Simulation.Core.Persistence;

/// <summary>
/// Store SQLite V2.0 (SYNE-110) : schéma relationnel
/// de 11 tables, transactions atomiques et rotation de
/// <c>maxBackups</c> runs. Le snapshot bit-à-bit complet (monde + cognition + RNG
/// 4×64) est conservé dans <c>tick_states.state</c>/<c>rng_state</c> pour la reprise
/// exacte. Les tables agents/ressources/groupes/événements/
/// messages/metrics restent le pendant relationnel requis par le schéma.
/// </summary>
public sealed partial class SqlitePersistenceStore : IDisposable
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
}
