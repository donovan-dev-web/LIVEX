namespace Simulation.Core.Configuration;

/// <summary>
/// Options brutes issues des flags de ligne de commande (Annexe H §3), avant fusion.
/// </summary>
public sealed record CliOptions(
    ulong? Seed,
    int? MaxTicks,
    (int Width, int Height)? WorldSize,
    bool? Headless,
    string? ConfigPath)
{
    /// <summary>Analyse les arguments de la ligne de commande ; lève une erreur explicite sur un flag inconnu.</summary>
    public static CliOptions Parse(string[] args)
    {
        ulong? seed = null;
        int? maxTicks = null;
        (int, int)? worldSize = null;
        bool? headless = null;
        string? configPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed":
                    seed = ulong.Parse(RequireValue(args, ref i, "--seed"));
                    break;
                case "--max-ticks":
                    maxTicks = int.Parse(RequireValue(args, ref i, "--max-ticks"));
                    break;
                case "--world-size":
                    int width = int.Parse(RequireValue(args, ref i, "--world-size"));
                    int height = int.Parse(RequireValue(args, ref i, "--world-size"));
                    worldSize = (width, height);
                    break;
                case "--headless":
                    headless = true;
                    break;
                case "--config":
                    configPath = RequireValue(args, ref i, "--config");
                    break;
                default:
                    throw new ArgumentException($"Flag CLI inconnu : \"{args[i]}\".");
            }
        }

        return new CliOptions(seed, maxTicks, worldSize, headless, configPath);
    }

    private static string RequireValue(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Le flag {flag} requiert une valeur.");
        }

        index++;
        return args[index];
    }
}