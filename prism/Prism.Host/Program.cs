using System.Text.Json;
using Prism.Content;
using Prism.Contracts;
using Prism.Host.Hosting;
using Prism.Host.Persistence;

if (args.Length == 1 && args[0] is "--info" or "--version")
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        id = "prism-host",
        version = "2.0.0-preview.1",
        protocolVersion = ProtocolVersion.Current.Major,
    }, ProtocolJson.Options));
    return;
}

if (args.Length == 2 && args[0] == "--content-hash")
{
    // Le hash du pack est requis par `/control/prepare` : l'imprimer évite de le recopier
    // d'un rapport de benchmark ou de le deviner après une modification du contenu.
    var compilation = new Prism.Content.ContentPackCompiler().CompileDirectory(args[1]);
    if (!compilation.Succeeded)
    {
        foreach (var error in compilation.Errors)
        {
            Console.Error.WriteLine($"{error.Code}: {error.Path}: {error.Message}");
        }

        Environment.ExitCode = 3;
        return;
    }

    Console.WriteLine(compilation.Pack!.ContentHash);
    return;
}

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    Console.WriteLine("PRISM headless host");
    Console.WriteLine("  --control-port <1-65535>  Local HTTP and WebSocket port");
    Console.WriteLine("  --content-hash <dir>      Compile a content pack and print its contentHash");
    Console.WriteLine("  --content <dir>           Content pack compiled by --autostart");
    Console.WriteLine("  --seed <n>                World seed for --autostart (default: 12345)");
    Console.WriteLine("  --autostart               Prepare and start the world at launch");
    Console.WriteLine("  --prepare                 Prepare the world at launch, stay ready (no tick)");
    Console.WriteLine("  --ticks-per-second <n>    Advance ticks automatically (0 = lockstep, default)");
    Console.WriteLine("  --read-port <1-65535>     Optional read-stream WebSocket port");
    Console.WriteLine("  --work-dir <path>         Instance data and checkpoint directory");
    Console.WriteLine("  --instance-id <id>        Supervised instance identifier");
    Console.WriteLine("  --log-dir <path>          Component log directory");
    Console.WriteLine("  --correlation-id <id>     Operation correlation identifier");
    Console.WriteLine("  --info                    Print component identity and exit");
    return;
}

var values = new Dictionary<string, string>(StringComparer.Ordinal);
var headless = false;
var autostart = false;
var prepareOnly = false;
for (var index = 0; index < args.Length; index++)
{
    if (args[index] is "--headless" or "--autostart" or "--prepare")
    {
        headless |= args[index] == "--headless";
        autostart |= args[index] == "--autostart";
        prepareOnly |= args[index] == "--prepare";
        continue;
    }

    if (args[index] is not ("--control-port" or "--read-port" or "--work-dir" or "--instance-id"
            or "--log-dir" or "--correlation-id" or "--content" or "--seed" or "--ticks-per-second")
        || index + 1 >= args.Length ||
        !values.TryAdd(args[index], args[index + 1]))
    {
        Console.Error.WriteLine($"Unknown, duplicate, or incomplete argument: {args[index]}");
        Environment.ExitCode = 2;
        return;
    }

    index++;
}

if (!values.TryGetValue("--control-port", out var portValue) ||
    !int.TryParse(portValue, out var port) ||
    port is < 1 or > 65535)
{
    Console.Error.WriteLine("A valid --control-port <1-65535> is required.");
    Environment.ExitCode = 2;
    return;
}

int? readPort = null;
if (values.TryGetValue("--read-port", out var readPortValue))
{
    if (!int.TryParse(readPortValue, out var parsedReadPort) ||
        parsedReadPort is < 1 or > 65535 ||
        parsedReadPort == port)
    {
        Console.Error.WriteLine("A valid --read-port distinct from --control-port is required.");
        Environment.ExitCode = 2;
        return;
    }

    readPort = parsedReadPort;
}

if (headless)
{
    foreach (var required in new[] { "--instance-id", "--log-dir", "--correlation-id", "--work-dir" })
    {
        if (!values.ContainsKey(required))
        {
            Console.Error.WriteLine($"Supervised headless launch requires {required}.");
            Environment.ExitCode = 2;
            return;
        }
    }
}

WorldCheckpointStore? checkpointStore = null;
if (values.TryGetValue("--work-dir", out var workDirectory))
{
    var fullWorkDirectory = Path.GetFullPath(workDirectory);
    Directory.CreateDirectory(fullWorkDirectory);
    checkpointStore = new WorldCheckpointStore(Path.Combine(fullWorkDirectory, "world.sqlite"));
}

if (values.TryGetValue("--log-dir", out var logDirectory))
{
    Directory.CreateDirectory(Path.GetFullPath(logDirectory));
}

