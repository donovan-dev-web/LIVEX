using System.Globalization;

namespace Simulation.Core.Configuration;

/// <summary>
/// Options brutes issues des flags de ligne de commande (Annexe H §3), avant fusion.
/// </summary>
public sealed record CliOptions(
    ulong? Seed,
    int? MaxTicks,
    (int Width, int Height)? WorldSize,
    bool? Headless,
    string? ConfigPath,
    bool? Observe = null,
    int? ObservePort = null,
    bool? Benchmark = null,
    int? BenchmarkTicks = null,
    string? BenchmarkPopulations = null,
    bool? Serve = null,
    int? ServePort = null,
    bool Help = false)
{
    /// <summary>Ports d'écoute acceptés par les écouteurs BCL.</summary>
    public const int MinPort = 1;

    public const int MaxPort = 65535;

    /// <summary>Texte d'usage affiché par <c>--help</c>.</summary>
    public const string Usage = """
        SYNE — simulation multi-agents déterministe (DETERMINISM.md §1).

        Usage :
          dotnet run --project Simulation.Console -- [options]

        Options de simulation :
          --seed <ulong>                  Graine du PRNG (xoshiro256**).
          --max-ticks <int>               Nombre maximal de ticks.
          --world-size <largeur> <hauteur> Dimensions du monde en unités monde.
          --headless                      Exécute sans rapport de progression.
          --config <chemin>               Fichier de configuration JSON.

        Observabilité :
          --observe                       Diffuse les trames sur WebSocket.
          --observe-port <port>           Port d'écoute (défaut 5180), avec
                                        --observe ou --serve.

        Serveur de contrôle :
          --serve                         Démarre le serveur HTTP de contrôle.
          --serve-port <port>             Port d'écoute (défaut 5181).

        Benchmark :
          --benchmark                     Mesure débit, budgets et checksum.
          --benchmark-ticks <int>         Ticks par palier de benchmark.
          --benchmark-populations <list>  Paliers de population, ex. 50,500,1000.

        Aides :
          --help, -h                      Affiche cette aide.

        Codes de sortie :
          0  succès
          2  configuration ou arguments invalides
        """;

    /// <summary>Drapeaux qui ne prennent pas de valeur.</summary>
    private static readonly HashSet<string> ValuelessFlags = new(StringComparer.Ordinal)
    {
        "--headless", "--observe", "--benchmark", "--serve", "--help", "-h",
    };

    /// <summary>
    /// Analyse les arguments de la ligne de commande ; lève une
    /// <see cref="ArgumentException"/> explicite sur un flag inconnu, une valeur
    /// manquante ou une valeur illisible.
    ///
    /// <para>
    /// Les valeurs sont analysées en culture invariante : sous une culture à
    /// virgule décimale, <c>int.Parse("1000")</c> pouvait échouer ou être mal
    /// interprétée, et <c>FormatException</c>/<c>OverflowException</c> n'étaient
    /// pas filtrées par le point d'entrée — l'utilisateur obtenait une pile
    /// d'appels au lieu d'un message.
    /// </para>
    /// </summary>
    public static CliOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ulong? seed = null;
        int? maxTicks = null;
        (int, int)? worldSize = null;
        bool? headless = null;
        string? configPath = null;
        bool? observe = null;
        int? observePort = null;
        bool? benchmark = null;
        int? benchmarkTicks = null;
        string? benchmarkPopulations = null;
        bool? serve = null;
        int? servePort = null;
        bool help = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed":
                    seed = ParseULong(RequireValue(args, ref i, "--seed"), "--seed");
                    break;
                case "--max-ticks":
                    maxTicks = ParseInt(RequireValue(args, ref i, "--max-ticks"), "--max-ticks");
                    break;
                case "--world-size":
                    // Deux valeurs : les lire d'un coup. Appeler RequireValue deux
                    // fois faisait avaler le flag suivant quand la hauteur
                    // manquait (« --world-size 500 --max-ticks 10 » analysait
                    // « --max-ticks » comme une hauteur).
                    if (i + 2 >= args.Length || LooksLikeFlag(args[i + 2]))
                    {
                        // « --world-size 500 --max-ticks 10 » : le second jeton est
                        // lui-même un drapeau, donc la hauteur manque. Le dire
                        // explicitement évite de rapporter « --max-ticks » comme
                        // une hauteur illisible.
                        throw new ArgumentException("--world-size requiert deux valeurs (largeur et hauteur).");
                    }

                    int width = ParseInt(args[i + 1], "--world-size <largeur>");
                    int height = ParseInt(args[i + 2], "--world-size <hauteur>");
                    worldSize = (width, height);
                    i += 2;
                    break;
                case "--headless":
                    headless = true;
                    break;
                case "--config":
                    configPath = RequireValue(args, ref i, "--config");
                    break;
                case "--observe":
                    observe = true;
                    break;
                case "--observe-port":
                    observePort = ParsePort(RequireValue(args, ref i, "--observe-port"), "--observe-port");
                    break;
                case "--benchmark":
                    benchmark = true;
                    break;
                case "--benchmark-ticks":
                    benchmarkTicks = ParseInt(RequireValue(args, ref i, "--benchmark-ticks"), "--benchmark-ticks");
                    break;
                case "--benchmark-populations":
                    benchmarkPopulations = RequireValue(args, ref i, "--benchmark-populations");
                    break;
                case "--serve":
                    serve = true;
                    break;
                case "--serve-port":
                    servePort = ParsePort(RequireValue(args, ref i, "--serve-port"), "--serve-port");
                    break;
                case "--help":
                case "-h":
                    help = true;
                    break;
                default:
                    throw new ArgumentException(
                        $"Flag CLI inconnu : \"{args[i]}\". Lancez --help pour la liste des options.");
            }
        }

        ValidateMode(observe, observePort, "--observe", "--observe-port", serve, servePort, "--serve", "--serve-port");

        return new CliOptions(
            seed, maxTicks, worldSize, headless, configPath,
            observe, observePort, benchmark, benchmarkTicks, benchmarkPopulations,
            serve, servePort, help);
    }

    /// <summary>
    /// Un port d'écoute sans aucun mode qui le consomme est une coquille : il
    /// était silencieusement ignoré, et l'utilisateur croyait avoir changé le
    /// port d'écoute.
    /// <para>
    /// Attention : <c>--observe-port</c> est consommé par <em>deux</em> modes.
    /// En mode <c>--serve</c>, <c>Program</c> démarre l'observability sur ce port
    /// pour que les clients d'un run contrôlé aient le même flux qu'en mode
    /// <c>--observe</c>. Refuser cette combinaison casserait le contrat
    /// d'intégration SYNE → ECHOS, qui lance
    /// <c>--serve --serve-port P --observe-port Q</c>. Le port d'observation
    /// n'est donc refusé que si <em>aucun</em> des deux modes n'est actif.
    /// </para>
    /// </summary>
    private static void ValidateMode(
        bool? observe, int? observePort, string observeFlag, string observePortFlag,
        bool? serve, int? servePort, string serveFlag, string servePortFlag)
    {
        if (observePort is not null && observe != true && serve != true)
        {
            throw new ArgumentException(
                $"{observePortFlag} exige {observeFlag} ou {serveFlag}.");
        }

        if (servePort is not null && serve != true)
        {
            throw new ArgumentException($"{servePortFlag} exige {serveFlag}.");
        }
    }

    /// <summary>Un jeton commençant par « -- » est un drapeau, jamais une valeur.</summary>
    private static bool LooksLikeFlag(string token) => token.StartsWith("--", StringComparison.Ordinal);

    private static string RequireValue(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Le flag {flag} requiert une valeur.");
        }

        string next = args[index + 1];
        if (ValuelessFlags.Contains(next))
        {
            throw new ArgumentException($"Le flag {flag} requiert une valeur (reçu : \"{next}\").");
        }

        index++;
        return next;
    }

    private static int ParseInt(string raw, string flag)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new ArgumentException($"Valeur invalide pour {flag} : \"{raw}\" (entier attendu).");
        }

        return value;
    }

    private static ulong ParseULong(string raw, string flag)
    {
        if (!ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value))
        {
            // Couvre le cas « -1 » : ulong.TryParse échoue et l'utilisateur
            // n'obtenait qu'une OverflowException non filtrée.
            throw new ArgumentException($"Valeur invalide pour {flag} : \"{raw}\" (entier non négatif attendu).");
        }

        return value;
    }

    private static int ParsePort(string raw, string flag)
    {
        int port = ParseInt(raw, flag);
        if (port is < MinPort or > MaxPort)
        {
            // 0 ferait écouter sur un port aléatoire sans le dire ; > 65535 fait
            // échouer HttpListener bien plus tard, avec un message opaque.
            throw new ArgumentException($"Port invalide pour {flag} : {port} (attendu dans [{MinPort}, {MaxPort}]).");
        }

        return port;
    }
}
