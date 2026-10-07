using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Xunit;

namespace Simulation.Console.Tests;

/// <summary>
/// Export batch du flux d'observabilité (J2B) : l'artefact produit doit être
/// rejouable par ECHOS avec le contrat du flux live, reproductible bit à bit pour
/// une graine donnée, et absent du workspace quand il n'a pas été demandé.
/// </summary>
public sealed class BatchStreamExportTests
{
    [Fact]
    public async Task SupervisedBatchExportsTheAlignedObservabilityStream()
    {
        BatchOutcome outcome = await RunBatchAsync(exportStream: true, ticks: 6, seed: 42);
        try
        {
            outcome.AssertSucceeded();

            string streamPath = Path.Combine(outcome.WorkDirectory, "data", "stream.jsonl");
            Assert.True(File.Exists(streamPath), "SYNE n'a pas écrit data/stream.jsonl.");

            List<JsonElement> messages = ReadMessages(streamPath);
            Assert.Equal("world_initialized", messages[0].GetProperty("type").GetString());

            List<JsonElement> snapshots = messages.Where(message => message.GetProperty("type").GetString() == "snapshot").ToList();
            Assert.Equal(
                Enumerable.Range(1, 6).Select(tick => (ulong)tick).ToArray(),
                snapshots.Select(snapshot => snapshot.GetProperty("tick").GetUInt64()).ToArray());

            // ECHOS exige 1 tick_summary par snapshot, sur le même tick.
            List<JsonElement> summaries = messages
                .Where(message => message.GetProperty("type").GetString() == "tick_summary")
                .ToList();
            Assert.Equal(
                Enumerable.Range(1, 6).Select(tick => (ulong)tick).ToArray(),
                summaries.Select(summary => summary.GetProperty("tick").GetUInt64()).ToArray());

            // Un run demandé par le Launcher porte l'identité qu'il a fournie :
            // c'est la clé sous laquelle ECHOS enregistre le run.
            foreach (JsonElement snapshot in snapshots)
            {
                Assert.Equal("EXP-STREAM-RUN-0001", snapshot.GetProperty("runId").GetString());
                Assert.Equal(42ul, snapshot.GetProperty("seed").GetUInt64());
            }
        }
        finally
        {
            outcome.Dispose();
        }
    }

