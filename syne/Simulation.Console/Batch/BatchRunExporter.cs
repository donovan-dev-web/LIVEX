using System.Globalization;
using System.Text;
using System.Text.Json;
using Simulation.Core.Entities;
using Simulation.Core.Loop;

namespace Simulation.Console.Batch;

public sealed record BatchRunResult(
    int Schema,
    string Simulation,
    ulong Seed,
    ulong Ticks,
    int AliveCount,
    string StateChecksum,
    string? RunId = null,
    string? StreamFile = null);

public static class BatchRunExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static async Task<string> WriteResultAsync(
        SimulationLoop loop,
        string simulation,
        ulong seed,
        ulong population,
        string exportDirectory,
        string? runId = null,
        string? streamFile = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentException.ThrowIfNullOrWhiteSpace(simulation);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportDirectory);

        Directory.CreateDirectory(exportDirectory);
        string resultPath = Path.Combine(exportDirectory, "result.json");
        var result = new BatchRunResult(
            1,
            simulation,
            seed,
            loop.CurrentTick,
            loop.World.Entities.Count,
            $"0x{ComputeStateChecksum(loop, population):x16}",
            runId,
            streamFile);
        string json = JsonSerializer.Serialize(result, JsonOptions);
        await File.WriteAllTextAsync(resultPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken)
            .ConfigureAwait(false);
        return resultPath;
    }

    private static ulong ComputeStateChecksum(SimulationLoop loop, ulong population)
    {
        var text = new StringBuilder();
        text.Append("population=").Append(population.ToString(CultureInfo.InvariantCulture));
        text.Append(";ticks=").Append(loop.CurrentTick.ToString(CultureInfo.InvariantCulture));
        text.AppendLine();
        foreach (Entity entity in loop.World.Entities.OrderBy(entity => entity.Id.Value))
        {
            text.Append(entity.Id.Value.ToString(CultureInfo.InvariantCulture)).Append(';');
            text.Append(entity.Position.X.ToString("0.00", CultureInfo.InvariantCulture)).Append(';');
            text.Append(entity.Position.Y.ToString("0.00", CultureInfo.InvariantCulture)).Append(';');
            double energy = loop.Cognition.HasMind(entity.Id.Value)
                ? loop.Cognition.MindOf(entity.Id.Value).Needs.Energy
                : 0.0;
            text.Append(energy.ToString("0.00", CultureInfo.InvariantCulture)).AppendLine();
        }

        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        foreach (byte value in Encoding.UTF8.GetBytes(text.ToString()))
        {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }
}
