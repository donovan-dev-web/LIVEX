using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Persistence;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Core.Tests;

/// <summary>
/// Persistance V2 SQLite (SYNE-110/111/112, PERSISTENCE.md §3-§5) : le snapshot
/// bit-à-bit capture l'état (monde + cognition + RNG 4×64), la reprise depuis le
/// dernier point de sauvegarde reproduit exactement la trajectoire d'un run
/// ininterrompu (même hash), et la rotation <c>maxBackups</c> borne le nombre de
/// runs conservés.
/// </summary>
public class PersistenceTests
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static SimulationLoop BuildScenario(ulong seed, int entityCount)
    {
        var world = new WorldType(new WorldSize(500, 500), spatialCellSize: 50);
        world.AddObstacle(new Obstacle("rocher-1", new Position(250, 150), radius: 15));
        world.AddObstacle(new Obstacle("rocher-2", new Position(420, 380), radius: 25));

        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= (ulong)entityCount; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, Simulation.Core.Configuration.ConfigLoader.LoadDefaults());
    }

    private static string Rounded(double value) =>
        Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    /// <summary>Journal canonique (même construction que Ph10DeterminismBaselineTests), tické sur <c>CurrentTick</c>.</summary>
    private static string BuildStateLog(SimulationLoop loop, int advance)
    {
        var log = new StringBuilder();
        for (int tick = 0; tick < advance; tick++)
        {
            loop.AdvanceOneTick();
            log.Append($"t={loop.CurrentTick};pop={loop.World.Entities.Count()};");
            log.Append($"sent={loop.Cognition.Communication.LastSent.Count};");
            log.Append($"groups={loop.Cognition.Groups.Active.Count};");
            log.Append($"births={loop.Cognition.Birth.LastBirths.Count};");
            log.Append($"deaths={loop.Cognition.Death.LastDeaths.Count};");
            log.AppendLine();
            foreach (Entity entity in loop.World.Entities.OrderBy(entity => entity.Id.Value))
            {
                Simulation.Core.Cognition.MindState mind = loop.Cognition.MindOf(entity.Id.Value);
                log.Append(entity.Id.Value);
                log.Append(';');
                log.Append(Rounded(entity.Position.X));
                log.Append(';');
                log.Append(Rounded(entity.Position.Y));
                log.Append(';');
                log.Append(Rounded(mind.Needs.Energy));
                log.Append(';');
                log.Append(Rounded(mind.Needs.Hunger));
                log.Append(';');
                log.Append(Rounded(mind.Needs.Thirst));
                log.Append(';');
                log.Append(mind.Intention?.Kind ?? Simulation.Core.Cognition.DesireKind.Idle);
                log.Append(';');
                log.Append(mind.Memory.Recall(loop.CurrentTick).Count.ToString(CultureInfo.InvariantCulture));
                log.Append(';');
                log.Append(mind.Trust.Count.ToString(CultureInfo.InvariantCulture));
                log.Append(';');
                log.Append(mind.Beliefs.Count.ToString(CultureInfo.InvariantCulture));
                log.AppendLine();
            }
        }

        return log.ToString();
    }

    private static ulong Fnv1a(string text)
    {
        ulong hash = FnvOffsetBasis;
        foreach (byte value in Encoding.UTF8.GetBytes(text))
        {
            hash ^= value;
            hash *= FnvPrime;
        }

        return hash;
    }

    private static string TempDb() => Path.Combine(Path.GetTempPath(), $"livex-persist-{Guid.NewGuid():N}.db");

    /// <summary>
    /// SYNE-111 : sauvegarde à T=100 puis reprise — la trajectoire 101→200 du run
    /// relancé est bit-à-bit identique au run ininterrompu (mémoire, intentions,
    /// position, énergie, confiance, naissances, mortalité).
    /// </summary>
    [Fact]
    public void ReloadFromSnapshot_ContinuesBitForBitIdenticalToUninterruptedRun()
    {
        string db = TempDb();
        try
        {
            const ulong seed = 12345;
            const int entityCount = 25;

            SimulationLoop reference = BuildScenario(seed, entityCount);
            Reference(reference, ticks: 100);
            string expected = BuildStateLog(reference, advance: 100);

            SimulationLoop saved = BuildScenario(seed, entityCount);
            Reference(saved, ticks: 100);

            using (var store = new SqlitePersistenceStore(db))
            {
                store.BeginRun("run-1", "persist-test", seed, Simulation.Core.Configuration.ConfigLoader.LoadDefaults());
                store.SaveTick("run-1", saved);
            }

            using var reloadStore = new SqlitePersistenceStore(db);
            SimulationLoop resumed = reloadStore.LoadLatestRun();
            string actual = BuildStateLog(resumed, advance: 100);

            Assert.Equal(expected, actual);
            Assert.Equal(Fnv1a(expected), Fnv1a(actual));
            Assert.Equal((ulong)200, resumed.CurrentTick);
        }
        finally
        {
            File.Delete(db);
        }
    }

    /// <summary>
    /// SYNE-111 : l'état complet du PRNG (4 × 64 bits) est persisté dans
    /// <c>tick_states.rng_state</c> et restauré sans tirage supplémentaire.
    /// </summary>
    [Fact]
    public void SaveTick_PersistsFullRngState_AndRestoresItFaithfully()
    {
        string db = TempDb();
        try
        {
            SimulationLoop loop = BuildScenario(12345, 25);
            Reference(loop, ticks: 100);
            SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);

            using (var store = new SqlitePersistenceStore(db))
            {
                store.BeginRun("run-rng", "rng-test", 12345, Simulation.Core.Configuration.ConfigLoader.LoadDefaults());
                store.SaveTick("run-rng", loop);
            }

            using var reloadStore = new SqlitePersistenceStore(db);
            SimulationLoop resumed = reloadStore.LoadLatestRun();

            Assert.Equal(snapshot.Rng.S0, resumed.Rng.State.S0);
            Assert.Equal(snapshot.Rng.S1, resumed.Rng.State.S1);
            Assert.Equal(snapshot.Rng.S2, resumed.Rng.State.S2);
            Assert.Equal(snapshot.Rng.S3, resumed.Rng.State.S3);
        }
        finally
        {
            File.Delete(db);
        }
    }

    /// <summary>
    /// SYNE-112 : reprise après « crash » — seuls les tick_states sauvegardés sont
    /// récupérables, la suite reproduit le run ininterrompu.
    /// </summary>
    [Fact]
    public void LoadLatest_AfterCrash_ResumesFromLastSavedTick()
    {
        string db = TempDb();
        try
        {
            SimulationLoop loop = BuildScenario(999, 25);
            using (var store = new SqlitePersistenceStore(db))
            {
                store.BeginRun("run-crash", "crash-test", 999, Simulation.Core.Configuration.ConfigLoader.LoadDefaults());
                for (int tick = 1; tick <= 150; tick++)
                {
                    loop.AdvanceOneTick();
                    if (tick % 50 == 0)
                    {
                        store.SaveTick("run-crash", loop);
                    }
                }
            }

            using var reloadStore = new SqlitePersistenceStore(db);
            SimulationLoop resumed = reloadStore.LoadLatestRun();

            Assert.Equal((ulong)150, resumed.CurrentTick);
            SimulationSnapshot referenceSnapshot = SimulationSnapshotCodec.Capture(loop);
            SimulationSnapshot resumedSnapshot = SimulationSnapshotCodec.Capture(resumed);
            Assert.Equal(SimulationSnapshotCodec.Hash(referenceSnapshot), SimulationSnapshotCodec.Hash(resumedSnapshot));
        }
        finally
        {
            File.Delete(db);
        }
    }

    /// <summary>
    /// SYNE-110 : le schéma V2.0 crée bien les 11 tables relationnelles exigées
    /// par PERSISTENCE.md §3 (Annexe G).
    /// </summary>
    [Fact]
    public void EnsureSchema_CreatesAllElevenTables()
    {
        string db = TempDb();
        try
        {
            using var store = new SqlitePersistenceStore(db);
            const string expected = "runs;tick_states;agents;agent_snapshots;resources;resource_snapshots;groups;group_memberships;events;messages;metrics";
            List<string> actual = TableNames(db);
            foreach (string table in expected.Split(';'))
            {
                Assert.Contains(table, actual);
            }

            Assert.Equal(11, actual.Count);
            Assert.Equal(2, StoreVersion(db));
        }
        finally
        {
            File.Delete(db);
        }
    }

    /// <summary>
    /// PERSISTENCE.md §5 : la rotation <c>maxBackups</c> borne le nombre de runs
    /// conservés (les plus anciens sont purgés).
    /// </summary>
    [Fact]
    public void PruneRuns_KeepsOnlyMaxBackupsMostRecentRuns()
    {
        string db = TempDb();
        try
        {
            using (var store = new SqlitePersistenceStore(db))
            {
                var config = Simulation.Core.Configuration.ConfigLoader.LoadDefaults();
                config.Simulation.MaxBackups = 2;

                store.BeginRun("run-1", "a", 1, config);
                store.BeginRun("run-2", "b", 2, config);
                store.BeginRun("run-3", "c", 3, config);
                store.BeginRun("run-4", "d", 4, config);
                store.BeginRun("run-5", "e", 5, config);
            }

            using var reloadStore = new SqlitePersistenceStore(db);
            Assert.Equal(2, reloadStore.CountRuns());
        }
        finally
        {
            File.Delete(db);
        }
    }

    /// <summary>Avance <paramref name="ticks"/> ticks.</summary>
    private static void Reference(SimulationLoop loop, int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
        {
            loop.AdvanceOneTick();
        }
    }

    /// <summary>
    /// SYNE-070 : la persistance émet les quatre réserves globales (dont <c>mineral</c>)
    /// et la reprise SQLite restaure les niveaux (minéral et valeurs régénérées).
    /// </summary>
    [Fact]
    public void Resources_ArePersistedWithFourKindsIncludingMineral()
    {
        string db = TempDb();
        try
        {
            SimulationLoop loop = BuildScenario(12345, 10);
            var config = Simulation.Core.Configuration.ConfigLoader.LoadDefaults();
            config.Resources.Mineral.Initial = 25;

            loop = new SimulationLoop(loop.World, loop.Rng, config);
            Reference(loop, ticks: 15);

            SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);
            Assert.True(snapshot.World.MineralStock >= 25.0, "La réserve de minéraux est persistée.");
            Assert.True(snapshot.World.WaterStock > 1000.0, "La régénération d'eau s'applique (SYNE-070).");

            using (var store = new SqlitePersistenceStore(db))
            {
                store.BeginRun("run-res", "res-test", 12345, config);
                store.SaveTick("run-res", loop);
            }

            using (var reloadStore = new SqlitePersistenceStore(db))
            {
                SimulationLoop resumed = reloadStore.LoadLatestRun();
                Assert.Equal(loop.CurrentTick, resumed.CurrentTick);
                Assert.Equal(snapshot.World.MineralStock, resumed.Resources.Stock(World.ResourceKind.Mineral), 10);
                Assert.Equal(snapshot.World.WaterStock, resumed.Resources.Stock(World.ResourceKind.Water), 10);
                Assert.Equal(snapshot.World.FoodStock, resumed.Resources.Stock(World.ResourceKind.Food), 10);
            }

            using var resourcesConnection = new SqliteConnection($"Data Source={db}");
            resourcesConnection.Open();
            using var resourcesCommand = resourcesConnection.CreateCommand();
            resourcesCommand.CommandText = "SELECT type FROM resources ORDER BY type;";
            var types = new List<string>();
            using (var reader = resourcesCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    types.Add(reader.GetString(0));
                }
            }

            Assert.Contains("mineral", types);
            Assert.Contains("food", types);
            Assert.Contains("water", types);
            Assert.Contains("wood", types);
            Assert.Equal(4, types.Count);
        }
        finally
        {
            File.Delete(db);
        }
    }

    private static List<string> TableNames(string dbPath)
    {
        var names = new List<string>();
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static int StoreVersion(string dbPath)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}