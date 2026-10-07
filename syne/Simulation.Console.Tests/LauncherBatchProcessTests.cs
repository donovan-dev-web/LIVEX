using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class LauncherBatchProcessTests
{
    [Fact]
    public async Task SupervisedServiceStopsOnlyAfterAuthenticatedShutdown()
    {
        string root = Path.Combine(Path.GetTempPath(), $"syne-launcher-service-{Guid.NewGuid():N}");
        string workDirectory = Path.Combine(root, "work");
        string logDirectory = Path.Combine(root, "logs");
        Directory.CreateDirectory(workDirectory);
        Directory.CreateDirectory(logDirectory);
        int controlPort = FreePort();
        const string correlationId = "launcher-service-acceptance";
        const string token = "launcher-service-test-token";

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(typeof(global::Simulation.Console.Program).Assembly.Location);
        AddServiceArguments(startInfo, controlPort, workDirectory, logDirectory, correlationId);
        startInfo.Environment["LIVEX_SESSION_TOKEN"] = token;
        startInfo.Environment["LIVEX_CORRELATION_ID"] = correlationId;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("SYNE service test process failed to start.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        try
        {
            bool ready = await WaitForReadinessAsync(process, controlPort, TimeSpan.FromSeconds(10));
            if (!ready)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                Assert.Fail($"SYNE never became ready. stderr: {await standardError}");
            }

            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{controlPort}/") };
            using HttpResponseMessage denied = await client.PostAsync(
                "control/shutdown",
                new ByteArrayContent([]));
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

            using var shutdown = new HttpRequestMessage(HttpMethod.Post, "control/shutdown");
            shutdown.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage accepted = await client.SendAsync(shutdown);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(
                process.ExitCode == 0,
                $"SYNE service failed ({process.ExitCode}). {await standardError}\n{await standardOutput}");
            string componentLog = await File.ReadAllTextAsync(Path.Combine(logDirectory, "syne.log"));
            Assert.Contains("event=service_stopping", componentLog, StringComparison.Ordinal);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SupervisedServiceFailsExplicitlyWhenItsControlPortIsOccupied()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        int controlPort = ((IPEndPoint)occupied.LocalEndpoint).Port;

        string root = Path.Combine(Path.GetTempPath(), $"syne-launcher-port-{Guid.NewGuid():N}");
        string workDirectory = Path.Combine(root, "work");
        string logDirectory = Path.Combine(root, "logs");
        Directory.CreateDirectory(workDirectory);
        Directory.CreateDirectory(logDirectory);
        const string correlationId = "launcher-port-acceptance";

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(typeof(global::Simulation.Console.Program).Assembly.Location);
        AddServiceArguments(startInfo, controlPort, workDirectory, logDirectory, correlationId);
        startInfo.Environment["LIVEX_SESSION_TOKEN"] = "launcher-port-test-token";
        startInfo.Environment["LIVEX_CORRELATION_ID"] = correlationId;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("SYNE port-conflict test process failed to start.");
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            string error = await standardError;
            Assert.Equal(2, process.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(error));
            Assert.False(File.Exists(Path.Combine(logDirectory, "syne.log")));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LauncherArgumentsRunBatchAndKeepOutputsInsideTheRunWorkspace()
    {
        string root = Path.Combine(Path.GetTempPath(), $"syne-launcher-{Guid.NewGuid():N}");
        string workDirectory = Path.Combine(root, "work");
        string logDirectory = Path.Combine(root, "logs");
        Directory.CreateDirectory(workDirectory);
        Directory.CreateDirectory(logDirectory);
        string configurationPath = Path.Combine(root, "launcher-config.json");
        await File.WriteAllTextAsync(configurationPath, """{"agents":{"initialCount":4}}""");
        int controlPort = FreePort();
        const string correlationId = "launcher-batch-acceptance";

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(typeof(global::Simulation.Console.Program).Assembly.Location);
        AddArguments(startInfo, controlPort, workDirectory, logDirectory, configurationPath, correlationId);
        startInfo.Environment["LIVEX_SESSION_TOKEN"] = "launcher-process-test-token";
        startInfo.Environment["LIVEX_CORRELATION_ID"] = correlationId;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("SYNE batch test process failed to start.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        try
        {
            bool ready = await WaitForReadinessAsync(process, controlPort, TimeSpan.FromSeconds(10));
            if (!ready)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                Assert.Fail($"SYNE never became ready. stderr: {await standardError}");
            }

            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            string output = await standardOutput;
            string error = await standardError;
            Assert.True(process.ExitCode == 0, $"SYNE batch failed ({process.ExitCode}). {error}\n{output}");

            string resultPath = Path.Combine(workDirectory, "data", "result.json");
            Assert.True(File.Exists(resultPath), "SYNE did not write data/result.json.");
            using JsonDocument result = JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
            Assert.Equal("reference", result.RootElement.GetProperty("simulation").GetString());
            Assert.Equal(42ul, result.RootElement.GetProperty("seed").GetUInt64());
            Assert.Equal(20ul, result.RootElement.GetProperty("ticks").GetUInt64());
            Assert.Matches(
                "^0x[0-9a-f]{16}$",
                result.RootElement.GetProperty("stateChecksum").GetString());

            string componentLog = await File.ReadAllTextAsync(Path.Combine(logDirectory, "syne.log"));
            Assert.Contains("event=batch_completed", componentLog, StringComparison.Ordinal);
            Assert.Equal(
                new[] { "launcher-config.json", "logs", "work" },
                Directory.EnumerateFileSystemEntries(root)
                    .Select(Path.GetFileName)
                    .OrderBy(name => name, StringComparer.Ordinal));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task BatchRefusesAnExportDirectoryOutsideTheRunWorkspace()
    {
        string root = Path.Combine(Path.GetTempPath(), $"syne-export-boundary-{Guid.NewGuid():N}");
        string workDirectory = Path.Combine(root, "work");
        string logDirectory = Path.Combine(root, "logs");
        string outsideDirectory = Path.Combine(root, "outside");
        Directory.CreateDirectory(workDirectory);
        Directory.CreateDirectory(logDirectory);
        int controlPort = FreePort();
        const string correlationId = "launcher-export-boundary";

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(typeof(global::Simulation.Console.Program).Assembly.Location);
        string[] arguments =
        [
            "--headless",
            "--instance-id", "syne-export-boundary",
            "--control-port", controlPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--work-dir", workDirectory,
            "--log-dir", logDirectory,
            "--simulation", "reference",
            "--seed", "42",
            "--ticks", "1",
            "--export-dir", outsideDirectory,
            "--autostart",
            "--correlation-id", correlationId,
        ];
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["LIVEX_SESSION_TOKEN"] = "launcher-export-boundary-token";
        startInfo.Environment["LIVEX_CORRELATION_ID"] = correlationId;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("SYNE export-boundary process failed to start.");
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(2, process.ExitCode);
            Assert.Contains("--export-dir doit être situé dans --work-dir", await standardError, StringComparison.Ordinal);
            Assert.False(Directory.Exists(outsideDirectory));
            Assert.Equal(
                new[] { "logs", "work" },
                Directory.EnumerateFileSystemEntries(root)
                    .Select(Path.GetFileName)
                    .OrderBy(name => name, StringComparer.Ordinal));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void AddArguments(
        ProcessStartInfo startInfo,
        int controlPort,
        string workDirectory,
        string logDirectory,
        string configurationPath,
        string correlationId)
    {
        string[] arguments =
        [
            "--headless",
            "--instance-id", "syne-acceptance",
            "--control-port", controlPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--work-dir", workDirectory,
            "--log-dir", logDirectory,
            "--simulation", "reference",
            "--seed", "42",
            "--max-ticks", "20",
            "--config", configurationPath,
            "--autostart",
            "--correlation-id", correlationId,
        ];

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static void AddServiceArguments(
        ProcessStartInfo startInfo,
        int controlPort,
        string workDirectory,
        string logDirectory,
        string correlationId)
    {
        string[] arguments =
        [
            "--headless",
            "--instance-id", "syne-service-acceptance",
            "--control-port", controlPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--work-dir", workDirectory,
            "--log-dir", logDirectory,
            "--correlation-id", correlationId,
        ];

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static async Task<bool> WaitForReadinessAsync(
        Process process,
        int port,
        TimeSpan timeout)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(250) };
        var endpoint = new Uri($"http://127.0.0.1:{port}/health/ready");
        var deadline = DateTime.UtcNow + timeout;
        while (!process.HasExited && DateTime.UtcNow < deadline)
        {
            try
            {
                using HttpResponseMessage response = await client.GetAsync(endpoint);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(20);
        }

        return false;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
