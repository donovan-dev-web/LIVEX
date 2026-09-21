using Simulation.Core.Configuration;
using Simulation.Core.Prng;

namespace Simulation.Console;

/// <summary>
/// Point d'entrée SYNE — mode CLI/batch (ADR-002).
/// Priorité de configuration : défauts → --config → flags CLI (CONFIGURATION.md §5).
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

            if (cli.Headless != true)
            {
                PrintSummary(options);
                PrintDeterminismProbe(options.Random.Seed);
            }

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

    private static void PrintSummary(SimulationOptions options)
    {
        System.Console.WriteLine("SYNE — configuration résolue :");
        System.Console.WriteLine($"  monde : {options.Simulation.WorldWidth} x {options.Simulation.WorldHeight}");
        System.Console.WriteLine($"  maxTicks : {options.Simulation.MaxTicks} (1 tick = 1 minute simulée)");
        System.Console.WriteLine($"  PRNG : {options.Random.Engine}, seed : {options.Random.Seed}");
    }

    private static void PrintDeterminismProbe(ulong seed)
    {
        var rng = Xoshiro256StarStar.Create(seed);
        System.Console.WriteLine("  sonde déterminisme (3 tirages) :");
        System.Console.Write("    ");
        for (int i = 0; i < 3; i++)
        {
            rng = rng.NextUInt64(out ulong value);
            System.Console.Write(value.ToString("x16") + "  ");
        }

        System.Console.WriteLine();
    }
}