using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Prng;
using Simulation.Core.Loop;

namespace Simulation.Console;

/// <summary>
/// Point d'entrée SYNE — mode CLI/batch (ADR-002).
/// Priorité de configuration : défauts → --config → flags CLI (CONFIGURATION.md §5).
/// Boucle minimale : monde + entités + grille spatiale + tick (SYNE-002/003/004).
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            CliOptions cli = CliOptions.Parse(args);

            SimulationOptions options = ConfigLoader.LoadDefaults();
            if (cli.ConfigPath is not null)
            {
                options = ConfigLoader.LoadFile(cli.ConfigPath);
            }

            options = ApplyCliOverrides(options, cli);

            IReadOnlyList<string> errors = SimulationOptionsValidator.Validate(options);
            if (errors.Count > 0)
            {
                System.Console.Error.WriteLine("Configuration invalide :");
                foreach (string error in errors)
                {
                    System.Console.Error.WriteLine($"  - {error}");
                }

                return 2;
            }

            RunBatch(options, cli.Headless == true);

            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or InvalidDataException)
        {
            System.Console.Error.WriteLine($"Erreur : {ex.Message}");
            return 2;
        }
    }

    private static SimulationOptions ApplyCliOverrides(SimulationOptions options, CliOptions cli)
    {
        if (cli.WorldSize is { } size)
        {
            options.Simulation.WorldWidth = size.Width;
            options.Simulation.WorldHeight = size.Height;
        }

        if (cli.MaxTicks is { } maxTicks)
        {
            options.Simulation.MaxTicks = maxTicks;
        }

        if (cli.Seed is { } seed)
        {
            options.Random.Seed = seed;
        }

        return options;
    }

    private static void RunBatch(SimulationOptions options, bool headless)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(options.Random.Seed);
        var world = new Simulation.Core.World.World(
            new Simulation.Core.World.WorldSize(options.Simulation.WorldWidth, options.Simulation.WorldHeight),
            options.Agents.Perception.Radius);

        EntityTemplate template = EntityTemplate.DefaultA;
        for (ulong i = 0; i < (ulong)options.Agents.InitialCount; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(template, world, rng, i + 1, bornAt: 0);
            world.AddEntity(entity);
        }

        var loop = new SimulationLoop(world, rng);
        loop.Run(options.Simulation.MaxTicks);

        sw.Stop();

        if (!headless)
        {
            PrintSummary(options, loop, template, sw.ElapsedMilliseconds);
        }
    }

    private static void PrintSummary(SimulationOptions options, SimulationLoop loop, EntityTemplate template, long elapsedMs)
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"SYNE — boucle minimale ({template.Species}) :");
        summary.AppendLine($"  monde : {options.Simulation.WorldWidth} x {options.Simulation.WorldHeight} | grille : {loop.World.Grid.CellCountX}x{loop.World.Grid.CellCountY}");
        summary.AppendLine($"  PRNG : {options.Random.Engine}, seed : {options.Random.Seed}");

        if (loop.CurrentTick > 0)
        {
            var head = BuildTickLine(1UL, loop.World.Entities.Count);
            var tail = BuildTickLine(loop.CurrentTick, loop.World.Entities.Count);
            summary.AppendLine("(tête) " + head);
            summary.AppendLine("  …    …");
            summary.AppendLine("(queue) " + tail);
        }

        summary.AppendLine($"  exécuté en {elapsedMs} ms | 1 tick = 1 minute simulée");
        System.Console.WriteLine(summary.ToString());
    }

    private static string BuildTickLine(ulong tick, int entityCount)
    {
        return $"tick {tick:D8}  {SimulationTime.FormatClock(tick)}  entités : {entityCount}";
    }
}