using System.Text.Json;
using Simulation.Console.Batch;
using Simulation.Console.Control;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class BatchRunExporterTests
{
    [Fact]
    public async Task SameScenarioSeedAndHorizonProduceByteIdenticalResult()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "TestArtifacts", Guid.NewGuid().ToString("N"));
        string firstDirectory = Path.Combine(root, "first");
        string secondDirectory = Path.Combine(root, "second");
        try
        {
            SimulationOptions options = new();
            options.Agents.InitialCount = 8;
            options.Simulation.MaxTicks = 3;
            var (_, firstLoop) = SimulationFactory.Build(options, 42);
            firstLoop.Run(3);
            var (_, secondLoop) = SimulationFactory.Build(options, 42);
            secondLoop.Run(3);

            string firstPath = await BatchRunExporter.WriteResultAsync(firstLoop, "reference", 42, 8, firstDirectory);
            string secondPath = await BatchRunExporter.WriteResultAsync(secondLoop, "reference", 42, 8, secondDirectory);
            byte[] first = await File.ReadAllBytesAsync(firstPath);
            byte[] second = await File.ReadAllBytesAsync(secondPath);

            Assert.Equal(first, second);
            using JsonDocument document = JsonDocument.Parse(first);
            Assert.Equal("reference", document.RootElement.GetProperty("simulation").GetString());
            Assert.Equal(42ul, document.RootElement.GetProperty("seed").GetUInt64());
            Assert.Equal(3ul, document.RootElement.GetProperty("ticks").GetUInt64());
            Assert.Matches("^0x[0-9a-f]{16}$", document.RootElement.GetProperty("stateChecksum").GetString());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
