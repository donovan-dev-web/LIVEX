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
    bool Help = false,
    string? Simulation = null,
    int? Ticks = null,
    string? ExportDirectory = null,
    string? RunId = null,
    bool ExportStream = false,
    bool AutoStart = false,
    string? InstanceId = null,
    int? ControlPort = null,
    string? WorkDirectory = null,
    string? LogDirectory = null,
    string? CorrelationId = null)
{
    /// <summary>Ports d'écoute acceptés par les écouteurs BCL.</summary>
    public const int MinPort = 1;

    public const int MaxPort = 65535;

    /// <summary>
    /// Identifiant de run accepté par <c>--run-id</c> : mêmes caractères que
    /// l'identifiant d'un run dans le flux d'observabilité et que ceux retenus
    /// par le contrat d'intégration v1. Le run exporté doit pouvoir être
    /// enregistré tel quel dans le magasin analytique ECHOS, donc aucune
    /// séparation de chemin ni espace.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex RunIdPattern =
        new("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Texte d'usage affiché par <c>--help</c>.</summary>
    public const string Usage = """
        SYNE — simulation multi-agents déterministe (DETERMINISM.md §1).

        Usage :
          dotnet run --project Simulation.Console -- [options]

        Options de simulation :
          --seed <ulong>                  Graine du PRNG (xoshiro256**).
          --max-ticks <int>               Nombre maximal de ticks.
          --simulation <id>               Scénario batch pris en charge : reference.
          --ticks <int>                   Horizon batch strictement positif.
          --export-dir <chemin>           Dossier des artefacts batch (result.json).
          --run-id <id>                   Identité du run écrite dans le flux exporté.
          --export-stream                 Écrit le flux d'observabilité (stream.jsonl)
                                         dans le dossier d'export.
          --autostart                     Démarre le batch après préparation.
          --world-size <largeur> <hauteur> Dimensions du monde en unités monde.
          --headless                      Exécute sans rapport de progression.
          --config <chemin>               Fichier de configuration JSON.

        Service supervisé :
          --instance-id <id>              Identité de cette instance.
          --control-port <port>           Port HTTP de contrôle et readiness.
          --work-dir <chemin>             Espace de travail de l'instance.
          --log-dir <chemin>              Dossier des journaux de l'instance.
          --correlation-id <id>           Identifiant de corrélation.

        Observabilité :
          --observe                       Diffuse les trames sur WebSocket.
          --observe-port <port>           Port d'écoute (défaut 5180), avec
                                        --observe, --serve ou --control-port.

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
        "--headless", "--observe", "--benchmark", "--serve", "--autostart", "--export-stream", "--help", "-h",
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
        string? simulation = null;
        int? ticks = null;
        string? exportDirectory = null;
        string? runId = null;
        bool exportStream = false;
        bool autoStart = false;
        string? instanceId = null;
        int? controlPort = null;
        string? workDirectory = null;
        string? logDirectory = null;
        string? correlationId = null;

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
                case "--simulation":
                    simulation = RequireValue(args, ref i, "--simulation");
                    break;
                case "--ticks":
                    ticks = ParseInt(RequireValue(args, ref i, "--ticks"), "--ticks");
                    break;
                case "--export-dir":
                    exportDirectory = RequireValue(args, ref i, "--export-dir");
                    break;
                case "--run-id":
                    runId = RequireValue(args, ref i, "--run-id");
                    break;
                case "--export-stream":
                    exportStream = true;
                    break;
                case "--autostart":
                    autoStart = true;
                    break;
                case "--instance-id":
                    instanceId = RequireValue(args, ref i, "--instance-id");
                    break;
                case "--control-port":
                    controlPort = ParsePort(RequireValue(args, ref i, "--control-port"), "--control-port");
                    break;
                case "--work-dir":
                    workDirectory = RequireValue(args, ref i, "--work-dir");
                    break;
                case "--log-dir":
                    logDirectory = RequireValue(args, ref i, "--log-dir");
                    break;
                case "--correlation-id":
                    correlationId = RequireValue(args, ref i, "--correlation-id");
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

        ValidateMode(observe, observePort, "--observe", "--observe-port", serve, servePort, "--serve", "--serve-port",
            controlPort is not null);

        if (simulation is not null && simulation != "reference")
        {
            throw new ArgumentException($"Scénario batch non pris en charge : \"{simulation}\". Valeur acceptée : reference.");
        }

        if (ticks is <= 0)
        {
            throw new ArgumentException("--ticks doit être strictement positif.");
        }

        if (ticks is not null && maxTicks is not null && ticks != maxTicks)
        {
            throw new ArgumentException("--ticks et --max-ticks ne peuvent pas définir des horizons différents.");
        }

        if (runId is not null && !RunIdPattern.IsMatch(runId))
        {
            throw new ArgumentException(
                "--run-id doit commencer par une lettre ou un chiffre et n'utiliser que lettres, chiffres, point, tiret ou souligné (128 caractères max).");
        }

        if (exportStream && exportDirectory is null && !autoStart)
        {
            throw new ArgumentException("--export-stream exige --export-dir.");
        }

        if (controlPort is not null)
        {
            if (serve == true)
            {
                throw new ArgumentException("--control-port et --serve ne peuvent pas être combinés.");
            }

            if (observe == true || benchmark == true)
            {
                throw new ArgumentException("--observe et --benchmark ne peuvent pas être combinés avec --control-port.");
            }

            RequireSupervisedValue(instanceId, "--instance-id");
            RequireSupervisedValue(workDirectory, "--work-dir");
            RequireSupervisedValue(logDirectory, "--log-dir");
            RequireSupervisedValue(correlationId, "--correlation-id");
        }
        else if (instanceId is not null || workDirectory is not null || logDirectory is not null || correlationId is not null)
        {
            throw new ArgumentException("--instance-id, --work-dir, --log-dir et --correlation-id exigent --control-port.");
        }

        return new CliOptions(
            seed, maxTicks, worldSize, headless, configPath,
            observe, observePort, benchmark, benchmarkTicks, benchmarkPopulations,
            serve, servePort, help, simulation, ticks, exportDirectory, runId, exportStream, autoStart,
            instanceId, controlPort, workDirectory, logDirectory, correlationId);
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
        bool? serve, int? servePort, string serveFlag, string servePortFlag,
        bool supervised)
    {
        if (observePort is not null && observe != true && serve != true && !supervised)
        {
            throw new ArgumentException(
                $"{observePortFlag} exige {observeFlag} ou {serveFlag}.");
        }

        if (servePort is not null && serve != true)
        {
            throw new ArgumentException($"{servePortFlag} exige {serveFlag}.");
        }
    }

    private static void RequireSupervisedValue(string? value, string flag)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{flag} est obligatoire avec --control-port.");
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