if (values.TryGetValue("--correlation-id", out var correlationId))
{
    Environment.SetEnvironmentVariable("LIVEX_CORRELATION_ID", correlationId);
}

// Le checkpoint d'arrêt sérialise l'état complet du monde dans une chaîne JSON :
// à 1 000 agents, elle dépasse la limite de 125 Mo de System.Text.Json et le
// dernier tick part en HTTP 400. C'est un artefact de mesure (un run réel le paie
// une fois, à l'arrêt), pas un coût de tick : un relevé de performance peut le
// désactiver avec PRISM_CHECKPOINT_ON_STOP=0. Défaut inchangé.
bool checkpointOnStop = Environment.GetEnvironmentVariable("PRISM_CHECKPOINT_ON_STOP") != "0";

var runtime = new PrismHostRuntime(
    checkpointStore: checkpointStore,
    checkpointOnStop: checkpointOnStop);

// Lancement autonome (Launcher, démonstration) : le pack est compilé ici, donc le
// contentHash ne peut pas diverger de celui du dossier. Sans --autostart, le host
// reste en lockstep et attend /control/prepare, comme pour les runs couplés.
double ticksPerSecond = 0;
if (values.TryGetValue("--ticks-per-second", out var tpsValue)
    && (!double.TryParse(tpsValue, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out ticksPerSecond) || ticksPerSecond < 0))
{
    Console.Error.WriteLine("A valid --ticks-per-second >= 0 is required.");
    Environment.ExitCode = 2;
    return;
}

if (autostart || prepareOnly)
{
    ulong seed = 12345;
    if (values.TryGetValue("--seed", out var seedValue)
        && !ulong.TryParse(seedValue, out seed))
    {
        Console.Error.WriteLine("A valid --seed is required.");
        Environment.ExitCode = 2;
        return;
    }

    // Le Launcher lance l'hôte depuis le répertoire de session : un chemin
    // relatif au manifeste ne désignerait pas l'installation. On résout donc le
    // pack installé (LIVEX_INSTALL_ROOT/content/minimal) avant les emplacements
    // de développement, et --content reste prioritaire quand il est fourni.
    string contentDirectory;
    if (values.GetValueOrDefault("--content") is { } explicitContent)
    {
        contentDirectory = Path.GetFullPath(explicitContent);
    }
    else
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("LIVEX_INSTALL_ROOT") is { } installRoot
                ? Path.Combine(installRoot, "content", "minimal")
                : null,
            Path.Combine(AppContext.BaseDirectory, "content", "minimal"),
            Path.Combine(AppContext.BaseDirectory, "..", "content", "minimal"),
        };
        contentDirectory = Path.GetFullPath(
            candidates.FirstOrDefault(candidate =>
                candidate is not null && File.Exists(Path.Combine(candidate, "pack.json")))
            ?? Path.Combine(AppContext.BaseDirectory, "content", "minimal"));
    }

    var compilation = new ContentPackCompiler().CompileDirectory(contentDirectory);
    if (!compilation.Succeeded)
    {
        foreach (var error in compilation.Errors)
        {
            Console.Error.WriteLine($"{error.Code}: {error.Path}: {error.Message}");
        }

        Environment.ExitCode = 3;
        return;
    }

    try
    {
        runtime.Prepare(new PrepareWorldRequest(seed, compilation.Pack!.ContentHash, contentDirectory));
        if (autostart)
        {
            runtime.Start(new StartWorldRequest());
        }
    }
    catch (Exception exception) when (exception is HostLifecycleException or InvalidDataException)
    {
        Console.Error.WriteLine($"autostart: {exception.Message}");
        Environment.ExitCode = 3;
        return;
    }
}

var app = PrismHostServer.Build(
    port,
    runtime,
    Environment.GetEnvironmentVariable("LIVEX_SESSION_TOKEN"),
    readPort);

// Horloge optionnelle : chaque tick est demandé au runtime, donc un client en
// lecture seule voit le monde avancer sans piloter quoi que ce soit. En mode
// couplé (SYNE envoie /data/tick), laisser 0 pour ne pas doubler les pas.
using var clockCancellation = new CancellationTokenSource();
app.Lifetime.ApplicationStopping.Register(clockCancellation.Cancel);
if (ticksPerSecond > 0)
{
    var interval = TimeSpan.FromSeconds(1d / ticksPerSecond);
    _ = Task.Run(async () =>
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(clockCancellation.Token))
            {
                try
                {
                    runtime.Step(new TickRequest(
                        ProtocolVersion.Current.ToString(),
                        runtime.Status.LastCompletedTick + 1,
                        []));
                }
                catch (Exception exception) when (exception is HostLifecycleException or InvalidOperationException)
                {
                    // Pause, arrêt ou tick concurrent : l'horloge attend le prochain battement.
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    });
}

await app.RunAsync();
