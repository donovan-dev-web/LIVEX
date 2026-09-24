using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Console.Control;

/// <summary>
/// Fabrique déterministe des runs SYNE (mode CLI et mode serveur de contrôle
/// HTTP :5181) : monde + entités + PRNG + boucle — ADR-002, DETERMINISM.md §3.
/// La construction elle-même ne consomme que le PRNG de création (entités) ;
/// la boucle est ensuite pilotée sans tirage supplémentaire.
/// </summary>
public static class SimulationFactory
{
    /// <summary>
    /// Résout les options finales d'un run : défauts → surcouche <c>config</c>
    /// (JSON partiel, même règle de fusion que <c>--config</c>) → override du seed.
    /// </summary>
    public static (SimulationOptions Options, ulong Seed) ResolveOptions(
        SimulationOptions baseOptions,
        SimulationOptions? overlay,
        ulong? seed)
    {
        SimulationOptions options = overlay is null ? baseOptions : ConfigLoader.Merge(baseOptions, overlay);
        ulong effectiveSeed = seed ?? options.Random.Seed;
        options.Random.Seed = effectiveSeed;
        return (options, effectiveSeed);
    }

    public static (WorldType World, SimulationLoop Loop) Build(
        SimulationOptions options,
        ulong population,
        ulong seed)
    {
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        var world = new WorldType(
            new Simulation.Core.World.WorldSize(options.Simulation.WorldWidth, options.Simulation.WorldHeight),
            options.Agents.Perception.Radius);

        EntityTemplate template = EntityTemplate.DefaultA;
        for (ulong i = 0; i < population; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(template, world, rng, i + 1, bornAt: 0);
            world.AddEntity(entity);
        }

        var loop = new SimulationLoop(world, rng, options);
        return (world, loop);
    }

    public static (WorldType World, SimulationLoop Loop) Build(
        SimulationOptions options,
        ulong seed)
    {
        return Build(options, (ulong)options.Agents.InitialCount, seed);
    }
}