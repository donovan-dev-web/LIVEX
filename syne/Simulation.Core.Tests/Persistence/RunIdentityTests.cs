// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Gate d'identité de run (S-26/S-27/D-17) : hash canonique de configuration (insensible à l'ordre des
//        clés, sensible aux valeurs), hash d'état complet (reproductible à seed égale, distinct sinon) et
//        empreinte publiée (aucune valeur de remplacement inventée).

using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Persistence;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

public class RunIdentityTests
{
    private static SimulationLoop Build(ulong seed, ulong entityCount = 5, SimulationOptions? options = null)
    {
        options ??= ConfigLoader.LoadDefaults();
        var world = new Simulation.Core.World.World(WorldSize.From(options), spatialCellSize: options.Agents.Perception.Radius);
        world.ApplyResourceLayout(options.Resources, seed, options.Agents.Perception.Radius);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= entityCount; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, options);
    }

    [Fact]
    public void ConfigHash_IgnoresKeyOrder_ButNotValues()
    {
        // Même configuration écrite dans un autre ordre de clés : le hash canonique
        // (clés triées récursivement) doit être identique — sans quoi deux runs
        // strictement comparables porteraient deux identités.
        SimulationOptions ordered = ConfigLoader.MergeJson(
            ConfigLoader.LoadDefaults(),
            """
            {
              "simulation": { "maxTicks": 100 },
              "agents": { "needs": { "thirstRate": 0.7, "hungerRate": 0.5 } },
              "random": { "seed": 12345 }
            }
            """);
        SimulationOptions reordered = ConfigLoader.MergeJson(
            ConfigLoader.LoadDefaults(),
            """
            {
              "random": { "seed": 12345 },
              "agents": { "needs": { "hungerRate": 0.5, "thirstRate": 0.7 } },
              "simulation": { "maxTicks": 100 }
            }
            """);
        Assert.Equal(RunIdentity.ConfigHash(ordered), RunIdentity.ConfigHash(reordered));

        // Une valeur sémantique différente doit changer l'identité (ici un taux).
        SimulationOptions modified = ConfigLoader.MergeJson(
            ConfigLoader.LoadDefaults(),
            """{ "agents": { "needs": { "hungerRate": 0.6 } } }""");
        Assert.NotEqual(RunIdentity.ConfigHash(ordered), RunIdentity.ConfigHash(modified));
    }

    [Fact]
    public void ConfigHash_IsStableAcrossEquivalentLoads()
    {
        // Deux chargements successifs des mêmes défauts : hash identique (aucune
        // dépendance à l'ordre d'itération de la réflexion ou de la sérialisation).
        Assert.Equal(
            RunIdentity.ConfigHash(ConfigLoader.LoadDefaults()),
            RunIdentity.ConfigHash(ConfigLoader.LoadDefaults()));
    }

    [Fact]
    public void StateHash_IsReproducibleForEqualRuns_AndDivergesOnAnotherSeed()
    {
        SimulationLoop first = Build(12345);
        SimulationLoop second = Build(12345);
        first.Run(30);
        second.Run(30);

        Assert.Equal(RunIdentity.StateHash(first), RunIdentity.StateHash(second));

        SimulationLoop other = Build(12346);
        other.Run(30);
        Assert.NotEqual(RunIdentity.StateHash(first), RunIdentity.StateHash(other));
    }

    [Fact]
    public void StateHash_ChangesWithTheState_NotWithTheIdentity()
    {
        SimulationLoop loop = Build(12345);
        loop.Run(30);
        ulong before = RunIdentity.StateHash(loop);
        ulong configBefore = RunIdentity.ConfigHash(loop.Options);

        loop.AdvanceOneTick();

        Assert.NotEqual(before, RunIdentity.StateHash(loop));
        Assert.Equal(configBefore, RunIdentity.ConfigHash(loop.Options)); // l'identité ne bouge pas pendant le run
    }

    [Fact]
    public void Snapshot_CarriesTheConfigurationHash()
    {
        SimulationLoop loop = Build(12345);
        loop.Run(10);

        SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);

        Assert.Equal(RunIdentity.ConfigHash(loop.Options), snapshot.ConfigHash);
        Assert.Equal(RunIdentity.ConfigHash(loop.Options), SimulationSnapshotCodec.FromJson(SimulationSnapshotCodec.ToJson(snapshot)).ConfigHash);
    }

    [Fact]
    public void Stamp_PublishesVersionsSeedAndHashes_WithoutInventingACommit()
    {
        SimulationLoop loop = Build(12345);
        loop.Run(10);

        RunIdentityStamp stamp = RunIdentity.Stamp(loop);

        Assert.Equal(Simulation.Core.Observability.ObservabilityContract.EngineVersion, stamp.EngineVersion);
        Assert.Equal(SimulationSnapshotCodec.SchemaVersion, stamp.SchemaVersion);
        Assert.Equal(12345UL, stamp.Seed);
        Assert.Equal(RunIdentity.ConfigHash(loop.Options), stamp.ConfigHash);
        Assert.Equal(RunIdentity.StateHash(loop), stamp.StateHash);
        Assert.Null(stamp.SourceCommit); // absence déclarée, jamais une valeur de remplissage
    }
}