    [Fact]
    public async Task TwoIdenticalSeedsProduceTheSameStreamBytes()
    {
        BatchOutcome first = await RunBatchAsync(exportStream: true, ticks: 5, seed: 4242);
        BatchOutcome second = await RunBatchAsync(exportStream: true, ticks: 5, seed: 4242);
        try
        {
            byte[] firstBytes = await File.ReadAllBytesAsync(
                Path.Combine(first.WorkDirectory, "data", "stream.jsonl"));
            byte[] secondBytes = await File.ReadAllBytesAsync(
                Path.Combine(second.WorkDirectory, "data", "stream.jsonl"));
            Assert.Equal(
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(firstBytes)),
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(secondBytes)));
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }
    }

    [Fact]
    public async Task DifferentSeedsProduceDifferentStreams()
    {
        BatchOutcome first = await RunBatchAsync(exportStream: true, ticks: 5, seed: 1);
        BatchOutcome second = await RunBatchAsync(exportStream: true, ticks: 5, seed: 2);
        try
        {
            byte[] firstBytes = await File.ReadAllBytesAsync(
                Path.Combine(first.WorkDirectory, "data", "stream.jsonl"));
            byte[] secondBytes = await File.ReadAllBytesAsync(
                Path.Combine(second.WorkDirectory, "data", "stream.jsonl"));
            Assert.NotEqual(firstBytes, secondBytes);
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }
    }

    [Fact]
    public async Task WithoutTheFlagTheWorkspaceKeepsOnlyTheResult()
    {
        BatchOutcome outcome = await RunBatchAsync(exportStream: false, ticks: 3, seed: 42);
        try
        {
            outcome.AssertSucceeded();
            Assert.Equal(
                new[] { "result.json" },
                Directory.EnumerateFiles(Path.Combine(outcome.WorkDirectory, "data"))
                    .Select(Path.GetFileName)
                    .OrderBy(name => name, StringComparer.Ordinal));
            using JsonDocument result = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(outcome.WorkDirectory, "data", "result.json")));
            Assert.Equal(JsonValueKind.Null, result.RootElement.GetProperty("streamFile").ValueKind);
        }
        finally
        {
            outcome.Dispose();
        }
    }

    [Fact]
    public async Task TheResultDeclaresTheStreamAndTheRunIdentity()
    {
        BatchOutcome outcome = await RunBatchAsync(exportStream: true, ticks: 3, seed: 42);
        try
        {
            using JsonDocument result = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(outcome.WorkDirectory, "data", "result.json")));
            Assert.Equal("stream.jsonl", result.RootElement.GetProperty("streamFile").GetString());
            Assert.Equal("EXP-STREAM-RUN-0001", result.RootElement.GetProperty("runId").GetString());
        }
        finally
        {
            outcome.Dispose();
        }
    }

    [Fact]
    public async Task AnUnsafeRunIdentityIsRefusedBeforeAnyProcessStarts()
    {
        BatchOutcome outcome = await RunBatchAsync(exportStream: true, ticks: 1, seed: 42, runId: "../evasion");
        using (outcome)
        {
            Assert.Equal(2, outcome.ExitCode);
            Assert.Contains("--run-id", outcome.StandardError, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(outcome.Root, "work", "data")));
        }
    }

    [Fact]
    public async Task ExportStreamWithoutAnExportDirectoryIsRefused()
    {
        string root = Path.Combine(Path.GetTempPath(), $"syne-stream-nodecl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(typeof(global::Simulation.Console.Program).Assembly.Location);
        foreach (string argument in new[] { "--seed", "42", "--max-ticks", "1", "--export-stream" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("SYNE export-stream test process failed to start.");
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(2, process.ExitCode);
        Assert.Contains("--export-stream exige --export-dir", error, StringComparison.Ordinal);
        Directory.Delete(root, recursive: true);
    }

    private static List<JsonElement> ReadMessages(string path) =>
        File.ReadAllLines(path)
            .Where(line => line.Length > 0)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToList();

    private static string? GetOptionalString(JsonElement message, string property) =>
        message.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static async Task<BatchOutcome> RunBatchAsync(bool exportStream, int ticks, ulong seed, string? runId = "EXP-STREAM-RUN-0001")
    {
        string root = Path.Combine(Path.GetTempPath(), $"syne-stream-{Guid.NewGuid():N}");
        string workDirectory = Path.Combine(root, "work");
        string logDirectory = Path.Combine(root, "logs");
        Directory.CreateDirectory(workDirectory);
        Directory.CreateDirectory(logDirectory);
        string configurationPath = Path.Combine(root, "launcher-config.json");
        await File.WriteAllTextAsync(configurationPath, """{"agents":{"initialCount":3}}""");
        int controlPort = FreePort();
        const string correlationId = "syne-stream-acceptance";

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
            "--instance-id", "syne-stream",
            "--control-port", controlPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--work-dir", workDirectory,
            "--log-dir", logDirectory,
            "--simulation", "reference",
            "--seed", seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--ticks", ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--config", configurationPath,
            "--autostart",
            "--correlation-id", correlationId,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (exportStream)
        {
            startInfo.ArgumentList.Add("--export-stream");
        }

        if (runId is not null)
        {
            startInfo.ArgumentList.Add("--run-id");
            startInfo.ArgumentList.Add(runId);
        }

        startInfo.Environment["LIVEX_SESSION_TOKEN"] = "syne-stream-token";
        startInfo.Environment["LIVEX_CORRELATION_ID"] = correlationId;

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("SYNE stream export test process failed to start.");
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
        public string Root { get; } = root;

        public string WorkDirectory { get; } = workDirectory;

        public string StandardOutput { get; } = standardOutput;

        public string StandardError { get; } = standardError;

        public int ExitCode => process.ExitCode;

        public void AssertSucceeded()
        {
            Assert.True(
                process.ExitCode == 0,
                $"SYNE batch a échoué ({process.ExitCode}). {StandardError}\n{StandardOutput}");
        }

        public void Dispose()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            process.Dispose();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}