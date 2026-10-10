using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Console.Tests;

/// <summary>
/// Parcours Launcher → SYNE pour le scénario <c>prism</c> (ADR-017) : la
/// résolution d'options place le profil <b>sous</b> la surcouche
/// <c>launcher-config.json</c>, et le batch supervisé transporté par le Launcher
/// (<c>--simulation prism --config … --autostart</c>) porte bien le profil dans
/// la trame <c>world_initialized</c> (description 1.1) et les snapshots.
/// </summary>
public sealed class Adr017PrismBatchTests
{
    [Fact]
    public void ResolveOptions_AppliesThePrismProfileUnderTheConfigOverlay()
    {
        string overlayPath = WriteTempOverlay("""{ "agents": { "initialCount": 3 }, "simulation": { "ticksPerSecond": 24 } }""");
        try
        {
            CliOptions cli = CliOptions.Parse([
                "--simulation", "prism",
                "--config", overlayPath,
                "--world-size", "999", "999",
                "--seed", "77"]);

            SimulationOptions options = Program.ResolveOptions(cli);

            // Profil prism (ADR-017)…
            Assert.Equal(32, options.Simulation.WorldCellSize);
            Assert.Equal(5, options.Simulation.SimulatedSecondsPerTick);
            Assert.Equal(1.0, options.World.MetersPerUnit);
            // …sous la surcouche du Launcher (priorité §5)…
            Assert.Equal(24, options.Simulation.TicksPerSecond);
            Assert.Equal(3, options.Agents.InitialCount);
            // …et sous les flags CLI (priorité la plus haute : monde et seed).
            Assert.Equal(999, options.Simulation.WorldWidth);
            Assert.Equal(999, options.Simulation.WorldHeight);
            Assert.Equal(77ul, options.Random.Seed);
        }
        finally
        {
            File.Delete(overlayPath);
        }
    }

    [Fact]
    public void ResolveOptions_KeepsTheReferenceBehaviourUntouched()
    {
        // `reference` (ou aucun scénario) n'injecte rien : le profil de référence
        // EST le défaut intégré (ADR-016) — comportement historique bit-à-bit.
        foreach (string? simulation in new[] { (string?)null, "reference" })
        {
            string[] args = simulation is null ? ["--headless"] : ["--simulation", simulation, "--headless"];
            SimulationOptions options = Program.ResolveOptions(CliOptions.Parse(args));

            Assert.Equal(500, options.Simulation.WorldWidth);
            Assert.Equal(60, options.Simulation.SimulatedSecondsPerTick);
            Assert.Equal(10, options.Simulation.TicksPerSecond);
            Assert.Equal(1.0, options.World.MetersPerUnit);
        }
    }

    [Fact]
    public async Task SupervisedPrismBatch_ExposesTheScaleOnWorldInitializedAndSnapshots()
    {
        // Shape exact des arguments du Launcher (ProcessRunExecutor) : surcouche
        // launcher-config.json (agents + cadence) + --simulation prism.
        BatchOutcome outcome = await RunPrismBatchAsync();
        try
        {
            outcome.AssertSucceeded();

            string streamPath = Path.Combine(outcome.WorkDirectory, "data", "stream.jsonl");
            Assert.True(File.Exists(streamPath), "SYNE n'a pas écrit data/stream.jsonl.");

            List<JsonElement> messages = ReadMessages(streamPath);
            JsonElement initialized = messages[0];
            Assert.Equal("world_initialized", initialized.GetProperty("type").GetString());
            Assert.Equal("1.1", initialized.GetProperty("version").GetString());

            JsonElement world = initialized.GetProperty("world");
            Assert.Equal(2240, world.GetProperty("width").GetInt32());
            Assert.Equal(32, world.GetProperty("cellSize").GetDouble());
            Assert.Equal(5, world.GetProperty("simulatedSecondsPerTick").GetInt32());
            Assert.Equal(1.0, world.GetProperty("metersPerUnit").GetDouble());
            // La surcouche du Launcher garde la priorité sur la cadence du profil.
            Assert.Equal(24, world.GetProperty("ticksPerSecond").GetInt32());

            List<JsonElement> snapshots = messages
                .Where(message => message.GetProperty("type").GetString() == "snapshot")
                .ToList();
            Assert.Equal(3, snapshots.Count);
            foreach (JsonElement snapshot in snapshots)
            {
                long tick = snapshot.GetProperty("tick").GetInt64();
                Assert.Equal(tick * 5, snapshot.GetProperty("simulatedTimeSeconds").GetInt64());
                Assert.Equal((tick * 5) / 60, snapshot.GetProperty("simulatedTimeMinutes").GetInt64());
            }
        }
        finally
        {
            outcome.Dispose();
        }
    }

    private static string WriteTempOverlay(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"syne-prism-overlay-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static async Task<BatchOutcome> RunPrismBatchAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"syne-prism-batch-{Guid.NewGuid():N}");
        string workDirectory = Path.Combine(root, "work");
        string logDirectory = Path.Combine(root, "logs");
        Directory.CreateDirectory(workDirectory);
        Directory.CreateDirectory(logDirectory);
        string configurationPath = Path.Combine(workDirectory, "launcher-config.json");
        await File.WriteAllTextAsync(
            configurationPath,
            """{"agents":{"initialCount":3},"simulation":{"ticksPerSecond":24}}""");
        int controlPort = FreePort();
        const string correlationId = "syne-prism-batch-acceptance";

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(typeof(global::Simulation.Console.Program).Assembly.Location);
        foreach (string argument in new[]
        {
            "--headless",
            "--instance-id", "syne-prism-batch",
            "--control-port", controlPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--work-dir", workDirectory,
            "--log-dir", logDirectory,
            "--simulation", "prism",
            "--seed", "12345",
            "--ticks", "3",
            "--config", configurationPath,
            "--autostart",
            "--export-stream",
            "--correlation-id", correlationId,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["LIVEX_SESSION_TOKEN"] = "syne-prism-token";
        startInfo.Environment["LIVEX_CORRELATION_ID"] = correlationId;

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("SYNE prism batch test process failed to start.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
            return new BatchOutcome(root, workDirectory, process, await standardOutput, await standardError);
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            Directory.Delete(root, recursive: true);
            throw;
        }
    }

    private static List<JsonElement> ReadMessages(string streamPath)
    {
        return [.. File.ReadAllLines(streamPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonDocument.Parse(line).RootElement)];
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class BatchOutcome(
        string root,
        string workDirectory,
        Process process,
        string standardOutput,
        string standardError) : IDisposable
    {
        public string WorkDirectory { get; } = workDirectory;

        public void AssertSucceeded()
        {
            Assert.True(
                process.ExitCode == 0,
                $"SYNE batch a échoué ({process.ExitCode}). {standardError}\n{standardOutput}");
        }

        public void Dispose()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            process.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
