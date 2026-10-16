// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Fabrique déterministe des runs SYNE (mode CLI et mode serveur de contrôle HTTP :5181) : monde + entités +
//        PRNG + boucle —,. La construction elle-même ne consomme que le PRNG de création (entités) ; la boucle est
//        ensuite pilotée sans tirage supplémentaire.

using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Console.Control;

/// <summary>
/// Fabrique déterministe des runs SYNE (mode CLI et mode serveur de contrôle
/// HTTP :5181) : monde + entités + PRNG + boucle —,.
/// La construction elle-même ne consomme que le PRNG de création (entités) ;
/// la boucle est ensuite pilotée sans tirage supplémentaire.
/// </summary>
public static class SimulationFactory
{
    /// <summary>
    /// Résout les options finales d'un run : <paramref name="baseOptions"/> →
    /// surcouche <c>config</c> (JSON <b>partiel</b>, même règle de fusion que
    /// <c>--config</c>) → override du seed.
    ///
    /// <para>
    /// La surcouche est transportée en <b>JSON brut</b> et fusionnée par
    /// <see cref="ConfigLoader.MergeJson(SimulationOptions, string)"/> : la désérialiser en
    /// <see cref="SimulationOptions"/> la rendrait complète, et chaque clé absente
    /// prendrait la valeur par défaut du type, écrasant le profil de base. C'est
    /// pour cette raison que ce paramètre est une chaîne et non un objet.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidDataException">Surcouche JSON mal formée.</exception>
    public static (SimulationOptions Options, ulong Seed) ResolveOptions(
        SimulationOptions baseOptions,
        string? configJson,
        ulong? seed)
    {
        SimulationOptions options = string.IsNullOrWhiteSpace(configJson)
            ? baseOptions
            : ConfigLoader.MergeJson(baseOptions, configJson);
        ulong effectiveSeed = seed ?? options.Random.Seed;
        options.Random.Seed = effectiveSeed;
        return (options, effectiveSeed);
    }

    /// <summary>Nombre de tirages consécutifs dans un obstacle au-delà duquel le placement échoue.</summary>
    private const int MaxPlacementAttempts = 1_000;

    /// <summary>
    /// Peuple le monde de <paramref name="population"/> entités et rend le PRNG
    /// avancé.
    ///
    /// Le layout d'obstacles (<c>world.obstacleLayout</c>) est appliqué <b>avant</b>
    /// la création des entités : un agent ne doit pas démarrer dans un obstacle,
    /// sinon le monde se contredit lui-même — la description publie la case comme
    /// bloquée (`walkable: false`) alors qu'une entité s'y tient, et le cheminement
    /// A* la refuse. Une position retirée est redonnée par le <b>même</b> PRNG, donc
    /// le tirage reste déterministe pour une graine donnée.
    ///
    /// Neutre pour un profil sans obstacle : aucun tirage supplémentaire n'est
    /// consommé, donc les checksums dorés du profil de référence sont inchangés.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Le layout couvre tant de cases qu'aucun placement ne sort d'un obstacle.
    /// </exception>
    public static Xoshiro256StarStar Populate(
        WorldType world,
        SimulationOptions options,
        ulong population,
        Xoshiro256StarStar rng)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(options);

        world.ApplyConfiguredLayout(options.World);

        EntityTemplate template = EntityTemplate.DefaultA;
        for (ulong i = 0; i < population; i++)
        {
            int attempt = 0;
            (Entity Entity, Xoshiro256StarStar Next) draw =
                EntityFactory.CreateNext(template, world, rng, i + 1, bornAt: 0);
            while (IsInBlockedCell(world, draw.Entity.Position, options.Simulation.WorldCellSize))
            {
                if (++attempt >= MaxPlacementAttempts)
                {
                    throw new InvalidOperationException(
                        $"Placement de l'entité {i + 1} impossible : {MaxPlacementAttempts} tirages consécutifs dans un obstacle du layout.");
                }

                draw = EntityFactory.CreateNext(template, world, draw.Next, i + 1, bornAt: 0);
            }

            rng = draw.Next;
            world.AddEntity(draw.Entity);
        }

        return rng;
    }

    /// <summary>
    /// Vrai si <paramref name="position"/> tombe dans une case que la description
    /// du monde publiera comme bloquée par un obstacle — même règle, même source
    /// (<see cref="Simulation.Core.World.ObstacleGeometry"/>) que
    /// <see cref="Simulation.Core.World.WorldDescriptionBuilder"/>.
    /// </summary>
    private static bool IsInBlockedCell(WorldType world, Simulation.Core.World.Position position, double cellSize)
    {
        if (world.Obstacles.Count == 0)
        {
            return false;
        }

        int cellX = (int)Math.Floor(position.X / cellSize);
        int cellY = (int)Math.Floor(position.Y / cellSize);
        double centerX = (cellX + 0.5) * cellSize;
        double centerY = (cellY + 0.5) * cellSize;
        foreach (Simulation.Core.World.Obstacle obstacle in world.Obstacles)
        {
            if (Simulation.Core.World.ObstacleGeometry.CoversCell(obstacle, centerX, centerY, cellSize))
            {
                return true;
            }
        }

        return false;
    }

    public static (WorldType World, SimulationLoop Loop) Build(
        SimulationOptions options,
        ulong population,
        ulong seed)
    {
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        var world = new WorldType(
            new Simulation.Core.World.WorldSize(options.Simulation.WorldWidth, options.Simulation.WorldHeight),
            options.Agents.Perception.SpatialCellSize);

        rng = Populate(world, options, population, rng);

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