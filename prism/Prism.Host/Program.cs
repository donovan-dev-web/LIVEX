using System.Text.Json;
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

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    Console.WriteLine("PRISM headless host");
    Console.WriteLine("  --control-port <1-65535>  Local HTTP and WebSocket port");
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
for (var index = 0; index < args.Length; index++)
{
    if (args[index] == "--headless")
    {
        headless = true;
        continue;
    }

    if (args[index] is not ("--control-port" or "--read-port" or "--work-dir" or "--instance-id" or "--log-dir" or "--correlation-id") ||
        index + 1 >= args.Length ||
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

var runtime = new PrismHostRuntime(checkpointStore: checkpointStore);
var app = PrismHostServer.Build(
    port,
    runtime,
    Environment.GetEnvironmentVariable("LIVEX_SESSION_TOKEN"),
    readPort);
await app.RunAsync();
