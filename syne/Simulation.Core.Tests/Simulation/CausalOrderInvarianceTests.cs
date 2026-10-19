// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Invariance causale à l'ordre d'insertion (A0-1 / BL-34) : l'ordre
//        d'itération des entités n'est plus une loi causale — perception sur
//        état figé + commit différé en fin de tick, itération triée par
//        identifiant, file d'effets triée, groupes/tris déterministes. Deux runs
//        à configuration et graine identiques mais à ORDRE D'INSERTION des
//        entités différent doivent produire la même empreinte d'état complète.
//
//        Périmètre assumé : l'invariant vise l'ordre d'insertion/itération.
//        Renommer les identifiants change la trajectoire PAR CONCEPTION (offsets
//        de déplacement hachés sur l'id, étalement de perception id % 4) : ce
//        n'est pas l'invariant visé ici.

using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Persistence;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Core.Tests;

/// <summary>
/// Invariance causale (A0-1, fenêtre causale fermée du chapitre III) : l'ordre
/// d'insertion des entités ne peut pas altérer l'état du run — aucun agent ne
/// perçoit ni ne subit un effet produit dans le même tick, et toutes les
/// itérations suivent des tris par identifiant.
/// </summary>
public class CausalOrderInvarianceTests
{
    private static SimulationLoop BuildScenario(ulong seed, ulong entityCount, bool reverseInsertion)
    {
        var world = new WorldType(new WorldSize(500, 500), spatialCellSize: 50);
        world.AddObstacle(new Obstacle("rocher-1", new Position(250, 150), radius: 15));
        world.AddObstacle(new Obstacle("rocher-2", new Position(420, 380), radius: 25));

        // Mêmes entités des deux côtés : la génération consomme le PRNG dans le
        // même ordre ; seule l'ordre d'INSERTION dans le monde diffère.
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        var entities = new List<Entity>();
        for (ulong i = 1; i <= entityCount; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            entities.Add(entity);
        }

        if (reverseInsertion)
        {
            entities.Reverse();
        }

        foreach (Entity entity in entities)
        {
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, ConfigLoader.LoadDefaults());
    }

    [Fact]
    public void PermutedInsertionOrder_SameSeedAndConfig_SameStateFingerprint()
    {
        SimulationLoop forward = BuildScenario(seed: 12345, entityCount: 25, reverseInsertion: false);
        SimulationLoop permuted = BuildScenario(seed: 12345, entityCount: 25, reverseInsertion: true);

        for (int tick = 1; tick <= 60; tick++)
        {
            forward.AdvanceOneTick();
            permuted.AdvanceOneTick();

            if (tick is 10 or 30 or 60)
            {
                Assert.Equal(
                    $"t={tick}:0x{RunIdentity.StateHash(forward):x16}",
                    $"t={tick}:0x{RunIdentity.StateHash(permuted):x16}");
            }
        }
    }

    [Fact]
    public void PermutedInsertionOrder_DifferentSeeds_Diverge()
    {
        // Contrôle : la propriété n'est pas vide — deux graines différentes avec
        // le même ordre inversé divergent (l'empreinte détecte bien l'état).
        SimulationLoop permutedA = BuildScenario(seed: 12345, entityCount: 25, reverseInsertion: true);
        SimulationLoop permutedB = BuildScenario(seed: 12346, entityCount: 25, reverseInsertion: true);

        for (int tick = 1; tick <= 30; tick++)
        {
            permutedA.AdvanceOneTick();
            permutedB.AdvanceOneTick();
        }

        Assert.NotEqual(RunIdentity.StateHash(permutedA), RunIdentity.StateHash(permutedB));
    }
}