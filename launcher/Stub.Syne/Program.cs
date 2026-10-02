using System.Globalization;
using System.Text.Json;

namespace Stub.Syne;

/// <summary>
/// Stub.Syne (INTEGRATION_CONTRACT.md §13) : moteur simulé conforme au contrat.
/// Endpoints : /health/live, /health/ready, /health/details, /info, /metrics, /control/shutdown.
/// Écrit uniquement dans le dossier fourni (--work-dir). Respecte --seed et --ticks, sort seul.
/// </summary>
internal static class Program
{
    private static int _tick;
    private static bool _shutdownRequested;
    private static int _exitCode;

    private static int Main(string[] args)
    {
        var options = ParseArguments(args);
        Console.WriteLine($"[Stub.Syne] instance={options.InstanceId} seed={options.Seed} ticks={options.Ticks} workDir={options.WorkDirectory}");

        if (!string.IsNullOrEmpty(options.WorkDirectory))
        {
            Directory.CreateDirectory(options.WorkDirectory);
            Directory.CreateDirectory(Path.Combine(options.WorkDirectory, "data"));
            Directory.CreateDirectory(Path.Combine(options.WorkDirectory, "logs"));
        }

        var server = new MiniHttpServer(options.ControlPort);
        server.Get("/health/live", _ => "{\"status\":\"Healthy\"}");
        server.Get("/health/ready", _ => _shutdownRequested ? null : "{\"status\":\"Healthy\"}");
        server.Get("/health/details", _ => $"{{\"status\":\"Healthy\",\"checks\":[{{\"name\":\"process\",\"status\":\"Healthy\"}},{{\"name\":\"tick_progress\",\"status\":\"Healthy\",\"tick\":{_tick}}}]}}");
        server.Get("/info", _ => $"{{\"id\":\"syne\",\"version\":\"0.1.0-stub\",\"protocolVersion\":1,\"seed\":{options.Seed}}}");
        server.Get("/metrics", _ => $"livex_syne_tick {_tick}\nlivex_syne_tick_rate_hz 12.5\nlivex_syne_agents {options.AgentCount}\n");
        server.Post("/control/shutdown", (_, _) =>
        {
            if (options.RefuseShutdown)
            {
                return "{\"error\":\"refused\"}";
            }

            _shutdownRequested = true;
            return "{\"status\":\"shutting_down\"}";
        });
        try
        {
            server.Start();
        }
        catch (System.Net.Sockets.SocketException exception)
        {
            // Port indisponible, échec d'écoute → code 3 (INTEGRATION_CONTRACT.md §4), jamais un crash.
            Console.Error.WriteLine($"[Stub.Syne] port {options.ControlPort} indisponible : {exception.Message}");
            return 3;
        }

        var delay = TimeSpan.FromMilliseconds(20 * Math.Max(0.1, options.SlowFactor));
        for (var tick = 1; tick <= options.Ticks && !_shutdownRequested; tick++)
        {
            Thread.Sleep(delay);
            _tick = tick;

            if (options.FreezeAtTick == tick)
            {
                // Gel : le tick ne progresse plus, le processus reste vivant (test Injoignable).
                Thread.Sleep(Timeout.Infinite);
            }

            if (options.CrashAtTick == tick)
            {
                Console.Error.WriteLine($"[Stub.Syne] crash injecté au tick {tick}");
                return 1;
            }
        }

        // Fin de l'horizon : écrit ses sorties dans le dossier du run, puis sort (mode batch).
        if (!string.IsNullOrEmpty(options.WorkDirectory))
        {
            File.WriteAllText(Path.Combine(options.WorkDirectory, "data", "result.json"),
                $"{{\"seed\":{options.Seed},\"ticks\":{_tick},\"agents\":{options.AgentCount},\"fingerprint\":\"stub-{options.Seed:D12}\"}}");
            File.WriteAllText(Path.Combine(options.WorkDirectory, "logs", "syne-stub.log"),
                $"stub run seed={options.Seed} ticks={_tick}\n");
        }

        Console.WriteLine($"[Stub.Syne] horizon atteint ({_tick} ticks), sortie normale");
        _exitCode = options.ExitCode ?? 0;
        return _exitCode;
    }

    private static StubOptions ParseArguments(string[] args)
    {
        var options = new StubOptions
        {
            ControlPort = int.Parse(Environment.GetEnvironmentVariable("STUB_CONTROL_PORT") ?? "5299", CultureInfo.InvariantCulture),
        };

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--instance-id" when i + 1 < args.Length:
                    options.InstanceId = args[++i];
                    break;
                case "--control-port" when i + 1 < args.Length:
                    options.ControlPort = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--work-dir" when i + 1 < args.Length:
                    options.WorkDirectory = args[++i];
                    break;
                case "--log-dir" when i + 1 < args.Length:
                    options.LogDirectory = args[++i];
                    break;
                case "--seed" when i + 1 < args.Length:
                    options.Seed = long.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--ticks" when i + 1 < args.Length:
                case "--max-ticks" when i + 1 < args.Length:
                    options.Ticks = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--config" when i + 1 < args.Length:
                    using (var configuration = JsonDocument.Parse(File.ReadAllText(args[++i])))
                    {
                        if (configuration.RootElement.TryGetProperty("agents", out var agents)
                            && agents.TryGetProperty("initialCount", out var initialCount))
                        {
                            options.AgentCount = initialCount.GetInt32();
                        }
                    }
                    break;
                case "--slow-factor" when i + 1 < args.Length:
                    options.SlowFactor = double.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--crash-at-tick" when i + 1 < args.Length:
                    options.CrashAtTick = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--freeze-at-tick" when i + 1 < args.Length:
                    options.FreezeAtTick = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--exit-code" when i + 1 < args.Length:
                    options.ExitCode = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--refuse-shutdown":
                    options.RefuseShutdown = true;
                    break;
            }
        }

        return options;
    }

    private sealed class StubOptions
    {
        public string InstanceId { get; set; } = "syne-0001";
        public int ControlPort { get; set; }
        public string WorkDirectory { get; set; } = string.Empty;
        public string LogDirectory { get; set; } = string.Empty;
        public long Seed { get; set; }
        public int Ticks { get; set; } = 10;
        public int AgentCount { get; set; } = 50;
        public double SlowFactor { get; set; } = 1.0;
        public int? CrashAtTick { get; set; }
        public int? FreezeAtTick { get; set; }
        public int? ExitCode { get; set; }
        public bool RefuseShutdown { get; set; }
    }
}
